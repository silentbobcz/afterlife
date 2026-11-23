using System.Text;
using Afterlife.Core;
using Afterlife.Infrastructure;

namespace Afterlife.Tests;

/// <summary>
/// Sample logs written after the known format of redscript_rCURRENT.log (no real log from a user yet): the
/// header lines, the error blocks with path, line and column, and the closing caused-by list.
/// </summary>
public class RedscriptLogTests
{
    private const string Stamp = "Mon, 23 Nov 2026 10:11:12 +0100";
    private const string Root = @"C:\Games\Cyberpunk 2077";

    private static string Start(string root) =>
        $"[INFO - {Stamp}] Using defaults for the script manifest (manifest not present)\n" +
        $"[INFO - {Stamp}] Compiling files in {Path.Combine(root, "r6", "scripts")}\n";

    private static string Error(string file, int line, int col, string message) =>
        $"[ERROR - {Stamp}] At {file}:{line}:{col}:\n" +
        "    let x: wref<MissingType> = this.GetThing();\n" +
        "                ^^^^^^^^^^^\n" +
        message + "\n";

    private static string Failure(params string[] mods) =>
        $"[ERROR - {Stamp}] REDScript compilation has failed.\n" +
        "This error has been caused by mods listed below:\n" +
        string.Concat(mods.Select(m => $"- {m}\n")) +
        "You should check if these mods are outdated and update them if possible. They may also be incompatible with the current version of the game, in which case you should remove them and try again.\n";

    private const string Success =
        $"[INFO - {Stamp}] Using defaults for the script manifest (manifest not present)\n" +
        $"[INFO - {Stamp}] Compiling files in C:\\Games\\Cyberpunk 2077\\r6\\scripts\n" +
        $"[WARN - {Stamp}] At C:\\Games\\Cyberpunk 2077\\r6\\scripts\\Old\\a.reds:3:1:\n    func Old() {{}}\n    ^^^^\nthis function is deprecated\n" +
        $"[INFO - {Stamp}] Output successfully saved to C:\\Games\\Cyberpunk 2077\\r6\\cache\\modded\\final.redscripts\n";

    private static string Script(string root, params string[] parts) => Path.Combine([root, "r6", "scripts", .. parts]);

    // ---------- the parser ----------

    [Fact]
    public void Successful_log_has_no_errors()
    {
        var r = RedscriptLog.Parse(Success);
        Assert.Equal(RedscriptOutcome.Succeeded, r.Outcome);
        Assert.True(r.Finished);
        Assert.Empty(r.Errors);
        Assert.Empty(r.CausedBy);
        var w = Assert.Single(r.Warnings);
        Assert.Equal("this function is deprecated", w.Message);
        Assert.Equal(@"C:\Games\Cyberpunk 2077\r6\scripts", r.ScriptsFolder);
    }

    [Fact]
    public void Failed_log_gives_location_message_and_caused_by()
    {
        var log = Start(Root) + Error(@"C:\Games\Cyberpunk 2077\r6\scripts\SomeMod\Main.reds", 42, 15, "unresolved type MissingType") + Failure("SomeMod", "OtherMod");
        var r = RedscriptLog.Parse(log);
        Assert.Equal(RedscriptOutcome.Failed, r.Outcome);
        Assert.True(r.Finished);
        var e = Assert.Single(r.Errors);
        Assert.Equal(@"C:\Games\Cyberpunk 2077\r6\scripts\SomeMod\Main.reds", e.File);
        Assert.Equal(42, e.Line);
        Assert.Equal(15, e.Column);
        Assert.Equal("unresolved type MissingType", e.Message);
        Assert.Contains("let x: wref<MissingType>", e.Text);
        Assert.DoesNotContain(Stamp, e.Text);
        Assert.Equal(["SomeMod", "OtherMod"], r.CausedBy);
    }

