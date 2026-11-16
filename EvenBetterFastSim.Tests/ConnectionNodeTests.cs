using System.Linq;
using System.Text.Json;
using System.Windows;
using EvenBetterFastSim.WPF.ViewModels;
using EvenBetterFastSim.WPF.ViewModels.Graph;
using MessagePack;
using SecsGemScenarioEngine.Models;
using Xunit;

namespace EvenBetterFastSim.Tests;

/// <summary>
/// The nodes Drop connection and Wait for communication on the canvas (GitLab secsgem/evencoolerfastsim#25). Their
/// parameter is a time in ms as text in TransactionName. The engine reads it as digits only (white space around them
/// ignored) and lets the node fail for anything else, so the title has to say when the value is not a time.
/// </summary>
public class ConnectionNodeTests
{
    [Theory]
    [InlineData("5000", "Drop connection · 5000 ms")]
    [InlineData("0", "Drop connection · 0 ms")]
    [InlineData("007", "Drop connection · 7 ms")]
    [InlineData(" 250 ", "Drop connection · 250 ms")]
    [InlineData("\t250\r\n", "Drop connection · 250 ms")]
    // The engine trims every kind of white space here (string.Trim), also a non-breaking space.
    [InlineData(" 250", "Drop connection · 250 ms")]
    [InlineData("2147483647", "Drop connection · 2147483647 ms")]
    [InlineData("2147483648", "Drop connection (invalid: 2147483648)")]
    [InlineData("+5", "Drop connection (invalid: +5)")]
    [InlineData("-1", "Drop connection (invalid: -1)")]
    [InlineData("5 s", "Drop connection (invalid: 5 s)")]
    [InlineData("1,5", "Drop connection (invalid: 1,5)")]
    [InlineData("15​00", "Drop connection (invalid: 15[U+200B]00)")]
    [InlineData("500​", "Drop connection (invalid: 500[U+200B])")]
    [InlineData("", "Drop connection (no downtime set)")]
    [InlineData("  ", "Drop connection (no downtime set)")]
    [InlineData(null, "Drop connection (no downtime set)")]
    public void Drop_connection_shows_its_downtime_or_what_is_wrong_with_it(string? text, string title)
    {
        var node = new ScenarioNodeViewModel { Type = NodeType.DropConnection, TransactionName = text };

        Assert.Equal(title, node.Title);
    }

    [Theory]
    [InlineData("20000", "Wait for communication · max 20000 ms")]
    [InlineData("1", "Wait for communication · max 1 ms")]
    [InlineData(" 250 ", "Wait for communication · max 250 ms")]
    [InlineData("0", "Wait for communication · no timeout")]
    [InlineData("000", "Wait for communication · no timeout")]
    [InlineData("2147483648", "Wait for communication (invalid: 2147483648)")]
    [InlineData("+5", "Wait for communication (invalid: +5)")]
    [InlineData("-0", "Wait for communication (invalid: -0)")]
    [InlineData("20 s", "Wait for communication (invalid: 20 s)")]
    [InlineData("20​", "Wait for communication (invalid: 20[U+200B])")]
    [InlineData("", "Wait for communication (no timeout set)")]
    [InlineData(null, "Wait for communication (no timeout set)")]
    public void Wait_for_communication_shows_its_timeout_or_what_is_wrong_with_it(string? text, string title)
    {
        var node = new ScenarioNodeViewModel { Type = NodeType.WaitForCommunication, TransactionName = text };

        Assert.Equal(title, node.Title);
    }

    /// <summary>The title follows the value, and a DisplayName from a file does not replace it.</summary>
    [Theory]
    [InlineData(NodeType.DropConnection, "Drop connection · 100 ms", "Drop connection · 0 ms")]
    [InlineData(NodeType.WaitForCommunication, "Wait for communication · max 100 ms", "Wait for communication · no timeout")]
    public void Title_follows_the_value_and_is_not_replaced_by_a_display_name(NodeType type, string first, string then)
    {
        var node = new ScenarioNodeViewModel { Type = type, TransactionName = "100", DisplayName = "Pause" };
        Assert.Equal(first, node.Title);

        node.TransactionName = "0";

        Assert.Equal(then, node.Title);
    }

