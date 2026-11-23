using System.Windows.Input;
using Afterlife.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>
/// One culprit of a failed redscript compilation. A mod of the library carries <see cref="PackageId"/> and a
/// "Switch off" checkbox; a script outside the library has neither, only <see cref="OpenFolderCommand"/>.
/// </summary>
public sealed partial class RedscriptCulpritVm : ObservableObject
{
    public RedscriptCulpritVm(string name, string version, IReadOnlyList<string> lines, string? packageId = null,
        bool canSwitchOff = false, string? note = null, ICommand? openFolderCommand = null)
    {
        Name = name;
        Version = version;
        Lines = lines;
        PackageId = packageId;
        CanSwitchOff = canSwitchOff;
        _switchOff = canSwitchOff;
        Note = note;
        OpenFolderCommand = openFolderCommand;
    }

    public string Name { get; }
    public string Version { get; }
    /// <summary>One line per error ("path:line:col  message"), at most a few, then "+ n more in the log".</summary>
    public IReadOnlyList<string> Lines { get; }
    public string? PackageId { get; }
    public bool IsLibrary => PackageId is not null;
    /// <summary>On in the selected profile (and the profile is not Mods off): the checkbox can be used.</summary>
    public bool CanSwitchOff { get; }
    public string? Note { get; }
    public ICommand? OpenFolderCommand { get; }

    [ObservableProperty] private bool _switchOff;

    /// <summary>Called when the checkbox changes, so the window can count the checked cards.</summary>
    public Action? Changed { get; set; }

    partial void OnSwitchOffChanged(bool value) => Changed?.Invoke();
}

/// <summary>
/// REDSCRIPT ERROR: the mods the redscript log names, with a checkbox to switch off those of the library, the
/// scripts outside it and the text to ask an AI. Hands back the checked mods; switching them off is the caller's
/// normal switch-off, nothing is applied here.
/// </summary>
public sealed partial class RedscriptVm : OverlayVm
{
    private readonly TaskCompletionSource<IReadOnlyList<string>?> _tcs = new();
    private readonly Action<string> _copy;

    public RedscriptVm(string message, string logLine, string libraryNote, IReadOnlyList<RedscriptCulpritVm> library,
        IReadOnlyList<RedscriptCulpritVm> foreign, string aiPrompt, Action<string> copy)
    {
        Message = message;
        LogLine = logLine;
        LibraryNote = libraryNote;
        Library = library;
        Foreign = foreign;
        AiPrompt = aiPrompt;
        _copy = copy;
        foreach (var c in library) c.Changed = OnCheckedChanged;
        CloseCommand = new RelayCommand(() => _tcs.TrySetResult(null));
    }

    public override string Title => "REDSCRIPT ERROR";
    public string Message { get; }
    public string LogLine { get; }
    /// <summary>The sentence under FROM YOUR LIBRARY (another one when the selected profile is Mods off).</summary>
    public string LibraryNote { get; }
    public IReadOnlyList<RedscriptCulpritVm> Library { get; }
    public IReadOnlyList<RedscriptCulpritVm> Foreign { get; }
    public bool HasLibrary => Library.Count > 0;
    public bool HasForeign => Foreign.Count > 0;
    public string AiPrompt { get; }
    public Task<IReadOnlyList<string>?> Result => _tcs.Task;

    public string LibraryClosing =>
        "AFTERLIFE only switches the mods off in this profile. Nothing is deleted; you can switch them on again at any time. Apply the changes, then launch the game again.";

    public string ForeignNote =>
        "AFTERLIFE did not install these files, so it does not switch them off. Update the mod they came from, or move its folder out of r6\\scripts while the game is closed.";

    public string ForeignHint =>
        "“Scan manual mods” in the left panel can add these scripts to your library, so AFTERLIFE can switch them off next time.";

    public string AiNote =>
        "Not sure what to do? Copy this text into an AI chat or a forum post. Nothing is sent anywhere; your user name is removed from the paths.";

    public int CheckedCount => Library.Count(c => c.CanSwitchOff && c.SwitchOff);

    public string SwitchOffLabel => "SWITCH OFF " + Plural.Of(CheckedCount, "MOD", "MODS");

    public string? SwitchOffTip => CheckedCount == 0 ? "Check the mods to switch off" : null;

    private void OnCheckedChanged()
    {
        OnPropertyChanged(nameof(CheckedCount));
        OnPropertyChanged(nameof(SwitchOffLabel));
        OnPropertyChanged(nameof(SwitchOffTip));
        SwitchOffCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSwitchOff))]
    private void SwitchOff() =>
        _tcs.TrySetResult(Library.Where(c => c.CanSwitchOff && c.SwitchOff && c.PackageId is not null).Select(c => c.PackageId!).ToList());

    private bool CanSwitchOff() => CheckedCount > 0;

    [RelayCommand]
    private void Copy() => _copy(AiPrompt);
}
