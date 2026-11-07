using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>

/// <summary>The mod's own installer (FOMOD), step by step, in AFTERLIFE's look. Returns the choices or null.</summary>
public sealed partial class FomodVm : OverlayVm
{
    private readonly TaskCompletionSource<IReadOnlyList<FomodChoice>?> _tcs = new();
    private readonly FomodConfig _config;
    private readonly Func<string, byte[]?> _image;
    private readonly Dictionary<(string Step, string Group), HashSet<string>> _picked = new();

    public FomodVm(string fileName, FomodConfig config, Func<string, byte[]?> image, IReadOnlyList<FomodChoice>? initial = null)
    {
        FileName = fileName;
        _config = config;
        _image = image;
        foreach (var s in config.Steps)
            foreach (var g in s.Groups)
            {
                var c = initial?.FirstOrDefault(x => string.Equals(x.Step, s.Name, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Group, g.Name, StringComparison.OrdinalIgnoreCase));
                var names = c is null ? FomodConfig.Defaults(g).Select(p => p.Name) : c.Plugins;
                _picked[(s.Name, g.Name)] = new HashSet<string>(names.Concat(g.Plugins.Where(p => p.Type == FomodPluginType.Required).Select(p => p.Name)), StringComparer.OrdinalIgnoreCase);
            }
        Build();
    }

    public override string Title => "INSTALL OPTIONS";
    public string FileName { get; }
    public string ModName => _config.Name;
    public Task<IReadOnlyList<FomodChoice>?> Result => _tcs.Task;

    [ObservableProperty] private int _index;
    [ObservableProperty] private string _stepName = "";
    [ObservableProperty] private string _stepCounter = "";
    [ObservableProperty] private IReadOnlyList<FomodGroupVm> _groups = [];
    [ObservableProperty] private string? _description;
    [ObservableProperty] private System.Windows.Media.ImageSource? _picture;
    [ObservableProperty] private string? _problem;
    public bool CanBack => Index > 0;
    public bool IsLast => Index >= Steps.Count - 1;
    public string NextLabel => IsLast ? "INSTALL" : "NEXT";

    private IReadOnlyList<FomodStep> Steps => _config.VisibleSteps(Choices());

    public IReadOnlyList<FomodChoice> Choices() =>
        _config.Steps.SelectMany(s => s.Groups.Select(g => new FomodChoice(s.Name, g.Name,
            g.Plugins.Where(p => _picked[(s.Name, g.Name)].Contains(p.Name)).Select(p => p.Name).ToList()))).ToList();

    private void Build()
    {
        var steps = Steps;
        if (steps.Count == 0) { StepName = "No options"; Groups = []; return; }
        if (Index >= steps.Count) Index = steps.Count - 1;
        var step = steps[Index];
        StepName = step.Name;
        StepCounter = $"STEP {Index + 1} / {steps.Count}";
        Groups = step.Groups.Select(g => new FomodGroupVm(this, step.Name, g)).ToList();
        OnPropertyChanged(nameof(CanBack));
        OnPropertyChanged(nameof(IsLast));
        OnPropertyChanged(nameof(NextLabel));
        var first = step.Groups.SelectMany(g => g.Plugins).FirstOrDefault(p => _picked.Any(k => k.Key.Step == step.Name && k.Value.Contains(p.Name)))
                    ?? step.Groups.SelectMany(g => g.Plugins).FirstOrDefault();
        Show(first);
        Problem = null;
    }

    internal bool IsPicked(string step, string group, string plugin) => _picked[(step, group)].Contains(plugin);

    internal void Pick(string step, FomodGroup g, FomodPlugin p, bool on)
    {
        var set = _picked[(step, g.Name)];
        if (p.Type == FomodPluginType.Required && !on) return;
        if (g.Type is FomodGroupType.SelectExactlyOne or FomodGroupType.SelectAtMostOne)
        {
            if (on) { set.Clear(); set.Add(p.Name); }
            else if (g.Type == FomodGroupType.SelectAtMostOne) set.Remove(p.Name);
        }
        else if (on) set.Add(p.Name); else set.Remove(p.Name);
        Show(p);
        foreach (var gv in Groups) gv.Refresh();
    }

