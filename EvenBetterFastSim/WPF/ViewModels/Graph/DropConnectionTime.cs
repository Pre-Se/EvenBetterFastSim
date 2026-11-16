namespace EvenBetterFastSim.WPF.ViewModels.Graph;

/// <summary>
/// The downtime of a Drop connection node, in milliseconds, kept as text in <c>ScenarioNode.TransactionName</c>. The
/// node aborts the connection with a TCP reset. With 0 a new connection is set up right away; with more the port
/// stays closed that long (GitLab secsgem/evencoolerfastsim#25).
/// </summary>
public static class DropConnectionTime
{
    /// <summary>The downtime a Drop connection node gets when it is dragged from the palette.</summary>
    public const int DefaultMilliseconds = 5000;

    /// <summary>
    /// The node's title: "Drop connection · 5000 ms", or what is wrong with the value. The engine lets the node fail
    /// for a value it cannot read (<see cref="NodeMilliseconds.TryReadDigits"/>), also for a missing one.
    /// </summary>
    public static string Title(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "Drop connection (no downtime set)";
        return NodeMilliseconds.TryReadDigits(text, out var milliseconds)
            ? $"Drop connection · {NodeMilliseconds.Format(milliseconds)} ms"
            : $"Drop connection (invalid: {NodeMilliseconds.Visible(text.Trim())})";
    }
}
