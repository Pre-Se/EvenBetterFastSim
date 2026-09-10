using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Enums;
using SecsGemBaseItems.Responders;

namespace EvenBetterFastSim.WPF.ViewModels.Responders;

public enum ResponderFieldMode
{
    /// <summary>Row in the trigger tree: carries an optional match condition.</summary>
    Trigger,

    /// <summary>Row in the response tree: carries a value source (static / echo / copy branch).</summary>
    Response
}

public enum ConditionKind
{
    Any,
    Equals,
    NotEquals
}

/// <summary>
/// One row in the responder editor's trigger or response tree. Wraps a single <see cref="SecsGemItem"/>
/// from a message template and remembers its positional <see cref="Path"/> so conditions/bindings can
/// be produced on save.
/// </summary>
public partial class ResponderFieldViewModel : ObservableObject
{
    public ResponderFieldMode Mode { get; }
    public string Path { get; }
    public SecsGemItem Item { get; }
    public ObservableCollection<ResponderFieldViewModel> Children { get; } = [];

    public bool IsList => Item.FormatType == SecsGemItemFormatType.List;
    public bool IsLeaf => !IsList;

    /// <summary>Every SECS/GEM format type, offered in the row's inline format dropdown.</summary>
    public IReadOnlyList<SecsGemItemFormatType> FormatChoices { get; } = Enum.GetValues<SecsGemItemFormatType>();

    /// <summary>
    /// The item's format. Two-way bound to the row's dropdown; a change raises
    /// <see cref="FormatChangeRequested"/> so the owner can swap the underlying item in place.
    /// </summary>
    [ObservableProperty]
    public partial SecsGemItemFormatType Format { get; set; }

    /// <summary>Raised when the user picks a different <see cref="Format"/> for this row.</summary>
    public event Action<ResponderFieldViewModel>? FormatChangeRequested;

    partial void OnFormatChanged(SecsGemItemFormatType value)
    {
        if (value != Item.FormatType)
            FormatChangeRequested?.Invoke(this);
    }

    /// <summary>Leaf value present in the template when the editor opened (to detect edited literals).</summary>
    public string InitialLeafValue { get; }

    public string FormatLabel => Item.FormatType.ToString();

    public string DisplayLabel
    {
        get
        {
            var descr = string.IsNullOrWhiteSpace(Item.Description) ? "" : $"  — {Item.Description}";
            if (IsList)
                return $"L ({Children.Count}){descr}";
            var values = string.Join(", ", Item.GetStringValues());
            return $"{Item.FormatType} = {values}{descr}";
        }
    }

    // --- Trigger mode ------------------------------------------------------
    [ObservableProperty]
    public partial ConditionKind Condition { get; set; } = ConditionKind.Any;

    [ObservableProperty]
    public partial string ConditionValue { get; set; } = string.Empty;

    // --- Response mode -----------------------------------------------------
    [ObservableProperty]
    public partial BindingSourceKind Source { get; set; } = BindingSourceKind.Literal;

    [ObservableProperty]
    public partial string StaticValue { get; set; } = string.Empty;

    /// <summary>Echo / CopyBranch — first dropdown: which received message to pull from.</summary>
    [ObservableProperty]
    public partial UpstreamMessageOption? SourceMessage { get; set; }

    /// <summary>Echo / CopyBranch — second dropdown: which parameter within <see cref="SourceMessage"/>.</summary>
    [ObservableProperty]
    public partial PathOption? SourceParameter { get; set; }

    /// <summary>Parameters available for the currently chosen <see cref="SourceMessage"/>.</summary>
    public ObservableCollection<PathOption> AvailableParameters { get; } = [];

    private readonly Func<string?, IReadOnlyList<PathOption>>? parameterLookup;

    public ResponderFieldViewModel(
        SecsGemItem item,
        string path,
        ResponderFieldMode mode,
        Func<string?, IReadOnlyList<PathOption>>? parameterLookup = null)
    {
        Item = item;
        Path = path;
        Mode = mode;
        Format = item.FormatType;
        this.parameterLookup = parameterLookup;

        InitialLeafValue = IsLeaf ? string.Join(",", item.GetStringValues()) : string.Empty;
        if (IsLeaf && mode == ResponderFieldMode.Response)
            StaticValue = InitialLeafValue;

        var index = 0;
        foreach (var child in item.Children.OfType<SecsGemItem>())
        {
            Children.Add(new ResponderFieldViewModel(child, SecsGemItemPath.Combine(path, index), mode, parameterLookup));
            index++;
        }
    }

    partial void OnSourceMessageChanged(UpstreamMessageOption? value)
    {
        AvailableParameters.Clear();
        if (parameterLookup is not null && value is not null)
        {
            foreach (var option in parameterLookup(value.NodeId))
                AvailableParameters.Add(option);
        }

        if (SourceParameter is not null && !AvailableParameters.Contains(SourceParameter))
            SourceParameter = null;
    }

    /// <summary>Produces a <see cref="MatchCondition"/> for this row, or null when set to "any".</summary>
    public MatchCondition? ToCondition() => Condition switch
    {
        ConditionKind.Equals => new MatchCondition { ItemPath = Path, Operator = MatchOperator.Equals, Value = ConditionValue },
        ConditionKind.NotEquals => new MatchCondition { ItemPath = Path, Operator = MatchOperator.NotEquals, Value = ConditionValue },
        _ => null
    };

    /// <summary>
    /// Produces a <see cref="ValueBinding"/> for this row, or null when it just keeps the template's
    /// own value (Literal source whose static value is unchanged).
    /// </summary>
    public ValueBinding? ToBinding() => Source switch
    {
        BindingSourceKind.Echo when SourceParameter is { } p =>
            new ValueBinding { TargetItemPath = Path, Source = BindingSourceKind.Echo, SourceRef = p.Path, SourceNodeId = p.SourceNodeId },
        BindingSourceKind.CopyBranch when SourceParameter is { } p =>
            new ValueBinding { TargetItemPath = Path, Source = BindingSourceKind.CopyBranch, SourceRef = p.Path, SourceNodeId = p.SourceNodeId },
        BindingSourceKind.Literal when IsLeaf && StaticValue != InitialLeafValue =>
            new ValueBinding { TargetItemPath = Path, Source = BindingSourceKind.Literal, SourceRef = StaticValue },
        _ => null
    };
}

/// <summary>First dropdown of a value binding: a received message on the path (or null for "the latest").</summary>
public sealed record UpstreamMessageOption(string? NodeId, string Label)
{
    public override string ToString() => Label;
}

/// <summary>
/// Second dropdown of a value binding: one item path inside a received message, plus a human label
/// and the id of the Receive node that captured it.
/// </summary>
public sealed record PathOption(string Path, string Label, string? SourceNodeId = null)
{
    public override string ToString() => Label;
}
