# AGENTS.md

See [CLAUDE.md](CLAUDE.md) for detailed architecture, DI wiring, and settings flow.

## Build

```bash
dotnet build EvenBetterFastSim.sln
dotnet run --project EvenBetterFastSim/EvenBetterFastSim.csproj
```

Configurations: `Debug` and `Release` (use the `SecsGemBase.MessageHandling` / `SecsGemBase.ScenarioEngine` pre-release NuGet packages from nuget.org), `DebugLocal` (uses SecsGemBase DLLs from `%USERPROFILE%\source\repos\SecsGemBase\`).

No linter, no typecheck. Tests: `EvenBetterFastSim.Tests` (xUnit) — build the app first, then test:

```bash
dotnet build EvenBetterFastSim.sln -c DebugLocal
dotnet test EvenBetterFastSim.Tests
```

## Where stuff lives

| What | Where |
|---|---|
| DI wiring | `App.xaml.cs:ConfigureServices()` |
| Central VM | `WPF/ViewModels/MainViewModel.cs` |
| Library manager | `WPF/LibraryManager/SecsGemLibraryManager.cs` |
| Settings | `Services/ApplicationSettings.cs` |
| XAML windows | `WPF/Windows/` |
| Event/report/variable mgmt | `WPF/LibraryManager/EventLibraryManager.cs` |

## Docs

- [specification/project_overview.md](specification/project_overview.md) — architecture summary
- [specification/patterns.md](specification/patterns.md) — coding rules
- [specification/dialog_pattern.md](specification/dialog_pattern.md) — dialog wiring
- [specification/secsgembase_library.md](specification/secsgembase_library.md) — SecsGemBase internals
- [specification/features.md](specification/features.md) — feature log
- [specification/scenario-system.md](specification/scenario-system.md) — scenario engine reference
- [specification/scenario-canvas-selection-deletion.md](specification/scenario-canvas-selection-deletion.md) — canvas selection/deletion
- [SESSION_REFACTORING_LOG.md](SESSION_REFACTORING_LOG.md) — SecsGemItem generic refactor log

## Gotchas

- **Settings are not written to `appsettings.json`.** Defaults come from `appsettings.json`, runtime edits are saved to `%APPDATA%/EvenBetterFastSim/usersettings.json` only on app close.
- **Add children via `child.SetParent(parent)`**, never assign `Children` directly.
- **DebugLocal** needs the SecsGemBase repo built first at `%USERPROFILE%\source\repos\SecsGemBase\`.
- **`CopyFrom()`** on settings fires `PropertyChanged` which triggers an automatic connection restart.
- **Ordering fix**: If SEND/RECEIVE timestamps appear out of order, add (or check for) `.ConfigureAwait(false)` on `await SendDataMessage(...)` in `CommunicationHandler.SendAndLogMessage()` in SecsGemBase. Without it, the SEND timestamp gets captured on the WPF dispatcher instead of immediately after TCP send, and a fast reply can get an earlier timestamp.
