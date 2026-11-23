using Afterlife.Infrastructure;

namespace Afterlife.Tests;

public class LibraryMoveTests
{
    /// <summary>A library with one mod applied to the game, closed, and a mover that records the saved folder.</summary>
    private sealed class Setup : IDisposable
    {
        public TestEnv Env { get; } = new();
        public string Target { get; }
        public string StateFile { get; }
        public string? Saved { get; private set; }
        public SortedDictionary<string, string> GameBefore { get; }

        public Setup()
        {
            Env.ImportZip("A", ("archive/pc/mod/a.archive", "a"), ("r6/scripts/a/main.reds", "script"));
            Env.C.SetEnabled(Env.C.Store.GetPackages().Single().PackageId, true);
            Env.ApplyOk();
            GameBefore = Env.Tree();
            Env.C.Dispose();
            Target = Path.Combine(Env.Root, "Moved", "Library");
            StateFile = Path.Combine(Env.Root, "settings", "library-move.json");
        }

        public LibraryMover Mover(bool rename = true, Action<string>? beforeCopy = null, Action<string>? save = null) =>
            new(StateFile, save ?? (root => Saved = root))
            {
                Rename = rename ? (from, to) => { Directory.Move(from, to); return true; } : (_, _) => false,
                BeforeCopy = beforeCopy,
            };

        public LibraryMovePlan Plan(LibraryMover mover) => mover.Check(Env.LibraryRoot, Target, Env.Game);

        public void AssertLibraryAt(string root)
        {
            using var c = InstallCoordinator.Open(root, Env.Fs, Env.Env);
            var package = Assert.Single(c.Store.GetPackages());
            Assert.True(File.Exists(c.Library.PackageFile(package.PackageId, package.Files[0].StoredPath)));
            Assert.True(c.SelectedProfile.IsEnabled(package.PackageId));
        }

        public void Dispose() => Env.Dispose();
    }

