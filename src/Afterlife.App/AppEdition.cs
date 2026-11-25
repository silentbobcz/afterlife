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

    /// <summary>
    /// <c>-p:Edition=OAuthPreview</c>: the full edition marked as a test build for Nexus Mods. Its version is its own
    /// (OAuthPreviewSuffix in Directory.Build.props), Log in with Nexus Mods is always shown, and the OAuth client ID comes from the first line of
    /// <see cref="ClientIdFile"/> next to the executable, so the login can be tried before an ID is built in.
    /// </summary>
#if OAUTH_PREVIEW
    public static bool OAuthPreview => true;
#else
    public static bool OAuthPreview => false;
#endif

    public const string ClientIdFile = "nexus-client-id.txt";

    /// <summary>The client ID a tester wrote into <see cref="ClientIdFile"/>: the first line that is not empty or a # comment.</summary>
    public static string? PreviewClientId()
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, ClientIdFile);
            return System.IO.File.Exists(path)
                ? System.IO.File.ReadLines(path).Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#'))
                : null;
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { return null; }
    }

    /// <summary>When AFTERLIFE asked Nexus Mods to register it.</summary>
    public const string RegistrationRequested = "27 October 2026";

    public static string Name => Offline ? "AFTERLIFE Offline" : OAuthPreview ? "AFTERLIFE OAuth preview" : "AFTERLIFE";

    /// <summary>Shown wherever an online feature is locked in the Offline edition.</summary>
    public const string LockedNote = "Waiting for Nexus Mods to register AFTERLIFE · AFTERLIFE Offline does not connect to Nexus Mods";
}
