using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using MessagePack;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvenBetterFastSim.Logging;
using EvenBetterFastSim.Logging.Interfaces;
using EvenBetterFastSim.WPF.ViewModels.Graph;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Nodify.Interactivity;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.LibraryManager;
using SecsGemMessageHandling.Data_Handling;
using SecsGemScenarioEngine.Models;
using SecsGemScenarioEngine.Services;

namespace EvenBetterFastSim.WPF.ViewModels;

public partial class ScenarioListItem : ObservableObject
{
    [ObservableProperty]
    private string name = "New Scenario";
    public ScenarioGraph Graph { get; set; } = new();
    public string? FilePath { get; set; }

    public ObservableCollection<ScenarioNodeViewModel>? CachedNodes { get; set; }
    public ObservableCollection<ConnectionViewModel>? CachedConnections { get; set; }
}

public partial class ScenariosViewModel : ObservableObject
{
    // Redirected per InstanceContext so each launched instance keeps its own scenario index.
    private static string ScenariosIndexPath =>
        Path.Combine(Services.InstanceContext.SettingsDirectory, "scenarios.json");

    private readonly ScenarioExecutionService scenarioExecutionService;
    private readonly ISecsGemLibraryManager libraryManager;
    private readonly DataMessageHandler dataMessageHandler;
    private readonly ILogService<LoggedString> logService;
    private readonly ILogger<ScenariosViewModel> logger;
    private CancellationTokenSource? runCts;

    [ObservableProperty]
    private ObservableCollection<ScenarioListItem> scenarios = [];

    [ObservableProperty]
    private ScenarioListItem? selectedScenario;

    [ObservableProperty]
    private ObservableCollection<ScenarioNodeViewModel> nodes = [];

    [ObservableProperty]
    private ObservableCollection<ConnectionViewModel> connections = [];

    [ObservableProperty]
    private ObservableCollection<ConnectionViewModel> selectedConnections = [];

    [ObservableProperty]
    private ObservableCollection<ScenarioNodeViewModel> selectedNodes = [];

    [ObservableProperty]
    private PendingConnectionViewModel pendingConnection = new();

    [ObservableProperty]
    private bool isRunning;

    public ICommand DisconnectConnectorCommand { get; }

    public ICommand DeleteSelectionCommand { get; }

    public ObservableCollection<SecsGemTransaction> AvailableTransactions => libraryManager.Library;

    public ScenariosViewModel(
        ScenarioExecutionService scenarioExecutionService,
        ISecsGemLibraryManager libraryManager,
        DataMessageHandler dataMessageHandler,
        ILogService<LoggedString> logService,
        ILogger<ScenariosViewModel> logger)
    {
        this.scenarioExecutionService = scenarioExecutionService;
        this.libraryManager = libraryManager;
        this.dataMessageHandler = dataMessageHandler;
        this.logService = logService;
        this.logger = logger;

        // Delete key on a focused connector would otherwise call RemoveConnections() and delete
        // all connections from that connector. We handle Delete via InputBindings on NodifyEditor.
        EditorGestures.Mappings.Connector.Disconnect.Value =
            new System.Windows.Input.MouseGesture(System.Windows.Input.MouseAction.LeftClick,
                                                   System.Windows.Input.ModifierKeys.Alt);

        PendingConnection.ConnectionCompleted += OnConnectionCompleted;

        DisconnectConnectorCommand = new RelayCommand<ConnectorViewModel?>(connector =>
        {
            if (connector == null) return;
            RemoveConnections(Connections.Where(c => c.Source == connector || c.Target == connector).ToList());
        });

        DeleteSelectionCommand = new RelayCommand(() =>
        {
            if (SelectedConnections.Count > 0)
                DeleteConnections(SelectedConnections);
            if (SelectedNodes.Count > 0)
                DeleteNodes(SelectedNodes);
        });

        LoadFromJson();
    }

    private void LogInfo(string message) =>
        logService.LogMessages.Add(new LoggedString { Level = LogLevel.Information, Message = message });

    private void LogError(string message) =>
        logService.LogMessages.Add(new LoggedString { Level = LogLevel.Error, Message = message });

