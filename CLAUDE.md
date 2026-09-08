# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
dotnet build EvenBetterFastSim.sln
dotnet run --project EvenBetterFastSim/EvenBetterFastSim.csproj
```

Build configurations: `Debug` and `Release` (use the public `SecsGemBase.*` NuGet packages from nuget.org — `SecsGemBase.MessageHandling` and `SecsGemBase.ScenarioEngine`), `DebugLocal` (uses local SecsGemBase repo at `%USERPROFILE%\source\repos\SecsGemBase\`).

When modifying SecsGemBase, publish the updated `SecsGemBase.*` packages to nuget.org, then bump the versions in [EvenBetterFastSim.csproj](EvenBetterFastSim/EvenBetterFastSim.csproj). Or use `DebugLocal` for local iterations: build SecsGemBase first (`dotnet build` in that repo), then rebuild EvenBetterFastSim with `-c DebugLocal`.

There are no automated tests in this project.

### Testing HSMS connection behaviour

To reproduce connection/SelectReq issues without real equipment, use a PowerShell TCP listener that accepts a connection but sends nothing (triggers T6 timeout):

```powershell
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, 5000)
$listener.Start()
$client = $listener.AcceptTcpClient()
Read-Host "Press Enter to close"
$client.Close()
$listener.Stop()
```

## Architecture

**EvenBetterFastSim** is a WPF desktop app (.NET 10, C# preview) that simulates SECS/GEM semiconductor equipment communication. It uses MVVM + Microsoft DI.

### Layer Overview

| Layer | Location | Responsibility |
|---|---|---|
| Presentation | `WPF/Windows/`, `WPF/ViewModels/` | XAML views + CommunityToolkit.MVVM ViewModels |
| Library Management | `WPF/LibraryManager/` | Loads Events, Reports, Equipment Variables; parses SECS/GEM XML libraries |
| Communication | via `SecsGemMessageHandling` NuGet | HSMS TCP/IP protocol, message transactions, control state machine |
| Logging | `Logging/` | `ILogService<T>` → `ObservableCollection<string>` bound to UI |
| Configuration | `appsettings.json` + `Services/ApplicationSettings.cs` | Strongly-typed settings (network, HSMS timers) via `Microsoft.Extensions.Configuration` |

### DI Wiring

All composition happens in [App.xaml.cs](EvenBetterFastSim/App.xaml.cs) `ConfigureServices()`:
- **Scoped**: `MainViewModel`, `ViewModelLocator`, `CommunicationHandler`, message handlers
- **Transient**: Dialog ViewModels (Add/Edit)
- **Singleton**: Configuration, `ILoggerProvider`

### Key Files

- [App.xaml.cs](EvenBetterFastSim/App.xaml.cs) — startup, DI setup
- [WPF/ViewModels/MainViewModel.cs](EvenBetterFastSim/WPF/ViewModels/MainViewModel.cs) — central orchestrator
- [WPF/LibraryManager/EventLibraryManager.cs](EvenBetterFastSim/WPF/LibraryManager/EventLibraryManager.cs) — events/reports/variables registry
- [Services/ApplicationSettings.cs](EvenBetterFastSim/Services/ApplicationSettings.cs) — settings binding
- [Library/](EvenBetterFastSim/Library/) — XML definitions for SECS/GEM messages

### Scenario Canvas

The scenario editor uses **Nodify** (v7.3.0) for the node graph canvas. See **[specifications/scenario-system.md](specifications/scenario-system.md)** for full architecture documentation covering node types, selection/deletion, transaction deep-copy, message comparison, execution engine, drag-and-drop, and persistence.

### Patterns

- **MVVM**: `ObservableObject` + `RelayCommand` from CommunityToolkit.MVVM; `EventToCommandAdaptor` bridges WPF events to commands
- **Dialogs**: `WindowManager` + `CloseAction` callback + `DialogMode` enum (Add/Edit)
- **Results**: `FluentResults` for error handling
- **SECS/GEM protocol**: configured via HSMS parameters (T3–T8 timers, SessionId) in `appsettings.json`

## Multi-instance Hub

Launching the app **without** arguments shows `LauncherWindow` (the "Hub", VM `LauncherViewModel`)
instead of `MainWindow`. The Hub manages named `InstanceProfile`s (name + IP/port/mode) stored in
`%APPDATA%\EvenBetterFastSim\profiles\profiles.json` and launches each as its **own process** via
`Process.Start(... --profile <name>)`. "New linked pair" creates a Passive + Active profile sharing
one endpoint so two instances can talk immediately.

`Services/InstanceContext.cs` reads `--profile <name>` at startup (first line of
`App.ConfigureServices()`) and redirects every per-instance file to
`%APPDATA%\EvenBetterFastSim\profiles\<name>\` — `SaveToJsonService.UserSettingsPath` and
`ScenariosViewModel.ScenariosIndexPath` both derive from `InstanceContext.SettingsDirectory`.
No profile ⇒ the legacy `%APPDATA%\EvenBetterFastSim\` paths (backward compatible).
`InstanceProfileStore.SeedSettingsFile()` merges the profile's endpoint into that folder's
`usersettings.json` on create/edit/launch, preserving other in-app setting edits.
`MainViewModel.WindowTitle` shows the profile name + endpoint so instances are distinguishable.

## Settings Change Flow

Port properties (IP, port, connection mode, HSMS timers) are edited in `SetUpWindow` / `SetUpViewModel`. The viewmodel holds local copies (`NetworkSettingsCopy`, `HsmsParametersCopy`) so the user can cancel without affecting the live state.

On OK: `AcceptButtonClick` → `SaveServiceAggregator.Save()` → `SaveService<T>.Save()` → `destination.CopyFrom(source)` on the scoped `INetworkSettings` / `IHSMSParameters` instances.

`CopyFrom` sets three properties individually, each firing `PropertyChanged`. `CommunicationHandler` (in SecsGemBase) subscribes to these events and automatically restarts the connection if the port is open — no manual disconnect/reconnect is needed. Multiple rapid property-change events are coalesced into a single restart by an Interlocked flag in `CommunicationHandler`.

Settings are **not** written to `appsettings.json` immediately — they are persisted only when the main window closes (`MainViewModel.OnClosing()` → `SaveToJsonService.Save()`). Unsaved settings survive as long as the process is alive.

## SecsGemBase (library source)

The `SecsGemMessageHandling` NuGet comes from `%USERPROFILE%\source\repos\SecsGemBase\`. Key files there:

- `SecsGemBaseItems/Data Containers/SecsGemItem.cs` — tree node model; `Name` is auto-set via `Children.CollectionChanged`. Adding children via `SetParent()` (not direct `Children =` assignment) is required to keep the name in sync.
- `SecsGemBaseItems/Data Containers/ItemFactory.cs` — fluent builder for outgoing messages
- `SecsGemMessageHandling/Data Handling/ControlMessageHandling.cs` — HSMS state machine (NotConnected → NotSelected → Selected); sends SelectReq on connect if `InitiateSelectRequest = true`
- `SecsGemMessageHandling/Data Handling/TransactionHandler.cs` — manages request/reply pairing with T3/T6 timeouts; calls `RestartConnection()` internally on timeout
- `SecsGemMessageHandling/Data Handling/MessageParsing.cs` — parses raw bytes into `SecsGemItem` tree

## Message Logging System

Messages (SECS/GEM data and control) are logged to a `TreeView` in the right panel of the main window.

### Architecture

```
EventBus (ThreadPoolScheduler)
  │
  ├── OnDataMessageIn  →  SecsMessageLogger.MessageIn()
  ├── OnDataMessageOut →  SecsMessageLogger.MessageOut()
  ├── OnControlMessageIn  →  SecsMessageLogger.ControlMessageIn()
  └── OnControlMessageOut →  SecsMessageLogger.ControlMessageOut()
                              │
                              ▼
                    AddItemToCollection()
                              │
                    ┌─────────┴──────────┐
                    │ seq = Interlocked  │  ← assigned on ThreadPool thread,
                    │       .Increment() │     before Dispatcher call (no race)
                    └─────────┬──────────┘
                              │
                    Dispatcher.InvokeAsync()
                              │
                    ┌─────────┴──────────┐
                    │ Insert in sorted   │  ← runs on UI thread
                    │ order by timestamp │
                    │ then by sequence   │
                    └────────────────────┘
