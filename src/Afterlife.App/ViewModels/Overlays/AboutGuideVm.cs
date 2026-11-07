using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using Afterlife.Core;
using Afterlife.Infrastructure;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>Base for modal panels shown over the main window.</summary>

public sealed class AboutVm : OverlayVm
{
    public override string Title => "ABOUT AFTERLIFE";
    public string Version => "Version " + AppInfo.DisplayVersion + " · unsigned";
    public string Copyright => "© 2026 Silentbobcz · MIT License · free & open source";
    public string Disclaimer => "This is an unofficial fan work and is not approved/endorsed by CD PROJEKT RED.";
    public string Trademarks => "Cyberpunk 2077 and related marks are trademarks of CD PROJEKT S.A. AFTERLIFE uses no game assets.";
    public string Contact => AppLinks.ContactEmail;
    public string License => Licenses.Mit;
    public IReadOnlyList<LineVm> Libraries { get; } =
    [
        new("SharpCompress", "MIT"),
        new("Microsoft.Data.Sqlite / SQLitePCLRaw", "MIT / Apache-2.0"),
        new("SQLite", "public domain"),
        new("CommunityToolkit.Mvvm", "MIT"),
        new("System.IO.Hashing", "MIT"),
        new("Chakra Petch font", "SIL OFL 1.1"),
        new("JetBrains Mono font", "SIL OFL 1.1"),
    ];
    public ICommand SourceCommand { get; } = new RelayCommand(() => AppLinks.Open(AppLinks.Source));
    public ICommand SupportCommand { get; } = new RelayCommand(() => AppLinks.Open(AppLinks.Sponsors));
    public ICommand ContactCommand { get; } = new RelayCommand(() => AppLinks.Open(AppLinks.ContactMailto));
    public ICommand FanContentCommand { get; } = new RelayCommand(() => AppLinks.Open(AppLinks.CdprFanContent));
}

public static class Licenses
{
    public const string Mit = """
        MIT License

        Copyright (c) 2026 Silentbobcz

        Permission is hereby granted, free of charge, to any person obtaining a copy
        of this software and associated documentation files (the "Software"), to deal
        in the Software without restriction, including without limitation the rights
        to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
        copies of the Software, and to permit persons to whom the Software is
        furnished to do so, subject to the following conditions:

        The above copyright notice and this permission notice shall be included in all
        copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
        IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
        FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
        AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
        LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
        OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
        SOFTWARE.
        """;
}

/// <summary>Visual guide to the list; a short basic part, everything on request.</summary>
public sealed partial class GuideVm : OverlayVm
{
    public override string Title => "QUICK GUIDE";
    [ObservableProperty] private bool _showAll;
    [RelayCommand] private void ToggleAll() => ShowAll = !ShowAll;
    public string ToggleLabel => ShowAll ? "‹ SHOW LESS" : "SHOW EVERYTHING ›";
    partial void OnShowAllChanged(bool value) => OnPropertyChanged(nameof(ToggleLabel));
}

/// <summary>Version as released: 1.0.0-alpha from the build (VersionPrefix + VersionSuffix), shown as "1.0.0 alpha".</summary>
public static class AppInfo
{
    public static string Version { get; } =
        (System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(AppInfo).Assembly)?.InformationalVersion
         ?? typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "1.0.0-alpha.4").Split('+')[0];
    public static string DisplayVersion => Version.Replace('-', ' ');
}