    /// <summary>
    /// Both nodes fail in normal use (no connection to drop, communication not back in time). Without a failure
    /// output such a failure could not be handled in the scenario and would end the whole run.
    /// </summary>
    [Theory]
    [InlineData(NodeType.DropConnection, "5000", "Drop connection · 5000 ms")]
    [InlineData(NodeType.WaitForCommunication, "20000", "Wait for communication · max 20000 ms")]
    public void Node_from_the_palette_has_its_default_and_a_failure_output(NodeType type, string value, string title)
    {
        var node = ScenarioNodeViewModel.CreateStructural(type, new Point(120, 80));

        Assert.Equal(type, node.Type);
        Assert.Equal(value, node.TransactionName);
        Assert.Equal(title, node.Title);
        Assert.Equal(new Point(120, 80), node.Location);
        Assert.Equal(["In"], node.Input.Select(connector => connector.Title));
        Assert.Equal(["Success", "Failure"], node.Output.Select(connector => connector.Title));
        Assert.True(node.IsDeletable);
        Assert.False(node.CanToggleMode);
        Assert.False(node.IsSendNode);
        Assert.False(node.IsReceiveNode);
    }

    /// <summary>The numbers are what files store; they must stay what the engine package defines.</summary>
    [Fact]
    public void Node_types_keep_their_numbers()
    {
        Assert.Equal(8, (int)NodeType.DropConnection);
        Assert.Equal(9, (int)NodeType.WaitForCommunication);
    }

    /// <summary>
    /// Type, value and the failure edge survive saving and loading, as a MessagePack file and as a JSON file (the
    /// calls of ScenariosViewModel). The type is stored as a number.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Nodes_and_their_failure_edge_survive_saving_and_loading(bool asJson)
    {
        var drop = ScenarioNodeViewModel.CreateStructural(NodeType.DropConnection, new Point(5, 6));
        drop.TransactionName = "1500";
        var wait = ScenarioNodeViewModel.CreateStructural(NodeType.WaitForCommunication, new Point(7, 8));
        wait.TransactionName = "0";
        var end = new ScenarioNodeViewModel { Type = NodeType.End };
        var graph = new ScenarioGraph
        {
            Name = "s",
            Nodes = [drop.ToModel(), wait.ToModel(), end.ToModel()],
            Edges =
            [
                new ScenarioEdge { SourceNodeId = drop.Id, TargetNodeId = wait.Id, IsFailurePath = false },
                new ScenarioEdge { SourceNodeId = drop.Id, TargetNodeId = end.Id, IsFailurePath = true },
                new ScenarioEdge { SourceNodeId = wait.Id, TargetNodeId = end.Id, IsFailurePath = true }
            ]
        };

        ScenarioGraph loaded;
        if (asJson)
        {
            var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
            Assert.Contains("\"Type\": 8", json);
            Assert.Contains("\"Type\": 9", json);
            loaded = JsonSerializer.Deserialize<ScenarioGraph>(json)!;
        }
        else
        {
            var cancellation = TestContext.Current.CancellationToken;
            var bytes = MessagePackSerializer.Serialize(ScenarioGraphDto.From(graph), cancellationToken: cancellation);
            loaded = MessagePackSerializer.Deserialize<ScenarioGraphDto>(bytes, cancellationToken: cancellation).ToModel();
        }

        var nodes = loaded.Nodes.Select(ScenarioNodeViewModel.FromModel).ToList();
        Assert.Equal([NodeType.DropConnection, NodeType.WaitForCommunication, NodeType.End], nodes.Select(n => n.Type));
        Assert.Equal(["1500", "0", null], nodes.Select(n => n.TransactionName));
        Assert.Equal(["Drop connection · 1500 ms", "Wait for communication · no timeout", "END"], nodes.Select(n => n.Title));
        Assert.Equal([drop.Id, wait.Id, end.Id], nodes.Select(n => n.Id));
        Assert.Equal(
            [(drop.Id, wait.Id, false), (drop.Id, end.Id, true), (wait.Id, end.Id, true)],
            loaded.Edges.Select(edge => (edge.SourceNodeId, edge.TargetNodeId, edge.IsFailurePath)));
    }
}
