using System.Globalization;
using System.Text;

namespace EvenBetterFastSim.WPF.ViewModels.Graph;

/// <summary>
/// For scenario nodes whose parameter is a time in milliseconds, kept as text in <c>ScenarioNode.TransactionName</c>:
/// Wait (<see cref="WaitTime"/>), Drop connection (<see cref="DropConnectionTime"/>) and Wait for communication
/// (<see cref="CommunicationTimeout"/>).
/// </summary>
public static class NodeMilliseconds
{
    public static string Format(int milliseconds) => milliseconds.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Reads the text the way the scenario engine reads the parameter of a Drop connection and a Wait for
    /// communication node (TryParseMilliseconds in ScenarioExecutionService): the digits 0-9 only, within the range of
    /// an int, white space around them ignored. Such a node fails when its parameter is anything else.
    /// </summary>
    public static bool TryReadDigits(string? text, out int milliseconds) =>
        int.TryParse(text?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out milliseconds);

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