    partial void OnSelectedScenarioChanged(ScenarioListItem? value)
    {
        if (value != null)
        {
            if (value.CachedNodes != null && value.CachedConnections != null)
            {
                SelectedNodes.Clear();
                SelectedConnections.Clear();
                Nodes = value.CachedNodes;
                Connections = value.CachedConnections;
            }
            else
            {
                LoadGraphToCanvas(value.Graph, value);
            }
        }
        else
        {
            ClearCanvas();
        }

        ExportScenarioCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void NewScenario()
    {
        var scenario = new ScenarioListItem
        {
            Name = $"Scenario {Scenarios.Count + 1}",
            Graph = CreateEmptyGraph()
        };
        Scenarios.Add(scenario);
        SelectedScenario = scenario;
    }

    [RelayCommand]
    private void DeleteScenario()
    {
        if (SelectedScenario == null) return;
        var idx = Scenarios.IndexOf(SelectedScenario);
        Scenarios.Remove(SelectedScenario);
        SelectedScenario = Scenarios.Count > 0
            ? Scenarios[Math.Min(idx, Scenarios.Count - 1)]
            : null;
        SaveIndex();
    }

    [RelayCommand]
    private void RenameScenario()
    {
        if (SelectedScenario == null) return;
        var newName = Microsoft.VisualBasic.Interaction.InputBox(
            "Enter a new name for the scenario:",
            "Rename Scenario",
            SelectedScenario.Name);
        if (!string.IsNullOrWhiteSpace(newName))
            SelectedScenario.Name = newName;
    }

    [RelayCommand]
    private async Task RunScenario()
    {
        if (IsRunning || SelectedScenario == null) return;

        IsRunning = true;
        runCts = new CancellationTokenSource();

        try
        {
            var graph = BuildGraphFromCanvas();
            SelectedScenario.Graph = graph;

            LogInfo($"Starting scenario '{SelectedScenario.Name}'...");
            var result = await scenarioExecutionService.ExecuteAsync(graph, runCts.Token);

            if (result.Success)
                LogInfo($"Scenario '{SelectedScenario.Name}' completed ({result.CompletedSteps} steps)");
            else
                LogError($"Scenario '{SelectedScenario.Name}' failed: {result.ErrorMessage}");
        }
        catch (OperationCanceledException)
        {
            LogInfo($"Scenario '{SelectedScenario.Name}' cancelled");
        }
        finally
        {
            IsRunning = false;
            runCts = null;
        }
    }

    [RelayCommand]
    private void CancelRun()
    {
        runCts?.Cancel();
    }

    public void Save()
    {
        if (SelectedScenario == null) return;

        var graph = BuildGraphFromCanvas();
        graph.Name = SelectedScenario.Name;
        SelectedScenario.Graph = graph;

        if (string.IsNullOrEmpty(SelectedScenario.FilePath))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Scenario",
                Filter = "MessagePack files (*.msgpack)|*.msgpack|JSON files (*.json)|*.json",
                FileName = SelectedScenario.Name
            };
            if (dialog.ShowDialog() != true) return;
            SelectedScenario.FilePath = dialog.FileName;
        }

