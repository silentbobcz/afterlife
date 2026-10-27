using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

public sealed record TagVm(string Label, bool Mine);

public sealed record LineVm(string Left, string Right, Tone Tone = Tone.Neutral);

public sealed class ProfileVm(ProfileSummary s, string subtitle, string? badge, Tone badgeTone)
{
    public string Id => s.Id;
    public string Name => s.Name;
    public bool IsSystem => s.IsSystem;
    public string Subtitle { get; } = subtitle;
    public string? Badge { get; } = badge;
    public Tone BadgeTone { get; } = badgeTone;
}

/// <summary>One row in the mod library. WANT is the user's intent; the chip shows what is really on disk.</summary>
public sealed partial class ModRowVm : ObservableObject
{
    private readonly Func<ModRowVm, bool, Task> _onWantChanged;
    private bool _want;

    public ModRowVm(ModOverview o, bool editable, Func<ModRowVm, bool, Task> onWantChanged)
    {
        Overview = o;
        _want = o.Wanted;
        CanToggle = editable;
        _onWantChanged = onWantChanged;
        var p = o.Package;
        var tags = BuildTags(p);
        // P51: collection label and whether the collection needs it
        if (o.Collections.Count > 0)
        {
            tags.InsertRange(0, o.Collections.Select(c => new TagVm(c, true)));
            tags.Insert(o.Collections.Count, new TagVm(o.CollectionOptional ? "OPTIONAL" : "REQUIRED", false));
        }
        Tags = tags;
        VersionNote = o.HasOverride ? "enabled with override" : null;
    }

    public ModOverview Overview { get; private set; }
    public string PackageId => Overview.Package.PackageId;
    /// <summary>P23: core mods form their own chapter at the top of the list.</summary>
    public bool IsCore => Overview.IsCore;
    /// <summary>P51: a collection is its own chapter, below CORE MODS.</summary>
    public string Chapter => IsCore ? MainViewModel.CoreChapter : Overview.Collections.Count > 0 ? MainViewModel.CollectionChapterPrefix + Overview.Collections[0] : MainViewModel.PlainChapter;
    public int ChapterRank => IsCore ? 0 : Overview.Collections.Count > 0 ? 1 : 2;
    public bool InCollection => !IsCore && Overview.Collections.Count > 0;
    /// <summary>P22: selection for bulk actions is its own state; clicking a row only shows its details.</summary>
    [ObservableProperty] private bool _isChecked;
    /// <summary>P25: drop marker while another row is dragged over this one (-1 above, 1 below).</summary>
    [ObservableProperty] private int _dropHint;
    public bool CanDrag => CanToggle && !IsCore && Overview.Package.LayerOfModId is null && !Overview.OrderLocked;
    public string Name => Overview.Package.LayerOfModId is null ? Overview.Package.ModName : "└ " + Overview.Package.ModName;
    /// <summary>M4: newer version on Nexus.</summary>
    public bool HasUpdate => Overview.Update is not null;
    public string? UpdateText => Overview.Update is { } u ? "UPDATE " + (u.Version is null ? "" : "v" + u.Version.TrimStart('v', 'V')) : null;

    /// <summary>P49: a required mod is missing (files or Nexus page): name in the error colour and a short line.</summary>
    public bool HasMissing => Overview.Missing.Count > 0;
    public string? MissingText => Overview.Missing.Count == 0 ? null
        : "missing: " + string.Join(", ", Overview.Missing.Take(3)) + (Overview.Missing.Count > 3 ? $" +{Overview.Missing.Count - 3}" : "");
    public string Version => Overview.Package.Version;
    public string? VersionNote { get; }
    public string StateLabel => Overview.StateLabel;
    public Tone Tone => Overview.Tone;
    public bool CanToggle { get; }
    public IReadOnlyList<TagVm> Tags { get; }
    public string AccessibleName => $"{Overview.Package.ModName}, {Overview.StateLabel.TrimStart('✓', '×', '!', '+', '−', '–', '↻', ' ')}";

    public bool Want
    {
        get => _want;
        set
        {
            if (_want == value) return;
            _want = value;
            OnPropertyChanged();
            _ = _onWantChanged(this, value);
        }
    }

