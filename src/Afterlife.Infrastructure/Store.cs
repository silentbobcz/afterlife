using System.Text.Json;
using System.Text.Json.Serialization;
using Afterlife.Core;
using Microsoft.Data.Sqlite;

namespace Afterlife.Infrastructure;

public enum OperationStatus { Planned, Running, Verified, Failed, Interrupted, RecoveryRequired, Recovered }

public sealed record OperationRecord(
    string Id,
    DateTimeOffset CreatedAt,
    OperationStatus Status,
    string ProfileId,
    long ProfileRevision,
    string TargetSnapshotJson,
    string? PreviousGenerationId,
    string? FingerprintJson,
    string? Error);

public sealed record StepRecord(string OperationId, PlanStep Step, string? TempPath, StepState State, string? Error);

public sealed record GenerationRecord(string Id, string OperationId, string ProfileId, long ProfileRevision, string SnapshotJson, DateTimeOffset CreatedAt);

public static class Json
{
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = false };
        o.Converters.Add(new JsonStringEnumConverter());
        return o;
    }

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);
    public static T Deserialize<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;
}

/// <summary>
/// SQLite state (WAL, synchronous=FULL). All writes that must be durable before touching the game folder
/// are committed before the corresponding filesystem change.
/// </summary>
public sealed class AfterlifeStore : IDisposable
{
    public const int SchemaVersion = 1;
    private readonly SqliteConnection _c;
    private SqliteTransaction? _tx;

    private AfterlifeStore(SqliteConnection c) => _c = c;

    public string DatabasePath { get; private init; } = "";

