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

/// <summary>
/// One dialog, two faces. For a Receive node it edits the match conditions that gate the step;
/// for a Send node it edits how each outgoing item gets its value — fixed, or pulled from the
/// message the nearest upstream Receive captured.
/// </summary>
public partial class NodeResponderViewModel : ObservableObject, IBaseViewModel
{
    public Action? CloseAction { get; set; }

    private ScenarioNodeViewModel? node;
    private SecsGemDataMessage? triggerShape;

    [ObservableProperty] public partial NodeResponderMode Mode { get; private set; }
    [ObservableProperty] public partial string HeaderText { get; private set; } = string.Empty;
    [ObservableProperty] public partial string SubtitleText { get; private set; } = string.Empty;

    public bool IsConditions => Mode == NodeResponderMode.Conditions;
    public bool IsBindings => Mode == NodeResponderMode.Bindings;

    public ObservableCollection<ResponderFieldViewModel> Fields { get; } = [];
    public ObservableCollection<PathOption> IncomingPaths { get; } = [];
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

        BuildFields(expected, ResponderFieldMode.Trigger);
        ApplyStoredConditions();
        OnPropertyChanged(nameof(IsConditions));
        OnPropertyChanged(nameof(IsBindings));
    }

    public void InitializeForSend(ScenarioNodeViewModel sendNode, SecsGemTransaction? upstreamTransaction, bool upstreamUsesReply)
    {
        node = sendNode;
        Mode = NodeResponderMode.Bindings;

        var outgoing = sendNode.Transaction?.PrimaryMessage;
        HeaderText = $"Response values — {outgoing?.Name ?? sendNode.Title}";

        if (upstreamTransaction is not null)
        {
            var source = upstreamUsesReply ? upstreamTransaction.ReplyMessage : upstreamTransaction.PrimaryMessage;
            triggerShape = (SecsGemDataMessage)source.Clone();
        }

        SubtitleText = triggerShape is null
            ? "No upstream Receive on this path — you can still set fixed values."
            : $"Each item can keep its value, be set to a fixed value, or be pulled from the received {triggerShape.Name}.";

        BuildFields(outgoing, ResponderFieldMode.Response);

        if (triggerShape is not null)
        {
            var index = 0;
            foreach (var item in triggerShape.Children.OfType<SecsGemItem>())
            {
                CollectPaths(new ResponderFieldViewModel(item, index.ToString(), ResponderFieldMode.Trigger));
                index++;
            }
            foreach (var path in IncomingPaths)
                SampleInputs.Add(new SampleInputViewModel(path));
        }

        ApplyStoredBindings();
        OnPropertyChanged(nameof(IsConditions));
        OnPropertyChanged(nameof(IsBindings));
    }

    // ---- build / restore --------------------------------------------

    private static SecsGemDataMessage? ExpectedMessage(ScenarioNodeViewModel node) =>
        node.Transaction is null ? null
        : node.UseReplyMessage ? node.Transaction.ReplyMessage : node.Transaction.PrimaryMessage;

    private void BuildFields(SecsGemDataMessage? message, ResponderFieldMode mode)
    {
        Fields.Clear();
        if (message is null) return;

        var clone = (SecsGemDataMessage)message.Clone();
        var index = 0;
        foreach (var item in clone.Children.OfType<SecsGemItem>())
        {
            Fields.Add(new ResponderFieldViewModel(item, index.ToString(), mode));
            index++;
        }
    }

    private void CollectPaths(ResponderFieldViewModel field)
    {
        IncomingPaths.Add(new PathOption(field.Path, $"[{field.Path}]  {field.DisplayLabel}"));
        foreach (var child in field.Children)
            CollectPaths(child);
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
                target.StaticValue = binding.SourceRef;
            else
                target.IncomingPath = IncomingPaths.FirstOrDefault(p => p.Path == binding.SourceRef)
                                      ?? new PathOption(binding.SourceRef, $"[{binding.SourceRef}]");
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

        var incoming = triggerShape is null ? null : (SecsGemDataMessage)triggerShape.Clone();
        if (incoming is not null)
        {
            foreach (var sample in SampleInputs)
            {
                if (SecsGemItemPath.TryResolve(incoming, sample.Path.Path, out var item))
                    item.SetValuesFromStrings(sample.Value.Split(',', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        var outgoing = (SecsGemDataMessage)node.Transaction.PrimaryMessage.Clone();
        foreach (var binding in CurrentBindings())
        {
            if (incoming is not null)
                binding.Apply(outgoing, incoming);
            else if (binding.Source == BindingSourceKind.Literal)
                binding.Apply(outgoing, outgoing);
        }

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
public partial class SampleInputViewModel(PathOption path) : ObservableObject
{
    public PathOption Path { get; } = path;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;
}
