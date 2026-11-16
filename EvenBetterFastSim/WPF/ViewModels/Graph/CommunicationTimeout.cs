namespace EvenBetterFastSim.WPF.ViewModels.Graph;

/// <summary>
/// The timeout of a Wait for communication node, in milliseconds, kept as text in
/// <c>ScenarioNode.TransactionName</c>. The node waits until data messages can be exchanged again (HSMS selected and
/// communication established); with 0 as long as the run may take (GitLab secsgem/evencoolerfastsim#25).
/// </summary>
public static class CommunicationTimeout
{
    /// <summary>The timeout a Wait for communication node gets when it is dragged from the palette.</summary>
    public const int DefaultMilliseconds = 20000;

    /// <summary>
    /// The node's title: "Wait for communication · max 20000 ms", or what is wrong with the value. The engine lets
    /// the node fail for a value it cannot read (<see cref="NodeMilliseconds.TryReadDigits"/>), also for a missing one.
    /// </summary>
    public static string Title(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Wait for communication (no timeout set)";
        if (!NodeMilliseconds.TryReadDigits(text, out var milliseconds))
            return $"Wait for communication (invalid: {NodeMilliseconds.Visible(text.Trim())})";
        return milliseconds == 0
            ? "Wait for communication · no timeout"
            : $"Wait for communication · max {NodeMilliseconds.Format(milliseconds)} ms";
    }
}