```

### Out-of-Order Fix

`CommunicationHandler.SendAndLogMessage()` (in SecsGemBase) creates the `LoggedDataMessage` (capturing `TimeStamp = DateTime.Now`) after `await SendDataMessage(...)`. Without `.ConfigureAwait(false)`, the continuation marshals back to the WPF dispatcher because the async chain originated on the UI thread. Meanwhile, `ParseDataReceived()` captures its timestamp immediately on the TCP thread. If the dispatcher is busy, the SEND timestamp can be captured **after** the RECEIVE timestamp — making the send appear later than its reply.

**Fix** ([CommunicationHandler.cs:187](https://github.com/anomalyco/SecsGemBase/blob/master/SecsGemMessageHandling/Data%20Handling/CommunicationHandler.cs)):

```
var (status, rawData, header) = await SendDataMessage(message, systemBytes).ConfigureAwait(false);
```

`.ConfigureAwait(false)` keeps the continuation on the thread pool, so `DateTime.Now` runs immediately after the TCP send completes — before the reply can arrive.

The logger (`SecsMessageLogger`) simply appends via `Dispatcher.InvokeAsync(() => MessagesLog.Add(...))`. No sequence‑number sorting is needed because the timestamps are now chronologically correct.

### UI Template

Each log item renders as:

```
[↑/↓ icon] [HH:mm:ss.fff] [message name]
```

Defined in [MainWindow.xaml](EvenBetterFastSim/WPF/Windows/MainWindow.xaml) lines 719–748 with two `HierarchicalDataTemplate`s (one for `LoggedControlMessage`, one for `LoggedDataMessage`):

| Element | Details |
|---|---|
| Icon | `ArrowCircleUp20` (sent, `#00CC44`) / `ArrowCircleDown20` (received, `#0088FF`), bound by `DataTrigger` on `MessageResult` |
| Timestamp | `{Binding TimeStamp, StringFormat={}{0:HH:mm:ss.fff}}` |
| Message | `Data.Header` for data messages (e.g. `"S1F13"`); `Header` with `MsgHeaderConverter` for control messages (strips leading timestamp and `Sent`/`Received` prefix) |

