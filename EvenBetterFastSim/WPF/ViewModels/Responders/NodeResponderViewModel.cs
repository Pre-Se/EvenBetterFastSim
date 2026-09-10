using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvenBetterFastSim.WPF.ViewModels.Graph;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Data_Containers.Interfaces;
using SecsGemBaseItems.Enums;
using SecsGemBaseItems.Responders;

namespace EvenBetterFastSim.WPF.ViewModels.Responders;

public enum NodeResponderMode
{
    /// <summary>Editing a Receive node: match conditions on the incoming message.</summary>
    Conditions,

    /// <summary>Editing a Send node: value bindings on the outgoing message.</summary>
    Bindings
}

/// <summary>An upstream node (Receive or Send) offered as a value source: its id, a display label, and a clone of its message shape.</summary>
public sealed record UpstreamMessage(string NodeId, string Label, SecsGemDataMessage Message);

/// <summary>
/// One dialog, two faces. For a Receive node it edits the match conditions that gate the step;
/// for a Send node it edits how each outgoing item gets its value — fixed, or pulled from any
/// message received earlier in the chain (pick the message, then the parameter).
/// </summary>
public partial class NodeResponderViewModel : ObservableObject, IBaseViewModel
{
    public Action? CloseAction { get; set; }

    private ScenarioNodeViewModel? node;

    /// <summary>
    /// The message whose shape the editor works on: a clone of the node's Primary (Send / Receive-primary)
    /// or Reply (Receive with <see cref="ScenarioNodeViewModel.UseReplyMessage"/>) message. Structural edits
    /// mutate this tree; it is written back to the node on OK only when <see cref="structureDirty"/> is set.
    /// </summary>
    private SecsGemDataMessage? workingMessage;

    /// <summary>True when the working message is the node's Reply message rather than its Primary.</summary>
    private bool editingReplyMessage;

    /// <summary>Set once the user adds / removes / moves / retypes an item, so OK persists the new shape.</summary>
    private bool structureDirty;

    /// <summary>Mode + parameter lookup last passed to <see cref="BuildFields"/>, reused when the tree is rebuilt.</summary>
    private ResponderFieldMode fieldMode;
    private Func<string?, IReadOnlyList<PathOption>>? fieldParameterLookup;

    /// <summary>Bindings mode: selectable parameters per Receive node id.</summary>
    private readonly Dictionary<string, List<PathOption>> parametersByNode = [];

    [ObservableProperty] public partial NodeResponderMode Mode { get; private set; }
    [ObservableProperty] public partial string HeaderText { get; private set; } = string.Empty;
    [ObservableProperty] public partial string SubtitleText { get; private set; } = string.Empty;

    public bool IsConditions => Mode == NodeResponderMode.Conditions;
    public bool IsBindings => Mode == NodeResponderMode.Bindings;

    public ObservableCollection<ResponderFieldViewModel> Fields { get; } = [];

    /// <summary>First dropdown options — the received messages available on this path.</summary>
    public ObservableCollection<UpstreamMessageOption> SourceMessages { get; } = [];

    public IReadOnlyList<ConditionKind> ConditionKinds { get; } = Enum.GetValues<ConditionKind>();
    public IReadOnlyList<BindingSourceKind> BindingSourceKinds { get; } = Enum.GetValues<BindingSourceKind>();

    /// <summary>Row selected in the field tree — target for the Add / Duplicate / Delete / Move buttons.</summary>
    [ObservableProperty] public partial ResponderFieldViewModel? SelectedField { get; set; }

    partial void OnSelectedFieldChanged(ResponderFieldViewModel? value) => NotifyStructureCommands();

    // ---- entry points --------------------------------------------------

    public void InitializeForReceive(ScenarioNodeViewModel receiveNode)
    {
        node = receiveNode;
        Mode = NodeResponderMode.Conditions;

        var expected = ExpectedMessage(receiveNode);
        HeaderText = $"Match — {expected?.Name ?? receiveNode.Title}";
        SubtitleText = "This step proceeds only when the received message satisfies every condition. Leave rows on \"any\" to ignore them.";

        editingReplyMessage = receiveNode.UseReplyMessage;
        workingMessage = expected is null ? null : (SecsGemDataMessage)expected.Clone();

        BuildFields(workingMessage, ResponderFieldMode.Trigger, parameterLookup: null);
        ApplyStoredConditions();
        NotifyStructureCommands();
        OnPropertyChanged(nameof(IsConditions));
        OnPropertyChanged(nameof(IsBindings));
    }

