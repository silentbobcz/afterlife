namespace Afterlife.App;

/// <summary>
/// Which edition this build is. A release on GitHub carries both; Nexus Mods gets only AFTERLIFE Offline.
/// <c>dotnet publish -p:Edition=Offline</c> builds AFTERLIFE Offline: the edition for
/// Nexus Mods while it has not registered AFTERLIFE as an app (API Acceptable Use Policy). It never reads an API
/// key, never registers for nxm:// links and never connects to Nexus Mods; the features that need it stay visible
/// with a lock and explain why. Everything else is the same code.
/// </summary>
public static class AppEdition
{
#if OFFLINE
    public static bool Offline => true;
#else
    public static bool Offline => false;
#endif

    /// <summary>When AFTERLIFE asked Nexus Mods to register it.</summary>
    public const string RegistrationRequested = "27 October 2026";

    public static string Name => Offline ? "AFTERLIFE Offline" : "AFTERLIFE";

    /// <summary>Shown wherever an online feature is locked in the Offline edition.</summary>
    public const string LockedNote = "Waiting for Nexus Mods to register AFTERLIFE · AFTERLIFE Offline does not connect to Nexus Mods";
}