    /// <summary>Revert without calling back (e.g. when the change was refused).</summary>
    public void ResetWant(bool value)
    {
        _want = value;
        OnPropertyChanged(nameof(Want));
    }

    public bool Matches(string filter) =>
        filter.Length == 0
        || Overview.Package.ModName.Contains(filter, StringComparison.OrdinalIgnoreCase)
        || Tags.Any(t => t.Label.Contains(filter, StringComparison.OrdinalIgnoreCase));

    private static List<TagVm> BuildTags(PackageManifest p)
    {
        var tags = new List<TagVm>();
        if (p.Provides.Count > 0) tags.Add(new TagVm("Framework", false));
        if (p.LayerOfModId is not null) tags.Add(new TagVm("my edits", true));
        if (!string.IsNullOrWhiteSpace(p.Category)) tags.Add(new TagVm(p.Category!, false));
        if (tags.Count == 0)
        {
            var kind = p.Files.Select(f => f.Target.ToLowerInvariant()).ToList();
            if (kind.Any(k => k.StartsWith("archive/"))) tags.Add(new TagVm("Archive", false));
            if (kind.Any(k => k.StartsWith("r6/scripts/"))) tags.Add(new TagVm("redscript", false));
            if (kind.Any(k => k.StartsWith("r6/tweaks/"))) tags.Add(new TagVm("Tweak", false));
            if (kind.Any(k => k.Contains("cyber_engine_tweaks/mods/"))) tags.Add(new TagVm("CET", false));
            if (kind.Any(k => k.StartsWith("mods/"))) tags.Add(new TagVm("REDmod", false));
            if (kind.Any(k => k.StartsWith("red4ext/plugins/"))) tags.Add(new TagVm("RED4ext", false));
        }
        return tags.Take(3).ToList();
    }
}

public sealed record RequirementVm(string Name, string Status, Tone Tone, bool Satisfied, ICommand? Action, string? ActionLabel)
{
    /// <summary>P85: a visible link to the page (or a search on Nexus) for what is missing.</summary>
    public bool CanOpenPage => !Satisfied && Url is not null && Open is not null;
    public string OpenPageLabel => Url is not null && Url.Contains("keyword=", StringComparison.Ordinal) ? "Search on Nexus ↗" : "Open page ↗";
    public string Glyph => Satisfied ? "✓" : (Tone == Tone.Warn ? "!" : "×");
    /// <summary>P46: where it was found (files, scripts, Nexus page).</summary>
    public string Source { get; init; } = "";
    public string? Url { get; init; }
    /// <summary>P47: click on the name: open its Nexus page when known, otherwise copy the name.</summary>
    public ICommand? Open { get; init; }
    public ICommand? Copy { get; init; }
    public string? Notes { get; init; }
    public bool CanCopy => !Satisfied;
    public string NameTip => Url is null ? "Copy the name to the clipboard" : "Open its page on Nexus Mods";
}

public sealed record ConflictVm(string Path, string Detail, bool ThisModWins, ICommand? UseThis);

public sealed partial class DetailsVm : ObservableObject
{
    /// <summary>P85: the page Nexus gave, else built from the mod number, else a search by name.</summary>
    private static string? PageOf(NexusRequirementStatus r) =>
        !string.IsNullOrWhiteSpace(r.Url) ? r.Url
        : r.External ? null
        : r.ModId is { Length: > 0 } id ? $"https://www.nexusmods.com/cyberpunk2077/mods/{id}"
        : NexusSearch(r.Name);

    private static string NexusSearch(string name) =>
        "https://www.nexusmods.com/games/cyberpunk2077/mods?keyword=" + Uri.EscapeDataString(name);