    [Fact]
    public void Forward_slashes_crlf_and_other_wording_are_read()
    {
        var log = "[ERROR - x] At /home/me/Games/Cyberpunk 2077/r6/scripts/A/b.reds:7\r\n  code\r\n  ^\r\nsomething broke\r\n" +
                  "[ERROR - x] Compilation failed\r\nCaused by mods:\r\n* A\r\n";
        var r = RedscriptLog.Parse(log);
        Assert.Equal(RedscriptOutcome.Failed, r.Outcome);
        var e = Assert.Single(r.Errors);
        Assert.Equal("/home/me/Games/Cyberpunk 2077/r6/scripts/A/b.reds", e.File);
        Assert.Equal(7, e.Line);
        Assert.Null(e.Column);
        Assert.Equal("something broke", e.Message);
        Assert.Equal(["A"], r.CausedBy);
    }

    [Fact]
    public void Empty_and_blank_logs_are_unknown()
    {
        Assert.Equal(RedscriptOutcome.Unknown, RedscriptLog.Parse("").Outcome);
        Assert.Equal(RedscriptOutcome.Unknown, RedscriptLog.Parse("  \r\n").Outcome);
        Assert.Equal(RedscriptOutcome.Unknown, RedscriptLog.Parse(Array.Empty<byte>()).Outcome);
        Assert.False(RedscriptLog.Parse((string?)null).Finished);
    }

    [Fact]
    public void Cut_log_keeps_what_it_has()
    {
        var full = Start(Root) + Error(@"C:\Games\Cyberpunk 2077\r6\scripts\SomeMod\Main.reds", 42, 15, "unresolved type MissingType") + Failure("SomeMod");
        var cut = full[..(full.IndexOf("let x", StringComparison.Ordinal) + 5)];
        var r = RedscriptLog.Parse(cut);
        Assert.Equal(RedscriptOutcome.Failed, r.Outcome);
        Assert.False(r.Finished);
        var e = Assert.Single(r.Errors);
        Assert.Equal(42, e.Line);
        Assert.Equal("", e.Message);
        Assert.Empty(r.CausedBy);

        var early = RedscriptLog.Parse(Start(Root)[..30]);
        Assert.Equal(RedscriptOutcome.Unknown, early.Outcome);
        Assert.Empty(early.Errors);
    }

