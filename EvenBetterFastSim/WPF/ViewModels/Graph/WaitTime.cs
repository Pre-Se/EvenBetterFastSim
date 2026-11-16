using System.Globalization;

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
    /// Reads the text the way the scenario engine does (<c>int.TryParse</c> with <c>NumberStyles.Integer</c> and the
    /// invariant culture in ExecuteWaitAsync): the digits 0-9, within the range of an int, with spaces around them and
    /// a leading sign allowed. The engine waits only if that number is greater than 0; 0 does not wait. A text it
    /// cannot read this way and a negative number do not wait either: the node still succeeds, and the engine logs
    /// a warning.
    /// </summary>
    /// <remarks>
    /// The engine reads this way since package <c>1.0.0-build-0029</c>. Up to <c>1.0.0-build-0027</c> it read with the
    /// culture of the computer, which could take a leading sign differently.
    /// </remarks>
    public static bool TryRead(string? text, out int milliseconds) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out milliseconds);

    /// <summary>
    /// The node's title: "Wait 12600 ms", or what is wrong with the value. Until GitLab secsgem/evencoolerfastsim#18
    /// the node showed only its DisplayName or the bare number, and nothing showed that "20 s" means no wait at all.
    /// </summary>
    public static string Title(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Wait (no time set)";
        return TryRead(text, out var milliseconds) && milliseconds >= 0
            ? $"Wait {NodeMilliseconds.Format(milliseconds)} ms"
            : $"Wait (invalid: {NodeMilliseconds.Visible(text.Trim(SkippedByTheEngine))})";
    }
}