        SaveScenarioToFile(SelectedScenario);
        SaveIndex();
    }

    public void SaveAll()
    {
        if (SelectedScenario != null)
        {
            var graph = BuildGraphFromCanvas();
            graph.Name = SelectedScenario.Name;
            SelectedScenario.Graph = graph;
        }

        foreach (var scenario in Scenarios.Where(s => s != SelectedScenario && s.CachedNodes != null))
        {
            var graph = BuildGraphFromCanvas(scenario.CachedNodes!, scenario.CachedConnections!);
            graph.Name = scenario.Name;
            scenario.Graph = graph;
        }

        foreach (var scenario in Scenarios.Where(s => !string.IsNullOrEmpty(s.FilePath)))
            SaveScenarioToFile(scenario);

        SaveIndex();
    }

    [RelayCommand]
    private void SaveScenarios()
    {
        Save();
        if (SelectedScenario != null)
            LogInfo($"Saved '{SelectedScenario.Name}'");
    }

    [RelayCommand]
    private void LoadScenarios()
    {
        LoadFromJson();
        LogInfo("Scenarios loaded");
    }

    [RelayCommand(CanExecute = nameof(CanExportImportScenario))]
    private void ExportScenario()
    {
        if (SelectedScenario == null) return;

        var graph = BuildGraphFromCanvas();
        graph.Name = SelectedScenario.Name;
        SelectedScenario.Graph = graph;

        var dialog = new SaveFileDialog
        {
            Title = "Export Scenario",
            Filter = "MessagePack files (*.msgpack)|*.msgpack|JSON files (*.json)|*.json",
            FileName = SelectedScenario.Name
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var path = dialog.FileName;
            if (Path.GetExtension(path).Equals(".msgpack", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = MessagePackSerializer.Serialize(ScenarioGraphDto.From(graph));
                File.WriteAllBytes(path, bytes);
            }
            else
            {
                var json = JsonSerializer.Serialize(graph, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            LogInfo($"Exported '{SelectedScenario.Name}' to {Path.GetFileName(path)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export scenario");
            LogError($"Export failed: {ex.Message}");
        }
    }

    [RelayCommand]
    private void ImportScenario()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import Scenario(s)",
            Multiselect = true,
            Filter = "MessagePack files (*.msgpack)|*.msgpack|JSON files (*.json)|*.json|All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        ScenarioListItem? last = null;
        foreach (var path in dialog.FileNames)
        {
            try
            {
                var graph = LoadGraphFromFile(path);
                if (graph == null)
                {
                    LogError($"Import failed for '{Path.GetFileName(path)}': file is empty or invalid");
                    continue;
                }

                var item = new ScenarioListItem { Name = graph.Name, Graph = graph, FilePath = path };
                Scenarios.Add(item);
                last = item;
                LogInfo($"Imported '{graph.Name}' from {Path.GetFileName(path)}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to import scenario from {Path}", path);
                LogError($"Import failed for '{Path.GetFileName(path)}': {ex.Message}");
            }
        }

        if (last != null)
        {
            SelectedScenario = last;
            SaveIndex();
        }
    }

    private bool CanExportImportScenario() => SelectedScenario != null;

    [RelayCommand]
    private void AddNodeToCanvas(object? parameter)
    {
        if (parameter is not SecsGemTransaction transaction) return;

        var nodeType = transaction.PrimaryMessage.Reply ? NodeType.SendAndWait : NodeType.Send;
        var node = new ScenarioNodeViewModel
        {
            Type = nodeType,
            Transaction = transaction.Clone(),
            TransactionName = transaction.PrimaryMessage.Name,
            DisplayName = transaction.Name,
            Location = new Point(200 + Nodes.Count * 180, 200)
        };

        Nodes.Add(node);
        LogInfo($"Added '{transaction.Name}' to scenario");
    }

    public void AddNodeFromDrop(SecsGemTransaction transaction, Point canvasPosition, bool isPrimary = true)
    {
        var nodeType = isPrimary
            ? (transaction.PrimaryMessage.Reply ? NodeType.SendAndWait : NodeType.Send)
            : NodeType.Receive;

        var displayName = isPrimary ? transaction.Name : transaction.ReplyMessage.Name;

        var primaryNode = new ScenarioNodeViewModel
        {
            Type = nodeType,
            Transaction = transaction.Clone(),
            TransactionName = transaction.PrimaryMessage.Name,
            DisplayName = displayName,
            UseReplyMessage = !isPrimary,
            Location = canvasPosition
        };

        Nodes.Add(primaryNode);

        if (isPrimary && transaction.PrimaryMessage.Reply)
        {
            var replyNode = new ScenarioNodeViewModel
            {
                Type = NodeType.Receive,
                Transaction = transaction.Clone(),
                TransactionName = transaction.PrimaryMessage.Name,
                DisplayName = transaction.ReplyMessage.Name,
                UseReplyMessage = true,
                Location = new Point(canvasPosition.X + 220, canvasPosition.Y)
            };

            Nodes.Add(replyNode);

            var successOut = primaryNode.Output.FirstOrDefault();
            var input = replyNode.Input.FirstOrDefault();

            if (successOut != null && input != null)
            {
                Connections.Add(new ConnectionViewModel { Source = successOut, Target = input });
                successOut.IsConnected = true;
                input.IsConnected = true;
            }

            LogInfo($"Added '{displayName}' + reply '{transaction.ReplyMessage.Name}' to scenario");
        }
        else
        {
            LogInfo($"Added '{displayName}' ({(isPrimary ? "primary" : "secondary")}) at ({canvasPosition.X:F0},{canvasPosition.Y:F0})");
        }
    }

    [RelayCommand]
    private void DeleteSelectedNode()
    {
        if (Nodes.Count == 0) return;
        DeleteNodes(new[] { Nodes[^1] });
    }

    public void DeleteNodes(IEnumerable<ScenarioNodeViewModel> nodes)
    {
        foreach (var node in nodes.ToList())
        {
            if (node.Type is NodeType.Start or NodeType.End) continue;

            RemoveConnections(Connections.Where(c =>
                NodeOwnsConnector(c.Source, node) || NodeOwnsConnector(c.Target, node)).ToList());
            Nodes.Remove(node);
        }
    }

    public void DeleteConnections(IEnumerable<ConnectionViewModel> connections) =>
        RemoveConnections(connections);

    [RelayCommand]
    private void DeleteConnection(ConnectionViewModel? connection)
    {
        if (connection == null) return;
        RemoveConnection(connection);
    }

    private void RemoveConnections(IEnumerable<ConnectionViewModel> conns)
    {
        var removed = conns.ToList();
        var affected = removed
            .SelectMany(c => new[] { c.Source, c.Target })
            .OfType<ConnectorViewModel>()
            .ToHashSet();

        foreach (var conn in removed)
            Connections.Remove(conn);

        var stillConnected = Connections
            .SelectMany(c => new[] { c.Source, c.Target })
            .OfType<ConnectorViewModel>()
            .ToHashSet();

        foreach (var connector in affected)
            connector.IsConnected = stillConnected.Contains(connector);
    }

    private void RemoveConnection(ConnectionViewModel conn) => RemoveConnections([conn]);

    private bool NodeOwnsConnector(ConnectorViewModel? connector, ScenarioNodeViewModel node)
    {
        if (connector == null) return false;
        return node.Input.Contains(connector) || node.Output.Contains(connector);
    }

    private void OnConnectionCompleted(ConnectorViewModel source, ConnectorViewModel target)
    {
        if (ConnectionExists(source, target)) return;

        var connection = new ConnectionViewModel { Source = source, Target = target };
        Connections.Add(connection);
        source.IsConnected = true;
        target.IsConnected = true;
    }

    private bool ConnectionExists(ConnectorViewModel source, ConnectorViewModel target)
    {
        return Connections.Any(c =>
            (c.Source == source && c.Target == target) ||
            (c.Source == target && c.Target == source));
    }

    private ScenarioGraph CreateEmptyGraph()
    {
        var startId = Guid.NewGuid().ToString("N")[..8];
        var endId = Guid.NewGuid().ToString("N")[..8];

        return new ScenarioGraph
        {
            Name = "New Scenario",
            Nodes =
            [
                new ScenarioNode { Id = startId, Type = NodeType.Start, X = 100, Y = 200 },
                new ScenarioNode { Id = endId, Type = NodeType.End, X = 800, Y = 200 }
            ],
            Edges = [] // No default edge — user connects nodes manually
        };
    }

    private void LoadGraphToCanvas(ScenarioGraph graph, ScenarioListItem? cacheTarget = null)
    {
        SelectedNodes.Clear();
        SelectedConnections.Clear();

        var nodeMap = new Dictionary<string, ScenarioNodeViewModel>(graph.Nodes.Count);
        var newNodes = new ObservableCollection<ScenarioNodeViewModel>();

        foreach (var modelNode in graph.Nodes)
        {
            var vmNode = ScenarioNodeViewModel.FromModel(modelNode);
            nodeMap[modelNode.Id] = vmNode;
            newNodes.Add(vmNode);
        }

        var newConnections = new ObservableCollection<ConnectionViewModel>();
        foreach (var edge in graph.Edges)
        {
            if (!nodeMap.TryGetValue(edge.SourceNodeId, out var sourceNode)) continue;
            if (!nodeMap.TryGetValue(edge.TargetNodeId, out var targetNode)) continue;

            var sourceConn = edge.IsFailurePath
                ? sourceNode.Output.ElementAtOrDefault(1)
                : sourceNode.Output.FirstOrDefault();
            var targetConn = targetNode.Input.FirstOrDefault();

            if (sourceConn != null && targetConn != null)
            {
                newConnections.Add(new ConnectionViewModel { Source = sourceConn, Target = targetConn });
                sourceConn.IsConnected = true;
                targetConn.IsConnected = true;
            }
        }

        Nodes = newNodes;
        Connections = newConnections;

        foreach (var modelNode in graph.Nodes)
            modelNode.TransactionJson = null;

        if (cacheTarget != null)
        {
            cacheTarget.CachedNodes = newNodes;
            cacheTarget.CachedConnections = newConnections;
        }
    }

    private ScenarioGraph BuildGraphFromCanvas()
    {
        return BuildGraphFromCanvas(Nodes, Connections);
    }

    private static ScenarioGraph BuildGraphFromCanvas(
        ObservableCollection<ScenarioNodeViewModel> nodes,
        ObservableCollection<ConnectionViewModel> connections)
    {
        var graph = new ScenarioGraph
        {
            Description = ""
        };

        var nodeIdMap = new Dictionary<ScenarioNodeViewModel, string>();

        foreach (var vmNode in nodes)
        {
            var modelNode = vmNode.ToModel();
            nodeIdMap[vmNode] = modelNode.Id;
            graph.Nodes.Add(modelNode);
        }

        foreach (var conn in connections)
        {
            var sourceNode = FindNodeForConnector(conn.Source, nodes);
            var targetNode = FindNodeForConnector(conn.Target, nodes);

            if (sourceNode != null && targetNode != null &&
                nodeIdMap.TryGetValue(sourceNode, out var sourceId) &&
                nodeIdMap.TryGetValue(targetNode, out var targetId))
            {
                bool isFailurePath = sourceNode.Output.Count > 1 &&
                                     sourceNode.Output[1] == conn.Source;
                graph.Edges.Add(new ScenarioEdge
                {
                    SourceNodeId = sourceId,
                    TargetNodeId = targetId,
                    IsFailurePath = isFailurePath
                });
            }
        }

        graph.Name = graph.Nodes.Count > 0 ? "Scenario" : "Unnamed";
        return graph;
    }

    private static ScenarioNodeViewModel? FindNodeForConnector(
        ConnectorViewModel? connector,
        ObservableCollection<ScenarioNodeViewModel> nodes)
    {
        if (connector == null) return null;
        return nodes.FirstOrDefault(n =>
            n.Input.Contains(connector) || n.Output.Contains(connector));
    }

    private ScenarioNodeViewModel? FindNodeForConnector(ConnectorViewModel? connector)
    {
        return FindNodeForConnector(connector, Nodes);
    }

    private void ClearCanvas()
    {
        SelectedNodes.Clear();
        SelectedConnections.Clear();
        Nodes.Clear();
        Connections.Clear();
    }

    private void SaveScenarioToFile(ScenarioListItem scenario)
    {
        if (string.IsNullOrEmpty(scenario.FilePath)) return;
        try
        {
            var ext = Path.GetExtension(scenario.FilePath);
            if (ext.Equals(".msgpack", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = MessagePackSerializer.Serialize(ScenarioGraphDto.From(scenario.Graph));
                File.WriteAllBytes(scenario.FilePath, bytes);
            }
            else
            {
                var json = JsonSerializer.Serialize(scenario.Graph, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(scenario.FilePath, json);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save scenario '{Name}' to '{Path}'", scenario.Name, scenario.FilePath);
        }
    }

    private void SaveIndex()
    {
        try
        {
            var paths = Scenarios
                .Where(s => !string.IsNullOrEmpty(s.FilePath))
                .Select(s => s.FilePath!)
                .ToList();
            var json = JsonSerializer.Serialize(paths, new JsonSerializerOptions { WriteIndented = true });
            Directory.CreateDirectory(Path.GetDirectoryName(ScenariosIndexPath)!);
            File.WriteAllText(ScenariosIndexPath, json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save scenario index");
        }
    }

    private static ScenarioGraph? LoadGraphFromFile(string path)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".msgpack", StringComparison.OrdinalIgnoreCase))
        {
            var bytes = File.ReadAllBytes(path);
            return MessagePackSerializer.Deserialize<ScenarioGraphDto>(bytes).ToModel();
        }
        else
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ScenarioGraph>(json);
        }
    }

    private void LoadFromJson()
    {
        try
        {
            if (!File.Exists(ScenariosIndexPath)) return;

            var json = File.ReadAllText(ScenariosIndexPath);
            var paths = JsonSerializer.Deserialize<List<string>>(json);

            Scenarios.Clear();
            if (paths != null)
            {
                foreach (var path in paths)
                {
                    try
                    {
                        var graph = LoadGraphFromFile(path);
                        if (graph != null)
                            Scenarios.Add(new ScenarioListItem { Name = graph.Name, Graph = graph, FilePath = path });
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to load scenario from '{Path}'", path);
                    }
                }
            }

            SelectedScenario = Scenarios.FirstOrDefault();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load scenario index");
        }
    }
}
