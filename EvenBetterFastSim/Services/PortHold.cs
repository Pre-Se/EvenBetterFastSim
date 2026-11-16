using System.Collections.Generic;
using SecsGemScenarioEngine.Models;

namespace EvenBetterFastSim.Services;

/// <summary>
/// Knows whether a running scenario keeps the port closed right now. A Drop connection node with a downtime reports
/// <see cref="ScenarioNodeRunState.Waiting"/> for as long as it keeps the port closed. The Connect button is disabled
/// meanwhile: a click would open the port in the middle of the downtime (GitLab secsgem/evencoolerfastsim#25).
/// Not thread-safe; used on the UI thread.
/// </summary>
public sealed class PortHold
{
    /// <summary>One run of a scenario. Reports that arrive after <see cref="End"/> are ignored.</summary>
    public sealed class Run
    {
        internal bool Ended { get; set; }
    }

    private readonly HashSet<(Run Run, string NodeId)> holders = [];

    /// <summary>True while at least one Drop connection node of a run that has not ended keeps the port closed.</summary>
    public bool Active => holders.Count > 0;

    public Run Begin() => new();

    /// <summary>Takes a progress report for a Drop connection node of the run.</summary>
    public void Report(Run run, string nodeId, ScenarioNodeRunState state)
    {
        if (run.Ended) return;
        if (state == ScenarioNodeRunState.Waiting)
            holders.Add((run, nodeId));
        else
            holders.Remove((run, nodeId));
    }

    /// <summary>
    /// The run is over, however it ended. A run that is cancelled during the downtime reports nothing more for the
    /// node; the engine opens the port again on its way out.
    /// </summary>
    public void End(Run run)
    {
        run.Ended = true;
        holders.RemoveWhere(holder => holder.Run == run);
    }
}
