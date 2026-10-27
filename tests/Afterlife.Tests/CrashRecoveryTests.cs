using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

/// <summary>
/// Kills the "process" before and after every mutating filesystem call of a realistic operation
/// (adds, replaces, removes, modlist takeover) and checks that recovery always converges.
/// </summary>
public class CrashRecoveryTests
{
    private const string ForeignModlist = "my.archive\n";

    private static (TestEnv Env, PackageManifest A, PackageManifest B, PackageManifest C) Setup(bool foreignModlist)
    {
        var t = new TestEnv();
        if (foreignModlist)
        {
            t.Write("archive/pc/mod/my.archive", "mine");
            t.Write("archive/pc/mod/modlist.txt", ForeignModlist);
        }
        var a = t.ImportZip("A", ("archive/pc/mod/a.archive", "a1"), ("archive/pc/mod/keep.archive", "keep"), ("r6/scripts/a/a.reds", "a script"));
        var b = t.ImportZip("B", ("archive/pc/mod/a.archive", "b-wins"), ("archive/pc/mod/b.archive", "b"), ("r6/scripts/b/deep/b.reds", "b script"));
        var c = t.ImportZip("C", ("archive/pc/mod/c.archive", "c"));
        t.C.UpdateSelected(p => p.WithEntry(a.PackageId, true).WithEntry(c.PackageId, true)
            .WithOverride(a.PackageId, Capabilities.Redscript).WithOverride(b.PackageId, Capabilities.Redscript));
        t.ApplyOk();
        // Target: A replaced on one path by B, C removed, B adds new dirs, custom archive order.
        t.C.UpdateSelected(p => p.WithEntry(b.PackageId, true).WithEntry(c.PackageId, false)
            .WithWinner("ARCHIVE/PC/MOD/A.ARCHIVE", b.PackageId)
            .WithArchiveOrder(new ArchiveOrderSettings(true, ["b.archive"], foreignModlist, true)));
        return (t, a, b, c);
    }

    private static SortedDictionary<string, string> ExpectedFinal(bool foreignModlist)
    {
        var d = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["archive/pc/content/basegame.archive"] = "base game",
            ["bin/x64/Cyberpunk2077.exe"] = "fake exe",
            ["archive/pc/mod/a.archive"] = "b-wins",
            ["archive/pc/mod/keep.archive"] = "keep",
            ["archive/pc/mod/b.archive"] = "b",
            ["r6/scripts/a/a.reds"] = "a script",
            ["r6/scripts/b/deep/b.reds"] = "b script",
            ["archive/pc/mod/modlist.txt"] = ModlistFormat.Render(foreignModlist
                ? ["b.archive", "a.archive", "keep.archive", "my.archive"]
                : ["b.archive", "a.archive", "keep.archive"]),
        };
        if (foreignModlist) d["archive/pc/mod/my.archive"] = "mine";
        return d;
    }

    private static int CountMutations(bool foreignModlist)
    {
        var (t, _, _, _) = Setup(foreignModlist);
        using (t)
        {
            var start = t.Fs.MutationCount;
            t.ApplyOk();
            Assert.Equal(ExpectedFinal(foreignModlist), t.Tree());
            return t.Fs.MutationCount - start;
        }
    }

    public static IEnumerable<object[]> CrashPoints()
    {
        foreach (var foreign in new[] { false, true })
        {
            var n = CountMutations(foreign);
            for (int i = 1; i <= n; i++)
            {
                yield return [foreign, i, CrashTiming.Before];
                yield return [foreign, i, CrashTiming.After];
            }
        }
    }

    [Theory]
    [MemberData(nameof(CrashPoints))]
    public void Crash_at_any_point_then_finish_converges_to_target(bool foreignModlist, int point, CrashTiming timing)
    {
        var (t, _, _, _) = Setup(foreignModlist);
        using (t)
        {
            t.Fs.CrashAt = t.Fs.MutationCount + point;
            t.Fs.Timing = timing;
            Assert.Throws<SimulatedCrashException>(() => t.C.Apply());
            t.Restart();
            Assert.NotNull(t.C.PendingRecovery);
            Assert.False(t.C.Apply().Succeeded); // must recover first
            var (outcome, follow) = t.C.Recover(RecoveryChoice.Finish);
            Assert.Empty(outcome.UnknownPaths);
            Assert.True(follow.Succeeded, follow.Message);
            Assert.Equal(ExpectedFinal(foreignModlist), t.Tree());
            Assert.Empty(Directory.EnumerateFiles(t.Game, "*.tmp", SearchOption.AllDirectories));
            Assert.True(t.C.Preview().Plan.IsNoop);
            if (foreignModlist)
            {
                // The original foreign modlist must still come back byte-for-byte when leaving custom order.
                t.C.SelectProfile(ProfileState.ModsOffId);
                t.ApplyOk();
                Assert.Equal(ForeignModlist, t.Read("archive/pc/mod/modlist.txt"));
            }
        }
    }

    [Theory]
    [MemberData(nameof(CrashPoints))]
    public void Crash_at_any_point_then_return_restores_previous_generation(bool foreignModlist, int point, CrashTiming timing)
    {
        var (t, _, _, _) = Setup(foreignModlist);
        using (t)
        {
            var previous = t.Tree();
            t.Fs.CrashAt = t.Fs.MutationCount + point;
            t.Fs.Timing = timing;
            Assert.Throws<SimulatedCrashException>(() => t.C.Apply());
            t.Restart();
            var (outcome, follow) = t.C.Recover(RecoveryChoice.ReturnToPreviousGeneration);
            Assert.Empty(outcome.UnknownPaths);
            Assert.True(follow.Succeeded, follow.Message);
            Assert.Equal(previous, t.Tree());
            Assert.Null(t.C.Store.GetBaseline(ModlistFormat.RelativePath));
        }
    }

    [Fact]
    public void Unknown_content_found_during_recovery_is_left_alone_and_blocks()
    {
        var (t, _, _, _) = Setup(false);
        using (t)
        {
            // Crash right after the first rename into the game folder, then a third party overwrites that file.
            t.Fs.Timing = CrashTiming.After;
            t.Fs.BeforeMutation = (op, _) =>
            {
                if (op is "move" or "replace" && t.Fs.CrashAt is null) t.Fs.CrashAt = t.Fs.MutationCount;
            };
            Assert.Throws<SimulatedCrashException>(() => t.C.Apply());
            var touched = t.C.Store.GetSteps(t.C.Store.GetUnfinishedOperation()!.Id).First(s => s.State == StepState.Prepared).Step.Path;
            t.Write(touched, "third party content");
            t.Restart();
            var (outcome, follow) = t.C.Recover(RecoveryChoice.Finish);
            Assert.Contains(touched, outcome.UnknownPaths);
            Assert.False(follow.Succeeded);
            Assert.Equal("third party content", t.Read(touched));
        }
    }
}
