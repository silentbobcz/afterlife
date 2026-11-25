using System.Text.RegularExpressions;

namespace Afterlife.Infrastructure;

/// <summary>
/// Keeps personal details out of everything AFTERLIFE writes to a log file. Logs are made to be sent with a
/// problem report, so they hold what AFTERLIFE did (mods, profiles, files inside the game) and never who did it
/// or where on the disk: the Windows user name, the computer name, the folders of the game and the library and
/// the secrets of a link are replaced before a line reaches the disk.
/// </summary>
public static class LogPrivacy
{
    /// <summary>Folder of the game, written as &lt;game&gt;. Set when an install is opened.</summary>
    public static string? GameRoot { get; set; }
    /// <summary>Folder of the library, written as &lt;library&gt;.</summary>
    public static string? LibraryRoot { get; set; }

    private static readonly Regex UserFolder = new(@"(?i)(\b[A-Z]:[\\/]+(?:Users|Documents and Settings)[\\/]+)[^\\/:*?""<>|\r\n]+", RegexOptions.Compiled);
    private static readonly Regex NetworkShare = new(@"\\\\[^\\/\s""']+\\", RegexOptions.Compiled);
    private static readonly Regex Secret = new(@"(?i)\b(apikey|api_key|key|expires|user_id|token|access_token|refresh_token|code_verifier|code_challenge|md5)=[^&\s""']+", RegexOptions.Compiled);
    private static readonly Regex Mail = new(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);

    public static string Scrub(string text) =>
        Scrub(text, GameRoot, LibraryRoot, Environment.UserName, Environment.MachineName,
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    /// <summary>The same with every personal value given, so it can be tested on any machine.</summary>
    public static string Scrub(string text, string? gameRoot, string? libraryRoot, string? userName, string? machineName, string? userProfile)
    {
        if (string.IsNullOrEmpty(text)) return text;
        // the longest folders first: the library usually lies inside the user's profile
        foreach (var (root, mark) in new[] { (libraryRoot, "<library>"), (gameRoot, "<game>"), (userProfile, "<home>") }
                     .Where(r => r.Item1 is { Length: > 3 }).OrderByDescending(r => r.Item1!.Length))
            text = ReplaceFolder(text, root!, mark);
        text = UserFolder.Replace(text, "$1<user>");
        text = NetworkShare.Replace(text, @"\\<server>\");
        text = Secret.Replace(text, "$1=<hidden>");
        text = Mail.Replace(text, "<mail>");
        text = ReplaceWord(text, userName, "<user>");
        text = ReplaceWord(text, machineName, "<pc>");
        return text;
    }

    /// <summary>Both spellings of the separators, whatever the case.</summary>
    private static string ReplaceFolder(string text, string root, string mark)
    {
        var trimmed = root.TrimEnd('\\', '/');
        var pattern = string.Join(@"[\\/]+", trimmed.Split('\\', '/').Select(Regex.Escape));
        return Regex.Replace(text, pattern, mark, RegexOptions.IgnoreCase);
    }

    /// <summary>A name is replaced only as a whole word and only when it is long enough not to hit ordinary text.</summary>
    private static string ReplaceWord(string text, string? word, string mark) =>
        word is not { Length: >= 3 } ? text
            : Regex.Replace(text, @"(?<![A-Za-z0-9])" + Regex.Escape(word) + @"(?![A-Za-z0-9])", mark, RegexOptions.IgnoreCase);
}