    public DetailsVm(ModOverview o, MainViewModel main)
    {
        Overview = o;
        var p = o.Package;
        Title = p.ModName;
        var origin = p.Origin switch
        {
            PackageOrigin.Nexus => "linked to Nexus",
            PackageOrigin.Captured => "your captured edits",
            _ => "manual import",
        };
        Subtitle = $"v{p.Version} · {origin} · {p.Files.Count} file{(p.Files.Count == 1 ? "" : "s")}";
        Tags = new ModRowVm(o, false, (_, _) => Task.CompletedTask).Tags;
        Source = p.SourceArchiveName is null ? null : $"{p.SourceArchiveName}{(p.ImportedAt is null ? "" : $" · imported {p.ImportedAt.Value.LocalDateTime:d}")}";
        NexusId = null; // P41: shown as a link instead

        Requirements = o.Requirements.Select(r =>
        {
            ICommand? action = null;
            string? label = null;
            if (r.CanOverride && r.ProviderPackageId is not null && r.Status.StartsWith("in library"))
            {
                action = new AsyncRelayCommand(() => main.SetWantAsync(r.ProviderPackageId, true));
                label = $"Enable {r.Name}";
            }
            return new RequirementVm(r.Name, r.Status, r.Tone, r.Tone == Tone.Ok, action, label)
            {
                Source = "from " + r.Source,
                Url = r.NexusUrl ?? NexusSearch(r.Name),
                Open = OpenOrCopy(main, r.Name, r.NexusUrl ?? NexusSearch(r.Name)),
                Copy = new RelayCommand(() => main.CopyToClipboard(r.Name)),
            };
        }).ToList();
        // P46: requirements only the Nexus page lists, same look, after a gap
        ExtraRequirements = o.RequirementLinks.Select(r => new RequirementVm(r.Name, r.Status, r.Tone, r.Tone == Tone.Ok, null, null)
        {
            Source = r.External ? "from Nexus page · outside Nexus" : "from Nexus page",
            Url = PageOf(r),
            Notes = r.Notes,
            Open = OpenOrCopy(main, r.Name, PageOf(r)),
            Copy = new RelayCommand(() => main.CopyToClipboard(r.Name)),
        }).ToList();
        MissingRequirements = o.Requirements.Where(r => r.CanOverride && r.ProviderPackageId is null).ToList();
        OverrideCommand = new AsyncRelayCommand(() => main.OverrideRequirementsAsync(p.PackageId, MissingRequirements.Select(r => r.Capability).ToList()));

        Conflicts = o.Conflicts.Select(c => new ConflictVm(
            c.Path,
            c.ThisModWins ? $"this mod wins · also in {string.Join(", ", c.Providers.Where(n => n != p.ModName))}"
                          : $"now from {c.WinnerName ?? "nobody (held back)"} · provided by {string.Join(", ", c.Providers)}",
            c.ThisModWins,
            c.ThisModWins ? null : new AsyncRelayCommand(() => main.SetWinnerAsync(c.Path, p.PackageId)))).ToList();

        var losing = o.Conflicts.Where(c => !c.ThisModWins).Select(c => c.Path).ToList();
        UseThisForAllCommand = losing.Count > 1 ? new AsyncRelayCommand(() => main.SetWinnersAsync(losing, p.PackageId, p.ModName)) : null;
        UseThisForAllLabel = $"USE THIS MOD FOR ALL {losing.Count} FILES";

        InstallMap = o.InstallMap.Select(l => new LineVm(l.Folder, l.Summary)).ToList();
        Files = p.Files.Select(f => f.Target).ToList();
        // P56: files grouped by folder; a click opens the folder in the game (or the library copy when not in the game)
        var lib = new LibraryPaths(AppSettings.ResolveLibrary(null));
        FileFolders = p.Files.GroupBy(f => { var i = f.Target.LastIndexOf('/'); return i < 0 ? "(game folder)" : f.Target[..i]; }, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var folder = g.Key == "(game folder)" ? "" : g.Key;
                var inGame = Path.Combine(main.GameFolder, folder.Replace('/', Path.DirectorySeparatorChar));
                var libCopy = Path.GetDirectoryName(lib.PackageFile(p.PackageId, g.First().StoredPath)) ?? lib.PackageRoot(p.PackageId);
                var size = g.Sum(f => f.Size);
                return new FileFolderVm(g.Key, $"{g.Count()} file{(g.Count() == 1 ? "" : "s")} · {SizeText(size)}",
                    g.Select(f => { var i = f.Target.LastIndexOf('/'); return i < 0 ? f.Target : f.Target[(i + 1)..]; }).ToList(),
                    new RelayCommand(() => AppLinks.OpenFolder(Directory.Exists(inGame) && o.InGame ? inGame : libCopy)),
                    o.InGame ? "Open in the game folder" : "Not in the game: opens the copy in the AFTERLIFE library");
            }).ToList();
        ForeignPaths = o.Issues.Where(i => i.Kind == IssueKind.ForeignFileAtTarget && i.Path is not null).Select(i => i.Path!).Distinct().ToList();
        ReplaceCommand = new AsyncRelayCommand(() => main.ReplaceForeignAsync(p.PackageId, p.ModName, ForeignPaths));
        DiscardCommand = new AsyncRelayCommand(() => main.RemoveFromLibraryAsync(p.PackageId, p.ModName));
        Issues = o.Issues
            .Where(i => i.Severity != Severity.Info && i.Kind != IssueKind.ForeignFileAtTarget)
            .Select(i => new LineVm(i.Message, i.Path ?? "", i.Severity == Severity.Blocking ? Tone.Block : Tone.Warn))
            .DistinctBy(l => l.Left + l.Right).ToList();
        RemoveCommand = new AsyncRelayCommand(() => main.RemoveFromLibraryAsync(p.PackageId, p.ModName));
        ViewFilesCommand = new RelayCommand(() => Tab = DetailTab.Files);
        // P39: keep the tab the user picked while switching between mods; otherwise show what needs attention
        _main = main;
        _tab = main.PreferredTab ?? o.State switch
        {
            ModState.MissingRequirement => DetailTab.Requirements,
            ModState.Conflict => DetailTab.Conflicts,
            _ => DetailTab.Overview,
        };

