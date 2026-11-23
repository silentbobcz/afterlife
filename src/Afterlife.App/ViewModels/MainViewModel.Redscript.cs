using System.Globalization;
using System.IO;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>
/// The game could not compile redscript: ANALYZE REDSCRIPT ERROR (first line of the LIST &amp; ORDER menu), the window
/// that names the mods behind it, and the banner after returning from the game or at start. The automatic check reads
/// only the log itself (no scan of the game folder) and stays quiet for a log the user has already seen.
/// </summary>
public sealed partial class MainViewModel
{
    private const int ErrorLinesPerCard = 3;

    /// <summary>When the game was last started with JACK IN in this session (set next to <c>LaunchGame</c>).</summary>
    private DateTimeOffset? _redscriptLaunchedAt;

    /// <summary>Write time of the log the automatic check looked at last in this session: the same log is not read twice.</summary>
    private DateTimeOffset? _redscriptSeen;

    /// <summary>Write time of the log the banner is about.</summary>
    private DateTimeOffset? _redscriptBannerLog;

    private bool _redscriptChecking;

    [ObservableProperty] private bool _redscriptBanner;

    [ObservableProperty] private string _redscriptBannerText = "";

    private sealed record RedscriptCheck(DateTimeOffset Written, bool Failed, int Named, bool AfterLaunch);

    /// <summary>
    /// The automatic check, at start and when the window comes back to the front: a banner when the log is newer than
    /// the last one handled and says the compilation failed. Never while an action or a dialog runs (except at start,
    /// where the background work after opening may already run) and never while the game runs.
    /// </summary>
    public async Task CheckRedscriptLogAsync(bool atStart = false)
    {
        if (!_host.IsOpen || _redscriptChecking) return;
        if (!atStart && (IsBusy || IsOperating || Overlay is not null)) return;
        _redscriptChecking = true;
        try
        {
            var launchedAt = _redscriptLaunchedAt;
            var seen = _redscriptSeen;
            var check = await _host.RunAsync(c => CheckLog(c, launchedAt, seen));
            if (check is null) return;
            _redscriptSeen = check.Written;
            if (!check.Failed) return;
            _redscriptBannerLog = check.Written;
            RedscriptBannerText = BannerText(check.Named, check.AfterLaunch);
            RedscriptBanner = true;
        }
        catch (Exception ex) when (IsExpected(ex)) { }
        finally { _redscriptChecking = false; }
    }

    /// <summary>Runs on the worker thread: the write time of the log, then only a parse of it and reads of the store.</summary>
    private static RedscriptCheck? CheckLog(InstallCoordinator c, DateTimeOffset? launchedAt, DateTimeOffset? seen)
    {
        if (string.IsNullOrWhiteSpace(c.GameRoot)) return null;
        var path = GamePath.Parse(RedscriptLog.RelativePath).ToOsPath(c.GameRoot);
        if (!File.Exists(path)) return null;
        var written = new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        if (seen == written) return null;
        if (!RedscriptHandled.IsNew(written, c.Store.GetMeta(RedscriptHandled.MetaKey))) return new RedscriptCheck(written, false, 0, false);
        // the game may still write the log; look again when the window comes back after the game
        if (new WindowsGameEnvironment().IsGameRunning()) return null;
        var notBefore = NotBefore(c, launchedAt);
        var report = RedscriptDiagnosis.Analyze(c.GameRoot, c.SelectedProfile, c.Store.GetPackages(), c.Store.GetLedger(), notBefore);
        return new RedscriptCheck(written, report.Status == RedscriptLogStatus.Failed,
            report.LibraryCulprits.Count + report.ForeignCulprits.Count, launchedAt is { } at && written >= at);
    }

    /// <summary>A log older than this describes another set of mods: the last apply or the last launch from AFTERLIFE.</summary>
    private static DateTimeOffset? NotBefore(InstallCoordinator c, DateTimeOffset? launchedAt)
    {
        DateTimeOffset? notBefore = c.Store.GetLastGeneration()?.CreatedAt;
        if (launchedAt is { } l && (notBefore is null || l > notBefore)) notBefore = l;
        return notBefore;
    }

