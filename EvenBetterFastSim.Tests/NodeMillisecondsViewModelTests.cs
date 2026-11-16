using System.Collections.Generic;
using EvenBetterFastSim.Services;
using EvenBetterFastSim.Window_Helpers;
using EvenBetterFastSim.WPF.ViewModels;
using EvenBetterFastSim.WPF.ViewModels.Graph;
using SecsGemScenarioEngine.Models;
using Xunit;

namespace EvenBetterFastSim.Tests;

/// <summary>
/// The editor for a node's time in milliseconds (GitLab secsgem/evencoolerfastsim#18). It only lets a whole number in
/// digits through, because the engine takes everything else as "no wait" without an error.
/// </summary>
public class NodeMillisecondsViewModelTests
{
    [Fact]
    public void Ok_writes_the_new_time_into_the_node()
    {
        var node = WaitNode("1500");
        var editor = NodeMillisecondsViewModel.ForWait(node);
        var closed = 0;
        editor.CloseAction = () => closed++;
        Assert.Equal("Wait", editor.HeaderText);
        Assert.Equal("1500", editor.Text);
        Assert.Null(editor.Error);

        editor.Text = " 2500 ";
        Assert.True(editor.AcceptButtonClickCommand.CanExecute(null));
        editor.AcceptButtonClickCommand.Execute(null);
        editor.ApplyTo(node);

        Assert.True(editor.Accepted);
        Assert.Equal(2500, editor.Milliseconds);
        Assert.Equal(1, closed);
        Assert.Equal("2500", node.TransactionName);
        Assert.Equal("Wait 2500 ms", node.Title);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+5")]
    [InlineData("1.5")]
    [InlineData("12,6")]
    [InlineData("12.600")]
    [InlineData("20 s")]
    [InlineData("1 500")]
    [InlineData("2147483648")]
    [InlineData("99999999999")]
    [InlineData("１５００")]
    public void Anything_but_a_whole_number_above_zero_cannot_be_confirmed(string text)
    {
        var node = WaitNode("1500");
        var editor = NodeMillisecondsViewModel.ForWait(node);
        var closed = 0;
        editor.CloseAction = () => closed++;

        editor.Text = text;

        Assert.NotNull(editor.Error);
        Assert.False(editor.AcceptButtonClickCommand.CanExecute(null));
        // Enter in the dialog executes the command only if it can; a direct call must not get through either.
        editor.AcceptButtonClickCommand.Execute(null);
        editor.ApplyTo(node);
        Assert.False(editor.Accepted);
        Assert.Equal(0, closed);
        Assert.Equal("1500", node.TransactionName);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("0001500", 1500)]
    [InlineData("2147483647", int.MaxValue)]
    public void Whole_numbers_above_zero_are_accepted(string text, int milliseconds)
    {
        var node = WaitNode("7");
        var editor = NodeMillisecondsViewModel.ForWait(node);

        editor.Text = text;
        editor.AcceptButtonClickCommand.Execute(null);
        editor.ApplyTo(node);

        Assert.Equal(milliseconds, editor.Milliseconds);
        Assert.Equal(milliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture), node.TransactionName);
    }

    /// <summary>A node from a file with "20 s" opens with that text and says what is wrong; correcting it clears the error.</summary>
    [Fact]
    public void Invalid_time_from_a_file_is_shown_and_can_be_corrected()
    {
        var node = WaitNode("20 s");
        var editor = NodeMillisecondsViewModel.ForWait(node);
        var changed = new List<string?>();
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        var requeried = 0;
        editor.AcceptButtonClickCommand.CanExecuteChanged += (_, _) => requeried++;
        Assert.Equal("20 s", editor.Text);
        Assert.NotNull(editor.Error);
        Assert.False(editor.AcceptButtonClickCommand.CanExecute(null));

        editor.Text = "20000";

        Assert.Null(editor.Error);
        Assert.Contains(nameof(NodeMillisecondsViewModel.Error), changed);
        // The OK button asks CanExecute again only when the command says so; without it the button stays grey.
        Assert.Equal(1, requeried);
        Assert.True(editor.AcceptButtonClickCommand.CanExecute(null));
        editor.AcceptButtonClickCommand.Execute(null);
        editor.ApplyTo(node);
        Assert.Equal("20000", node.TransactionName);
    }

