using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>

public sealed partial class VariantChoiceVm : OverlayVm
{
    private readonly TaskCompletionSource<string?> _tcs = new();

    public VariantChoiceVm(string fileName, IReadOnlyList<string> variants, IReadOnlyList<string> problems)
    {
        FileName = fileName;
        Variants = variants;
        Problems = problems;
        _selected = variants.FirstOrDefault();
    }

    public override string Title => "CHOOSE ONE VARIANT";
    public string FileName { get; }
    public IReadOnlyList<string> Variants { get; }
    public IReadOnlyList<string> Problems { get; }
    public string Hint => "This archive contains several install options. Only one is installed; you can import the archive again for another.";
    public Task<string?> Result => _tcs.Task;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string? _selected;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => _tcs.TrySetResult(Selected);
    private bool CanConfirm() => Selected is not null;

    [RelayCommand]
    private void Cancel() => _tcs.TrySetResult(null);
}

public sealed partial class ConfirmVm : OverlayVm
{
    private readonly TaskCompletionSource<bool> _tcs = new();

    public ConfirmVm(string title, string message, string confirmLabel, bool danger = false,
        IReadOnlyList<string>? lines = null, string? optionLabel = null, bool optionChecked = true, string? linesTitle = null)
    {
        TitleText = title;
        Message = message;
        ConfirmLabel = confirmLabel;
        Danger = danger;
        Lines = lines ?? [];
        LinesTitle = linesTitle;
        OptionLabel = optionLabel;
        _optionChecked = optionChecked;
        // Escape: with a third button it means "leave everything as it is", otherwise the second button.
        CloseCommand = new RelayCommand(() => { if (AbortLabel is not null) Aborted = true; _tcs.TrySetResult(false); });
    }

    public string TitleText { get; }
    public override string Title => TitleText;
    public string Message { get; }
    public string ConfirmLabel { get; }
    /// <summary>Optional third button that changes nothing (the other two both do something). Sets <see cref="Aborted"/>.</summary>
    public string? AbortLabel { get; init; }
    public bool HasAbort => AbortLabel is not null;
    public bool Aborted { get; private set; }
    public bool Danger { get; }
    /// <summary>One item per line: what exactly will happen.</summary>
    public IReadOnlyList<string> Lines { get; }
    public string? LinesTitle { get; }
    public bool HasLines => Lines.Count > 0;
    /// <summary>Optional checkbox, for example "also switch off the mods that need it".</summary>
    public string? OptionLabel { get; }
    [ObservableProperty] private bool _optionChecked;
    public Task<bool> Result => _tcs.Task;
    /// <summary>The second button can say what it does ("Keep both", "Not now").</summary>
    public string CancelLabel { get; init; } = "Cancel";

    [RelayCommand] private void Confirm() => _tcs.TrySetResult(true);
    [RelayCommand] private void Cancel() => _tcs.TrySetResult(false);
    [RelayCommand] private void Abort() { Aborted = true; _tcs.TrySetResult(false); }
}

public sealed partial class TextPromptVm : OverlayVm
{
    private readonly TaskCompletionSource<string?> _tcs = new();

    public TextPromptVm(string title, string label, string initial)
    {
        TitleText = title;
        Label = label;
        _text = initial;
    }

    public string TitleText { get; }
    public override string Title => TitleText;
    public string Label { get; }
    public Task<string?> Result => _tcs.Task;
    public string ConfirmLabel { get; init; } = "CREATE";
    public int MaxLength { get; init; } = 40;
    /// <summary>An empty answer is allowed (rename: back to the original name).</summary>
    public bool AllowEmpty { get; init; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string _text;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => _tcs.TrySetResult(Text.Trim());
    private bool CanConfirm() => (AllowEmpty || !string.IsNullOrWhiteSpace(Text)) && Text.Trim().Length <= MaxLength;

    [RelayCommand] private void Cancel() => _tcs.TrySetResult(null);
}

public sealed partial class ProfileChoiceVm : ObservableObject
{
    public ProfileChoiceVm(ProfileSummary p, bool chosen) { Profile = p; _isChecked = chosen; }
    public ProfileSummary Profile { get; }
    public string Name => Profile.Name;
    [ObservableProperty] private bool _isChecked;
}

/// <summary>Choose profiles for several mods at once.</summary>
public sealed partial class ProfilePickerVm : OverlayVm
{
    private readonly TaskCompletionSource<IReadOnlyList<string>?> _tcs = new();

    public ProfilePickerVm(string what, IReadOnlyList<ProfileSummary> profiles, string selectedId)
    {
        What = what;
        Profiles = profiles.Select(p => new ProfileChoiceVm(p, p.Id == selectedId)).ToList();
    }

    public override string Title => "ADD TO PROFILE";
    public string What { get; }
    public string Hint => $"{What} will be wanted in the checked profiles. The game changes only when you apply the profile.";
    public IReadOnlyList<ProfileChoiceVm> Profiles { get; }
    public Task<IReadOnlyList<string>?> Result => _tcs.Task;

    [RelayCommand] private void Confirm() => _tcs.TrySetResult(Profiles.Where(p => p.IsChecked).Select(p => p.Profile.Id).ToList());
    [RelayCommand] private void Cancel() => _tcs.TrySetResult(null);
}

/// <summary>One of several options (category of the checked mods, what a new collection version does).</summary>
public sealed partial class ChoiceVm : OverlayVm
{
    private readonly TaskCompletionSource<int?> _tcs = new();

    public ChoiceVm(string title, string message, IReadOnlyList<string> options, string confirmLabel = "OK", string? optionLabel = null, int selected = 0)
    {
        TitleText = title;
        Message = message;
        Options = options;
        ConfirmLabel = confirmLabel;
        OptionLabel = optionLabel;
        _selected = options.Count == 0 ? null : options[Math.Clamp(selected, 0, options.Count - 1)];
        CloseCommand = CancelCommand;
    }

    public string TitleText { get; }
    public override string Title => TitleText;
    public string Message { get; }
    public IReadOnlyList<string> Options { get; }
    public string ConfirmLabel { get; }
    /// <summary>Optional checkbox, for example "use this for all".</summary>
    public string? OptionLabel { get; }
    [ObservableProperty] private bool _optionChecked;
    public Task<int?> Result => _tcs.Task;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmCommand))]
    private string? _selected;

    [RelayCommand(CanExecute = nameof(CanConfirm))]
    private void Confirm() => _tcs.TrySetResult(Selected is null ? null : Options.ToList().IndexOf(Selected));
    private bool CanConfirm() => Selected is not null;

    [RelayCommand] private void Cancel() => _tcs.TrySetResult(null);
}
