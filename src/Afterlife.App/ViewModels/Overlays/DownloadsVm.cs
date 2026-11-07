using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>

/// <summary>Mods downloaded from Nexus Mods through AFTERLIFE, plus a link to the full history on the website.</summary>
public sealed partial class DownloadsVm(IReadOnlyList<DownloadVm> items) : OverlayVm
{
    public override string Title => "DOWNLOAD HISTORY";
    public IReadOnlyList<DownloadVm> All { get; } = items;
    /// <summary>Filtered by the search box (mod name and file).</summary>
    public IReadOnlyList<DownloadVm> Items => string.IsNullOrWhiteSpace(Search) ? All
        : All.Where(i => i.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase) || i.Detail.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Items), nameof(NoMatch))] private string _search = "";
    public bool HasItems => All.Count > 0;
    public bool NoMatch => All.Count > 0 && Items.Count == 0;
    public bool IsEmpty => All.Count == 0;
    public string Summary => All.Count == 0
        ? "Nothing downloaded through AFTERLIFE yet. Use Mod Manager Download on a mod page; it comes here."
        : $"{All.Count} download{(All.Count == 1 ? "" : "s")} through AFTERLIFE · newest first";
    [RelayCommand] private void OpenWebHistory() => AppLinks.Open(DownloadHistory.WebPage);
    [RelayCommand] private void OpenNexus() => AppLinks.Open(NexusClient.CyberpunkPage);
}

public sealed record DownloadVm(string Name, string Detail, string Status, Tone Tone, ICommand Open, ICommand? Reimport)
{
    public bool CanReimport => Reimport is not null;
}

/// <summary>The list of active mods with versions, to copy or save when looking for a problem mod.</summary>
public sealed partial class ExportVm : OverlayVm
{
    private readonly Func<ExportScope, bool, string> _text;
    private readonly Func<ExportScope, bool, string> _csv;
    private readonly Action<string> _copy;

    public ExportVm(Func<ExportScope, bool, string> text, Func<ExportScope, bool, string> csv, Action<string> copy, bool hasPending)
    {
        _text = text;
        _csv = csv;
        _copy = copy;
        HasPending = hasPending;
        _preview = text(ExportScope.InGameNow, false);
    }

    public override string Title => "EXPORT MOD LIST";
    public string Hint => "Everything that is in the game now, with versions, in load order. Paste it on a forum, Discord or into a bug report. Your Nexus key and paths outside the game are never included.";
    public bool HasPending { get; }
    [ObservableProperty] private bool _afterApply;
    [ObservableProperty] private bool _includeOff;
    [ObservableProperty] private string _preview;
    [ObservableProperty] private string? _message;

    private ExportScope Scope => AfterApply ? ExportScope.SelectedAfterApply : ExportScope.InGameNow;
    partial void OnAfterApplyChanged(bool value) => Preview = _text(Scope, IncludeOff);
    partial void OnIncludeOffChanged(bool value) => Preview = _text(Scope, IncludeOff);

    [RelayCommand] private void Copy() => _copy(Preview);
    [RelayCommand] private void SaveText() => Save("txt", "Text file|*.txt", Preview);
    [RelayCommand] private void SaveCsv() => Save("csv", "CSV for Excel|*.csv", _csv(Scope, IncludeOff));

    private void Save(string ext, string filter, string content)
    {
        var d = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = $"afterlife-mods-{DateTime.Now:yyyy-MM-dd-HHmm}.{ext}" };
        if (d.ShowDialog() != true) return;
        try { File.WriteAllText(d.FileName, content, new System.Text.UTF8Encoding(true)); Message = "Saved: " + d.FileName; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Message = "Could not save: " + ex.Message; }
    }
}
