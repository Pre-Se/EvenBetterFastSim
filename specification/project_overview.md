# Project Overview

EvenBetterFastSim is a WPF desktop app (.NET 10, C# preview) simulating SECS/GEM semiconductor equipment communication. Uses MVVM + Microsoft DI.

Related docs: [CLAUDE.md](../CLAUDE.md), [AGENTS.md](../AGENTS.md), [README.md](../README.md), [patterns.md](patterns.md), [dialog_pattern.md](dialog_pattern.md), [secsgembase_library.md](secsgembase_library.md), [features.md](features.md), [scenario-system.md](scenario-system.md)

## Build

```bash
dotnet build EvenBetterFastSim.sln
dotnet run --project EvenBetterFastSim/EvenBetterFastSim.csproj
```

Configs: `Debug`, `DebugLocal` (uses local SecsGemBase at `%USERPROFILE%\source\repos\SecsGemBase\`), `Release`.
When modifying SecsGemBase: build it first, then rebuild EvenBetterFastSim with `DebugLocal`.

## DI Wiring (App.xaml.cs)

- **Scoped**: `MainViewModel`, `ViewModelLocator`, `CommunicationHandler`, message handlers
- **Transient**: Dialog ViewModels (one per dialog open)
- **Singleton**: Configuration, `ILoggerProvider`

Transient VMs registered: `SetUpViewModel`, `SecsGemItemViewModel`, `SecsGemDataMessageViewModel`, `InspectSecsGemItemViewModel`, `SecsGemTransactionViewModel`, `AddEventReportViewModel`, `AddReportViewModel`, `AddEquipmentVariableViewModel`

## Key Files

- `EvenBetterFastSim/App.xaml.cs` — startup, DI setup, all service registrations
- `EvenBetterFastSim/WPF/ViewModels/MainViewModel.cs` — central orchestrator
- `EvenBetterFastSim/Services/WindowMapper.cs` — maps ViewModel type → Window type for dialogs
- `EvenBetterFastSim/Window Helpers/DialogWindow.xaml` — DataTemplates mapping ViewModel type → UserControl view
- `EvenBetterFastSim/Services/ApplicationSettings.cs` — strongly-typed settings
- `EvenBetterFastSim/Library/` — XML definitions for SECS/GEM messages

## Two TreeViews in MainWindow.xaml

1. **Library TreeView** — editable items; double-click opens edit dialog via `ModifySelectedItemCommand`
2. **Messages Log TreeView** (Grid.Column=2) — received messages; double-click on SecsGemItem opens inspect dialog via `InspectLoggedItemCommand`

## Settings Change Flow

`SetUpViewModel` holds local copies (`NetworkSettingsCopy`, `HsmsParametersCopy`). On OK: `SaveServiceAggregator.Save()` → `destination.CopyFrom(source)`. `CommunicationHandler` subscribes to `PropertyChanged` and auto-restarts connection. Settings persisted to disk only on main window close (`MainViewModel.OnClosing()` → `SaveToJsonService.Save()`).