    /// <summary>
    /// A value from a file with characters nobody can see around the digits (a non-breaking space, a zero-width
    /// space, a byte order mark). The editor opens with the bare digits, and OK repairs the value.
    /// </summary>
    [Theory]
    [InlineData("\u00A01500\u00A0")]
    [InlineData("1500\u200B")]
    [InlineData("\uFEFF1500")]
    [InlineData(" \t1500\r\n ")]
    public void Time_with_characters_nobody_can_see_around_it_is_repaired_by_ok(string fromFile)
    {
        var node = WaitNode(fromFile);
        var editor = NodeMillisecondsViewModel.ForWait(node);
        Assert.Equal("1500", editor.Text);
        Assert.Null(editor.Error);

        editor.AcceptButtonClickCommand.Execute(null);
        editor.ApplyTo(node);

        Assert.Equal("1500", node.TransactionName);
        Assert.Equal("Wait 1500 ms", node.Title);
    }

    /// <summary>The same characters typed or pasted into the box: OK writes the bare digits.</summary>
    [Fact]
    public void Pasted_time_with_characters_nobody_can_see_around_it_is_accepted()
    {
        var node = WaitNode("7");
        var editor = NodeMillisecondsViewModel.ForWait(node);

        editor.Text = "\u00A02500\u200B";
        Assert.Null(editor.Error);
        editor.AcceptButtonClickCommand.Execute(null);
        editor.ApplyTo(node);

        Assert.Equal("2500", node.TransactionName);
    }

    /// <summary>
    /// Such a character inside the number: the box shows "1500", but it is not a number. The message has to say
    /// why, "use digits only" would not help.
    /// </summary>
    [Fact]
    public void Character_nobody_can_see_inside_the_number_is_named_in_the_message()
    {
        var node = WaitNode("15\u200B00");
        var editor = NodeMillisecondsViewModel.ForWait(node);

        Assert.Equal("15\u200B00", editor.Text);
        Assert.False(editor.AcceptButtonClickCommand.CanExecute(null));
        Assert.Equal(
            "The text contains characters that cannot be seen: 15[U+200B]00. Type the number again, in digits only.",
            editor.Error);

        editor.Text = "1 500";
        Assert.Equal("This is not a whole number from 1 to 2147483647. Use digits only, for example 1500.", editor.Error);

        editor.Text = "1500";
        Assert.Null(editor.Error);
        Assert.True(editor.AcceptButtonClickCommand.CanExecute(null));
    }

    /// <summary>Without the mapping a double-click on a Wait node ends in an exception of WindowManager.</summary>
    [Fact]
    public void Editor_opens_in_the_shared_dialog_window()
    {
        var editor = NodeMillisecondsViewModel.ForWait(WaitNode("1500"));

        Assert.Equal(typeof(DialogWindow), new WindowMapper().GetWindowTypeForViewModel(editor));
    }

    [Fact]
    public void Cancel_leaves_the_node_as_it_is()
    {
        var node = WaitNode("1500");
        var editor = NodeMillisecondsViewModel.ForWait(node);

        editor.Text = "9000";
        editor.ApplyTo(node);

        Assert.False(editor.Accepted);
        Assert.Equal("1500", node.TransactionName);
    }

    /// <summary>The minimum is a parameter: a later node type may allow 0.</summary>
    [Fact]
    public void Minimum_decides_whether_zero_is_allowed()
    {
        Assert.False(new NodeMillisecondsViewModel("t", "l", "0", minimum: 1).AcceptButtonClickCommand.CanExecute(null));
        Assert.True(new NodeMillisecondsViewModel("t", "l", "0", minimum: 0).AcceptButtonClickCommand.CanExecute(null));
        Assert.Contains(" 0 ", new NodeMillisecondsViewModel("t", "l", null, minimum: 0).Hint);
        Assert.Contains(" 1 ", new NodeMillisecondsViewModel("t", "l", null, minimum: 1).Error);
    }

