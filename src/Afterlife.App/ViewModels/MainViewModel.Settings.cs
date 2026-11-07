using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using Afterlife.App.Services;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Settings panel: game folder, Nexus key, nxm handler, display, storage.</summary>
public sealed partial class MainViewModel
{
    private void CopyDiagnostics()
    {
        if (_overview is null) { ShowToast("Open a game install first."); return; }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(ModListExport.Text(_overview, ExportScope.InGameNow, true, AppVersion, DateTimeOffset.Now));
        sb.AppendLine($"Windows {Environment.OSVersion.Version} · .NET {Environment.Version} · {System.Globalization.CultureInfo.CurrentUICulture.Name}");
        sb.AppendLine($"Unfinished change: {(_overview.Status.Unfinished is null ? "no" : "yes")} · library files in game: {_overview.Status.LedgerFiles}");
        try
        {
            var log = Directory.Exists(App.LogDirectory) ? Directory.GetFiles(App.LogDirectory, "*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault() : null;
            if (log is not null)
            {
                var lines = File.ReadAllLines(log).Where(l => l.Contains("ERR", StringComparison.OrdinalIgnoreCase) || l.Contains("WARN", StringComparison.OrdinalIgnoreCase)).TakeLast(30).ToList();
                if (lines.Count > 0) { sb.AppendLine("LAST WARNINGS AND ERRORS"); foreach (var l in lines) sb.AppendLine("  " + l); }
            }
        }
        catch (IOException) { }
        CopyToClipboard(sb.ToString().Replace(_overview.Status.GameRoot, "<game>", StringComparison.OrdinalIgnoreCase)
            .Replace(Environment.UserName, "<user>", StringComparison.OrdinalIgnoreCase));
    }

    private async Task LoadStorageAsync(SettingsVm vm)
    {
        if (!_host.IsOpen) return;
        try { vm.SetStorage(await _host.RunAsync(c => c.StorageInfo())); }
        catch (Exception ex) when (IsExpected(ex)) { }
    }

    private async Task PurgeAsync(SettingsVm vm, string kind)
    {
        if (!_host.IsOpen) return;
        var (removed, downloads, temp) = kind switch
        {
            "removed" => (true, false, false), "downloads" => (false, true, false), "temp" => (false, false, true), _ => (true, true, true),
        };
        var lines = new List<string>();
        if (removed) lines.Add($"Removed files ({vm.RemovedSize}): mods, manual mods and leftovers you took out. They can no longer be restored.");
        if (downloads) lines.Add($"Downloaded archives ({vm.DownloadSize}): Import again in Download history stops working; the mods in your library stay.");
        if (temp) lines.Add($"Temporary files ({vm.TempSize}): unpacked imports that did not finish.");
        var ok = await AskAsync(new ConfirmVm("DELETE KEPT FILES?",
            "These files are deleted for good. Your game, the mods in the library and your profiles are not touched.",
            "DELETE", danger: true, lines, null, true, "WHAT GOES"));
        if (!ok) return;
        try
        {
            var freed = await _host.RunAsync(c => c.PurgeStorage(removed, downloads, temp));
            vm.Message = $"Freed {SettingsVm.Size(freed)}.";
            vm.MessageTone = Tone.Ok;
            await RefreshAsync();
        }
        catch (Exception ex) when (IsExpected(ex)) { vm.Message = ex.Message; vm.MessageTone = Tone.Block; }
        await LoadStorageAsync(vm);
    }

    /// <summary>Undoes what AFTERLIFE set up in Windows; the game and the library stay unless chosen.</summary>
    private async Task RemoveFromPcAsync(SettingsVm vm)
    {
        var confirm = new ConfirmVm("REMOVE AFTERLIFE FROM THIS PC?",
            "AFTERLIFE gives Mod Manager Download links back to the previous app, deletes its settings and your saved Nexus key. Then delete AFTERLIFE.exe yourself. "
            + "Mods stay in the game as they are now; to take them out first, switch to Mods off and apply.",
            "REMOVE", danger: true, [$"Library: {AppSettings.ResolveLibrary(null)}"], "Also delete the library (all imported mods and removed files)", false, "KEPT UNLESS CHECKED");
        if (!await AskAsync(confirm)) return;
        try
        {
            if (NxmProtocol.IsRegistered) NxmProtocol.Unregister();
            NexusKeyStore.Clear();
            var lib = AppSettings.ResolveLibrary(null);
            if (confirm.OptionChecked) { _host.Dispose(); try { Directory.Delete(lib, true); } catch (IOException) { } }
            try { Directory.Delete(AppSettings.SettingsDirectory, true); } catch (IOException) { }
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            vm.Message = "Windows refused it: " + ex.Message;
            vm.MessageTone = Tone.Block;
        }
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var vm = new SettingsVm(GameFolder, GameVersion is null ? GameBuild : $"{GameVersion} · build {GameBuild}", AppSettings.ResolveLibrary(null),
            AppSettings.Load().ReduceMotion, AppSettings.Load().ActivityLog, NexusKeyStore.Load() is not null, SafeNxmRegistered(), SafeOtherHandler(),
            new SettingsActions(ChangeGameFolderAsync, SetReduceMotion, SetActivityLog, SaveNexusKeyAsync, ForgetNexusKey, CheckNexusAsync, SetNxmHandlerAsync,
                () => OpenGameFolder(), () => OpenInbox(), LoadStorageAsync, PurgeAsync, CopyDiagnostics, RemoveFromPcAsync))
        { CloseCommand = new RelayCommand(() => Overlay = null) };
        Overlay = vm;
        vm.LoadStorageLater();
        if (vm.HasNexusKey) _ = CheckNexusAsync(vm);
    }

    private static bool SafeNxmRegistered() { try { return NxmProtocol.IsRegistered; } catch (Exception) { return false; } }

    private static string? SafeOtherHandler() { try { return NxmProtocol.OtherHandler(); } catch (Exception) { return null; } }

    private async Task ChangeGameFolderAsync(SettingsVm vm, string folder)
    {
        try
        {
            await _host.RunAsync(c => c.ChangeGameRoot(folder));
            vm.GameFolder = folder;
            vm.Message = "Game folder changed. The new folder was scanned; nothing in it was changed.";
            vm.MessageTone = Tone.Ok;
            await RefreshAsync();
            ScanCompleted?.Invoke();
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            vm.Message = ex.Message;
            vm.MessageTone = Tone.Block;
        }
    }

    private async Task SaveNexusKeyAsync(SettingsVm vm, string key)
    {
        vm.NexusStatus = "Checking the key with Nexus Mods…";
        vm.NexusTone = Tone.Info;
        try
        {
            using var client = new NexusClient(key, AppVersion);
            var acc = await client.ValidateAsync();
            NexusKeyStore.Save(key);
            vm.HasNexusKey = true;
            SetAccount(acc, vm);
        }
        catch (NexusException ex)
        {
            vm.NexusStatus = "Not saved: " + ex.Message;
            vm.NexusTone = Tone.Block;
        }
    }

    private void ForgetNexusKey()
    {
        NexusKeyStore.Clear();
        _nexusAccount = null;
        NexusBadge = "NEXUS · no key";
        NexusTone = Tone.Neutral;
    }

    /// <summary>Asks Nexus who owns the key: name and Premium. Runs at start and when Settings opens.</summary>
    private async Task CheckNexusAsync(SettingsVm? vm)
    {
        var key = NexusKeyStore.Load();
        if (key is null)
        {
            ForgetNexusKey();
            if (vm is not null) { vm.HasNexusKey = false; vm.NexusStatus = "No key yet"; vm.NexusTone = Tone.Neutral; }
            return;
        }
        try
        {
            using var client = new NexusClient(key, AppVersion);
            SetAccount(await client.ValidateAsync(), vm);
        }
        catch (NexusException ex)
        {
            NexusBadge = "NEXUS · offline";
            NexusTone = Tone.Warn;
            if (vm is not null) { vm.NexusStatus = ex.Message; vm.NexusTone = Tone.Warn; }
        }
    }

    private void SetAccount(NexusAccount acc, SettingsVm? vm)
    {
        _nexusAccount = acc;
        OnPropertyChanged(nameof(CanUpdateAll));
        NexusBadge = $"NEXUS · {acc.Tier.ToUpperInvariant()}";
        NexusTone = acc.IsPremium ? Tone.Ok : Tone.Info;
        if (vm is null) return;
        vm.NexusTier = acc.Tier;
        vm.NexusTone = acc.IsPremium ? Tone.Ok : Tone.Info;
        vm.NexusStatus = acc.IsPremium
            ? $"{acc.Name} · Premium. Mod Manager Download links install straight away; direct downloads from AFTERLIFE work too."
            : $"{acc.Name} · {acc.Tier}. Nexus allows free accounts to download only from the website: click Mod Manager Download and the mod arrives here.";
    }

    private Task SetNxmHandlerAsync(SettingsVm vm, bool on)
    {
        try
        {
            if (on) NxmProtocol.Register(); else NxmProtocol.Unregister();
            vm.Message = on ? "Mod Manager Download links now open AFTERLIFE." : "AFTERLIFE no longer handles Mod Manager Download links.";
            vm.MessageTone = Tone.Ok;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            vm.SetNxmSilently(!on);
            vm.Message = "Windows refused the change: " + ex.Message;
            vm.MessageTone = Tone.Block;
        }
        return Task.CompletedTask;
    }

    private void SetReduceMotion(bool reduce)
    {
        Controls.Fx.SetReduceMotion(reduce);
        var current = AppSettings.Load();
        (current with { LibraryRoot = current.LibraryRoot ?? AppSettings.ResolveLibrary(null), ReduceMotion = reduce }).Save();
    }

    /// <summary>Activity log on/off, saved with the settings and effective at once.</summary>
    private void SetActivityLog(bool on)
    {
        var current = AppSettings.Load();
        (current with { LibraryRoot = current.LibraryRoot ?? AppSettings.ResolveLibrary(null), ActivityLog = on }).Save();
        ActivityLog.Enabled = on;
        ActivityLog.Write("app", $"activity log switched on · AFTERLIFE {AppVersion} · game {GameVersion ?? "?"} build {GameBuild}");
    }
}
