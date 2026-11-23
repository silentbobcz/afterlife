using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>
/// The LIST &amp; ORDER menu under the mod list (Views/ToolsMenu.xaml) holds the tools used now and then:
/// EXPORT LIST, EXPORT ORDER, IMPORT ORDER and RESET ORDER.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>The menu is open; its button and its popup both follow this.</summary>
    [ObservableProperty] private bool _toolsMenuOpen;

    /// <summary>A line of the menu: close the menu first, then run the tool it stands for.</summary>
    [RelayCommand]
    private void RunTool(ICommand? tool)
    {
        ToolsMenuOpen = false;
        if (tool?.CanExecute(null) == true) tool.Execute(null);
    }
}
