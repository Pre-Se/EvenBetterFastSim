using EvenBetterFastSim.Services;
using SecsGemScenarioEngine.Models;
using Xunit;

namespace EvenBetterFastSim.Tests;

/// <summary>
/// While a Drop connection node keeps the port closed, the Connect button is disabled: a click would open the port in
/// the middle of the downtime (GitLab secsgem/evencoolerfastsim#25). The node reports Waiting for that time.
/// </summary>
public class PortHoldTests
{
    [Fact]
    public void Port_is_held_from_the_waiting_report_until_the_node_is_done()
    {
        var hold = new PortHold();
        var run = hold.Begin();
        Assert.False(hold.Active);

        hold.Report(run, "drop", ScenarioNodeRunState.Running);
        Assert.False(hold.Active);

        hold.Report(run, "drop", ScenarioNodeRunState.Waiting);
        Assert.True(hold.Active);

        hold.Report(run, "drop", ScenarioNodeRunState.Succeeded);
        Assert.False(hold.Active);
    }

    [Theory]
    [InlineData(ScenarioNodeRunState.Failed)]
    [InlineData(ScenarioNodeRunState.Idle)]
    [InlineData(ScenarioNodeRunState.Running)]
    public void Any_other_report_for_the_node_ends_the_hold(ScenarioNodeRunState state)
    {
        var hold = new PortHold();
        var run = hold.Begin();
        hold.Report(run, "drop", ScenarioNodeRunState.Waiting);

        hold.Report(run, "drop", state);

        Assert.False(hold.Active);
    }

    /// <summary>A run that is cancelled during the downtime reports nothing more for the node.</summary>
    [Fact]
    public void End_of_the_run_ends_the_hold()
    {
        var hold = new PortHold();
        var run = hold.Begin();
        hold.Report(run, "drop", ScenarioNodeRunState.Waiting);

        hold.End(run);

        Assert.False(hold.Active);
    }

    /// <summary>
    /// A report can still be on its way to the UI thread when the run ends. It must not disable the button for good.
    /// </summary>
    [Fact]
    public void Report_after_the_end_of_the_run_is_ignored()
    {
        var hold = new PortHold();
        var run = hold.Begin();
        hold.End(run);

        hold.Report(run, "drop", ScenarioNodeRunState.Waiting);

        Assert.False(hold.Active);
    }

    /// <summary>Several scenarios can run at once, and all share the one connection.</summary>
    [Fact]
    public void Each_run_and_each_node_holds_on_its_own()
    {
        var hold = new PortHold();
        var first = hold.Begin();
        var second = hold.Begin();
        hold.Report(first, "drop", ScenarioNodeRunState.Waiting);
        hold.Report(second, "drop", ScenarioNodeRunState.Waiting);
        hold.Report(second, "other", ScenarioNodeRunState.Waiting);

        hold.End(first);
        Assert.True(hold.Active);

        hold.Report(second, "other", ScenarioNodeRunState.Succeeded);
        // The second run's "drop" is still held: the end of the first run, which held the same id, did not take it.
        Assert.True(hold.Active);

        hold.Report(second, "drop", ScenarioNodeRunState.Succeeded);
        Assert.False(hold.Active);
    }

    /// <summary>
    /// Two runs can hold a node with the same id: a scenario that was imported twice keeps the ids of its file, and
    /// both copies can run at once. A report of one run must not end the hold of the other.
    /// </summary>
    [Fact]
    public void Report_of_one_run_does_not_end_the_hold_of_another_run_on_the_same_node_id()
    {
        var hold = new PortHold();
        var first = hold.Begin();
        var second = hold.Begin();
        hold.Report(first, "drop", ScenarioNodeRunState.Waiting);
        hold.Report(second, "drop", ScenarioNodeRunState.Waiting);

        hold.Report(second, "drop", ScenarioNodeRunState.Succeeded);
        Assert.True(hold.Active);

        hold.Report(first, "drop", ScenarioNodeRunState.Succeeded);
        Assert.False(hold.Active);
    }
}
