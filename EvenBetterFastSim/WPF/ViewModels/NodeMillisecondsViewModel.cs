using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvenBetterFastSim.WPF.ViewModels.Graph;
using SecsGemScenarioEngine.Models;

namespace EvenBetterFastSim.WPF.ViewModels;

/// <summary>
/// Editor for a scenario node's parameter in milliseconds: the time of a Wait node, the downtime of a Drop connection
/// node, the timeout of a Wait for communication node. Hosted in the shared <c>DialogWindow</c>, whose OK button
/// binds to <see cref="AcceptButtonClickCommand"/> (GitLab secsgem/evencoolerfastsim#18 and #25).
/// </summary>
/// <remarks>
/// <para>
/// The engine does not wait for a Wait node's time it cannot read, and it lets the other two node types fail. So the
/// editor only accepts a whole number written in digits, and the node stores it in that form.
/// </para>
/// <para>
/// Not an ObservableValidator on purpose: DialogWindow binds its content to the view model itself, and WPF then draws
/// its red validation frame around the whole dialog for a view model that reports data errors. The message under the
/// field is enough.
/// </para>
/// </remarks>
public partial class NodeMillisecondsViewModel : ObservableObject, IBaseViewModel
{
    private readonly int minimum;

    public Action? CloseAction { get; set; }

    /// <summary>True when the user confirmed with OK (vs. cancelling / closing the dialog).</summary>
    public bool Accepted { get; private set; }

    /// <summary>The value the user confirmed; valid once <see cref="Accepted"/> is true.</summary>
    public int Milliseconds { get; private set; }

    /// <summary>Title of the dialog.</summary>
    public string HeaderText { get; }

    /// <summary>Label in front of the input field.</summary>
    public string Label { get; }

    /// <summary>Says which values are allowed.</summary>
    public string Hint { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Error))]
    [NotifyCanExecuteChangedFor(nameof(AcceptButtonClickCommand))]
    private string text;

    /// <summary>What is wrong with <see cref="Text"/>, or null.</summary>
    public string? Error
    {
        get
        {
            if (TryParse(Text, minimum, out _)) return null;
            // A character nobody can see inside the number (from a file or pasted): the box shows "1500", so say why.
            var entered = NodeMilliseconds.TrimEnds(Text);
            var visible = NodeMilliseconds.Visible(entered);
            return visible != entered
                ? $"The text contains characters that cannot be seen: {visible}. Type the number again, in digits only."
                : $"This is not a whole number from {minimum} to {int.MaxValue}. Use digits only, for example 1500.";
        }
    }

    /// <param name="minimum">Smallest value the editor accepts (1 for a time that has to be greater than 0).</param>
    /// <param name="note">What a special value means, added to the hint; for example what 0 does.</param>
    public NodeMillisecondsViewModel(string headerText, string label, string? text, int minimum, string? note = null)
    {
        HeaderText = headerText;
        Label = label;
        this.minimum = minimum;
        Hint = $"Whole number from {minimum} to {int.MaxValue}." + (note is null ? string.Empty : " " + note);
        this.text = NodeMilliseconds.TrimEnds(text);
    }

    /// <summary>
    /// The editor for a node with a time in ms, or null for a node type without one. It starts with the node's
    /// current text, also when that is invalid.
    /// </summary>
    public static NodeMillisecondsViewModel? For(ScenarioNodeViewModel node) => node.Type switch
    {
        NodeType.Wait => ForWait(node),
        NodeType.DropConnection => ForDropConnection(node),
        NodeType.WaitForCommunication => ForWaitForCommunication(node),
        _ => null
    };

    /// <summary>The editor for the time of a Wait node.</summary>
    public static NodeMillisecondsViewModel ForWait(ScenarioNodeViewModel node) =>
        new("Wait", "Wait time (ms)", node.TransactionName, minimum: 1);

    /// <summary>The editor for the downtime of a Drop connection node.</summary>
    public static NodeMillisecondsViewModel ForDropConnection(ScenarioNodeViewModel node) =>
        new("Drop connection", "Downtime (ms)", node.TransactionName, minimum: 0,
            note: "With 0 a new connection is set up right away; with more the port stays closed that long.");

    /// <summary>The editor for the timeout of a Wait for communication node.</summary>
    public static NodeMillisecondsViewModel ForWaitForCommunication(ScenarioNodeViewModel node) =>
        new("Wait for communication", "Timeout (ms)", node.TransactionName, minimum: 0,
            note: "With 0 the node waits as long as the run may take.");

    /// <summary>
    /// Digits only, within the range of an int and not below the minimum. Spaces and characters nobody can see at
    /// the two ends are ignored (<see cref="NodeMilliseconds.TrimEnds"/>); OK writes the bare digits.
    /// </summary>
    public static bool TryParse(string? text, int minimum, out int milliseconds) =>
        int.TryParse(NodeMilliseconds.TrimEnds(text), NumberStyles.None, CultureInfo.InvariantCulture, out milliseconds) &&
        milliseconds >= minimum;

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void AcceptButtonClick()
    {
        if (!TryParse(Text, minimum, out var milliseconds)) return;
        Milliseconds = milliseconds;
        Accepted = true;
        CloseAction?.Invoke();
    }

    private bool CanAccept() => Error is null;

    /// <summary>Writes the confirmed value into the node; does nothing if the dialog was cancelled.</summary>
    public void ApplyTo(ScenarioNodeViewModel node)
    {
        if (Accepted)
            node.TransactionName = NodeMilliseconds.Format(Milliseconds);
    }
}
