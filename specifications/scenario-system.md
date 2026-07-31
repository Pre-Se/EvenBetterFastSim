# Scenario System — Architecture & Reference

> Covers 20 files / ~3000 lines across EvenBetterFastSim and SecsGemBase.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Node Types & Pipeline](#node-types--pipeline)
- [Canvas Selection & Deletion](#canvas-selection--deletion)
- [Transaction Deep-Copy (JSON)](#transaction-deep-copy-json)
- [Message Comparison (Receive nodes)](#message-comparison-receive-nodes)
- [Execution Engine](#execution-engine)
- [Drag & Drop](#drag--drop)
- [Persistence (scenarios.json)](#persistence-scenariosjson)
- [Complete File Index](#complete-file-index)

---

## Overview

The scenario system lets users build a visual node graph representing a SECS/GEM message exchange sequence. Nodes (Send, Receive, Wait, Condition, etc.) are connected by edges. The graph is executed sequentially, dispatching real SECS/GEM messages over HSMS.

**Technology:** WPF + Nodify (v7.3.0) for the canvas, CommunityToolkit.MVVM for ViewModels, System.Text.Json for serialization, SecsGemMessageHandling NuGet for HSMS communication.

---

## Architecture

```
┌─ EvenBetterFastSim ─────────────────────────────────────┐
│  MainWindow.xaml          MainWindow.xaml.cs              │
│  ├─ NodifyEditor canvas   ├─ Drag-drop from library tree   │
│  │  (nodes, connections,  └─ Viewport transform handling   │
│  │   selection, keybinds)                                  │
│  └─ Transaction tree (drag source)                        │
│                                                           │
│  ScenariosViewModel (central orchestrator)                │
│  ├─ Nodes / Connections / SelectedNodes / SelectedConn.   │
│  ├─ AddNodeFromDrop / AddNodeToCanvas                     │
│  ├─ DeleteConnections / DeleteNodes / DeleteSelectionCmd  │
│  ├─ BuildGraphFromCanvas / LoadGraphToCanvas              │
│  ├─ SaveToJson / LoadFromJson                             │
│  └─ RunScenario → ScenarioExecutionService                │
│                                                           │
│  Graph ViewModels                                         │
│  ├─ ScenarioNodeViewModel (type, connectors, toggle)      │
│  ├─ ConnectionViewModel (source, target, IsSelected)      │
│  ├─ ConnectorViewModel (anchor, IsConnected)             │
│  └─ PendingConnectionViewModel (drag-to-connect)          │
└──────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─ SecsGemBase ────────────────────────────────────────────┐
│  Models                                                   │
│  ├─ ScenarioNode (Id, Type, TransactionName/Json, X, Y)   │
│  ├─ ScenarioGraph (Name, Nodes[], Edges[])                │
│  ├─ ScenarioEdge (SourceNodeId, TargetNodeId)             │
│  └─ NodeType enum                                         │
│                                                           │
│  ScenarioExecutionService                                 │
│  ├─ ExecuteAsync → DFS traversal → ExecuteNodeAsync       │
│  ├─ ExecuteSendAsync (sends via DataMessageHandler)       │
│  ├─ ExecuteWaitAsync (Task.Delay)                         │
│  └─ ExecuteReceiveAsync (waits for matching inbound msg)  │
│                                                           │
│  Data Containers                                          │
│  ├─ SecsGemTransaction (PrimaryMessage, ReplyMessage)     │
│  ├─ SecsGemDataMessage (Stream, Function, Children)       │
│  ├─ SecsGemItem (FormatType, Values, Children)            │
│  └─ TransactionJsonConverter (JSON serialize/deserialize) │
│                                                           │
│  DataMessageHandler                                       │
│  ├─ SendDataMessage (for Send nodes)                      │
│  ├─ CanSendMessage (pre-send gate check)                  │
│  └─ WaitForReceivedMessage (for Receive nodes)            │
└──────────────────────────────────────────────────────────┘
```

### DI Wiring

```
App.xaml.cs:ConfigureServices()
  services.AddScoped<ScenariosViewModel>()
  services.AddScoped<ScenarioExecutionService>()
  ↓
MainViewModel(ScenariosViewModel scenariosVm)
  public ScenariosViewModel ScenariosVm { get; }
  ↓
MainWindow.DataContext = MainViewModel
  XAML bindings → {Binding ScenariosVm.Nodes}, etc.
```

---

## Node Types & Pipeline

### Enum `NodeType` (SecsGemScenarioEngine/Models/NodeType.cs)

| Value | Behavior | Connectors |
|-------|----------|------------|
| `Start` | Entry point. Skips during execution. | Output: {Out} |
| `SendAndWait` | Sends PrimaryMessage, waits for reply. On success → next node. | Input: {In}, Output: {Reply} |
| `Send` | Same as SendAndWait (sending always waits for reply/Hsms confirm). Legacy. | Input: {In}, Output: {Done} |
| `Receive` | Waits for equipment to send a message matching the stored transaction clone. On match → next node. | Input: {In}, Output: {Out} |
| `Wait` | Pauses for N milliseconds (TransactionName stores the delay). | Input: {In}, Output: {Out} |
| `End` | Termination. Skips. | Input: {In} |
| `Condition` | Branch point. (Not yet implemented in execution engine.) | Input: {In}, Output: {YES}, {NO} |

### Mode toggle button

Each Send/SendAndWait/Receive node has a button (`← Receive` / `→ Send & Wait`) that cycles:
- `Send` / `SendAndWait` → `Receive`
- `Receive` → `SendAndWait`

Implemented in `ScenarioNodeViewModel.cs:ToggleSendMode()`. Trigger updates connectors and title via `OnTypeChanged`.

### Execution pipeline

```
Start → [Send/SendAndWait] → [Receive] → [Wait] → ... → End
         │                    │            │
         ├─ Sends message     ├─ Waits for │
         │  waits for reply   │  matching  ├─ Delays N ms
         │  on reply → next   │  msg → next│  → next
```

DFS from Start node follows edges to build linear execution order. Branches (Condition) are not yet handled.

---

## Canvas Selection & Deletion

### How selection works (Nodify pattern)

**Connections:**
1. `BaseConnection` style in `NodifyEditor.Resources` sets `IsSelectable="True"` and `IsSelected="{Binding IsSelected}"`
2. Left-click → Nodify's `ConnectionContainer.OnMouseDown` → `ConnectionsMultiSelector.Select()` → `SelectedConnections` DP
3. `NodifyEditor.SelectedConnections` is bound to `ScenariosViewModel.SelectedConnections` (ObservableCollection)
4. Both `SelectedConnections` and `SelectedItems` DPs on `NodifyEditor` **default to null** — must be bound to a collection (matching Playground pattern)

**Nodes:**
1. `ItemContainer` style sets `IsSelected="{Binding IsSelected}"` (two-way to `ScenarioNodeViewModel.IsSelected`)
2. `NodifyEditor.SelectedItems` bound to `ScenariosViewModel.SelectedNodes`

### Visual feedback
- Selected connections: stroke changes to accent color, thickness increases to 3 (via `BaseConnection` style trigger)
- Selected nodes: Nodify's default `ItemContainer` selection border

### Deletion

| Method | Mechanism |
|--------|-----------|
| Delete key | `KeyBinding Key="Delete"` on NodifyEditor → `DeleteSelectionCommand` → `DeleteConnections(SelectedConnections)` + `DeleteNodes(SelectedNodes)` |
| Right-click connection → Delete | Context menu on `LineConnection` bound to `DeleteConnectionCommand` with `ConnectionViewModel` as parameter |
| Right-click connector → Disconnect | Context menu on `NodeInput`/`NodeOutput` bound to `DisconnectConnectorCommand` with `ConnectorViewModel` as parameter |

### Key files
- `MainWindow.xaml:525-540` — `BaseConnection` style + `ItemContainer` style with `BasedOn`
- `MainWindow.xaml:513-519` — NodifyEditor bindings (`SelectedItems`, `SelectedConnections`)
- `MainWindow.xaml:527-529` — `KeyBinding` for Delete
- `ConnectionViewModel.cs:28-31` — `IsSelected` property
- `ScenarioNodeViewModel.cs:29-30` — `IsSelected` property
- `ScenariosViewModel.cs:57-61, 66-68, 94-100` — `SelectedConnections`, `SelectedNodes`, `DeleteSelectionCommand`

---

## Transaction Deep-Copy (JSON)

### Problem

Multiple transactions can share the same name (e.g., two different S1F1 items). During execution, a name-based `FirstOrDefault` lookup always returned the first match. Deep-copying the transaction when dropped onto the canvas makes each node independent.

### Implementation

When a transaction is dropped / added to the canvas:

```csharp
// ScenariosViewModel.cs:AddNodeFromDrop()
var node = new ScenarioNodeViewModel
{
    TransactionName = transaction.Name,
    TransactionJson = SecsGemTransactionJsonConverter.Serialize(transaction.Clone()),
    // ...
};
```

During execution:

```csharp
// ScenarioExecutionService.cs:FindTransaction()
if (!string.IsNullOrWhiteSpace(node.TransactionJson))
{
    var tx = SecsGemTransactionJsonConverter.Deserialize(node.TransactionJson);
    if (tx != null) return tx;  // ← uses the exact clone
}
// Fallback: name-based library lookup for legacy nodes
return libraryManager.Library.FirstOrDefault(t => t.Name == node.TransactionName);
```

### JSON Converter

`SecsGemBaseItems/Data Containers/Serialization/SecsGemTransactionJsonConverter.cs`

Custom `System.Text.Json` converter that handles the full tree:
- `SecsGemTransaction` → Name, Description, PrimaryMessage, ReplyMessage
- `SecsGemDataMessage` → Name, Description, Reply, Stream, Function, IsPrimary, Children
- `SecsGemItem` → Name, Description, FormatType, Values[], Children (recursive)

Static helpers:
```csharp
SecsGemTransactionJsonConverter.Serialize(transaction)  // → string
SecsGemTransactionJsonConverter.Deserialize(json)       // → SecsGemTransaction?
```

### Persistence

`TransactionJson` is a `string?` on `ScenarioNode`. It serializes into `scenarios.json` alongside the node. On deserialization, `ScenarioNodeViewModel.FromModel()` restores it. Legacy nodes without `TransactionJson` gracefully fall back to name lookup.

---

## Message Comparison (Receive nodes)

Used by `ExecuteReceiveAsync` to match incoming equipment messages against the expected transaction clone.

### API

```csharp
// SecsGemDataMessage.cs
public static bool IsEquivalent(SecsGemDataMessage? a, SecsGemDataMessage? b)
// Compares Stream, Function, and recursively compares all children (SecsGemItem)

// SecsGemItem.cs
public static bool IsEquivalent(SecsGemItem? a, SecsGemItem? b)
// Compares FormatType, Values[] (ordinal), and recursively compares all children
```

Both handle nulls via `ReferenceEquals` and null checks. Comparison is structural (not reference equality).

### Usage in execution

```csharp
// ScenarioExecutionService.cs:ExecuteReceiveAsync()
var received = await dataMessageHandler.WaitForReceivedMessage(
    msg => SecsGemDataMessage.IsEquivalent(msg, transaction.PrimaryMessage),
    TimeSpan.FromSeconds(30),
    token);
```

`DataMessageHandler.WaitForReceivedMessage()` subscribes to `communicationHandler.OnDataMessageIn` with a `TaskCompletionSource`. When a message arrives matching the predicate, the task completes. Timeout returns null → Receive node fails.

---

## Execution Engine

### File

`SecsGemBase/SecsGemScenarioEngine/Services/ScenarioExecutionService.cs` (243 lines)

### Flow

```
RunScenario(graph)
  ├─ GetExecutionOrder(graph)
  │    └─ DFS from Start node, following all outgoing edges
  │       (order excludes Start, includes End)
  │
  ├─ foreach node in order:
  │    └─ ExecuteNodeAsync(node, token)
  │         ├─ Start/End → skip (success)
  │         ├─ Send / SendAndWait → ExecuteSendAsync
  │         │    ├─ FindTransaction (JSON or name lookup)
  │         │    ├─ CanSendMessage gate check
  │         │    └─ dataMessageHandler.SendDataMessage(primaryMsg)
  │         ├─ Receive → ExecuteReceiveAsync
  │         │    ├─ FindTransaction
  │         │    └─ WaitForReceivedMessage (30s timeout)
  │         └─ Wait → ExecuteWaitAsync
  │              └─ Task.Delay(TransactionName as int)
  │
  └─ Result (Success, CompletedSteps, FailedNodeId, ErrorMessage)
```

### Cancellation

`Cancel()` → `CancellationTokenSource.Cancel()` → `OperationCanceledException` caught → result with "Cancelled" error.

---

## Drag & Drop

### Source

`TransactionTree_PreviewMouseMove` in `MainWindow.xaml.cs:68-89`
- Fires on `PreviewMouseMove` of each `TreeViewItem` in the library tree
- Finds the containing `TreeViewItem` via visual tree walk (`VisualTreeHelper.GetParent`)
- Gets `DataContext` as `SecsGemTransaction`
- Starts drag with `DragDrop.DoDragDrop(tvi, transaction, DragDropEffects.Copy)`

### Target

`ScenarioCanvas_PreviewDrop` in `MainWindow.xaml.cs:111-125`
- `ScenarioCanvas_PreviewDragOver` accepts the drop (checks for `SecsGemTransaction` type)
- On drop: extracts `SecsGemTransaction` from `e.Data`
- Converts drop position via `ScenarioCanvas.ViewportTransform.Inverse.Transform(position)` (canvas pan/zoom adjustment)
- Calls `ScenariosVm.AddNodeFromDrop(transaction, canvasPosition)`

### Node creation

`AddNodeFromDrop` determines `NodeType` from `transaction.PrimaryMessage.Reply`:
- `Reply == true` → `SendAndWait`
- `Reply == false` → `Send`

Creates `ScenarioNodeViewModel`, serializes cloned transaction as JSON, adds to `Nodes` collection.

---

## Persistence (scenarios.json)

### Location
`%APPDATA%/EvenBetterFastSim/scenarios.json`

### Format
```json
[
  {
    "Name": "Scenario 1",
    "Graph": {
      "Name": "Scenario 1",
      "Description": "",
      "Nodes": [
        {
          "Id": "a1b2c3d4",
          "Type": "SendAndWait",
          "TransactionName": "S1F1",
          "TransactionJson": "{\"Name\":\"S1F1\",...}",
          "X": 200.0,
          "Y": 300.0
        }
      ],
      "Edges": [
        { "SourceNodeId": "start01", "TargetNodeId": "a1b2c3d4" }
      ]
    }
  }
]
```

### Save/Load

- **Save**: `ScenariosViewModel.SaveToJson()` — calls `JsonSerializer.Serialize(Scenarios.ToList())`. `Scenarios` is an `ObservableCollection<ScenarioListItem>`.
- **Build graph**: `BuildGraphFromCanvas()` — iterates `Nodes` (ViewModel), calls `.ToModel()` to get `ScenarioNode`, maps edges via connector references.
- **Load graph**: `LoadGraphToCanvas(graph)` — calls `ScenarioNodeViewModel.FromModel()` for each node, recreates connections from edges.
- **Trigger**: `OnSelectedScenarioChanged` — automatically loads the selected scenario's graph to the canvas.
- **Edges**: `ScenarioEdge` has `SourceNodeId`/`TargetNodeId`. When loading, edges are matched to connectors by node ID → first Input/Output connector.

---

## Complete File Index

| # | File | Lines | Role |
|---|------|-------|------|
| | **EvenBetterFastSim** | | |
| 1 | `WPF/ViewModels/Graph/ScenarioNodeViewModel.cs` | 152 | Visual node VM: connectors, type toggle (Send/Receive) |
| 2 | `WPF/ViewModels/Graph/ConnectionViewModel.cs` | 36 | Canvas edge: Source/Target connectors, IsSelected |
| 3 | `WPF/ViewModels/Graph/PendingConnectionViewModel.cs` | 27 | Drag-to-connect tracking |
| 4 | `WPF/ViewModels/Graph/ConnectorViewModel.cs` | 37 | Node port: anchor point, connection state |
| 5 | `WPF/ViewModels/ScenariosViewModel.cs` | 452 | Central orchestrator: CRUD, persistence, run |
| 6 | `WPF/Windows/MainWindow.xaml` (L451–607) | ~100 | Scenarios tab XAML: canvas, styles, bindings |
| 7 | `WPF/Windows/MainWindow.xaml.cs` | 126 | Drag-drop from library tree onto canvas |
| 8 | `App.xaml.cs` | 124 | DI: `ScenariosViewModel` + `ScenarioExecutionService` |
| 9 | `WPF/ViewModels/MainViewModel.cs` | 585 | Exposes `ScenariosVm` to window DataContext |
| | **SecsGemBase** | | |
| 10 | `SecsGemScenarioEngine/Models/ScenarioNode.cs` | 11 | Serializable node model |
| 11 | `SecsGemScenarioEngine/Models/ScenarioGraph.cs` | 9 | Top-level scenario container |
| 12 | `SecsGemScenarioEngine/Models/ScenarioEdge.cs` | 7 | Directed edge between node IDs |
| 13 | `SecsGemScenarioEngine/Models/NodeType.cs` | 12 | Enum: Start, SendAndWait, Send, End, Condition, Wait, Receive |
| 14 | `SecsGemScenarioEngine/Services/ScenarioExecutionService.cs` | 243 | Runtime engine: DFS + send/wait/receive |
| 15 | `SecsGemScenarioEngine/Services/IScenarioExecutionService.cs` | 17 | Interface + result DTO |
| 16 | `SecsGemBaseItems/Data Containers/SecsGemTransaction.cs` | 83 | Transaction with Primary/Reply messages |
| 17 | `SecsGemBaseItems/Data Containers/SecsGemDataMessage.cs` | 133 | SECS/GEM message: Stream, Function, items |
| 18 | `SecsGemBaseItems/Data Containers/SecsGemItem.cs` | 338 | Leaf data item: format, values, encoding |
| 19 | `SecsGemBaseItems/Data Containers/Serialization/SecsGemTransactionJsonConverter.cs` | 160 | JSON converter for transaction tree |
| 20 | `SecsGemMessageHandling/Data Handling/DataMessageHandler.cs` | 329 | HSMS dispatch: send, receive, gate check |

**Total: 20 files, ~3000 lines across both repositories.**
