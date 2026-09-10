using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvenBetterFastSim.WPF.ViewModels.Graph;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Responders;

namespace EvenBetterFastSim.WPF.ViewModels.Responders;

public enum NodeResponderMode
{
    /// <summary>Editing a Receive node: match conditions on the incoming message.</summary>
    Conditions,

    /// <summary>Editing a Send node: value bindings on the outgoing message.</summary>
    Bindings
}

/// <summary>An upstream Receive node offered as a value source: its id, a display label, and a clone of its message shape.</summary>
public sealed record UpstreamReceive(string NodeId, string Label, SecsGemDataMessage Message);

/// <summary>
/// One dialog, two faces. For a Receive node it edits the match conditions that gate the step;
/// for a Send node it edits how each outgoing item gets its value — fixed, or pulled from any
/// message received earlier in the chain (pick the message, then the parameter).
/// </summary>
public partial class NodeResponderViewModel : ObservableObject, IBaseViewModel
{
    public Action? CloseAction { get; set; }

    private ScenarioNodeViewModel? node;

    /// <summary>Bindings mode: cloned message shapes keyed by the Receive node id that captured them.</summary>
    private readonly Dictionary<string, SecsGemDataMessage> triggerShapes = [];

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

    public ObservableCollection<SampleInputViewModel> SampleInputs { get; } = [];

    public IReadOnlyList<ConditionKind> ConditionKinds { get; } = Enum.GetValues<ConditionKind>();
    public IReadOnlyList<BindingSourceKind> BindingSourceKinds { get; } = Enum.GetValues<BindingSourceKind>();

    [ObservableProperty] public partial string TestResult { get; set; } = string.Empty;
    public ObservableCollection<ResponderFieldViewModel> TestPreviewFields { get; } = [];

    // ---- entry points --------------------------------------------------

    public void InitializeForReceive(ScenarioNodeViewModel receiveNode)
    {
        node = receiveNode;
        Mode = NodeResponderMode.Conditions;

        var expected = ExpectedMessage(receiveNode);
        HeaderText = $"Match — {expected?.Name ?? receiveNode.Title}";
        SubtitleText = "This step proceeds only when the received message satisfies every condition. Leave rows on \"any\" to ignore them.";

        BuildFields(expected, ResponderFieldMode.Trigger, parameterLookup: null);
        ApplyStoredConditions();
        OnPropertyChanged(nameof(IsConditions));
        OnPropertyChanged(nameof(IsBindings));
    }

    public void InitializeForSend(ScenarioNodeViewModel sendNode, IReadOnlyList<UpstreamReceive> upstreamReceives)
    {
        node = sendNode;
        Mode = NodeResponderMode.Bindings;

        var outgoing = sendNode.Transaction?.PrimaryMessage;
        HeaderText = $"Response values — {outgoing?.Name ?? sendNode.Title}";

        foreach (var upstream in upstreamReceives)
        {
            triggerShapes[upstream.NodeId] = upstream.Message;
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

        SubtitleText = upstreamReceives.Count == 0
            ? "No upstream Receive on this path — you can still set fixed values."
            : "Echo / Copy branch: pick a received message, then the parameter inside it.";

        BuildFields(outgoing, ResponderFieldMode.Response, LookupParameters);
        ApplyStoredBindings();
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
        Fields.Clear();
        if (message is null) return;

        var clone = (SecsGemDataMessage)message.Clone();
        var index = 0;
        foreach (var item in clone.Children.OfType<SecsGemItem>())
        {
            Fields.Add(new ResponderFieldViewModel(item, index.ToString(), mode, parameterLookup));
            index++;
        }
    }

    private void CollectParameters(ResponderFieldViewModel field, string sourceNodeId, string sourceLabel, List<PathOption> into)
    {
        into.Add(new PathOption(field.Path, $"[{field.Path}]  {field.DisplayLabel}", sourceNodeId));
        if (field.IsLeaf)
            SampleInputs.Add(new SampleInputViewModel(sourceNodeId, $"{sourceLabel} · [{field.Path}]", field.Path));

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

    // ---- test preview (Bindings mode) -----------------------------

    [RelayCommand]
    private void RunTest()
    {
        TestPreviewFields.Clear();
        if (Mode != NodeResponderMode.Bindings || node?.Transaction is null)
        {
            TestResult = "Nothing to preview.";
            return;
        }

        var samples = triggerShapes.ToDictionary(
            kvp => kvp.Key,
            kvp => (SecsGemDataMessage)kvp.Value.Clone());

        foreach (var sample in SampleInputs)
        {
            if (samples.TryGetValue(sample.SourceNodeId, out var message)
                && SecsGemItemPath.TryResolve(message, sample.ItemPath, out var item))
            {
                item.SetValuesFromStrings(sample.Value.Split(',', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        var fallback = samples.Values.LastOrDefault();
        SecsGemDataMessage? Resolve(string? id) =>
            id is not null && samples.TryGetValue(id, out var m) ? m : fallback;

        var outgoing = (SecsGemDataMessage)node.Transaction.PrimaryMessage.Clone();
        foreach (var binding in CurrentBindings())
            binding.Apply(outgoing, Resolve);

        var index = 0;
        foreach (var item in outgoing.Children.OfType<SecsGemItem>())
        {
            TestPreviewFields.Add(new ResponderFieldViewModel(item, index.ToString(), ResponderFieldMode.Response));
            index++;
        }
        TestResult = $"Resolved {node.Transaction.PrimaryMessage.Name}";
    }

    private IEnumerable<MatchCondition> CurrentConditions() =>
        Flatten(Fields).Select(f => f.ToCondition()).OfType<MatchCondition>();

    private IEnumerable<ValueBinding> CurrentBindings() =>
        Flatten(Fields).Select(f => f.ToBinding()).OfType<ValueBinding>();

    // ---- commit --------------------------------------------------

    [RelayCommand]
    private void AcceptButtonClick()
    {
        if (node is null)
        {
            CloseAction?.Invoke();
            return;
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

/// <summary>An incoming leaf plus a sample value, used by the Bindings-mode "Test" button.</summary>
public partial class SampleInputViewModel(string sourceNodeId, string label, string itemPath) : ObservableObject
{
    public string SourceNodeId { get; } = sourceNodeId;
    public string Label { get; } = label;
    public string ItemPath { get; } = itemPath;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;
}
