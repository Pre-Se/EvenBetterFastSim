using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Data_Containers.Serialization;
using SecsGemBaseItems.Responders;
using SecsGemScenarioEngine.Models;

namespace EvenBetterFastSim.WPF.ViewModels.Graph;

public partial class ScenarioNodeViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    [ObservableProperty]
    public partial NodeType Type { get; set; }
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;
    [ObservableProperty]
    public partial string? TransactionName { get; set; }

    [ObservableProperty]
    public partial string? DisplayName { get; set; }

    [ObservableProperty]
    private Point location;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
    [ObservableProperty]
    public partial bool UseReplyMessage { get; set; }

    /// <summary>Receive node: serialized <c>List&lt;MatchCondition&gt;</c> that gates the trigger.</summary>
    [ObservableProperty]
    public partial string? MatchConditionsJson { get; set; }

    /// <summary>Send node: serialized <c>List&lt;ValueBinding&gt;</c> filled from the upstream Receive.</summary>
    [ObservableProperty]
    public partial string? ResponseBindingsJson { get; set; }

    public SecsGemTransaction? Transaction { get; set; }

    public string? ConditionSummary
    {
        get
        {
            var conditions = ResponderJson.DeserializeConditions(MatchConditionsJson);
            return conditions.Count == 0 ? null
                : conditions.Count == 1 ? conditions[0].ToString()
                : $"{conditions[0]}  (+{conditions.Count - 1})";
        }
    }

    public string? BindingSummary
    {
        get
        {
            var count = ResponderJson.DeserializeBindings(ResponseBindingsJson).Count;
            return count == 0 ? null : $"↩ {count} bound";
        }
    }

    partial void OnMatchConditionsJsonChanged(string? value) => OnPropertyChanged(nameof(ConditionSummary));
    partial void OnResponseBindingsJsonChanged(string? value) => OnPropertyChanged(nameof(BindingSummary));

    /// <summary>Live state while a scenario runs (drives the node's highlight on the canvas).</summary>
    [ObservableProperty]
    public partial ScenarioNodeRunState RunState { get; set; }

    /// <summary>e.g. "waiting for S6F11" — shown on the node while it's the current/stuck step.</summary>
    [ObservableProperty]
    public partial string? RunDetail { get; set; }

    public bool CanToggleMode => Type is NodeType.Send or NodeType.SendAndWait or NodeType.Receive;

    /// <summary>Receive node — can carry match conditions on the incoming message.</summary>
    public bool IsReceiveNode => Type is NodeType.Receive;

    /// <summary>Send node — can carry value bindings for its outgoing message.</summary>
    public bool IsSendNode => Type is NodeType.Send or NodeType.SendAndWait;

    public string SendModeLabel => Type switch
    {
        NodeType.Send => "→ Send",
        NodeType.SendAndWait => "→ Send & Wait",
        NodeType.Receive => "← Receive",
        _ => ""
    };

    public ObservableCollection<ConnectorViewModel> Input { get; } = [];
    public ObservableCollection<ConnectorViewModel> Output { get; } = [];

    /// <summary>Start and End are structural — they can't be deleted from the canvas.</summary>
    public bool IsDeletable => Type is not (NodeType.Start or NodeType.End);

    public ScenarioNodeViewModel()
    {
        InitializeConnectors();
        // Type defaults to Start (enum 0), so OnTypeChanged never fires for a Start node —
        // set the title explicitly here or it renders with an empty header.
        UpdateTitleFromType();
    }

    partial void OnTypeChanged(NodeType value)
    {
        InitializeConnectors();
        UpdateTitleFromType();
        OnPropertyChanged(nameof(CanToggleMode));
        OnPropertyChanged(nameof(SendModeLabel));
        OnPropertyChanged(nameof(IsReceiveNode));
        OnPropertyChanged(nameof(IsSendNode));
        OnPropertyChanged(nameof(IsDeletable));
    }

    [RelayCommand]
    private void ToggleSendMode()
    {
        Type = Type switch
        {
            NodeType.Send or NodeType.SendAndWait => NodeType.Receive,
            NodeType.Receive => NodeType.SendAndWait,
            _ => Type
        };
    }

    partial void OnTransactionNameChanged(string? value)
    {
        UpdateTitleFromType();
    }

    partial void OnDisplayNameChanged(string? value)
    {
        UpdateTitleFromType();
    }

    private void InitializeConnectors()
    {
        string[] inputTitles = Type == NodeType.Start ? [] : ["In"];
        string[] outputTitles = Type switch
        {
            NodeType.Start       => ["Out"],
            NodeType.End         => [],
            NodeType.Send        => ["Success", "Failure"],
            NodeType.SendAndWait => ["Success", "Failure"],
            NodeType.Receive     => ["Success", "Failure"],
            NodeType.Condition   => ["YES", "NO"],
            NodeType.And         => ["Out"],
            _                    => ["Done"],
        };

        ApplyConnectors(Input, inputTitles);
        ApplyConnectors(Output, outputTitles);
    }

    private static void ApplyConnectors(ObservableCollection<ConnectorViewModel> connectors, string[] titles)
    {
        if (connectors.Count == titles.Length)
        {
            for (var i = 0; i < titles.Length; i++)
                connectors[i].Title = titles[i];
        }
        else
        {
            connectors.Clear();
            foreach (var title in titles)
                connectors.Add(new ConnectorViewModel { Title = title });
        }
    }

    private void UpdateTitleFromType()
    {
        var label = DisplayName ?? TransactionName;
        Title = Type switch
        {
            NodeType.Start => "START",
            NodeType.End => "END",
            NodeType.SendAndWait => label ?? "Send & Wait",
            NodeType.Send => label ?? "Send",
            NodeType.Condition => label ?? "If",
            NodeType.Wait => label ?? "Wait",
            NodeType.Receive => label ?? "Receive",
            NodeType.And => "AND",
            _ => label ?? Type.ToString()
        };
    }

    public ScenarioNode ToModel()
    {
        return new ScenarioNode
        {
            Id = Id,
            Type = Type,
            TransactionName = TransactionName,
            DisplayName = DisplayName,
            TransactionJson = Transaction != null
                ? SecsGemTransactionJsonConverter.Serialize(Transaction)
                : null,
            UseReplyMessage = UseReplyMessage,
            MatchConditionsJson = MatchConditionsJson,
            ResponseBindingsJson = ResponseBindingsJson,
            X = Location.X,
            Y = Location.Y
        };
    }

    public static ScenarioNodeViewModel FromModel(ScenarioNode node)
    {
        var vm = new ScenarioNodeViewModel
        {
            Id = node.Id,
            Type = node.Type,
            TransactionName = node.TransactionName,
            DisplayName = node.DisplayName,
            UseReplyMessage = node.UseReplyMessage,
            MatchConditionsJson = node.MatchConditionsJson,
            ResponseBindingsJson = node.ResponseBindingsJson,
            Location = new Point(node.X, node.Y)
        };

        if (!string.IsNullOrWhiteSpace(node.TransactionJson))
            vm.Transaction = SecsGemTransactionJsonConverter.Deserialize(node.TransactionJson);

        return vm;
    }
}