    /// <summary>
    /// Drop connection and Wait for communication use the same editor with their own texts (GitLab
    /// secsgem/evencoolerfastsim#25). 0 is a value of its own there, so it is allowed and the hint says what it does.
    /// </summary>
    [Theory]
    [InlineData(NodeType.DropConnection, "Drop connection", "Downtime (ms)", "With 0 a new connection is set up right away")]
    [InlineData(NodeType.WaitForCommunication, "Wait for communication", "Timeout (ms)", "With 0 the node waits as long as the run may take")]
    public void Connection_nodes_get_the_editor_with_their_own_texts_and_zero_allowed(
        NodeType type, string header, string label, string note)
    {
        var node = new ScenarioNodeViewModel { Type = type, TransactionName = "5 s" };
        var editor = NodeMillisecondsViewModel.For(node)!;

        Assert.Equal(header, editor.HeaderText);
        Assert.Equal(label, editor.Label);
        Assert.StartsWith("Whole number from 0 to 2147483647. ", editor.Hint);
        Assert.Contains(note, editor.Hint);
        Assert.Equal("5 s", editor.Text);
        Assert.Equal("This is not a whole number from 0 to 2147483647. Use digits only, for example 1500.", editor.Error);
        Assert.False(editor.AcceptButtonClickCommand.CanExecute(null));

        editor.Text = "0";
        Assert.Null(editor.Error);
        editor.AcceptButtonClickCommand.Execute(null);
        editor.ApplyTo(node);

        Assert.Equal("0", node.TransactionName);
    }

    /// <summary>What the editor accepts, the engine reads: digits only, also for these two node types.</summary>
    [Theory]
    [InlineData(NodeType.DropConnection, "+5")]
    [InlineData(NodeType.DropConnection, "-1")]
    [InlineData(NodeType.DropConnection, "1.5")]
    [InlineData(NodeType.WaitForCommunication, "+5")]
    [InlineData(NodeType.WaitForCommunication, "2147483648")]
    [InlineData(NodeType.WaitForCommunication, "")]
    public void Connection_nodes_take_digits_only(NodeType type, string text)
    {
        var node = new ScenarioNodeViewModel { Type = type, TransactionName = "100" };
        var editor = NodeMillisecondsViewModel.For(node)!;

        editor.Text = text;
        editor.AcceptButtonClickCommand.Execute(null);
        editor.ApplyTo(node);

        Assert.NotNull(editor.Error);
        Assert.False(editor.Accepted);
        Assert.Equal("100", node.TransactionName);
    }

    [Fact]
    public void Wait_node_gets_its_editor_and_does_not_allow_zero()
    {
        var editor = NodeMillisecondsViewModel.For(WaitNode("0"))!;

        Assert.Equal("Wait", editor.HeaderText);
        Assert.Equal("Wait time (ms)", editor.Label);
        Assert.Equal("Whole number from 1 to 2147483647.", editor.Hint);
        Assert.NotNull(editor.Error);
    }

    [Theory]
    [InlineData(NodeType.Start)]
    [InlineData(NodeType.End)]
    [InlineData(NodeType.And)]
    [InlineData(NodeType.Send)]
    [InlineData(NodeType.SendAndWait)]
    [InlineData(NodeType.Receive)]
    [InlineData(NodeType.Condition)]
    public void Node_types_without_a_time_have_no_such_editor(NodeType type)
    {
        Assert.Null(NodeMillisecondsViewModel.For(new ScenarioNodeViewModel { Type = type }));
    }

    private static ScenarioNodeViewModel WaitNode(string? time) => new() { Type = NodeType.Wait, TransactionName = time };
}