        // P41/P43: Nexus page
        NexusUrl = o.NexusUrl;
        NexusConfirmed = o.NexusConfirmed;
        Summary = o.Nexus?.Summary;
        NexusMeta = o.Nexus is { } n
            ? string.Join(" · ", new[] { n.Author is null ? null : "by " + n.Author, n.Endorsements > 0 ? $"{Compact(n.Endorsements)} endorsements" : null,
                n.Downloads > 0 ? $"{Compact(n.Downloads)} downloads" : null }.Where(x => x is not null))
            : null;
        OpenNexusCommand = new RelayCommand(() => { if (NexusUrl is not null) AppLinks.Open(NexusUrl); });
        UpdateLine = o.Update is { } up ? $"Newer on Nexus: {up.Name}{(up.Version is null ? "" : " v" + up.Version.TrimStart('v', 'V'))}{(up.Exact ? "" : " (by version number)")}" : null;
        UpdateCommand = new AsyncRelayCommand(() => main.UpdateModAsync(p.PackageId));
        LinkNexusCommand = new AsyncRelayCommand(() => main.LinkToNexusAsync(p.PackageId, o.NexusId));
    }

    private readonly MainViewModel _main;

    partial void OnTabChanged(DetailTab value) => _main.PreferredTab = value == DetailTab.Overview ? null : value;

    private static string Compact(long n) => n switch
    {
        >= 1_000_000 => $"{n / 1_000_000.0:0.#}M",
        >= 1_000 => $"{n / 1_000.0:0.#}k",
        _ => n.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    public string? NexusUrl { get; }
    public bool HasNexusUrl => NexusUrl is not null;
    public bool NexusConfirmed { get; }
    public string NexusLinkLabel => NexusConfirmed ? "Open on Nexus ↗" : "Open on Nexus ↗ (guessed from the file name)";
    public bool CanLinkNexus => !NexusConfirmed;
    public string LinkNexusLabel => NexusUrl is null ? "Link to Nexus…" : "Not this mod? Link to Nexus…";
    public string? Summary { get; }
    /// <summary>P86: picture from the Nexus page, from the library cache.</summary>
    [ObservableProperty, NotifyPropertyChangedFor(nameof(HasImage), nameof(ImageStretch))] private System.Windows.Media.ImageSource? _image;
    public bool HasImage => Image is not null;
    /// <summary>P87: wide pictures (at least 2.1 : 1) fill the strip, the rest is shown whole and centred.</summary>
    public System.Windows.Media.Stretch ImageStretch =>
        Image is { Width: > 0, Height: > 0 } i && i.Width / i.Height >= 2.1 ? System.Windows.Media.Stretch.UniformToFill : System.Windows.Media.Stretch.Uniform;
    public string? NexusMeta { get; }
    public IReadOnlyList<RequirementVm> ExtraRequirements { get; }
    public bool HasExtraRequirements => ExtraRequirements.Count > 0;

    private static ICommand OpenOrCopy(MainViewModel main, string name, string? url) =>
        new RelayCommand(() => { if (url is not null) AppLinks.Open(url); else main.CopyToClipboard(name); });
    public ICommand OpenNexusCommand { get; }
    public string? UpdateLine { get; }
    public ICommand UpdateCommand { get; }
    public ICommand LinkNexusCommand { get; }

    public ModOverview Overview { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public IReadOnlyList<TagVm> Tags { get; }
    public string? Source { get; }
    public string? NexusId { get; }
    public string StateLabel => Overview.StateLabel;
    public Tone Tone => Overview.Tone;
    public string Explanation => Overview.Explanation;
    public bool IsBlocked => Overview.Tone == Tone.Block;
    public IReadOnlyList<RequirementVm> Requirements { get; }
    public IReadOnlyList<RequirementStatus> MissingRequirements { get; }
    public bool CanOverride => MissingRequirements.Count > 0 && Overview.Wanted;
    public IReadOnlyList<ConflictVm> Conflicts { get; }
    public ICommand? UseThisForAllCommand { get; }
    public string UseThisForAllLabel { get; }
    public IReadOnlyList<LineVm> InstallMap { get; }
    public IReadOnlyList<string> Files { get; }
    public IReadOnlyList<FileFolderVm> FileFolders { get; }

    private static string SizeText(long b) => b switch
    {
        >= 1L << 30 => $"{b / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{b / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{b / 1024.0:0} KB",
        _ => $"{b} B",
    };
    public IReadOnlyList<LineVm> Issues { get; }
    public bool HasRequirements => Requirements.Count > 0;
    public bool HasConflicts => Conflicts.Count > 0;
    public bool HasIssues => Issues.Count > 0;
    public string RequirementsHeader => (Requirements.Count + ExtraRequirements.Count) == 0
        ? (Overview.Nexus is null ? "No requirements found in the files. Link the mod to Nexus to read its page too." : "No requirements found in the files or on the Nexus page.")
        : Overview.Nexus is null ? "Found in the files. Link the mod to Nexus to read its page too." : "Found in the files and on the Nexus page. Click a name to open it on Nexus.";
    public string ConflictsHeader => Conflicts.Count == 0
        ? "No direct file conflicts. Archive contents are not analyzed."
        : "Same file path in more than one mod. Archive contents are not analyzed.";
    public ICommand OverrideCommand { get; }
    /// <summary>P30/P32: files already in the game at this mod's paths, with REPLACE and DISCARD IMPORT.</summary>
    public IReadOnlyList<string> ForeignPaths { get; }
    public bool HasForeign => ForeignPaths.Count > 0;
    public ICommand ReplaceCommand { get; }
    public ICommand DiscardCommand { get; }
    public ICommand RemoveCommand { get; }
    public ICommand ViewFilesCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverview), nameof(IsFiles), nameof(IsRequirements), nameof(IsConflicts))]
    private DetailTab _tab;

    public bool IsOverview { get => Tab == DetailTab.Overview; set { if (value) Tab = DetailTab.Overview; } }
    public bool IsFiles { get => Tab == DetailTab.Files; set { if (value) Tab = DetailTab.Files; } }
    public bool IsRequirements { get => Tab == DetailTab.Requirements; set { if (value) Tab = DetailTab.Requirements; } }
    public bool IsConflicts { get => Tab == DetailTab.Conflicts; set { if (value) Tab = DetailTab.Conflicts; } }
}

