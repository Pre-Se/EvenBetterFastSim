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
    [NotifyCanExecuteChangedFor(nameof(RunScenarioCommand))]
    private bool isRunning;

    /// <summary>When on, the selected scenario restarts from Start every time it finishes.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunScenarioCommand))]
    private bool isLooping;

    public ICommand DisconnectConnectorCommand { get; }

    public ICommand DeleteSelectionCommand { get; }

    public ObservableCollection<SecsGemTransaction> AvailableTransactions => libraryManager.Library;

    private readonly Services.IWindowManager windowManager;
    private readonly Services.ViewModelLocator viewModelLocator;

    public ScenariosViewModel(
        ScenarioExecutionService scenarioExecutionService,
        ISecsGemLibraryManager libraryManager,
        DataMessageHandler dataMessageHandler,
        ILogService<LoggedString> logService,
        ILogger<ScenariosViewModel> logger,
        Services.IWindowManager windowManager,
        Services.ViewModelLocator viewModelLocator)
    {
        this.scenarioExecutionService = scenarioExecutionService;
        this.libraryManager = libraryManager;
        this.dataMessageHandler = dataMessageHandler;
        this.logService = logService;
        this.logger = logger;
        this.windowManager = windowManager;
        this.viewModelLocator = viewModelLocator;

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
        if (IsLooping)
            IsLooping = false; // don't keep looping a scenario the user navigated away from

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

    /// <summary>
    /// Overall per-run deadline in seconds; <c>0</c> = wait indefinitely (Receive nodes block until their
    /// message arrives or the run is cancelled). Also bounds each loop iteration.
    /// </summary>
    [ObservableProperty]
    private int runTimeoutSeconds = 30;

    private bool CanRunScenario() => !IsRunning && !IsLooping && SelectedScenario != null;

    /// <summary>Runs the selected scenario once.</summary>
    [RelayCommand(CanExecute = nameof(CanRunScenario))]
    private async Task RunScenario()
    {
        if (SelectedScenario is { } scenario)
            await RunOnceAsync(scenario, isLoopIteration: false);
    }

    /// <summary>One pass of a scenario from Start to End (or failure / cancellation).</summary>
    private async Task RunOnceAsync(
        ScenarioListItem scenario,
        bool isLoopIteration,
        HashSet<global::Logging.Interfaces.ILoggedDataMessage>? consumedAcrossRuns = null)
    {
        if (IsRunning) return;

        IsRunning = true;
        runCts = new CancellationTokenSource();

        try
        {
            var graph = BuildGraphFromCanvas();
            scenario.Graph = graph;

            if (!isLoopIteration)
                LogInfo($"Starting scenario '{scenario.Name}'...");

            TimeSpan? deadline = RunTimeoutSeconds > 0 ? TimeSpan.FromSeconds(RunTimeoutSeconds) : null;
            var result = await scenarioExecutionService.ExecuteAsync(graph, runCts.Token, consumedAcrossRuns, deadline);

            if (result.Success)
                LogInfo($"Scenario '{scenario.Name}' completed ({result.CompletedSteps} steps)");
            else
                LogError($"Scenario '{scenario.Name}' failed: {result.ErrorMessage}");
        }
        catch (OperationCanceledException)
        {
            LogInfo($"Scenario '{scenario.Name}' cancelled");
        }
        catch (Exception ex)
        {
            LogError($"Scenario '{scenario.Name}' errored: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            runCts = null;
        }
    }

    partial void OnIsLoopingChanged(bool value)
    {
        if (value)
            _ = RunLoopAsync();
        else
            runCts?.Cancel(); // stop the in-flight iteration promptly
    }

    private async Task RunLoopAsync()
    {
        if (SelectedScenario is not { } scenario)
        {
            IsLooping = false;
            return;
        }

        // One shared set for the whole loop session: a message the equipment sent once can satisfy
        // only one Receive across all iterations, so a single trigger doesn't re-fire every loop.
        var consumed = new HashSet<global::Logging.Interfaces.ILoggedDataMessage>(ReferenceEqualityComparer.Instance);

        LogInfo($"Loop started for '{scenario.Name}' — it will restart from Start after each finish.");
        while (IsLooping && ReferenceEquals(SelectedScenario, scenario))
        {
            if (consumed.Count > 1000) consumed.Clear(); // messages older than the ~5 s inbound buffer can't be replayed anyway
            await RunOnceAsync(scenario, isLoopIteration: true, consumed);
            if (!IsLooping) break;
            try { await Task.Delay(200); } catch { /* ignore */ }
        }
        LogInfo($"Loop stopped for '{scenario.Name}'.");
    }

    [RelayCommand]
    private void CancelRun()
    {
        if (IsLooping)
            IsLooping = false; // OnIsLoopingChanged cancels the current iteration
        else
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

    /// <summary>
    /// Opens the editor for a node: match conditions for a Receive node, response values for a
    /// Send node. Invoked by right-click / double-click / Enter on the canvas.
    /// </summary>
    [RelayCommand]
    private void OpenNodeEditor(ScenarioNodeViewModel? node)
    {
        switch (node?.Type)
        {
            case NodeType.Receive:
            {
                var vm = viewModelLocator.GetViewModel<Responders.NodeResponderViewModel>();
                vm.InitializeForReceive(node);
                windowManager.ShowDialog(vm);
                break;
            }
            case NodeType.Send or NodeType.SendAndWait:
            {
                var upstreams = BuildUpstreamReceives(node);
                var vm = viewModelLocator.GetViewModel<Responders.NodeResponderViewModel>();
                vm.InitializeForSend(node, upstreams);
                windowManager.ShowDialog(vm);
                break;
            }
        }
    }

    /// <summary>Opens the editor for the single selected node (bound to the Enter key on the canvas).</summary>
    [RelayCommand]
    private void EditSelectedNode() =>
        OpenNodeEditor(SelectedNodes.Count == 1 ? SelectedNodes[0] : null);

    /// <summary>
    /// Walks flow edges backwards from <paramref name="start"/> collecting every Receive node on the
    /// path (earliest first), each as a value source the response editor can pull parameters from.
    /// </summary>
    private IReadOnlyList<Responders.UpstreamReceive> BuildUpstreamReceives(ScenarioNodeViewModel start)
    {
        var receives = new List<ScenarioNodeViewModel>();
        var visited = new HashSet<string>();
        var current = start;

        while (current is not null && visited.Add(current.Id))
        {
            var input = current.Input.FirstOrDefault();
            if (input is null) break;
            var connection = Connections.FirstOrDefault(c => c.Target == input);
            if (connection?.Source is null) break;
            var previous = FindNodeForConnector(connection.Source);
            if (previous is null) break;
            if (previous.Type == NodeType.Receive)
                receives.Add(previous);
            current = previous;
        }

        receives.Reverse(); // chain order

        var result = new List<Responders.UpstreamReceive>();
        var nameCounts = new Dictionary<string, int>();
        foreach (var receive in receives)
        {
            if (receive.Transaction is null) continue;
            var source = receive.UseReplyMessage ? receive.Transaction.ReplyMessage : receive.Transaction.PrimaryMessage;

            nameCounts.TryGetValue(source.Name, out var seen);
            nameCounts[source.Name] = seen + 1;
            var label = seen == 0 ? source.Name : $"{source.Name} #{seen + 1}";

            result.Add(new Responders.UpstreamReceive(receive.Id, label, (SecsGemDataMessage)source.Clone()));
        }
        return result;
    }

    /// <summary>Drops a structural node (e.g. an AND join) at the given canvas point.</summary>
    public void AddStructuralNode(NodeType type, Point canvasPosition)
    {
        var node = new ScenarioNodeViewModel
        {
            Type = type,
            Location = canvasPosition
        };
        Nodes.Add(node);
        LogInfo($"Added {type} node to scenario");
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
        var requested = nodes.ToList();
        var blocked = 0;

        foreach (var node in requested)
        {
            if (!node.IsDeletable)
            {
                blocked++;
                continue;
            }

            RemoveConnections(Connections.Where(c =>
                NodeOwnsConnector(c.Source, node) || NodeOwnsConnector(c.Target, node)).ToList());
            Nodes.Remove(node);
        }

        if (blocked > 0 && blocked == requested.Count)
            LogInfo("Start and End nodes can't be deleted.");
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
