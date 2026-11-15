using System.Globalization;
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
/// A Wait node can be made on the canvas and shows its time (GitLab secsgem/evencoolerfastsim#18). The time is text in
/// TransactionName; the engine waits only for a whole number greater than 0 and takes anything else as "no wait"
/// without an error, so the title has to say when the value is not a time.
/// </summary>
public class WaitNodeTests
{
    [Theory]
    [InlineData("1500", "Wait 1500 ms")]
    [InlineData("12600", "Wait 12600 ms")]
    [InlineData(" 250 ", "Wait 250 ms")]
    [InlineData("0", "Wait 0 ms")]
    [InlineData("20 s", "Wait (invalid: 20 s)")]
    [InlineData("12,6", "Wait (invalid: 12,6)")]
    [InlineData("12.600", "Wait (invalid: 12.600)")]
    [InlineData("-5", "Wait (invalid: -5)")]
    [InlineData("99999999999", "Wait (invalid: 99999999999)")]
    [InlineData("2147483647", "Wait 2147483647 ms")]
    [InlineData("2147483648", "Wait (invalid: 2147483648)")]
    [InlineData("\t250\r\n", "Wait 250 ms")]
    [InlineData("\t20 s\r\n", "Wait (invalid: 20 s)")]
    [InlineData("", "Wait (no time set)")]
    [InlineData("  ", "Wait (no time set)")]
    [InlineData(null, "Wait (no time set)")]
    public void Title_shows_the_time_or_what_is_wrong_with_it(string? text, string title)
    {
        var node = new ScenarioNodeViewModel { Type = NodeType.Wait, TransactionName = text };

        Assert.Equal(title, node.Title);
    }

    /// <summary>
    /// The engine skips only space, tab and line ends around the number. Any other character nobody can see (a
    /// non-breaking space from a hand-edited file) makes the value invalid, so the title has to show it:
    /// "Wait (invalid: 1500)" would look like a valid time.
    /// </summary>
    [Theory]
    [InlineData("\u00A01500", "Wait (invalid: [U+00A0]1500)")]
    [InlineData("1500\u200B", "Wait (invalid: 1500[U+200B])")]
    [InlineData("\uFEFF1500", "Wait (invalid: [U+FEFF]1500)")]
    [InlineData("12\t600", "Wait (invalid: 12[U+0009]600)")]
    [InlineData(" 1 500 ", "Wait (invalid: 1 500)")]
    public void Title_shows_characters_nobody_can_see(string text, string title)
    {
        var node = new ScenarioNodeViewModel { Type = NodeType.Wait, TransactionName = text };

        Assert.Equal(title, node.Title);
    }

    /// <summary>
    /// A leading sign is the one thing the engine versions read differently: up to package 1.0.0-build-0027 with
    /// the culture of the computer, later with the invariant culture. The culture decides which characters are the
    /// signs: Windows takes them from the regional settings of the user, and ICU data write the plus sign of Persian
    /// or Hebrew with a direction mark in front (U+200E U+002B). On a computer with such a plus sign "+5" is a
    /// number for one engine and not for the other, so the title must not promise a wait. The culture is made by
    /// hand, so the test does not depend on the culture data of the computer it runs on.
    /// </summary>
    [Fact]
    public void Title_promises_a_wait_only_if_every_engine_version_reads_the_time()
    {
        var withMark = (CultureInfo)CultureInfo.InvariantCulture.Clone();
        withMark.NumberFormat.PositiveSign = "\u200E+";
        var before = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            Assert.Equal("Wait 5 ms", WaitTime.Title("+5"));

            CultureInfo.CurrentCulture = withMark;
            // The culture is in effect: a read with the culture of the computer takes the other spelling.
            Assert.False(int.TryParse("+5", out _));
            Assert.True(int.TryParse("\u200E+5", out _));

            Assert.Equal("Wait (invalid: +5)", WaitTime.Title("+5"));
            Assert.Equal("Wait (invalid: [U+200E]+5)", WaitTime.Title("\u200E+5"));
            Assert.Equal("Wait 12600 ms", WaitTime.Title("12600"));
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }

    /// <summary>Until #18 a DisplayName replaced the number, so the node did not show how long it waits.</summary>
    [Fact]
    public void Title_follows_the_time_and_is_not_replaced_by_a_display_name()
    {
        var node = new ScenarioNodeViewModel { Type = NodeType.Wait, TransactionName = "1500", DisplayName = "Pause" };
        Assert.Equal("Wait 1500 ms", node.Title);

        node.TransactionName = "250";

        Assert.Equal("Wait 250 ms", node.Title);
    }

    [Fact]
    public void Wait_node_from_the_palette_starts_with_one_second()
    {
        var node = ScenarioNodeViewModel.CreateStructural(NodeType.Wait, new Point(120, 80));

        Assert.Equal(NodeType.Wait, node.Type);
        Assert.Equal("1000", node.TransactionName);
        Assert.Equal("Wait 1000 ms", node.Title);
        Assert.Equal(new Point(120, 80), node.Location);
        Assert.Single(node.Input);
        Assert.Equal(["Done"], node.Output.Select(connector => connector.Title));
        Assert.True(node.IsDeletable);
    }

    [Fact]
    public void And_node_from_the_palette_has_no_parameter()
    {
        var node = ScenarioNodeViewModel.CreateStructural(NodeType.And, new Point(10, 20));

        Assert.Equal(NodeType.And, node.Type);
        Assert.Null(node.TransactionName);
        Assert.Equal("AND", node.Title);
        Assert.Equal(new Point(10, 20), node.Location);
    }

    /// <summary>The time survives saving and loading as a MessagePack file (<c>.msgpack</c>).</summary>
    [Fact]
    public void Time_survives_saving_and_loading_as_MessagePack()
    {
        var node = ScenarioNodeViewModel.CreateStructural(NodeType.Wait, new Point(5, 6));
        node.TransactionName = "12600";
        var graph = new ScenarioGraph { Name = "s", Nodes = [node.ToModel()] };

        var cancellation = TestContext.Current.CancellationToken;
        var bytes = MessagePackSerializer.Serialize(ScenarioGraphDto.From(graph), cancellationToken: cancellation);
        var loaded = MessagePackSerializer.Deserialize<ScenarioGraphDto>(bytes, cancellationToken: cancellation).ToModel();
        var reloaded = ScenarioNodeViewModel.FromModel(loaded.Nodes.Single());

        Assert.Equal(NodeType.Wait, reloaded.Type);
        Assert.Equal("12600", reloaded.TransactionName);
        Assert.Equal("Wait 12600 ms", reloaded.Title);
        Assert.Equal(node.Id, reloaded.Id);
    }

    /// <summary>
    /// The same as a JSON file, written and read with the calls of ScenariosViewModel (every other extension than
    /// <c>.msgpack</c>, and the export).
    /// </summary>
    [Fact]
    public void Time_survives_saving_and_loading_as_JSON()
    {
        var node = ScenarioNodeViewModel.CreateStructural(NodeType.Wait, new Point(5, 6));
        node.TransactionName = "12600";
        var graph = new ScenarioGraph { Name = "s", Nodes = [node.ToModel()] };

        var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
        var loaded = JsonSerializer.Deserialize<ScenarioGraph>(json)!;
        var reloaded = ScenarioNodeViewModel.FromModel(loaded.Nodes.Single());

        Assert.Equal(NodeType.Wait, reloaded.Type);
        Assert.Equal("12600", reloaded.TransactionName);
        Assert.Equal("Wait 12600 ms", reloaded.Title);
        Assert.Equal(node.Id, reloaded.Id);
    }
}
