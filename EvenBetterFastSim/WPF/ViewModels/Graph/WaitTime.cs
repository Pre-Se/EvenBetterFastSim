using System.Globalization;
using System.Text;

namespace EvenBetterFastSim.WPF.ViewModels.Graph;

/// <summary>
/// The time of a Wait node. It is kept as text in <c>ScenarioNode.TransactionName</c>, in milliseconds.
/// </summary>
public static class WaitTime
{
    /// <summary>The time a Wait node gets when it is dragged from the palette.</summary>
    public const int DefaultMilliseconds = 1000;

    /// <summary>The characters <c>int.TryParse</c> skips around a number: space, tab, line feed, vertical tab, form feed, carriage return.</summary>
    private static readonly char[] SkippedByTheEngine = [' ', '\t', '\n', '\v', '\f', '\r'];

    /// <summary>
    /// True for a text that every version of the scenario engine reads as the same whole number (<c>int.TryParse</c>
    /// in ExecuteWaitAsync): the digits 0-9, within the range of an int, with spaces around them and a leading sign
    /// allowed. The engine waits only if that number is greater than 0; anything else is no wait, without an error.
    /// </summary>
    /// <remarks>
    /// The engine up to package <c>1.0.0-build-0027</c> reads with the culture of the computer, a later one with the
    /// invariant culture. The two can differ only in the sign, because the culture decides which characters are the
    /// plus and the minus sign (Windows takes them from the regional settings of the user). "+5" is then a number
    /// for one engine and not for the other. Such a text does not count as a time here, so the title never promises
    /// a wait that the engine in use skips. On a computer where the plus sign is "+" this changes nothing.
    /// </remarks>
    public static bool TryRead(string? text, out int milliseconds) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out milliseconds) &&
        int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out _);

    public static string Format(int milliseconds) => milliseconds.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// The node's title: "Wait 12600 ms", or what is wrong with the value. Until GitLab secsgem/evencoolerfastsim#18
    /// the node showed only its DisplayName or the bare number, and nothing showed that "20 s" means no wait at all.
    /// </summary>
    public static string Title(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Wait (no time set)";
        return TryRead(text, out var milliseconds) && milliseconds >= 0
            ? $"Wait {Format(milliseconds)} ms"
            : $"Wait (invalid: {Visible(text.Trim(SkippedByTheEngine))})";
    }

    /// <summary>
    /// A character nobody can see in a title or a text box: white space other than the space, control characters and
    /// format characters (a non-breaking space, a tab, a zero-width space, a byte order mark).
    /// </summary>
    public static bool IsHidden(char character) =>
        character != ' ' &&
        (char.IsWhiteSpace(character) || char.IsControl(character) ||
         char.GetUnicodeCategory(character) == UnicodeCategory.Format);

    /// <summary>
    /// Writes characters nobody can see as [U+00A0]. A value from a hand-edited file with a non-breaking space or a
    /// zero-width character in it would otherwise show as "Wait (invalid: 1500)", which looks like a valid time.
    /// </summary>
    public static string Visible(string text)
    {
        var visible = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (IsHidden(character))
                visible.Append(CultureInfo.InvariantCulture, $"[U+{(int)character:X4}]");
            else
                visible.Append(character);
        }
        return visible.ToString();
    }

    /// <summary>The text without the spaces and the characters nobody can see at its two ends.</summary>
    public static string TrimEnds(string? text)
    {
        if (text is null) return string.Empty;
        var start = 0;
        var end = text.Length;
        while (start < end && (text[start] == ' ' || IsHidden(text[start]))) start++;
        while (end > start && (text[end - 1] == ' ' || IsHidden(text[end - 1]))) end--;
        return text[start..end];
    }
}