    [Fact]
    public void Encodings_are_decoded()
    {
        var log = Start(@"C:\Users\Jiří\Hry\Cyberpunk 2077") + Error(@"C:\Users\Jiří\Hry\Cyberpunk 2077\r6\scripts\Mód\Main.reds", 1, 2, "boom") + Failure("Mód");
        var forms = new[]
        {
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(log)).ToArray(),
            Encoding.UTF8.GetBytes(log),
            Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(log)).ToArray(),
            Encoding.Unicode.GetBytes(log),
            Encoding.BigEndianUnicode.GetPreamble().Concat(Encoding.BigEndianUnicode.GetBytes(log)).ToArray(),
        };
        foreach (var bytes in forms)
        {
            var r = RedscriptLog.Parse(bytes);
            Assert.Equal(RedscriptOutcome.Failed, r.Outcome);
            Assert.Equal(["Mód"], r.CausedBy);
            Assert.EndsWith(@"\Mód\Main.reds", Assert.Single(r.Errors).File);
        }

        // Latin-1 (not valid UTF-8): readable, not thrown away
        var latin = RedscriptLog.Parse(Encoding.Latin1.GetBytes(log));
        Assert.Equal(["Mód"], latin.CausedBy);

        // a UTF-8 log cut inside "ó"
        var utf8 = Encoding.UTF8.GetBytes(Start(Root) + Error(@"C:\Games\Cyberpunk 2077\r6\scripts\A\a.reds", 1, 1, "x") + "Mó");
        var text = RedscriptLog.Decode(utf8[..^1]);
        Assert.EndsWith("M", text.TrimEnd());
        Assert.Contains("Cyberpunk 2077", text);
    }

    [Theory]
    [InlineData(@"C:\Games\Cyberpunk 2077\r6\scripts\Mod\a.reds", Root, "r6/scripts/Mod/a.reds")]
    [InlineData(@"c:/games/cyberpunk 2077//r6/scripts/Mod/a.reds", Root, "r6/scripts/Mod/a.reds")]
    [InlineData(@"D:\Other\Cyberpunk 2077\r6\scripts\loose.reds", Root, "r6/scripts/loose.reds")]
    [InlineData(@"C:\Games\Cyberpunk 2077\mods\RedMod\scripts\x.reds", null, "mods/RedMod/scripts/x.reds")]
    [InlineData(@"C:\Games\Cyberpunk 2077\red4ext\plugins\Lib\Lib.reds", Root, "red4ext/plugins/Lib/Lib.reds")]
    [InlineData(@"Mod\a.reds", null, "r6/scripts/Mod/a.reds")]
    [InlineData(@"C:\Elsewhere\a.reds", Root, null)]
    public void Log_paths_become_game_paths(string file, string? root, string? expected) =>
        Assert.Equal(expected, RedscriptLog.ToGamePath(file, root));

    [Theory]
    [InlineData("r6/scripts/Mod/x/a.reds", "Mod")]
    [InlineData("r6/scripts/a.reds", null)]
    [InlineData("mods/X/scripts/a.reds", null)]
    public void Script_folder(string path, string? folder) => Assert.Equal(folder, RedscriptLog.ScriptFolder(path));

    // ---------- tying the log to the library ----------

    private static void Deploy(TestEnv t, params PackageManifest[] packages)
    {
        t.C.UpdateSelected(p =>
        {
            foreach (var m in packages) p = p.WithEntry(m.PackageId, true).WithOverride(m.PackageId, Capabilities.Redscript);
            return p;
        });
        t.ApplyOk();
    }

    private static void WriteLog(TestEnv t, string text, DateTime? writtenUtc = null)
    {
        t.Write(RedscriptLog.RelativePath, text);
        File.SetLastWriteTimeUtc(t.GameFile(RedscriptLog.RelativePath), writtenUtc ?? DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public void One_error_of_one_library_mod_is_found_by_its_deployed_file()
    {
        using var t = new TestEnv();
        var some = t.ImportZip("SomeMod", ("r6/scripts/SomeMod/Main.reds", "main"));
        Deploy(t, some);
        WriteLog(t, Start(t.Game) + Error(Script(t.Game, "SomeMod", "Main.reds"), 42, 15, "unresolved type MissingType") + Failure("SomeMod"));

        var r = t.C.AnalyzeRedscript();
        Assert.Equal(RedscriptLogStatus.Failed, r.Status);
        Assert.Equal(t.C.SelectedProfile.Id, r.ProfileId);
        var c = Assert.Single(r.LibraryCulprits);
        Assert.Equal(some.PackageId, c.PackageId);
        Assert.Equal("SomeMod", c.Name);
        Assert.Equal("1.0", c.Version);
        Assert.True(c.Enabled);
        Assert.True(c.NamedInLog);
        Assert.Equal(RedscriptMatch.DeployedFile, c.Match);
        Assert.Equal(["r6/scripts/SomeMod/Main.reds"], c.Files);
        Assert.Equal("unresolved type MissingType", Assert.Single(c.Errors).Message);
        Assert.Empty(r.ForeignCulprits);
        Assert.Contains("SomeMod 1.0", r.AiPrompt);
        Assert.Contains("unresolved type MissingType", r.AiPrompt);

        // the same mod in a profile where it is off
        var other = t.C.CreateProfile("Other");
        Assert.False(Assert.Single(t.C.AnalyzeRedscript(other).LibraryCulprits).Enabled);
    }

    [Fact]
    public void Several_mods_by_file_by_folder_and_by_name_in_the_log()
    {
        using var t = new TestEnv();
        var some = t.ImportZip("SomeMod", ("r6/scripts/SomeMod/Main.reds", "main"));
        var other = t.ImportZip("OtherMod", ("r6/scripts/OtherMod/Core.reds", "core"));
        var third = t.ImportZip("ThirdMod", ("r6/scripts/ThirdMod/T.reds", "t"));
        var idle = t.ImportZip("Idle", ("r6/scripts/Idle/I.reds", "i"));
        Deploy(t, some, other, third, idle);
        // a file the library does not know, inside the folder of a library mod
        t.Write("r6/scripts/OtherMod/Extra.reds", "extra by hand");
        WriteLog(t, Start(t.Game)
            + Error(Script(t.Game, "SomeMod", "Main.reds"), 1, 1, "first")
            + Error(Script(t.Game, "SomeMod", "Main.reds"), 9, 3, "second")
            + Error(Script(t.Game, "OtherMod", "Extra.reds"), 5, 2, "third")
            + Failure("SomeMod", "OtherMod", "ThirdMod"));

        var r = t.C.AnalyzeRedscript();
        Assert.Equal(RedscriptLogStatus.Failed, r.Status);
        Assert.Equal(["SomeMod", "OtherMod", "ThirdMod"], r.LibraryCulprits.Select(c => c.Name));
        var s = r.LibraryCulprits[0];
        Assert.Equal(2, s.Errors.Count);
        Assert.Single(s.Files);
        var o = r.LibraryCulprits[1];
        Assert.Equal(RedscriptMatch.ScriptFolder, o.Match);
        Assert.Equal(["r6/scripts/OtherMod/Extra.reds"], o.Files);
        var th = r.LibraryCulprits[2];
        Assert.True(th.NamedInLog);
        Assert.Empty(th.Errors);
        Assert.DoesNotContain(r.LibraryCulprits, c => c.PackageId == idle.PackageId);
        Assert.Empty(r.ForeignCulprits);
    }

    [Fact]
    public void Scripts_installed_by_hand_are_not_library_mods()
    {
        using var t = new TestEnv();
        var lib = t.ImportZip("HandMod", ("r6/scripts/LibOnly/x.reds", "x"));
        Deploy(t, lib);
        t.Write("r6/scripts/HandMod/h.reds", "by hand");
        t.Write("r6/scripts/Loose.reds", "loose");
        WriteLog(t, Start(t.Game)
            + Error(Script(t.Game, "HandMod", "h.reds"), 3, 4, "no such method")
            + Error(Script(t.Game, "Loose.reds"), 1, 1, "syntax error")
            + Failure("HandMod", "Loose.reds", "Ghost"));

        var r = t.C.AnalyzeRedscript();
        Assert.Empty(r.LibraryCulprits); // same mod name, but no file and no folder of it: no guessing
        Assert.Equal(["HandMod", "Loose.reds", "Ghost"], r.ForeignCulprits.Select(c => c.Name));
        Assert.Equal(["r6/scripts/HandMod/h.reds"], r.ForeignCulprits[0].Files);
        Assert.All(r.ForeignCulprits, c => Assert.True(c.NamedInLog));
        Assert.Empty(r.ForeignCulprits[2].Errors);
    }

    [Fact]
    public void Cut_log_in_the_game_is_still_tied_to_the_mod()
    {
        using var t = new TestEnv();
        var some = t.ImportZip("SomeMod", ("r6/scripts/SomeMod/Main.reds", "main"));
        Deploy(t, some);
        var full = Start(t.Game) + Error(Script(t.Game, "SomeMod", "Main.reds"), 42, 15, "unresolved type MissingType") + Failure("SomeMod");
        WriteLog(t, full[..(full.IndexOf("^^^", StringComparison.Ordinal) + 3)]);

        var r = t.C.AnalyzeRedscript();
        Assert.Equal(RedscriptLogStatus.Failed, r.Status);
        Assert.False(r.Log.Finished);
        Assert.Equal(some.PackageId, Assert.Single(r.LibraryCulprits).PackageId);
        Assert.False(r.LibraryCulprits[0].NamedInLog);
        Assert.Contains("ends early", r.AiPrompt);
    }

    [Fact]
    public void Missing_and_stale_logs_are_reported()
    {
        using var t = new TestEnv();
        var some = t.ImportZip("SomeMod", ("r6/scripts/SomeMod/Main.reds", "main"));
        Deploy(t, some);
        var missing = t.C.AnalyzeRedscript();
        Assert.Equal(RedscriptLogStatus.Missing, missing.Status);
        Assert.Null(missing.LogWritten);
        Assert.False(missing.HasCulprits);

        // written before the last apply: it describes another set of mods
        WriteLog(t, Start(t.Game) + Error(Script(t.Game, "SomeMod", "Main.reds"), 1, 1, "old") + Failure("SomeMod"), DateTime.UtcNow.AddDays(-1));
        var stale = t.C.AnalyzeRedscript();
        Assert.Equal(RedscriptLogStatus.Stale, stale.Status);
        Assert.Equal(RedscriptOutcome.Failed, stale.Log.Outcome);

        WriteLog(t, Success.Replace(Root, t.Game));
        Assert.Equal(RedscriptLogStatus.Succeeded, t.C.AnalyzeRedscript().Status);
        Assert.Equal(RedscriptLogStatus.Stale, t.C.AnalyzeRedscript(launchedAt: DateTimeOffset.UtcNow.AddHours(1)).Status);
    }

    [Fact]
    public void Ai_prompt_has_no_personal_paths()
    {
        const string game = @"C:\Users\Jane Doe\Games\Cyberpunk 2077";
        var log = RedscriptLog.Parse(Start(game) + Error(game + @"\r6\scripts\HandMod\h.reds", 3, 4, "no such method") + Failure("HandMod"));
        var profile = ProfileState.Create("street", "Street");
        var report = RedscriptDiagnosis.Match(log, RedscriptLogStatus.Failed, game + @"\r6\logs\redscript_rCURRENT.log", DateTimeOffset.UtcNow,
            game, profile, [], new Dictionary<string, LedgerEntry>());
        var text = RedscriptPrompt.Build(report, "2.3 · build 3.0.88.46235", ["Codeware 1.15.0", "redscript 0.5.27"],
            s => LogPrivacy.Scrub(s, game, null, "Jane Doe", "JANE-PC", @"C:\Users\Jane Doe"));

        Assert.DoesNotContain("Jane", text);
        Assert.Contains("<game>", text);
        Assert.Contains("2.3 · build 3.0.88.46235", text);
        Assert.Contains("HandMod (installed by hand", text);
        Assert.Contains("r6/scripts/HandMod/h.reds", text);
        Assert.Contains("no such method", text);
        Assert.Contains("- Codeware 1.15.0", text);
        Assert.Contains("compilation failed", text);
    }

    [Fact]
    public void Frameworks_of_the_profile_and_by_hand_are_listed()
    {
        var cw = new PackageManifest("p1", "codeware", "Codeware", "1.15.0", PackageOrigin.Manual, null, "d", [], [Capabilities.Codeware], []);
        var off = new PackageManifest("p2", "axl", "ArchiveXL", "1.2", PackageOrigin.Manual, null, "d", [], [Capabilities.ArchiveXl], []);
        var profile = ProfileState.Create("s", "S").WithEntry("p1", true).WithEntry("p2", false);
        var list = RedscriptPrompt.Frameworks(profile, [cw, off], new HashSet<string> { Capabilities.Red4ext },
            new Dictionary<string, string?> { [Capabilities.Red4ext] = "1.25.1" });
        Assert.Equal(["Codeware 1.15.0", "RED4ext 1.25.1 (installed by hand)"], list);
    }
}
