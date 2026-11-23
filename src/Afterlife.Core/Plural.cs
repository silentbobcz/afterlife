namespace Afterlife.Core;

/// <summary>Picks the singular or plural form of a word by count, for texts that print the count.</summary>
public static class Plural
{
    /// <summary>"1 mod", "5 mods". The plural defaults to the singular plus "s".</summary>
    public static string Of(int count, string singular, string? plural = null) =>
        count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Word(count, singular, plural);

    /// <summary>Same as <see cref="Of(int, string, string?)"/> for a <see cref="long"/> count.</summary>
    public static string Of(long count, string singular, string? plural = null) =>
        count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + Word(count, singular, plural);

    /// <summary>Only the word: "mod" for 1, "mods" otherwise.</summary>
    public static string Word(int count, string singular, string? plural = null) =>
        count == 1 ? singular : plural ?? singular + "s";

    /// <summary>Only the word, for a <see cref="long"/> count.</summary>
    public static string Word(long count, string singular, string? plural = null) =>
        count == 1 ? singular : plural ?? singular + "s";
}
