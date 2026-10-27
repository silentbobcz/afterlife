using System.IO.Compression;
using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

public sealed class SimulatedCrashException(string point) : Exception($"simulated crash at {point}");

public enum CrashTiming { Before, After }

/// <summary>Wraps the real filesystem and can "kill the process" before or after the N-th mutating call.</summary>
public sealed class FaultFileOps(IFileOps inner) : IFileOps
{
    public int MutationCount { get; private set; }
    public int? CrashAt { get; set; }
    public CrashTiming Timing { get; set; } = CrashTiming.Before;
    public Action<string, string>? BeforeMutation { get; set; } // (operation, path)

    private void Mutate(string op, string path, Action action)
    {
        MutationCount++;
        BeforeMutation?.Invoke(op, path);
        if (CrashAt == MutationCount && Timing == CrashTiming.Before) throw new SimulatedCrashException($"{op} {path} (before)");
        action();
        if (CrashAt == MutationCount && Timing == CrashTiming.After) throw new SimulatedCrashException($"{op} {path} (after)");
    }

    public bool FileExists(string path) => inner.FileExists(path);
    public FileMeta? GetMeta(string path) => inner.GetMeta(path);
    public string Hash(string path) => inner.Hash(path);
    public byte[] ReadAllBytes(string path) => inner.ReadAllBytes(path);
    public bool DirectoryExists(string path) => inner.DirectoryExists(path);
    public void CreateDirectory(string path) => Mutate("mkdir", path, () => inner.CreateDirectory(path));
    public bool IsDirectoryEmpty(string path) => inner.IsDirectoryEmpty(path);
    public void DeleteDirectory(string path) => Mutate("rmdir", path, () => inner.DeleteDirectory(path));
    public IEnumerable<string> EnumerateFiles(string directory) => inner.EnumerateFiles(directory);
    public void CopyToTemp(string source, string temp) => Mutate("copy", temp, () => inner.CopyToTemp(source, temp));
    public void WriteToTemp(string temp, byte[] content) => Mutate("write", temp, () => inner.WriteToTemp(temp, content));
    public void MoveNoOverwrite(string temp, string target) => Mutate("move", target, () => inner.MoveNoOverwrite(temp, target));
    public void MoveReplace(string temp, string target) => Mutate("replace", target, () => inner.MoveReplace(temp, target));
    public void Delete(string path) => Mutate("delete", path, () => inner.Delete(path));
    public void EnsureNoReparsePoints(string root, string fullPath) => inner.EnsureNoReparsePoints(root, fullPath);
}

public sealed class FakeGameEnvironment : IGameEnvironment
{
    public bool Running { get; set; }
    public bool IsGameRunning() => Running;

    public GameFingerprint? Fingerprint(string gameRoot)
    {
        var fi = new FileInfo(Path.Combine(gameRoot, "bin", "x64", "Cyberpunk2077.exe"));
        return fi.Exists ? new GameFingerprint("3.0.88.46235", fi.Length, fi.LastWriteTimeUtc.Ticks) : null;
    }

    public List<(string Exe, IReadOnlyList<string> Args)> ToolRuns { get; } = new();
    public int? ToolExitCode { get; set; } = 0;

    public int? RunTool(string exe, IReadOnlyList<string> args, string workDir, TimeSpan timeout)
    {
        ToolRuns.Add((exe, args));
        if (ToolExitCode == 0)
        {
            var root = args.First(a => a.StartsWith("-root=", StringComparison.Ordinal))[6..];
            Directory.CreateDirectory(Path.Combine(root, "r6", "cache", "modded"));
        }
        return ToolExitCode;
    }
}

public sealed class TestEnv : IDisposable
{
    public string Root { get; }
    public string Game { get; }
    public string LibraryRoot { get; }
    public string Inputs { get; }
    public FaultFileOps Fs { get; } = new(new PhysicalFileOps());
    public FakeGameEnvironment Env { get; } = new();
    public InstallCoordinator C { get; private set; }

    public TestEnv()
    {
        Root = Path.Combine(Path.GetTempPath(), "afterlife-tests", Guid.NewGuid().ToString("N")[..10]);
        Game = Path.Combine(Root, "Cyberpunk 2077");
        LibraryRoot = Path.Combine(Root, "Library");
        Inputs = Path.Combine(Root, "inputs");
        Directory.CreateDirectory(Path.Combine(Game, "bin", "x64"));
        Directory.CreateDirectory(Path.Combine(Game, "archive", "pc", "content"));
        Directory.CreateDirectory(Inputs);
        File.WriteAllText(Path.Combine(Game, "bin", "x64", "Cyberpunk2077.exe"), "fake exe");
        File.WriteAllText(Path.Combine(Game, "archive", "pc", "content", "basegame.archive"), "base game");
        C = InstallCoordinator.Initialize(Game, LibraryRoot, Fs, Env);
    }

    /// <summary>Simulates restarting the app after a crash: new store connection, same files.</summary>
    public void Restart()
    {
        C.Dispose();
        Fs.CrashAt = null;
        Fs.BeforeMutation = null;
        C = InstallCoordinator.Open(LibraryRoot, Fs, Env);
    }

    public string Zip(string name, params (string Path, string Content)[] files)
    {
        var path = Path.Combine(Inputs, name + ".zip");
        if (File.Exists(path)) File.Delete(path);
        using var z = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (p, content) in files)
        {
            var e = z.CreateEntry(p);
            using var w = new StreamWriter(e.Open());
            w.Write(content);
        }
        return path;
    }

    public PackageManifest ImportZip(string name, params (string Path, string Content)[] files)
    {
        var r = C.Import(new ImportRequest(Zip(name, files), name, "1.0"));
        Assert.True(r.Succeeded, string.Join("; ", r.Analysis.Problems));
        return r.Package!;
    }

    public string GameFile(string rel) => Path.Combine(Game, rel.Replace('/', Path.DirectorySeparatorChar));
    public string? Read(string rel) => File.Exists(GameFile(rel)) ? File.ReadAllText(GameFile(rel)) : null;
    public void Write(string rel, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(GameFile(rel))!);
        File.WriteAllText(GameFile(rel), content);
    }

    /// <summary>Relative path → content for everything in the game folder.</summary>
    public SortedDictionary<string, string> Tree()
    {
        var d = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in Directory.EnumerateFiles(Game, "*", SearchOption.AllDirectories))
            d[Path.GetRelativePath(Game, f).Replace(Path.DirectorySeparatorChar, '/')] = File.ReadAllText(f);
        return d;
    }

    public SortedSet<string> Dirs() =>
        new(Directory.EnumerateDirectories(Game, "*", SearchOption.AllDirectories).Select(d => Path.GetRelativePath(Game, d).Replace(Path.DirectorySeparatorChar, '/')), StringComparer.Ordinal);

    public ApplyResult ApplyOk()
    {
        var r = C.Apply();
        Assert.True(r.Succeeded, r.Message);
        return r;
    }

    public void Dispose()
    {
        C.Dispose();
        try
        {
            foreach (var f in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(Root, true);
        }
        catch { /* best effort */ }
    }
}