    private static List<string> RelativeFiles(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f))
            .Where(f => !f.StartsWith("locks" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).ToList();

    [Fact]
    public void Same_drive_renames_the_folder_and_leaves_the_game_alone()
    {
        using var s = new Setup();
        var mover = s.Mover();
        var plan = s.Plan(mover);
        Assert.True(plan.Files > 0);
        Assert.False(Directory.Exists(s.Target)); // the check leaves no trace

        mover.Move(plan);

        Assert.Equal(FolderRules.Normalize(s.Target), s.Saved);
        Assert.False(Directory.Exists(s.Env.LibraryRoot));
        Assert.Equal(LibraryMovePending.None, mover.Inspect());
        Assert.False(File.Exists(s.StateFile));
        Assert.Equal(s.GameBefore, s.Env.Tree());
        s.AssertLibraryAt(s.Target);
    }

    [Fact]
    public void Another_drive_copies_every_file_then_empties_the_old_folder()
    {
        using var s = new Setup();
        var before = RelativeFiles(s.Env.LibraryRoot);
        var mover = s.Mover(rename: false);
        var reports = new List<int>();

        mover.Move(s.Plan(mover), new SyncProgress(p => reports.Add(p.Percent)));

        Assert.Equal(before, RelativeFiles(s.Target));
        Assert.Equal(100, reports[^1]);
        Assert.Equal(FolderRules.Normalize(s.Target), s.Saved);
        Assert.False(Directory.Exists(s.Env.LibraryRoot));
        Assert.Equal(s.GameBefore, s.Env.Tree());
        s.AssertLibraryAt(s.Target);
    }

    [Fact]
    public void A_file_of_the_user_in_the_old_folder_is_kept()
    {
        using var s = new Setup();
        var mover = s.Mover(rename: false);
        var plan = s.Plan(mover);
        var own = Path.Combine(s.Env.LibraryRoot, "notes.txt");
        var late = false;
        var copying = s.Mover(rename: false, beforeCopy: _ =>
        {
            // written after the list of files was taken: it is not in the new folder, so it must stay
            if (late) return;
            late = true;
            File.WriteAllText(own, "mine");
        });

        copying.Move(plan);

        Assert.True(File.Exists(own));
        Assert.False(AppSettings.IsInitialized(s.Env.LibraryRoot));
        s.AssertLibraryAt(s.Target);
    }

    [Fact]
    public void Folders_that_cannot_take_the_library_are_refused()
    {
        using var s = new Setup();
        var mover = s.Mover();
        var full = Path.Combine(s.Env.Root, "Full");
        Directory.CreateDirectory(full);
        File.WriteAllText(Path.Combine(full, "something.txt"), "x");

        Assert.Throws<InvalidOperationException>(() => mover.Check(s.Env.LibraryRoot, full, s.Env.Game));
        Assert.Throws<InvalidOperationException>(() => mover.Check(s.Env.LibraryRoot, Path.Combine(s.Env.Game, "Library"), s.Env.Game));
        Assert.Throws<InvalidOperationException>(() => mover.Check(s.Env.LibraryRoot, s.Env.Root, s.Env.Game));
        Assert.Throws<InvalidOperationException>(() => mover.Check(s.Env.LibraryRoot, Path.Combine(s.Env.LibraryRoot, "inner"), s.Env.Game));
        Assert.Throws<InvalidOperationException>(() => mover.Check(s.Env.LibraryRoot, s.Env.LibraryRoot + Path.DirectorySeparatorChar, s.Env.Game));
        Assert.Throws<InvalidOperationException>(() => mover.Check(Path.Combine(s.Env.Root, "NoLibrary"), s.Target, s.Env.Game));

        // an empty folder that already exists is fine
        var empty = Path.Combine(s.Env.Root, "Empty");
        Directory.CreateDirectory(empty);
        Assert.Equal(FolderRules.Normalize(empty), mover.Check(s.Env.LibraryRoot, empty + Path.DirectorySeparatorChar, s.Env.Game).To);
        Assert.Null(s.Saved);
        s.AssertLibraryAt(s.Env.LibraryRoot);
    }

    [Fact]
    public void A_copy_cut_off_half_way_can_be_continued()
    {
        using var s = new Setup();
        var copied = 0;
        var crashing = s.Mover(rename: false, beforeCopy: f => { if (++copied == 2) throw new SimulatedCrashException("copy " + f); });
        Assert.Throws<SimulatedCrashException>(() => crashing.Move(s.Plan(crashing)));

        // the next start: the old folder is still the library, the new one is half full
        var mover = s.Mover(rename: false);
        Assert.Equal(LibraryMovePending.Interrupted, mover.Inspect());
        Assert.Null(s.Saved);
        s.AssertLibraryAt(s.Env.LibraryRoot);
        var again = new List<string>();
        var resumed = s.Mover(rename: false, beforeCopy: again.Add);

        resumed.Continue();

        Assert.NotEmpty(again);
        Assert.Equal(FolderRules.Normalize(s.Target), s.Saved);
        Assert.False(Directory.Exists(s.Env.LibraryRoot));
        Assert.Equal(LibraryMovePending.None, mover.Inspect());
        Assert.Equal(s.GameBefore, s.Env.Tree());
        s.AssertLibraryAt(s.Target);
    }

    [Fact]
    public void A_copy_cut_off_half_way_can_be_undone()
    {
        using var s = new Setup();
        var before = RelativeFiles(s.Env.LibraryRoot);
        var copied = 0;
        var crashing = s.Mover(rename: false, beforeCopy: f => { if (++copied == 2) throw new SimulatedCrashException("copy " + f); });
        Assert.Throws<SimulatedCrashException>(() => crashing.Move(s.Plan(crashing)));
        var mover = s.Mover(rename: false);
        Assert.Equal(LibraryMovePending.Interrupted, mover.Inspect());

        mover.Undo();

        Assert.False(Directory.Exists(s.Target));
        Assert.Null(s.Saved);
        Assert.Equal(LibraryMovePending.None, mover.Inspect());
        Assert.Equal(before, RelativeFiles(s.Env.LibraryRoot));
        s.AssertLibraryAt(s.Env.LibraryRoot);
    }

    [Fact]
    public void A_failed_copy_removes_itself_and_keeps_the_library()
    {
        using var s = new Setup();
        var copied = 0;
        var failing = s.Mover(rename: false, beforeCopy: _ => { if (++copied == 2) throw new IOException("disk full"); });

        var ex = Assert.Throws<InvalidOperationException>(() => failing.Move(s.Plan(failing)));

        Assert.Contains("disk full", ex.Message);
        Assert.False(Directory.Exists(s.Target));
        Assert.Null(s.Saved);
        Assert.Equal(LibraryMovePending.None, failing.Inspect());
        s.AssertLibraryAt(s.Env.LibraryRoot);
    }

    [Fact]
    public void A_crash_between_the_rename_and_the_settings_is_finished_at_the_next_start()
    {
        using var s = new Setup();
        var crashing = s.Mover(save: _ => throw new SimulatedCrashException("save settings"));
        Assert.Throws<SimulatedCrashException>(() => crashing.Move(s.Plan(crashing)));

        var mover = s.Mover();
        Assert.Equal(LibraryMovePending.Finish, mover.Inspect());
        Assert.Equal(FolderRules.Normalize(s.Target), mover.FinishPending());

        Assert.Equal(FolderRules.Normalize(s.Target), s.Saved);
        Assert.Equal(LibraryMovePending.None, mover.Inspect());
        s.AssertLibraryAt(s.Target);
    }

    [Fact]
    public void A_library_open_elsewhere_is_not_moved()
    {
        using var s = new Setup();
        var mover = s.Mover(rename: false);
        var plan = s.Plan(mover);
        using (var open = InstallCoordinator.Open(s.Env.LibraryRoot, s.Env.Fs, s.Env.Env))
        {
            if (OperatingSystem.IsWindows())
                Assert.Throws<InvalidOperationException>(() => mover.Move(plan));
        }
        Assert.Null(s.Saved);
        s.AssertLibraryAt(s.Env.LibraryRoot);
    }

    [Fact]
    public void A_game_folder_written_with_a_separator_at_the_end_still_counts()
    {
        var sep = Path.DirectorySeparatorChar;
        var game = Path.Combine(Path.GetTempPath(), "afterlife-tests", "rules", "Game");
        Assert.True(FolderRules.Overlap(game + sep, Path.Combine(game, "Library")));
        Assert.True(FolderRules.Overlap(Path.Combine(game, "Library") + sep, game));
        Assert.True(FolderRules.Overlap(game, game + sep));
        Assert.False(FolderRules.Overlap(game, game + " 2"));
        Assert.False(FolderRules.Overlap(game, Path.Combine(Path.GetDirectoryName(game)!, "Library")));

        using var t = new TestEnv();
        var inside = Path.Combine(t.Game, "Library");
        Assert.Throws<InvalidOperationException>(() => InstallCoordinator.Initialize(t.Game + sep, inside, t.Fs, t.Env));
        // the other direction when the game folder changes: a game inside the library is refused
        var gameInLibrary = Path.Combine(t.LibraryRoot, "Game");
        Directory.CreateDirectory(Path.Combine(gameInLibrary, "bin", "x64"));
        File.WriteAllText(Path.Combine(gameInLibrary, "bin", "x64", "Cyberpunk2077.exe"), "fake exe");
        Assert.Throws<InvalidOperationException>(() => t.C.ChangeGameRoot(gameInLibrary));
    }

    /// <summary>Progress that reports on the calling thread, so a test sees every report before Move returns.</summary>
    private sealed class SyncProgress(Action<LibraryMoveProgress> report) : IProgress<LibraryMoveProgress>
    {
        public void Report(LibraryMoveProgress value) => report(value);
    }
}