    private static string BannerText(int named, bool afterLaunch)
    {
        if (named == 0)
            return "The game could not compile redscript, so none of your script mods ran in the last session. Open the analysis to see the files the log names.";
        var first = afterLaunch
            ? "The game could not compile redscript, so none of your script mods ran in the last session."
            : "The game could not compile redscript the last time it ran, so none of your script mods ran.";
        return $"{first} The log names {Plural.Of(named, "mod")}. AFTERLIFE can switch {Plural.Word(named, "it", "them")} off for you; nothing is deleted.";
    }

    /// <summary>Dismiss on the banner: hidden until the game writes a new log.</summary>
    [RelayCommand]
    private async Task DismissRedscript()
    {
        RedscriptBanner = false;
        if (_redscriptBannerLog is { } written) await MarkRedscriptHandledAsync(written);
    }

    private async Task MarkRedscriptHandledAsync(DateTimeOffset written)
    {
        try { await _host.RunAsync(c => c.Store.SetMeta(RedscriptHandled.MetaKey, RedscriptHandled.Format(written))); }
        catch (Exception ex) when (IsExpected(ex)) { }
    }

    /// <summary>
    /// ANALYZE REDSCRIPT ERROR and ANALYZE on the banner: always ends with an answer, the window when the compilation
    /// failed, otherwise a line in the notice.
    /// </summary>
    [RelayCommand]
    private Task AnalyzeRedscriptLog() => RunUiAsync(async () =>
    {
        if (!_host.IsOpen) return;
        if (string.IsNullOrWhiteSpace(GameFolder)) { Say("Set the game folder in Settings first.", Tone.Warn); return; }
        var launchedAt = _redscriptLaunchedAt;
        var (report, gameRoot, notBefore) = await _host.RunAsync(c => (c.AnalyzeRedscript(null, launchedAt), c.GameRoot, NotBefore(c, launchedAt)));
        switch (report.Status)
        {
            case RedscriptLogStatus.Missing:
                Say("No script log found in r6\\logs. Start the game once with redscript installed, then analyze again.", Tone.Warn);
                return;
            case RedscriptLogStatus.Unreadable:
            case RedscriptLogStatus.Unknown:
                Say("The script log could not be read completely. Close the game if it is still running, then analyze again.", Tone.Warn);
                return;
            case RedscriptLogStatus.Stale:
                Say($"The script log is older than the last launch (log {When(report.LogWritten)} · launch {When(notBefore)}), so it does not describe the last session. Start the game, return here and analyze again.", Tone.Warn);
                return;
            case RedscriptLogStatus.Succeeded:
                Say("Redscript compiled without errors in the last game session. Nothing to fix.", Tone.Ok);
                return;
        }

        // the user is looking at this log now: the banner goes and does not come back for it
        RedscriptBanner = false;
        if (report.LogWritten is { } written)
        {
            _redscriptSeen = written;
            await MarkRedscriptHandledAsync(written);
        }

        var ids = await AskAsync(BuildRedscriptVm(report, gameRoot, launchedAt), v => ((RedscriptVm)v).Result);
        if (ids is null || ids.Count == 0) return;
        // the normal switch-off: the dialog about the mods that need them comes next when there are any
        var all = await WithRelatedAsync(ids, false);
        if (all is null) return;
        await _host.RunAsync(c => c.SetEnabledMany(c.SelectedProfile.Id, all, false));
        await RefreshAsync();
        Say($"{Plural.Of(all.Count, "mod")} switched off in {SelectedProfile?.Name}. Apply to remove {Plural.Word(all.Count, "it", "them")} from the game.", Tone.Info);
    });