    public void InitializeForSend(ScenarioNodeViewModel sendNode, IReadOnlyList<UpstreamMessage> upstreamMessages)
    {
        node = sendNode;
        Mode = NodeResponderMode.Bindings;

        var outgoing = sendNode.Transaction?.PrimaryMessage;
        HeaderText = $"Response values — {outgoing?.Name ?? sendNode.Title}";

        editingReplyMessage = false;
        workingMessage = outgoing is null ? null : (SecsGemDataMessage)outgoing.Clone();

        foreach (var upstream in upstreamMessages)
        {
            SourceMessages.Add(new UpstreamMessageOption(upstream.NodeId, upstream.Label));

            var parameters = new List<PathOption>();
            var index = 0;
            foreach (var item in upstream.Message.Children.OfType<SecsGemItem>())
            {
                CollectParameters(new ResponderFieldViewModel(item, index.ToString(), ResponderFieldMode.Trigger),
                    upstream.NodeId, upstream.Label, parameters);
                index++;
            }
            parametersByNode[upstream.NodeId] = parameters;
        }

        SubtitleText = upstreamMessages.Count == 0
            ? "No upstream Receive on this path — you can still set fixed values."
            : "Echo / Copy branch: pick a received message, then the parameter inside it.";

        BuildFields(workingMessage, ResponderFieldMode.Response, LookupParameters);
        ApplyStoredBindings();
        NotifyStructureCommands();
        OnPropertyChanged(nameof(IsConditions));
        OnPropertyChanged(nameof(IsBindings));
    }

    private IReadOnlyList<PathOption> LookupParameters(string? nodeId) =>
        nodeId is not null && parametersByNode.TryGetValue(nodeId, out var list) ? list : [];

    // ---- build / restore --------------------------------------------

    private static SecsGemDataMessage? ExpectedMessage(ScenarioNodeViewModel node) =>
        node.Transaction is null ? null
        : node.UseReplyMessage ? node.Transaction.ReplyMessage : node.Transaction.PrimaryMessage;

    private void BuildFields(SecsGemDataMessage? message, ResponderFieldMode mode,
        Func<string?, IReadOnlyList<PathOption>>? parameterLookup)
    {
        fieldMode = mode;
        fieldParameterLookup = parameterLookup;

        Fields.Clear();
        if (message is null) return;

        // Wrap the working message directly (no extra clone) so item identity is stable across
        // rebuilds — CommitStructuralChange relies on it to carry user edits over.
        var index = 0;
        foreach (var item in message.Children.OfType<SecsGemItem>())
        {
            Fields.Add(new ResponderFieldViewModel(item, index.ToString(), mode, parameterLookup));
            index++;
        }

        foreach (var field in Flatten(Fields))
            field.FormatChangeRequested += OnFieldFormatChangeRequested;
    }

    private void CollectParameters(ResponderFieldViewModel field, string sourceNodeId, string sourceLabel, List<PathOption> into)
    {
        into.Add(new PathOption(field.Path, $"[{field.Path}]  {field.DisplayLabel}", sourceNodeId));

        foreach (var child in field.Children)
            CollectParameters(child, sourceNodeId, sourceLabel, into);
    }

    private void ApplyStoredConditions()
    {
        foreach (var condition in ResponderJson.DeserializeConditions(node?.MatchConditionsJson))
        {
            if (FindByPath(Fields, condition.ItemPath) is not { } target) continue;
            target.Condition = condition.Operator == MatchOperator.Equals ? ConditionKind.Equals : ConditionKind.NotEquals;
            target.ConditionValue = condition.Value;
        }
    }

    private void ApplyStoredBindings()
    {
        foreach (var binding in ResponderJson.DeserializeBindings(node?.ResponseBindingsJson))
        {
            if (FindByPath(Fields, binding.TargetItemPath) is not { } target) continue;
            target.Source = binding.Source;

            if (binding.Source == BindingSourceKind.Literal)
            {
                target.StaticValue = binding.SourceRef;
                continue;
            }

            // Setting SourceMessage repopulates AvailableParameters, then we pick the parameter.
            target.SourceMessage =
                SourceMessages.FirstOrDefault(m => m.NodeId == binding.SourceNodeId)
                ?? SourceMessages.FirstOrDefault();
            target.SourceParameter =
                target.AvailableParameters.FirstOrDefault(p => p.Path == binding.SourceRef);
        }
    }

    private static ResponderFieldViewModel? FindByPath(IEnumerable<ResponderFieldViewModel> fields, string path)
    {
        foreach (var field in fields)
        {
            if (field.Path == path) return field;
            if (FindByPath(field.Children, path) is { } hit) return hit;
        }
        return null;
    }

    private static IEnumerable<ResponderFieldViewModel> Flatten(IEnumerable<ResponderFieldViewModel> fields)
    {
        foreach (var field in fields)
        {
            yield return field;
            foreach (var child in Flatten(field.Children))
                yield return child;
        }
    }

    private IEnumerable<MatchCondition> CurrentConditions() =>
        Flatten(Fields).Select(f => f.ToCondition()).OfType<MatchCondition>();