    internal void Show(FomodPlugin? p)
    {
        Description = p is null ? null : string.IsNullOrWhiteSpace(p.Description) ? p.Name : p.Description;
        Picture = null;
        var path = p?.Image ?? _config.Image;
        if (path is null) return;
        try
        {
            var bytes = _image(path);
            if (bytes is null) return;
            var img = new System.Windows.Media.Imaging.BitmapImage();
            img.BeginInit();
            img.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            img.StreamSource = new MemoryStream(bytes);
            img.DecodePixelWidth = 520;
            img.EndInit();
            img.Freeze();
            Picture = img;
        }
        catch (Exception ex) when (ex is NotSupportedException or IOException or ArgumentException or InvalidOperationException) { }
    }

    private string? Validate()
    {
        var steps = Steps;
        if (steps.Count == 0) return null;
        var step = steps[Index];
        foreach (var g in step.Groups)
        {
            var n = _picked[(step.Name, g.Name)].Count;
            if (g.Type == FomodGroupType.SelectExactlyOne && n != 1) return $"Choose one option in \"{g.Name}\".";
            if (g.Type == FomodGroupType.SelectAtLeastOne && n < 1) return $"Choose at least one option in \"{g.Name}\".";
        }
        return null;
    }

    [RelayCommand]
    private void Next()
    {
        Problem = Validate();
        if (Problem is not null) return;
        if (IsLast) { _tcs.TrySetResult(Choices()); return; }
        Index++;
        Build();
    }

    [RelayCommand]
    private void Back()
    {
        if (Index == 0) return;
        Index--;
        Build();
    }

    [RelayCommand]
    private void Cancel() => _tcs.TrySetResult(null);
}

public sealed partial class FomodGroupVm : ObservableObject
{
    public FomodGroupVm(FomodVm owner, string step, FomodGroup g)
    {
        Name = g.Name;
        Rule = g.Type switch
        {
            FomodGroupType.SelectExactlyOne => "choose one",
            FomodGroupType.SelectAtMostOne => "choose one or none",
            FomodGroupType.SelectAtLeastOne => "choose at least one",
            FomodGroupType.SelectAll => "all included",
            _ => "choose any",
        };
        var radio = g.Type is FomodGroupType.SelectExactlyOne or FomodGroupType.SelectAtMostOne;
        Options = g.Plugins.Select(p => new FomodOptionVm(owner, step, g, p, radio)).ToList();
    }

    public string Name { get; }
    public string Rule { get; }
    public IReadOnlyList<FomodOptionVm> Options { get; }
    public void Refresh() { foreach (var o in Options) o.Refresh(); }
}

public sealed partial class FomodOptionVm(FomodVm owner, string step, FomodGroup group, FomodPlugin plugin, bool radio) : ObservableObject
{
    public string Name => plugin.Name;
    public bool IsRadio => radio;
    public bool IsCheck => !radio;
    public string GroupKey => step + "|" + group.Name;
    public bool IsEnabled => plugin.Type is not (FomodPluginType.NotUsable or FomodPluginType.Required) && group.Type != FomodGroupType.SelectAll;
    public string? Badge => plugin.Type switch
    {
        FomodPluginType.Recommended => "RECOMMENDED",
        FomodPluginType.Required => "REQUIRED",
        FomodPluginType.NotUsable => "NOT USABLE",
        _ => null,
    };

    public bool IsChecked
    {
        get => owner.IsPicked(step, group.Name, plugin.Name);
        set { owner.Pick(step, group, plugin, value); OnPropertyChanged(); }
    }

    public void Refresh() => OnPropertyChanged(nameof(IsChecked));

    [RelayCommand] private void Hover() => owner.Show(plugin);
}