The `MsgHeaderConverter` ([MessageHeaderConverter.cs](EvenBetterFastSim/Helpers/MessageHeaderConverter.cs)) strips `"Sent "` / `"Received "` (data messages) and leading `"HH:mm:ss.fff "` pattern + `"Sent"`/`"Received"` (control messages) from the `Header` string so only the message name remains.

### Colors

| Direction | Icon | Hex |
|---|---|---|
| Sent (outgoing) | ↑ `ArrowCircleUp20` | `#00CC44` |
| Received (incoming) | ↓ `ArrowCircleDown20` | `#0088FF` |

### Cloning

`SecsGemTransaction`, `SecsGemDataMessage`, and `SecsGemItem` all implement `IDeepCloneable<T>` and expose a `Clone()` method. Each uses `MemberwiseClone` then reassigns `Children`, clones the children list into it via `child.SetParent(clone)`, and fixes up any value-type collections (e.g. `Values`).

`SecsGemDataMessage.Clone()` returns `ISecsGemDataMessage`; cast to `SecsGemDataMessage` when assigning to the `PrimaryMessage`/`ReplyMessage` setters.

### `ICanBeParent` — plain methods, not commands

`ICanBeParent` exposes three methods for parent-child wiring:

```csharp
bool CanAddChild(IDataItem? child);
bool TryAddChild(IDataItem child);
bool TryRemoveChild(IDataItem child);
```

`DataItem.SetParent` calls these directly. **Do not go back to exposing `IRelayCommand` on this interface.** Commands hold delegates bound to a specific instance; `MemberwiseClone` copies the backing field by reference, so any command on a clone would silently execute on the original. Plain methods are resolved on the actual instance at call time and have no such problem.