public enum DetailTab { Overview, Files, Requirements, Conflicts }

/// <summary>P56: one folder of a mod in the Files tab.</summary>
public sealed record FileFolderVm(string Folder, string Summary, IReadOnlyList<string> Files, ICommand Open, string OpenTip);


public sealed class ObservableList<T> : ObservableCollection<T>
{
    public void Reset(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var i in items) Items.Add(i);
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Count"));
        OnPropertyChanged(new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Reset));
    }
}

/// <summary>P12: one row of the core table in the header.</summary>
public sealed class CoreModVm(CoreModStatus s)
{
    public CoreModStatus Status { get; } = s;
    public string Name => Status.Name;
    public string Version => Status.Version ?? "–";
    public string Source => Status.SourceLabel;
    public string? NexusUrl => Status.NexusUrl;
    public bool Installed => Status.Source is CoreSource.InGame or CoreSource.Manual;
    public bool CanGet => !Installed && Status.NexusUrl is not null;
    public string Glyph => Installed ? "■" : Status.Source == CoreSource.Library ? "◩" : "□";
    public Tone Tone => Status.Source switch
    {
        CoreSource.InGame => Tone.Ok,
        CoreSource.Manual => Tone.Info,
        CoreSource.Library => Tone.Warn,
        _ => Status.Optional ? Tone.Neutral : Tone.Warn,
    };
}