    public static AfterlifeStore Open(string dbPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(dbPath))!);
        var c = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        c.Open();
        var s = new AfterlifeStore(c) { DatabasePath = dbPath };
        s.Exec("PRAGMA journal_mode=WAL;");
        s.Exec("PRAGMA synchronous=FULL;");
        s.Exec("PRAGMA foreign_keys=ON;");
        s.Exec("PRAGMA busy_timeout=5000;");
        s.Migrate();
        return s;
    }

    private void Migrate()
    {
        Exec("""
            CREATE TABLE IF NOT EXISTS meta(key TEXT PRIMARY KEY, value TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS packages(id TEXT PRIMARY KEY, mod_id TEXT NOT NULL, json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS profiles(id TEXT PRIMARY KEY, json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS ledger(key TEXT PRIMARY KEY, path TEXT NOT NULL, kind TEXT NOT NULL, owner TEXT NOT NULL, hash TEXT NOT NULL, size INTEGER NOT NULL, mtime INTEGER NOT NULL);
            CREATE TABLE IF NOT EXISTS managed_dirs(key TEXT PRIMARY KEY, path TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS baselines(key TEXT PRIMARY KEY, existed INTEGER NOT NULL, hash TEXT, content TEXT);
            CREATE TABLE IF NOT EXISTS operations(id TEXT PRIMARY KEY, created TEXT NOT NULL, status TEXT NOT NULL, profile_id TEXT NOT NULL, profile_rev INTEGER NOT NULL, snapshot TEXT NOT NULL, prev_generation TEXT, fingerprint TEXT, error TEXT);
            CREATE TABLE IF NOT EXISTS op_steps(op_id TEXT NOT NULL REFERENCES operations(id), idx INTEGER NOT NULL, step TEXT NOT NULL, temp_path TEXT, state TEXT NOT NULL, error TEXT, PRIMARY KEY(op_id, idx));
            CREATE TABLE IF NOT EXISTS generations(id TEXT PRIMARY KEY, op_id TEXT NOT NULL, profile_id TEXT NOT NULL, profile_rev INTEGER NOT NULL, snapshot TEXT NOT NULL, created TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS unmanaged(key TEXT PRIMARY KEY, path TEXT NOT NULL, size INTEGER NOT NULL, seen TEXT NOT NULL);
            """);
        var v = GetMeta("schema.version");
        if (v is null) SetMeta("schema.version", SchemaVersion.ToString());
        else if (int.Parse(v) > SchemaVersion)
            throw new InvalidOperationException($"Library was created by a newer AFTERLIFE (schema {v}); refusing to write.");
    }

    // ---------- plumbing ----------
    private SqliteCommand Cmd(string sql, params (string, object?)[] args)
    {
        var cmd = _c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = _tx;
        foreach (var (n, v) in args) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
        return cmd;
    }

    private void Exec(string sql, params (string, object?)[] args)
    {
        using var cmd = Cmd(sql, args);
        cmd.ExecuteNonQuery();
    }

    private List<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string, object?)[] args)
    {
        using var cmd = Cmd(sql, args);
        using var r = cmd.ExecuteReader();
        var list = new List<T>();
        while (r.Read()) list.Add(map(r));
        return list;
    }

    /// <summary>Runs the action in one durable transaction (nested calls join the outer transaction).</summary>
    public void InTransaction(Action action)
    {
        if (_tx is not null) { action(); return; }
        _tx = _c.BeginTransaction();
        try
        {
            action();
            _tx.Commit();
        }
        catch
        {
            _tx.Rollback();
            throw;
        }
        finally
        {
            _tx.Dispose();
            _tx = null;
        }
    }

    // ---------- meta ----------
    public string? GetMeta(string key) =>
        Query("SELECT value FROM meta WHERE key=$k", r => r.GetString(0), ("$k", key)).FirstOrDefault();

    public void SetMeta(string key, string value) =>
        Exec("INSERT INTO meta(key,value) VALUES($k,$v) ON CONFLICT(key) DO UPDATE SET value=excluded.value", ("$k", key), ("$v", value));

    // ---------- packages ----------
    public void AddPackage(PackageManifest m) =>
        Exec("INSERT INTO packages(id,mod_id,json) VALUES($i,$m,$j)", ("$i", m.PackageId), ("$m", m.ModId), ("$j", Json.Serialize(m)));

    public PackageManifest? GetPackage(string id) =>
        Query("SELECT json FROM packages WHERE id=$i", r => Json.Deserialize<PackageManifest>(r.GetString(0)), ("$i", id)).FirstOrDefault();

    public IReadOnlyList<PackageManifest> GetPackages() =>
        Query("SELECT json FROM packages ORDER BY rowid", r => Json.Deserialize<PackageManifest>(r.GetString(0)));

    /// <summary>Updates metadata of a package (requirements, provides, Nexus link); files and digest never change.</summary>
    public void UpdatePackage(PackageManifest m) =>
        Exec("UPDATE packages SET json=$j WHERE id=$i", ("$i", m.PackageId), ("$j", Json.Serialize(m)));

    public void DeletePackageRecord(string id) => Exec("DELETE FROM packages WHERE id=$i", ("$i", id));

    // ---------- profiles ----------
    public void SaveProfile(ProfileState p) =>
        Exec("INSERT INTO profiles(id,json) VALUES($i,$j) ON CONFLICT(id) DO UPDATE SET json=excluded.json", ("$i", p.Id), ("$j", Json.Serialize(p)));

    public ProfileState? GetProfile(string id) =>
        Query("SELECT json FROM profiles WHERE id=$i", r => Json.Deserialize<ProfileState>(r.GetString(0)), ("$i", id)).FirstOrDefault();

    public void DeleteProfile(string id) => Exec("DELETE FROM profiles WHERE id=$i", ("$i", id));

    public IReadOnlyList<ProfileState> GetProfiles() =>
        Query("SELECT json FROM profiles ORDER BY rowid", r => Json.Deserialize<ProfileState>(r.GetString(0)));

    // ---------- ledger ----------
    public Dictionary<string, LedgerEntry> GetLedger() =>
        Query("SELECT path,kind,owner,hash,size,mtime FROM ledger", r => new LedgerEntry(
                r.GetString(0), Enum.Parse<ArtifactKind>(r.GetString(1)), r.GetString(2), r.GetString(3), r.GetInt64(4), r.GetInt64(5)))
            .ToDictionary(e => e.Path.ToUpperInvariant(), StringComparer.Ordinal);

    public void UpsertLedger(LedgerEntry e) =>
        Exec("""
            INSERT INTO ledger(key,path,kind,owner,hash,size,mtime) VALUES($k,$p,$kind,$o,$h,$s,$m)
            ON CONFLICT(key) DO UPDATE SET path=excluded.path, kind=excluded.kind, owner=excluded.owner, hash=excluded.hash, size=excluded.size, mtime=excluded.mtime
            """, ("$k", e.Path.ToUpperInvariant()), ("$p", e.Path), ("$kind", e.Kind.ToString()), ("$o", e.Owner), ("$h", e.Hash), ("$s", e.Size), ("$m", e.MTimeTicks));

    public void DeleteLedger(string path) => Exec("DELETE FROM ledger WHERE key=$k", ("$k", path.ToUpperInvariant()));

    // ---------- managed directories ----------
    public bool IsManagedDir(string path) =>
        Query("SELECT 1 FROM managed_dirs WHERE key=$k", r => 1, ("$k", path.ToUpperInvariant())).Count > 0;

    public void AddManagedDir(string path) =>
        Exec("INSERT OR IGNORE INTO managed_dirs(key,path) VALUES($k,$p)", ("$k", path.ToUpperInvariant()), ("$p", path));

    public void RemoveManagedDir(string path) => Exec("DELETE FROM managed_dirs WHERE key=$k", ("$k", path.ToUpperInvariant()));

    public List<string> GetManagedDirs() => Query("SELECT path FROM managed_dirs", r => r.GetString(0));

    // ---------- baselines ----------
    public BaselineInfo? GetBaseline(string path) =>
        Query("SELECT existed,hash,content FROM baselines WHERE key=$k",
            r => new BaselineInfo(r.GetInt64(0) == 1, r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2)),
            ("$k", path.ToUpperInvariant())).FirstOrDefault();

    public void SetBaseline(string path, BaselineInfo b) =>
        Exec("INSERT INTO baselines(key,existed,hash,content) VALUES($k,$e,$h,$c) ON CONFLICT(key) DO NOTHING",
            ("$k", path.ToUpperInvariant()), ("$e", b.Existed ? 1 : 0), ("$h", b.Hash), ("$c", b.Content));

    public void ClearBaseline(string path) => Exec("DELETE FROM baselines WHERE key=$k", ("$k", path.ToUpperInvariant()));

    // ---------- operations ----------
    public void CreateOperation(OperationRecord op, IEnumerable<StepRecord> steps) => InTransaction(() =>
    {
        Exec("INSERT INTO operations(id,created,status,profile_id,profile_rev,snapshot,prev_generation,fingerprint,error) VALUES($i,$c,$s,$p,$r,$sn,$g,$f,$e)",
            ("$i", op.Id), ("$c", op.CreatedAt.ToString("O")), ("$s", op.Status.ToString()), ("$p", op.ProfileId), ("$r", op.ProfileRevision),
            ("$sn", op.TargetSnapshotJson), ("$g", op.PreviousGenerationId), ("$f", op.FingerprintJson), ("$e", op.Error));
        foreach (var s in steps)
            Exec("INSERT INTO op_steps(op_id,idx,step,temp_path,state,error) VALUES($o,$x,$s,$t,$st,$e)",
                ("$o", s.OperationId), ("$x", s.Step.Index), ("$s", Json.Serialize(s.Step)), ("$t", s.TempPath), ("$st", s.State.ToString()), ("$e", s.Error));
    });

    public void SetOperationStatus(string id, OperationStatus status, string? error = null) =>
        Exec("UPDATE operations SET status=$s, error=COALESCE($e,error) WHERE id=$i", ("$s", status.ToString()), ("$e", error), ("$i", id));

    public void SetStepState(string opId, int idx, StepState state, string? error = null) =>
        Exec("UPDATE op_steps SET state=$s, error=$e WHERE op_id=$o AND idx=$x", ("$s", state.ToString()), ("$e", error), ("$o", opId), ("$x", idx));

    private static OperationRecord MapOp(SqliteDataReader r) => new(
        r.GetString(0), DateTimeOffset.Parse(r.GetString(1)), Enum.Parse<OperationStatus>(r.GetString(2)), r.GetString(3), r.GetInt64(4),
        r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8));

    private const string OpColumns = "id,created,status,profile_id,profile_rev,snapshot,prev_generation,fingerprint,error";

    public OperationRecord? GetOperation(string id) =>
        Query($"SELECT {OpColumns} FROM operations WHERE id=$i", MapOp, ("$i", id)).FirstOrDefault();

    /// <summary>The operation that still needs recovery, if any (at most one exists because of the install lock).</summary>
    public OperationRecord? GetUnfinishedOperation() =>
        Query($"SELECT {OpColumns} FROM operations WHERE status IN ('Planned','Running','Failed','Interrupted','RecoveryRequired') ORDER BY created DESC LIMIT 1", MapOp)
            .FirstOrDefault();

    public List<OperationRecord> GetOperations(int limit = 20) =>
        Query($"SELECT {OpColumns} FROM operations ORDER BY created DESC LIMIT $l", MapOp, ("$l", limit));

    public List<StepRecord> GetSteps(string opId) =>
        Query("SELECT op_id,step,temp_path,state,error FROM op_steps WHERE op_id=$o ORDER BY idx", r => new StepRecord(
            r.GetString(0), Json.Deserialize<PlanStep>(r.GetString(1)), r.IsDBNull(2) ? null : r.GetString(2),
            Enum.Parse<StepState>(r.GetString(3)), r.IsDBNull(4) ? null : r.GetString(4)), ("$o", opId));

    // ---------- generations ----------
    public void AddGeneration(GenerationRecord g) =>
        Exec("INSERT INTO generations(id,op_id,profile_id,profile_rev,snapshot,created) VALUES($i,$o,$p,$r,$s,$c)",
            ("$i", g.Id), ("$o", g.OperationId), ("$p", g.ProfileId), ("$r", g.ProfileRevision), ("$s", g.SnapshotJson), ("$c", g.CreatedAt.ToString("O")));

    public GenerationRecord? GetLastGeneration() =>
        Query("SELECT id,op_id,profile_id,profile_rev,snapshot,created FROM generations ORDER BY created DESC, rowid DESC LIMIT 1",
            r => new GenerationRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt64(3), r.GetString(4), DateTimeOffset.Parse(r.GetString(5)))).FirstOrDefault();

    public GenerationRecord? GetGeneration(string id) =>
        Query("SELECT id,op_id,profile_id,profile_rev,snapshot,created FROM generations WHERE id=$i",
            r => new GenerationRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt64(3), r.GetString(4), DateTimeOffset.Parse(r.GetString(5))), ("$i", id)).FirstOrDefault();

    /// <summary>Package ids referenced by the last N generations (protected from deletion).</summary>
    public HashSet<string> GetPackagesInRecentGenerations(int n = 3)
    {
        var snaps = Query("SELECT snapshot FROM generations ORDER BY created DESC, rowid DESC LIMIT $n", r => r.GetString(0), ("$n", n));
        var set = new HashSet<string>();
        foreach (var s in snaps) foreach (var id in Json.Deserialize<DeploymentSnapshot>(s).IncludedPackages) set.Add(id);
        return set;
    }

    // ---------- unmanaged inventory ----------
    public void ReplaceUnmanaged(IEnumerable<(string Path, long Size)> items) => InTransaction(() =>
    {
        Exec("DELETE FROM unmanaged");
        var now = DateTimeOffset.UtcNow.ToString("O");
        foreach (var (p, s) in items)
            Exec("INSERT OR REPLACE INTO unmanaged(key,path,size,seen) VALUES($k,$p,$s,$n)", ("$k", p.ToUpperInvariant()), ("$p", p), ("$s", s), ("$n", now));
    });

    public List<(string Path, long Size)> GetUnmanaged() =>
        Query("SELECT path,size FROM unmanaged ORDER BY path", r => (r.GetString(0), r.GetInt64(1)));

    public void Dispose()
    {
        _tx?.Dispose();
        _c.Dispose();
    }
}
