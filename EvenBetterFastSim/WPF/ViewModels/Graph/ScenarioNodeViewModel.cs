using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Data_Containers.Serialization;
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

    public SecsGemTransaction? Transaction { get; set; }

    public bool CanToggleMode => Type is NodeType.Send or NodeType.SendAndWait or NodeType.Receive;

    public string SendModeLabel => Type switch
    {
        NodeType.Send => "→ Send",
        NodeType.SendAndWait => "→ Send & Wait",
        NodeType.Receive => "← Receive",
        _ => ""
    };

    public ObservableCollection<ConnectorViewModel> Input { get; } = [];
    public ObservableCollection<ConnectorViewModel> Output { get; } = [];

    public ScenarioNodeViewModel()
    {
        InitializeConnectors();
    }

    partial void OnTypeChanged(NodeType value)
    {
        InitializeConnectors();
        UpdateTitleFromType();
        OnPropertyChanged(nameof(CanToggleMode));
        OnPropertyChanged(nameof(SendModeLabel));
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
            Location = new Point(node.X, node.Y)
        };

        if (!string.IsNullOrWhiteSpace(node.TransactionJson))
            vm.Transaction = SecsGemTransactionJsonConverter.Deserialize(node.TransactionJson);

        return vm;
    }
}
