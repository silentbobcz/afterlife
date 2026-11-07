using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>

public sealed partial class DecisionItemVm : ObservableObject
{
    public DecisionItemVm(string path) => Path = path;
    public string Path { get; }
    [ObservableProperty] private bool _keep = true;
}

public sealed partial class DecisionGroupVm(string mod, IReadOnlyList<string> files) : ObservableObject
{
    public string Mod { get; } = mod;
    public IReadOnlyList<string> Files { get; } = files;
    public string Summary => Files.Count == 1 ? Files[0] : $"{Files.Count} files · {string.Join(", ", Files.Take(3).Select(f => f[(f.LastIndexOf('/') + 1)..]))}{(Files.Count > 3 ? ", …" : "")}";
    public string AllFiles => string.Join("\n", Files);
    [ObservableProperty] private bool _keep = true;
}

/// <summary>Files changed outside AFTERLIFE: keep the edits as a layer, or discard them.</summary>
public sealed partial class DecisionsVm : OverlayVm
{
    private readonly TaskCompletionSource<IReadOnlyList<DecisionItemVm>?> _tcs = new();

    /// <summary>One choice per mod, not per file.</summary>
    public DecisionsVm(IEnumerable<(string Path, string Mod)> files) =>
        Groups = files.GroupBy(f => f.Mod).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new DecisionGroupVm(g.Key, g.Select(f => f.Path).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList())).ToList();

    public override string Title => "FILES CHANGED OUTSIDE AFTERLIFE";
    public string Hint => "Mods often change their own settings files. Keep: the changed files are saved in the library as \"my edits\" and stay with the mod. Discard: they are replaced or removed as planned. One choice per mod.";
    public IReadOnlyList<DecisionGroupVm> Groups { get; }
    public Task<IReadOnlyList<DecisionItemVm>?> Result => _tcs.Task;

    [RelayCommand] private void KeepAll() { foreach (var g in Groups) g.Keep = true; }
    [RelayCommand] private void DiscardAll() { foreach (var g in Groups) g.Keep = false; }
    [RelayCommand] private void Confirm() =>
        _tcs.TrySetResult(Groups.SelectMany(g => g.Files.Select(f => new DecisionItemVm(f) { Keep = g.Keep })).ToList());
    [RelayCommand] private void Cancel() => _tcs.TrySetResult(null);
}

public sealed partial class PriorityVm : OverlayVm
{
    private readonly Func<ArchiveOrderSettings, Task> _save;

    public PriorityVm(ArchiveOrderSettings current, IReadOnlyList<string> managed, IReadOnlyList<string> unmanaged,
        IReadOnlyList<string> conflictLines, bool editable, Func<ArchiveOrderSettings, Task> save)
    {
        _save = save;
        Editable = editable;
        _custom = current.Custom;
        _includeUnmanaged = current.IncludeUnmanagedArchives;
        _allowTakeover = current.AllowModlistTakeover;
        UnmanagedCount = unmanaged.Count;
        var all = managed.Concat(current.IncludeUnmanagedArchives ? unmanaged : []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var first = current.Order.Where(o => all.Contains(o, StringComparer.OrdinalIgnoreCase)).ToList();
        var rest = all.Except(first, StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase);
        Archives = new ObservableCollection<string>(first.Concat(rest));
        ConflictLines = conflictLines;
    }

    public override string Title => "PRIORITY & CONFLICTS";
    public bool Editable { get; }
    public int UnmanagedCount { get; }
    public ObservableCollection<string> Archives { get; }
    public IReadOnlyList<string> ConflictLines { get; }
    public bool HasConflicts => ConflictLines.Count > 0;
    public string Honest => "Archive order matters only when two archives change the same game resource. AFTERLIFE does not look inside .archive files, so it cannot tell you when that happens.";

    [ObservableProperty] private bool _custom;
    [ObservableProperty] private bool _includeUnmanaged;
    [ObservableProperty] private bool _allowTakeover;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(MoveUpCommand), nameof(MoveDownCommand))]
    private string? _selected;

    [RelayCommand(CanExecute = nameof(CanUp))]
    private void MoveUp() => Move(-1);
    private bool CanUp() => Selected is not null && Archives.IndexOf(Selected) > 0;

    [RelayCommand(CanExecute = nameof(CanDown))]
    private void MoveDown() => Move(1);
    private bool CanDown() => Selected is not null && Archives.IndexOf(Selected) is var i && i >= 0 && i < Archives.Count - 1;

    private void Move(int d)
    {
        if (Selected is null) return;
        var s = Selected;
        var i = Archives.IndexOf(s);
        Archives.Move(i, i + d);
        Selected = s;
        Custom = true;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private Task Save() => _save(new ArchiveOrderSettings(Custom, Custom ? Archives.ToList() : [], IncludeUnmanaged, AllowTakeover));
}

public sealed partial class PlanPreviewVm : OverlayVm
{
    public PlanPreviewVm(string profile, PreviewResult r, Func<string, string?>? nameOf = null)
    {
        Profile = profile;
        Summary = r.Plan.IsNoop ? "Nothing to change. The game already matches this profile." : OverviewBuilder.Summary(r.Plan, r.Snapshot);
        nameOf ??= _ => null;
        Steps = r.Plan.Steps.Select(s => new LineVm($"{s.Kind.ToString().ToUpperInvariant(),-8} {s.Path}", s.Reason,
            s.Kind == StepKind.Remove ? Tone.Warn : Tone.Info)).ToList();
        Issues = r.EnvironmentIssues.Concat(r.Plan.Blockers).Concat(r.Plan.Warnings)
            .Select(i => new LineVm(i.Message, i.Path ?? "", i.Severity == Severity.Blocking ? Tone.Block : Tone.Warn)).ToList();
        // the same plan by mod, with the reason, so a long remove list can be checked at a glance
        Groups = OverviewBuilder.GroupPlan(r, nameOf)
            .Select(g => new LineVm($"{g.Name}   {g.Counts}", g.Why, g.Why.StartsWith("held back", StringComparison.Ordinal) ? Tone.Warn : g.Remove > 0 && g.Add == 0 ? Tone.Warn : Tone.Info)).ToList();
    }

    public override string Title => "PREVIEW · " + Profile.ToUpperInvariant();
    public string Profile { get; }
    public string Summary { get; }
    public string SavesHint => "Saves made with a mod may not load without it. Switching profiles never touches your saves.";
    public IReadOnlyList<LineVm> Steps { get; }
    public IReadOnlyList<LineVm> Issues { get; }
    /// <summary>By mod: counts and why.</summary>
    public IReadOnlyList<LineVm> Groups { get; }
    public bool HasGroups => Groups.Count > 0;
    [ObservableProperty] private bool _byFile;
    public string ByFileLabel => ByFile ? "BY MOD ›" : "BY FILE ›";
    partial void OnByFileChanged(bool value) => OnPropertyChanged(nameof(ByFileLabel));
    [RelayCommand] private void ToggleView() => ByFile = !ByFile;
}