public sealed record NexusSource(string ModId, string? FileId, string? ModName, string? Version, string? ExistingModId = null);

public enum ChapterKind { Core, Collection, Plain }

/// <summary>P68/P82/P83: header of a chapter in the mod list: fold arrow, count and (core, collection) a switch for all its mods.</summary>
public sealed class CollectionChapterVm(ChapterKind kind, string key, string label, string name, int on, int total, bool canToggle,
    bool collapsed, ICommand? toggle, ICommand? manage, ICommand fold)
{
    public ChapterKind Kind { get; } = kind;
    public string Slug { get; } = key;
    public string Label { get; } = label;
    public string Name { get; } = name;
    public int On { get; } = on;
    public int Total { get; } = total;
    public bool IsPlain => Kind == ChapterKind.Plain;
    public bool IsOn => On > 0;
    public bool CanToggle { get; } = canToggle && toggle is not null;
    public bool IsCollapsed { get; } = collapsed;
    public string CountText => $"{On} / {Total} ON";
    public Tone Tone => Kind switch { ChapterKind.Core => Tone.Brand, ChapterKind.Collection => Tone.Info, _ => Tone.Neutral };
    public string ArrowData => IsCollapsed ? "M0,0 L6,5 L0,10 Z" : "M0,0 L10,0 L5,6 Z";
    public string FoldTip => IsCollapsed ? "Show the mods" : "Hide the mods (fold)";
    public string ToggleTip => Kind switch
    {
        ChapterKind.Core => "Switch all core mods on or off in this profile",
        ChapterKind.Collection => "Switch the whole collection on or off in this profile",
        _ => "Switch all these mods on or off in this profile",
    };
    public ICommand? ToggleCommand { get; } = toggle;
    public ICommand? ManageCommand { get; } = manage;
    public ICommand FoldCommand { get; } = fold;

    // P89: what needs attention inside, as small marks in the header (also when folded)
    public int Updates { get; init; }
    public int Conflicts { get; init; }
    public int Missing { get; init; }
    public bool HasUpdates => Updates > 0;
    public bool HasConflicts => Conflicts > 0;
    public bool HasMissing => Missing > 0;
    public string UpdatesText => $"▲ {Updates}";
    public string ConflictsText => $"⚠ {Conflicts}";
    public string MissingText => $"✕ {Missing}";
    public string UpdatesTip => $"{Updates} mod(s) with a newer version on Nexus · click to show the first";
    public string ConflictsTip => $"{Conflicts} mod(s) with file conflicts · click to show the first";
    public string MissingTip => $"{Missing} mod(s) missing something they need · click to show the first";
    public ICommand? ShowUpdateCommand { get; init; }
    public ICommand? ShowConflictCommand { get; init; }
    public ICommand? ShowMissingCommand { get; init; }
}
