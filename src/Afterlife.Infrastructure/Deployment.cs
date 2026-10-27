using System.Text;
using Afterlife.Core;

namespace Afterlife.Infrastructure;

public sealed class IntegrityException(string message) : IOException(message);

public sealed class OperationAbortedException(string message) : Exception(message);

public sealed class GameStateObserver(string gameRoot, IFileOps fs) : IStateObserver
{
    public Observation Observe(GamePath path)
    {
        var meta = fs.GetMeta(path.ToOsPath(gameRoot));
        return meta is null ? Observation.Absent : new Observation(true, meta.Size, meta.MTimeTicks);
    }

    public string FullHash(GamePath path) => fs.Hash(path.ToOsPath(gameRoot));
}

public sealed record ExecutionResult(string OperationId, OperationStatus Status, int StepsApplied, string? Error);

public enum DeployPhase { Preparing, Applying, Verifying, Done }

public sealed record DeployProgress(DeployPhase Phase, int Done, int Total, string? Path = null, StepKind? Kind = null);

public sealed record RecoveredStep(string Path, StepKind Kind, RecoveryVerdict Verdict);

public sealed record RecoveryOutcome(string OperationId, IReadOnlyList<RecoveredStep> Steps, IReadOnlyList<string> UnknownPaths);

/// <summary>
/// Executes a stored plan with the write-ahead protocol (AFTERLIFE-architektura-v2.1.md, "Protokol nasazení"):
/// temp file → hash → step Prepared (durable) → rename/delete → step Applied + ledger (one transaction).
/// </summary>
public sealed class DeploymentExecutor(AfterlifeStore store, LibraryPaths library, string gameRoot, IFileOps fs, Func<string?>? abortCheck = null)
{
    public static string TempPathFor(string targetOsPath, string operationId, int index) =>
        targetOsPath + $".afl-{operationId[..8]}-{index}.tmp";

