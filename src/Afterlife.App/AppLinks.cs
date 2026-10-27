using System.Diagnostics;

namespace Afterlife.App;

/// <summary>External links shown in the footer and About. One place to change them before release.</summary>
public static class AppLinks
{
    public const string Sponsors = "https://github.com/sponsors/silentbobcz?frequency=one-time&sponsor=silentbobcz";
    public const string Source = "https://github.com/silentbobcz/afterlife";
    public const string ContactEmail = "silentbob@silentbob.cz";
    public const string ContactMailto = "mailto:silentbob@silentbob.cz?subject=AFTERLIFE";
    public const string CdprFanContent = "https://www.cdprojektred.com/en/fan-content";

    public static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* no browser or mail client registered: nothing else to do */ }
    }

    public static void OpenFolder(string path)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe") { ArgumentList = { path }, UseShellExecute = false }); }
        catch { }
    }
}
