using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>

public abstract partial class OverlayVm : ObservableObject
{
    public abstract string Title { get; }
    public ICommand? CloseCommand { get; init; }
}
