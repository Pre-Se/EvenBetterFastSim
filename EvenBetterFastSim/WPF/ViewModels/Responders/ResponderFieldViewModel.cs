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

    /// <summary>Value used for this leaf when the editor's "Test" builds a sample incoming message.</summary>
    [ObservableProperty]
    public partial string SampleValue { get; set; } = string.Empty;

    // --- Response mode -----------------------------------------------------
    [ObservableProperty]
    public partial BindingSourceKind Source { get; set; } = BindingSourceKind.Literal;

    [ObservableProperty]
    public partial string StaticValue { get; set; } = string.Empty;

    /// <summary>Chosen incoming path for Echo / CopyBranch (one of <see cref="NodeResponderViewModel.IncomingPaths"/>).</summary>
    [ObservableProperty]
    public partial PathOption? IncomingPath { get; set; }

    public ResponderFieldViewModel(SecsGemItem item, string path, ResponderFieldMode mode)
    {
        Item = item;
        Path = path;
        Mode = mode;

        InitialLeafValue = IsLeaf ? string.Join(",", item.GetStringValues()) : string.Empty;
        if (IsLeaf)
        {
            if (mode == ResponderFieldMode.Response)
                StaticValue = InitialLeafValue;
            else
                SampleValue = InitialLeafValue;
        }

        var index = 0;
        foreach (var child in item.Children.OfType<SecsGemItem>())
        {
            Children.Add(new ResponderFieldViewModel(child, SecsGemItemPath.Combine(path, index), mode));
            index++;
        }
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
        BindingSourceKind.Echo when IncomingPath is { } p =>
            new ValueBinding { TargetItemPath = Path, Source = BindingSourceKind.Echo, SourceRef = p.Path },
        BindingSourceKind.CopyBranch when IncomingPath is { } p =>
            new ValueBinding { TargetItemPath = Path, Source = BindingSourceKind.CopyBranch, SourceRef = p.Path },
        BindingSourceKind.Literal when IsLeaf && StaticValue != InitialLeafValue =>
            new ValueBinding { TargetItemPath = Path, Source = BindingSourceKind.Literal, SourceRef = StaticValue },
        _ => null
    };
}

/// <summary>An incoming-message item path plus a human label, offered in Echo/CopyBranch pickers.</summary>
public sealed record PathOption(string Path, string Label)
{
    public override string ToString() => Label;
}
