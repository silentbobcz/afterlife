using System.Windows.Input;
using Afterlife.Core;
using CommunityToolkit.Mvvm.Input;

namespace Afterlife.App.ViewModels;

/// <summary>AFTERLIFE Offline: what waits for the Nexus Mods registration, what works now, where the core mods are.</summary>
public sealed class OfflineInfoVm : OverlayVm
{
    public override string Title => "WAITING FOR NEXUS MODS";

    public string Intro =>
        $"This is AFTERLIFE Offline. Nexus Mods asks every public app that talks to its servers to be registered first. AFTERLIFE asked on {AppEdition.RegistrationRequested} "
        + "and is waiting for the answer. Until then this edition never connects to Nexus Mods and needs no API key.";

    public IReadOnlyList<LineVm> Waiting { get; } =
    [
        new("Mod Manager Download", "nxm:// links straight into the library"),
        new("Updates", "check, UPDATE, UPDATE ALL"),
        new("Collections", "add, download and switch collections"),
        new("Nexus page in the details", "description, picture, requirements listed on the page"),
        new("Download history", "what you downloaded through AFTERLIFE"),
        new("Log in with Nexus Mods", "account and Premium downloads"),
    ];

    public IReadOnlyList<LineVm> Works { get; } =
    [
        new("Library and profiles", "import .zip / .7z / .rar / .archive, REDmod, FOMOD"),
        new("Safe apply", "journal, verify, finish or roll back after a crash"),
        new("Conflicts and load order", "winners, blocks, categories, modlist.txt"),
        new("Requirements from files", "and Scan manual mods"),
    ];

    public string HowTo => "To add a mod: on its Nexus Mods page choose Files → Manual download, then drop the file into the AFTERLIFE window.";

    /// <summary>Every core mod with a button that opens its page in the browser (no API involved).</summary>
    public IReadOnlyList<CoreLinkVm> CoreMods { get; } = FrameworkCatalog.Contracts
        .Where(c => c.NexusUrl is not null)
        .Select(c => new CoreLinkVm(Capabilities.DisplayName(c.Capability) + " ↗", new RelayCommand(() => AppLinks.Open(c.NexusUrl!))))
        .ToList();

    public string FullEdition => "The full edition with the Nexus Mods features, for your own personal API key, is on GitHub.";
    public ICommand SourceCommand { get; } = new RelayCommand(() => AppLinks.Open(AppLinks.Source + "/releases"));
}

public sealed record CoreLinkVm(string Name, ICommand Open);
