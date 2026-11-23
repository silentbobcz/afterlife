using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>One name under a line of the preview; <see cref="Open"/> opens its Nexus page (full edition only).</summary>
public sealed record LoadListItemVm(string Text, ICommand? Open = null, string? OpenLabel = null)
{
    public bool CanOpen => Open is not null;
}

/// <summary>A line of the preview with a number ("12 mods change place"); the names open below it.</summary>
public sealed partial class LoadListGroupVm : ObservableObject
{
    public LoadListGroupVm(string header, IReadOnlyList<LoadListItemVm> items, Tone tone = Tone.Neutral, bool open = false)
    {
        Header = header;
        Items = items;
        Tone = tone;
        _isOpen = open;
        ToggleCommand = new RelayCommand(() => IsOpen = !IsOpen);
    }

    public string Header { get; }
    public IReadOnlyList<LoadListItemVm> Items { get; }
    public Tone Tone { get; }
    public bool HasItems => Items.Count > 0;
    public ICommand ToggleCommand { get; }
    public string Arrow => IsOpen ? "▾" : "▸";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(Arrow))] private bool _isOpen;
}

/// <summary>What the preview hands back besides IMPORT: a collection or missing mods to get from Nexus first.</summary>
public enum LoadListAction { None, Import, GetFromNexus, AddCollection }

/// <summary>
/// IMPORT ORDER: what a load list changes in the selected profile, before anything is written. The options rebuild
/// the plan; IMPORT hands back exactly the plan shown.
/// </summary>
public sealed partial class LoadListPreviewVm : OverlayVm
{
    private readonly TaskCompletionSource<LoadListAction> _tcs = new();
    private readonly Func<LoadListOptions, LoadListPlan> _plan;
    private readonly bool _online;

    public LoadListPreviewVm(string fileName, LoadListDocument doc, string profile, Func<LoadListOptions, LoadListPlan> plan, bool online, bool canDownload,
        bool pageByPage = false)
    {
        _plan = plan;
        _online = online;
        CanDownload = canDownload;
        PageByPage = pageByPage;
        var count = doc.Mods.Count();
        Subtitle = $"{fileName} · {count} mod{(count == 1 ? "" : "s")}" + (doc.MadeBy is { } v ? $" · made by AFTERLIFE {v}" : "");
        IntoProfile = "Into profile: " + profile;
        Plan = plan(new LoadListOptions());
        ShowUnlock = Plan.LockedKept.Count > 0;
        Groups = BuildGroups(Plan);
        CloseCommand = new RelayCommand(() => _tcs.TrySetResult(LoadListAction.None));
    }

    public override string Title => "IMPORT ORDER FROM A LIST";
    public string Subtitle { get; }
    public string IntoProfile { get; }
    public Task<LoadListAction> Result => _tcs.Task;

    /// <summary>The plan for the options as they are now; the import writes this one.</summary>
    public LoadListPlan Plan { get; private set; }

    [ObservableProperty] private IReadOnlyList<LoadListGroupVm> _groups;

    [ObservableProperty] private bool _switchOnOff = true;
    [ObservableProperty] private bool _switchOffOthers;
    [ObservableProperty] private bool _unlockCollections;

    /// <summary>The unlock option is shown only when the list has a locked collection.</summary>
    public bool ShowUnlock { get; }

    public LoadListOptions Options => new(SwitchOnOff, SwitchOffOthers, UnlockCollections);

    partial void OnSwitchOnOffChanged(bool value) => Rebuild();
    partial void OnSwitchOffOthersChanged(bool value) => Rebuild();
    partial void OnUnlockCollectionsChanged(bool value) => Rebuild();