    private RedscriptVm BuildRedscriptVm(RedscriptReport report, string gameRoot, DateTimeOffset? launchedAt)
    {
        var modsOff = IsModsOff;
        var library = report.LibraryCulprits.Select(c => new RedscriptCulpritVm(c.Name, c.Version,
            CardLines(c.Errors, c.Files, gameRoot), c.PackageId, c.Enabled && !modsOff,
            c.Enabled || modsOff ? null : "Already off in this profile. Apply the changes to take it out of the game.")).ToList();
        var foreign = report.ForeignCulprits.Select(c => ForeignCard(c, gameRoot)).ToList();

        const string failed = "The game could not compile redscript, so no script mod is running.";
        var message = library.Count > 0 ? $"{failed} These mods from your library are named in the log."
            : foreign.Count == 1 ? $"{failed} The log names a script that was not installed through AFTERLIFE."
            : foreign.Count > 1 ? $"{failed} The log names {foreign.Count} scripts that were not installed through AFTERLIFE."
            : $"{failed} The log does not say which file caused it; the text below has what the log says.";
        var afterLaunch = launchedAt is { } at && report.LogWritten >= at;
        var logLine = $"Log written {When(report.LogWritten)}{(afterLaunch ? ", after the last launch" : "")} · profile {report.ProfileName}";
        var libraryNote = modsOff
            ? "The selected profile is Mods off. Choose the profile you play with to switch mods off."
            : "Usually an older mod that does not match the game or a framework it needs. Switching it off lets the other script mods load again.";
        return new RedscriptVm(message, logLine, libraryNote, library, foreign, report.AiPrompt, t => CopyToClipboard(t, "Copied to clipboard"));
    }

    /// <summary>"r6\scripts\Mod\Main.reds:42:15  message", the first few, then how many more the log has.</summary>
    private static List<string> CardLines(IReadOnlyList<RedscriptError> errors, IReadOnlyList<string> files, string gameRoot)
    {
        if (errors.Count == 0) return files.Select(Shown).ToList();
        var lines = errors.Take(ErrorLinesPerCard).Select(e =>
        {
            var at = Shown(e.File is null ? "" : RedscriptLog.ToGamePath(e.File, gameRoot) ?? e.File)
                + (e.Line is { } l ? $":{l}" : "") + (e.Column is { } col ? $":{col}" : "");
            return $"{at}  {e.Message}".Trim();
        }).ToList();
        if (errors.Count > ErrorLinesPerCard) lines.Add($"+ {errors.Count - ErrorLinesPerCard} more in the log");
        return lines;
    }

    /// <summary>A script outside the library: its name, where it lies and a link to its folder.</summary>
    private static RedscriptCulpritVm ForeignCard(RedscriptForeignCulprit c, string gameRoot)
    {
        var file = c.Files.FirstOrDefault() ?? "r6/scripts/" + c.Name;
        var rooted = Path.IsPathRooted(file);
        var shown = rooted ? Shown(file) : "<game>\\" + Shown(file);
        string? folder;
        try
        {
            var module = rooted ? null : RedscriptLog.ScriptFolder(file);
            folder = module is not null ? Path.Combine(gameRoot, "r6", "scripts", module)
                : rooted ? Path.GetDirectoryName(file)
                : c.Files.Count == 0 ? Path.Combine(gameRoot, "r6", "scripts", c.Name)
                : Path.GetDirectoryName(Path.Combine(gameRoot, file.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch (ArgumentException) { folder = null; }
        RelayCommand? open = null;
        if (folder is { } dir && Directory.Exists(dir)) open = new RelayCommand(() => AppLinks.OpenFolder(dir));
        return new RedscriptCulpritVm(c.Name, "", [shown], openFolderCommand: open);
    }

    private static string Shown(string path) => path.Replace('/', '\\');

    /// <summary>"23 Nov 2026, 21:14" in local time.</summary>
    private static string When(DateTimeOffset? t) =>
        t is { } v ? v.ToLocalTime().ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) : "unknown";
}