    private IEnumerable<ValueBinding> CurrentBindings() =>
        Flatten(Fields).Select(f => f.ToBinding()).OfType<ValueBinding>();

    // ---- structural message editing --------------------------------
    //
    // The toolbar in NodeResponderView drives these. Each one mutates `workingMessage`, then
    // CommitStructuralChange rebuilds the field tree (so positional paths are recomputed) while
    // carrying the user's in-progress condition / binding edits across by item identity.

    /// <summary>Add Item targets a container: a selected List, or the message body when nothing is selected.</summary>
    private bool CanAddChildItem() => workingMessage is not null && SelectedField is null or { IsList: true };
    private bool HasSelectedField() => workingMessage is not null && SelectedField is not null;

    private void NotifyStructureCommands()
    {
        AddItemCommand.NotifyCanExecuteChanged();
        AddSiblingCommand.NotifyCanExecuteChanged();
        DuplicateItemCommand.NotifyCanExecuteChanged();
        DeleteItemCommand.NotifyCanExecuteChanged();
        MoveItemUpCommand.NotifyCanExecuteChanged();
        MoveItemDownCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Adds a U1 item as the last child of the selected List (or of the message body).</summary>
    [RelayCommand(CanExecute = nameof(CanAddChildItem))]
    private void AddItem()
    {
        if (workingMessage is null) return;

        ICanBeParent container = SelectedField is { IsList: true } listField ? listField.Item : workingMessage;
        var newItem = SecsGemItem.Create(SecsGemItemFormatType.U1);
        newItem.SetParent(container);
        CommitStructuralChange(newItem);
    }

    /// <summary>Adds a U1 item as the next sibling of the selected item.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedField))]
    private void AddSibling()
    {
        if (SelectedField is not { } field || ContainerOf(field) is not { } location) return;

        var newItem = SecsGemItem.Create(SecsGemItemFormatType.U1);
        newItem.SetParent(location.Container);
        MoveWithin(location.Container, ChildrenOf(location.Container).Count - 1, location.Index + 1);
        CommitStructuralChange(newItem);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedField))]
    private void DuplicateItem()
    {
        if (SelectedField is not { } field || ContainerOf(field) is not { } location) return;

        var clone = field.Item.Clone();
        clone.SetParent(location.Container);
        MoveWithin(location.Container, ChildrenOf(location.Container).Count - 1, location.Index + 1);
        CommitStructuralChange(clone);
    }

    [RelayCommand(CanExecute = nameof(HasSelectedField))]
    private void DeleteItem()
    {
        if (SelectedField is not { } field || ContainerOf(field) is not { } location) return;

        var siblings = ChildrenOf(location.Container);
        field.Item.SetParent(null);
        var next = siblings.Count == 0 ? null : siblings[Math.Min(location.Index, siblings.Count - 1)] as SecsGemItem;
        CommitStructuralChange(next);
    }

    [RelayCommand(CanExecute = nameof(CanMoveItemUp))]
    private void MoveItemUp() => MoveSelected(-1);

    private bool CanMoveItemUp() =>
        SelectedField is { } f && ContainerOf(f) is { } location && location.Index > 0;

    [RelayCommand(CanExecute = nameof(CanMoveItemDown))]
    private void MoveItemDown() => MoveSelected(+1);

    private bool CanMoveItemDown() =>
        SelectedField is { } f && ContainerOf(f) is { } location
        && location.Index < ChildrenOf(location.Container).Count - 1;

    private void MoveSelected(int delta)
    {
        if (SelectedField is not { } field || ContainerOf(field) is not { } location) return;

        var target = location.Index + delta;
        if (target < 0 || target >= ChildrenOf(location.Container).Count) return;
        MoveWithin(location.Container, location.Index, target);
        CommitStructuralChange(field.Item);
    }

    private void OnFieldFormatChangeRequested(ResponderFieldViewModel field)
    {
        // The change comes from a ComboBox selection commit; rebuilding Fields synchronously
        // underneath it upsets the binding pipeline, so hop through the dispatcher when there is one.
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess() == false)
            ApplyFormatChange(field);
        else
            dispatcher.BeginInvoke(() => ApplyFormatChange(field));
    }

    private void ApplyFormatChange(ResponderFieldViewModel field)
    {
        if (workingMessage is null || field.Format == field.Item.FormatType) return;
        if (ContainerOf(field) is not { } location) return;

        var replacement = SecsGemItem.Create(field.Format);
        replacement.Description = field.Item.Description;
        if (field.Format == SecsGemItemFormatType.List)
        {
            // Preserve any children when a value item becomes a list.
            foreach (var child in field.Item.Children.OfType<SecsGemItem>().ToList())
                child.SetParent(replacement);
        }
        else
        {
            // Best-effort value carry-over; parsing drops anything the new format can't represent.
            replacement.SetValuesFromStrings(field.Item.GetStringValues());
        }

        var siblings = ChildrenOf(location.Container);
        field.Item.SetParent(null);
        replacement.SetParent(location.Container);
        MoveWithin(location.Container, siblings.Count - 1, location.Index);
        CommitStructuralChange(replacement);
    }

    /// <summary>The container (message body or a List item) holding a field, plus the field's index in it.</summary>
    private readonly record struct FieldLocation(ICanBeParent Container, int Index);

    private FieldLocation? ContainerOf(ResponderFieldViewModel field)
    {
        if (workingMessage is null) return null;

        var segments = field.Path.Split('.');
        if (!int.TryParse(segments[^1], out var index)) return null;

        if (segments.Length == 1)
            return new FieldLocation(workingMessage, index);

        var parentPath = string.Join('.', segments[..^1]);
        return SecsGemItemPath.TryResolve(workingMessage, parentPath, out var parentItem)
            ? new FieldLocation(parentItem, index)
            : null;
    }

    private static ObservableCollection<IDataItem> ChildrenOf(ICanBeParent container) =>
        (ObservableCollection<IDataItem>)((IDataItem)container).Children;

    private static void MoveWithin(ICanBeParent container, int from, int to)
    {
        var children = ChildrenOf(container);
        if (from >= 0 && to >= 0 && from < children.Count && to < children.Count && from != to)
            children.Move(from, to);
    }

    /// <summary>
    /// Rebuilds the field tree from the mutated <see cref="workingMessage"/>, re-applies the user's
    /// pending condition / binding edits (keyed by item identity, stable across the rebuild), and
    /// re-selects <paramref name="itemToSelect"/>.
    /// </summary>
    private void CommitStructuralChange(SecsGemItem? itemToSelect)
    {
        structureDirty = true;

        var conditionEdits = new Dictionary<SecsGemItem, (ConditionKind Kind, string Value)>();
        var bindingEdits = new Dictionary<SecsGemItem, (BindingSourceKind Source, string StaticValue, string? NodeId, string? ParamPath)>();
        foreach (var field in Flatten(Fields))
        {
            if (fieldMode == ResponderFieldMode.Trigger)
            {
                if (field.Condition != ConditionKind.Any)
                    conditionEdits[field.Item] = (field.Condition, field.ConditionValue);
            }
            else
            {
                bindingEdits[field.Item] =
                    (field.Source, field.StaticValue, field.SourceMessage?.NodeId, field.SourceParameter?.Path);
            }
        }

        BuildFields(workingMessage, fieldMode, fieldParameterLookup);

        foreach (var field in Flatten(Fields))
        {
            if (conditionEdits.TryGetValue(field.Item, out var condition))
            {
                field.Condition = condition.Kind;
                field.ConditionValue = condition.Value;
            }

            if (bindingEdits.TryGetValue(field.Item, out var binding))
            {
                field.Source = binding.Source;
                if (binding.Source == BindingSourceKind.Literal)
                {
                    field.StaticValue = binding.StaticValue;
                }
                else
                {
                    field.SourceMessage = SourceMessages.FirstOrDefault(m => m.NodeId == binding.NodeId)
                        ?? SourceMessages.FirstOrDefault();
                    field.SourceParameter = field.AvailableParameters.FirstOrDefault(p => p.Path == binding.ParamPath);
                }
            }
        }

        SelectedField = itemToSelect is null
            ? null
            : Flatten(Fields).FirstOrDefault(f => ReferenceEquals(f.Item, itemToSelect));
        NotifyStructureCommands();
    }

    // ---- commit --------------------------------------------------

    [RelayCommand]
    private void AcceptButtonClick()
    {
        if (node is null)
        {
            CloseAction?.Invoke();
            return;
        }

        // Persist a structurally edited message shape back onto the node's transaction. Paths inside
        // the conditions / bindings below are recomputed from the (already rebuilt) field tree, so
        // they line up with the new shape.
        if (structureDirty && workingMessage is not null && node.Transaction is { } transaction)
        {
            if (editingReplyMessage)
                transaction.ReplyMessage = workingMessage;
            else
                transaction.PrimaryMessage = workingMessage;
        }

        if (Mode == NodeResponderMode.Conditions)
        {
            var conditions = CurrentConditions().ToList();
            node.MatchConditionsJson = conditions.Count == 0 ? null : ResponderJson.SerializeConditions(conditions);
        }
        else
        {
            var bindings = CurrentBindings().ToList();
            node.ResponseBindingsJson = bindings.Count == 0 ? null : ResponderJson.SerializeBindings(bindings);
        }

        CloseAction?.Invoke();
    }

    [RelayCommand]
    private void CancelClick() => CloseAction?.Invoke();
}