    private void Rebuild()
    {
        Plan = _plan(Options);
        Groups = BuildGroups(Plan);
        OnPropertyChanged(nameof(Plan));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(NothingChanges));
    }

    /// <summary>Notes under the lines: what the window cannot show, the default order, shared categories.</summary>
    public IReadOnlyList<string> Notes
    {
        get
        {
            var notes = new List<string>();
            if (Plan.SectionOrderNote) notes.Add("Categories always load after the mods without a category.");
            if (Plan.TakesOwnOrder) notes.Add("This profile still uses the game's default order (by archive file name); the import gives it its own order, as the first drag does.");
            notes.Add("Categories are shared by all profiles. Nothing changes in the game until you press APPLY.");
            return notes;
        }
    }

    public bool NothingChanges => Plan.IsEmpty;

    /// <summary>The missing mods with a Nexus file can be fetched (full edition, Nexus key with a known account).</summary>
    public bool CanDownload { get; }

    /// <summary>Not Premium: Nexus allows downloads only on the website, so the pages open one after another.</summary>
    public bool PageByPage { get; }

    public string DownloadLabel => PageByPage ? "Get from Nexus, page by page" : "Get from Nexus";

    public string DownloadTip => PageByPage
        ? "Open the Nexus page of each missing mod that names a file, one after another: click Mod Manager Download there and the file comes in switched off. Then load this list again"
        : "Download the missing mods that name a Nexus file, then load this list again";

    /// <summary>The collection to add from Nexus when <see cref="LoadListAction.AddCollection"/> is the result.</summary>
    public LoadListSection? CollectionToAdd { get; private set; }

    [RelayCommand] private void Confirm() => _tcs.TrySetResult(LoadListAction.Import);
    [RelayCommand] private void Cancel() => _tcs.TrySetResult(LoadListAction.None);
    [RelayCommand] private void GetFromNexus() => _tcs.TrySetResult(LoadListAction.GetFromNexus);

    private void AddCollection(LoadListSection s)
    {
        CollectionToAdd = s;
        _tcs.TrySetResult(LoadListAction.AddCollection);
    }

    private static string Count(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

    public static string NexusPage(string modId) => $"https://www.nexusmods.com/{NxmLink.Cyberpunk}/mods/{modId}";

    private IReadOnlyList<LoadListGroupVm> BuildGroups(LoadListPlan p)
    {
        static IReadOnlyList<LoadListItemVm> Names(IEnumerable<string> names) => names.Select(n => new LoadListItemVm(n)).ToList();
        var groups = new List<LoadListGroupVm>();
        if (p.Moved.Count > 0) groups.Add(new LoadListGroupVm(Count(p.Moved.Count, "mod changes place", "mods change place"), Names(p.Moved), Tone.Info));
        if (p.SwitchOn.Count > 0 || p.SwitchOff.Count > 0)
            groups.Add(new LoadListGroupVm($"{p.SwitchOn.Count} switched on, {p.SwitchOff.Count} switched off",
                Names(p.SwitchOnNames.Select(n => "on   " + n).Concat(p.SwitchOffNames.Select(n => "off  " + n))), Tone.Info));
        if (p.NewCategories.Count > 0)
            groups.Add(new LoadListGroupVm(Count(p.NewCategories.Count, "new category", "new categories") + ": " + string.Join(", ", p.NewCategories), Names(p.NewCategories), Tone.Info));
        if (p.CategoryMoves.Count > 0)
            groups.Add(new LoadListGroupVm(Count(p.CategoryMoves.Count, "mod moves to another category", "mods move to another category") + " (in every profile)", Names(p.CategoryMoves)));
        if (p.CutNames.Count > 0)
            groups.Add(new LoadListGroupVm(Count(p.CutNames.Count, "category name is", "category names are") + $" cut to {BlockLayout.MaxNameLength} characters", Names(p.CutNames), Tone.Warn));
        if (p.OtherVersions.Count > 0)
        {
            var first = p.OtherVersions[0];
            groups.Add(new LoadListGroupVm(Count(p.OtherVersions.Count, "other version", "other versions") + $": {first.Package.Name} ({first.Line.Version} in the list, {first.Package.Version} here)"
                    + (p.OtherVersions.Count > 1 ? " …" : ""),
                Names(p.OtherVersions.Select(m => $"{m.Package.Name} ({m.Line.Version} in the list, {m.Package.Version} here)")), Tone.Warn));
        }
        // a match by name only can be a different mod with the same name: every one on its own line, open
        if (p.NameOnly.Count > 0)
            groups.Add(new LoadListGroupVm(Count(p.NameOnly.Count, "mod", "mods") + " matched by name only – check they are the right ones",
                Names(p.NameOnly.Select(m => $"{m.Package.Name} {m.Package.Version} – matched by name only (line {m.Line.Line})")), Tone.Warn, open: true));
        if (p.Missing.Count > 0)
        {
            var items = p.Missing.Select(m =>
            {
                var where = m.NexusModId is { } id ? "   " + NexusPage(id) : "";
                ICommand? open = _online && m.NexusModId is { } mod ? new RelayCommand(() => AppLinks.Open(NexusPage(mod))) : null;
                return new LoadListItemVm($"{m.Name}{(m.Version is null ? "" : " " + m.Version)} (line {m.Line}){where}", open, open is null ? null : "Page ↗");
            }).ToList();
            groups.Add(new LoadListGroupVm(Count(p.Missing.Count, "missing in the library", "missing in the library") + ": " + string.Join(", ", p.Missing.Take(3).Select(m => m.Name)) + (p.Missing.Count > 3 ? " …" : ""),
                items, Tone.Warn));
        }
        if (p.MissingCollections.Count > 0)
        {
            var items = p.MissingCollections.Select(s =>
            {
                ICommand? add = _online && s.Slug is not null ? new RelayCommand(() => AddCollection(s)) : null;
                return new LoadListItemVm($"{s.Name} → category {LoadListPlanner.CutName(LoadListPlanner.CollectionCategoryName(s.Name))}", add, add is null ? null : "Add collection from Nexus…");
            }).ToList();
            groups.Add(new LoadListGroupVm(Count(p.MissingCollections.Count, "collection is", "collections are") + " not in your library – its mods go to a category", items, Tone.Warn, open: true));
        }
        if (p.LockedKept.Count > 0)
            groups.Add(new LoadListGroupVm(Count(p.LockedKept.Count, "collection keeps", "collections keep") + " the curator's order",
                Names(p.LockedKept.Select(n => $"{n}: order locked, kept as the curator set it"))));
        if (p.StayOutside.Count > 0)
            groups.Add(new LoadListGroupVm(Count(p.StayOutside.Count, "mod stays", "mods stay") + " where the library has it (collections come from Nexus)", Names(p.StayOutside)));
        if (p.Duplicates.Count > 0)
            groups.Add(new LoadListGroupVm(Count(p.Duplicates.Count, "line is", "lines are") + " skipped: the mod is listed twice", Names(p.Duplicates.Select(d => $"Line {d.Line}: {d.Name}"))));
        if (p.NotInList.Count > 0)
            groups.Add(new LoadListGroupVm(Count(p.NotInList.Count, "mod here is", "mods here are") + " not in the list – "
                + (Options.SwitchOffOthers ? "switched off" : p.NotInList.Count == 1 ? "it stays where it is" : "they stay where they are"), Names(p.NotInList)));
        return groups;
    }
}

/// <summary>
/// The missing mods of a load list, page by page on Nexus (free accounts). The progress block is the collection
/// download view; the window adds skipping the open page and loading the list again.
/// </summary>
public sealed partial class LoadListQueueVm : OverlayVm
{
    public LoadListQueueVm(string fileName, CollectionProgressVm progress, ICommand skip, ICommand loadAgain)
    {
        Subtitle = fileName;
        Progress = progress;
        SkipCommand = skip;
        LoadAgainCommand = loadAgain;
    }

    public override string Title => "IMPORT ORDER · MISSING MODS";
    public string Subtitle { get; }
    public CollectionProgressVm Progress { get; }
    public ICommand SkipCommand { get; }
    public ICommand LoadAgainCommand { get; }
    [ObservableProperty] private bool _canSkip;
    [ObservableProperty] private bool _canLoadAgain;
}