    public ExecutionResult Execute(string operationId, IProgress<DeployProgress>? progress = null)
    {
        var op = store.GetOperation(operationId) ?? throw new InvalidOperationException($"unknown operation {operationId}");
        var steps = store.GetSteps(operationId);
        store.SetOperationStatus(operationId, OperationStatus.Running);
        int applied = 0;
        var total = steps.Count(s => s.State == StepState.Planned);
        progress?.Report(new DeployProgress(DeployPhase.Applying, 0, total));
        try
        {
            foreach (var rec in steps.Where(s => s.State == StepState.Planned))
            {
                var reason = abortCheck?.Invoke();
                if (reason is not null)
                {
                    store.SetOperationStatus(operationId, OperationStatus.Interrupted, reason);
                    return new ExecutionResult(operationId, OperationStatus.Interrupted, applied, reason);
                }
                ApplyStep(operationId, rec);
                applied++;
                progress?.Report(new DeployProgress(DeployPhase.Applying, applied, total, rec.Step.Path, rec.Step.Kind));
            }

            progress?.Report(new DeployProgress(DeployPhase.Verifying, applied, total));
            var verifyError = Verify(steps.Select(s => s.Step));
            if (verifyError is not null)
            {
                store.SetOperationStatus(operationId, OperationStatus.RecoveryRequired, verifyError);
                return new ExecutionResult(operationId, OperationStatus.RecoveryRequired, applied, verifyError);
            }

            SweepEmptyManagedDirs();
            store.InTransaction(() =>
            {
                store.SetOperationStatus(operationId, OperationStatus.Verified);
                store.AddGeneration(new GenerationRecord("gen-" + Guid.NewGuid().ToString("N")[..12], operationId, op.ProfileId, op.ProfileRevision,
                    op.TargetSnapshotJson, DateTimeOffset.UtcNow));
            });
            progress?.Report(new DeployProgress(DeployPhase.Done, applied, total));
            return new ExecutionResult(operationId, OperationStatus.Verified, applied, null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            store.SetOperationStatus(operationId, OperationStatus.RecoveryRequired, ex.Message);
            return new ExecutionResult(operationId, OperationStatus.RecoveryRequired, applied, ex.Message);
        }
    }

    private void ApplyStep(string opId, StepRecord rec)
    {
        var step = rec.Step;
        var gp = GamePath.Parse(step.Path);
        var target = gp.ToOsPath(gameRoot);
        fs.EnsureNoReparsePoints(gameRoot, target);

        switch (step.Kind)
        {
            case StepKind.Add:
            case StepKind.Replace:
            {
                EnsureParentDirectories(gp);
                var temp = rec.TempPath ?? throw new InvalidOperationException("missing temp path");
                if (fs.FileExists(temp)) fs.Delete(temp); // our own leftover from this operation (path recorded at plan time)
                if (step.SourceContent is not null) fs.WriteToTemp(temp, Encoding.UTF8.GetBytes(step.SourceContent));
                else fs.CopyToTemp(library.PackageFile(step.Owner!, step.SourceStoredPath!), temp);
                if (fs.Hash(temp) != step.NewHash)
                {
                    fs.Delete(temp);
                    Fail(opId, step, "library content does not match its manifest");
                }

                // Baseline for a taken-over modlist must exist before the file is touched.
                BaselineInfo? baseline = null;
                if (step.Baseline == BaselineAction.CaptureAbsent) baseline = new BaselineInfo(false, null, null);
                if (step.Baseline == BaselineAction.CaptureCurrent)
                {
                    var bytes = fs.ReadAllBytes(target);
                    if (ContentHash.Of(bytes) != step.ExpectedHash) { fs.Delete(temp); Fail(opId, step, "file changed since planning"); }
                    baseline = new BaselineInfo(true, step.ExpectedHash, Encoding.UTF8.GetString(bytes));
                }
                store.InTransaction(() =>
                {
                    if (baseline is not null) store.SetBaseline(step.Path, baseline);
                    store.SetStepState(opId, step.Index, StepState.Prepared);
                });

                if (step.Kind == StepKind.Add)
                {
                    try { fs.MoveNoOverwrite(temp, target); }
                    catch (IOException)
                    {
                        if (fs.FileExists(temp)) fs.Delete(temp);
                        Fail(opId, step, "a file appeared at the target after planning; nothing was overwritten");
                    }
                }
                else
                {
                    var current = fs.FileExists(target) ? fs.Hash(target) : null;
                    if (current != step.ExpectedHash)
                    {
                        fs.Delete(temp);
                        Fail(opId, step, "file changed since planning; nothing was overwritten");
                    }
                    fs.MoveReplace(temp, target);
                }
                var meta = fs.GetMeta(target)!;
                store.InTransaction(() =>
                {
                    store.UpsertLedger(new LedgerEntry(gp.Value, step.Artifact, step.Owner ?? "afterlife", step.NewHash!, meta.Size, meta.MTimeTicks));
                    if (step.Baseline == BaselineAction.Clear) store.ClearBaseline(step.Path);
                    store.SetStepState(opId, step.Index, StepState.Applied);
                });
                break;
            }
            case StepKind.Remove:
            {
                var current = fs.FileExists(target) ? fs.Hash(target) : null;
                if (current is null)
                {
                    store.InTransaction(() =>
                    {
                        store.DeleteLedger(step.Path);
                        if (step.Baseline == BaselineAction.Clear) store.ClearBaseline(step.Path);
                        store.SetStepState(opId, step.Index, StepState.Applied);
                    });
                    break;
                }
                if (current != step.ExpectedHash) Fail(opId, step, "file changed since planning; it was not deleted");
                store.SetStepState(opId, step.Index, StepState.Prepared);
                fs.Delete(target);
                store.InTransaction(() =>
                {
                    store.DeleteLedger(step.Path);
                    if (step.Baseline == BaselineAction.Clear) store.ClearBaseline(step.Path);
                    store.SetStepState(opId, step.Index, StepState.Applied);
                });
                CleanupEmptyManagedDirs(gp.ParentValue);
                break;
            }
            case StepKind.Forget:
                store.InTransaction(() =>
                {
                    store.DeleteLedger(step.Path);
                    if (step.Baseline == BaselineAction.Clear) store.ClearBaseline(step.Path);
                    store.SetStepState(opId, step.Index, StepState.Applied);
                });
                break;
            case StepKind.Retag:
            {
                var meta = fs.GetMeta(target) ?? throw new IntegrityException($"{step.Path} disappeared");
                if (fs.Hash(target) != step.NewHash) Fail(opId, step, "file changed since planning");
                store.InTransaction(() =>
                {
                    store.UpsertLedger(new LedgerEntry(gp.Value, step.Artifact, step.Owner ?? "afterlife", step.NewHash!, meta.Size, meta.MTimeTicks));
                    store.SetStepState(opId, step.Index, StepState.Applied);
                });
                break;
            }
        }
    }

    private void Fail(string opId, PlanStep step, string message)
    {
        store.InTransaction(() =>
        {
            if (step.Baseline is BaselineAction.CaptureAbsent or BaselineAction.CaptureCurrent) store.ClearBaseline(step.Path);
            store.SetStepState(opId, step.Index, StepState.Failed, message);
        });
        throw new IntegrityException($"{step.Path}: {message}");
    }

    private string? Verify(IEnumerable<PlanStep> steps)
    {
        foreach (var s in steps)
        {
            var os = GamePath.Parse(s.Path).ToOsPath(gameRoot);
            switch (s.Kind)
            {
                case StepKind.Add or StepKind.Replace or StepKind.Retag:
                    if (!fs.FileExists(os) || fs.Hash(os) != s.NewHash) return $"verification failed for {s.Path}";
                    break;
                case StepKind.Remove:
                    if (fs.FileExists(os)) return $"verification failed for {s.Path}";
                    break;
            }
        }
        return null;
    }

    private void EnsureParentDirectories(GamePath gp)
    {
        var parent = gp.ParentValue;
        if (parent is null) return;
        var segments = parent.Split('/');
        var rel = "";
        foreach (var seg in segments)
        {
            rel = rel.Length == 0 ? seg : rel + "/" + seg;
            var os = GamePath.Parse(rel).ToOsPath(gameRoot);
            if (fs.DirectoryExists(os)) { fs.EnsureNoReparsePoints(gameRoot, os); continue; }
            store.AddManagedDir(rel); // intent first, then create
            fs.CreateDirectory(os);
        }
    }

    private void CleanupEmptyManagedDirs(string? relDir)
    {
        while (relDir is not null)
        {
            if (!store.IsManagedDir(relDir)) return;
            var os = GamePath.Parse(relDir).ToOsPath(gameRoot);
            if (fs.DirectoryExists(os))
            {
                if (!fs.IsDirectoryEmpty(os)) return;
                fs.DeleteDirectory(os);
            }
            store.RemoveManagedDir(relDir);
            var i = relDir.LastIndexOf('/');
            relDir = i < 0 ? null : relDir[..i];
        }
    }

    public void SweepEmptyManagedDirs()
    {
        foreach (var d in store.GetManagedDirs().OrderByDescending(d => d.Length))
        {
            var os = GamePath.Parse(d).ToOsPath(gameRoot);
            if (!fs.DirectoryExists(os)) { store.RemoveManagedDir(d); continue; }
            if (fs.IsDirectoryEmpty(os)) { fs.DeleteDirectory(os); store.RemoveManagedDir(d); }
        }
    }

    /// <summary>
    /// Brings the ledger in line with the disk for an unfinished operation, using the durable intent records.
    /// Never deletes or adopts content it cannot prove it wrote.
    /// </summary>
    public RecoveryOutcome Recover(string operationId)
    {
        var steps = store.GetSteps(operationId);
        var report = new List<RecoveredStep>();
        var unknown = new List<string>();
        foreach (var rec in steps)
        {
            var s = rec.Step;
            var target = GamePath.Parse(s.Path).ToOsPath(gameRoot);
            if (rec.TempPath is not null && fs.FileExists(rec.TempPath)) fs.Delete(rec.TempPath);

            if (rec.State != StepState.Prepared) continue;
            var exists = fs.FileExists(target);
            var hash = exists ? fs.Hash(target) : null;
            var verdict = RecoveryClassifier.Classify(s, exists, hash);
            report.Add(new RecoveredStep(s.Path, s.Kind, verdict));
            switch (verdict)
            {
                case RecoveryVerdict.Applied:
                    store.InTransaction(() =>
                    {
                        if (s.Kind == StepKind.Remove) store.DeleteLedger(s.Path);
                        else
                        {
                            var meta = fs.GetMeta(target)!;
                            store.UpsertLedger(new LedgerEntry(GamePath.Parse(s.Path).Value, s.Artifact, s.Owner ?? "afterlife", s.NewHash!, meta.Size, meta.MTimeTicks));
                        }
                        if (s.Baseline == BaselineAction.Clear) store.ClearBaseline(s.Path);
                        store.SetStepState(operationId, s.Index, StepState.Applied);
                    });
                    break;
                case RecoveryVerdict.NotApplied:
                    store.InTransaction(() =>
                    {
                        // A takeover that never happened must not leave a baseline behind: we do not own the file.
                        if (s.Baseline is BaselineAction.CaptureAbsent or BaselineAction.CaptureCurrent) store.ClearBaseline(s.Path);
                        store.SetStepState(operationId, s.Index, StepState.Failed, "not applied (recovered)");
                    });
                    break;
                default:
                    unknown.Add(s.Path);
                    store.SetStepState(operationId, s.Index, StepState.Failed, "unknown content found during recovery; left untouched");
                    break;
            }
        }
        SweepEmptyManagedDirs();
        store.SetOperationStatus(operationId, OperationStatus.Recovered, unknown.Count > 0 ? $"{unknown.Count} path(s) with unknown content" : null);
        return new RecoveryOutcome(operationId, report, unknown);
    }
}
