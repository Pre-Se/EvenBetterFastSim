# AGENTS.md

See `CLAUDE.md` for detailed architecture, DI wiring, and settings flow.

## Build

Use the `build-and-verify` skill to build the project.

```bash
dotnet build EvenBetterFastSim.sln
dotnet run --project EvenBetterFastSim/EvenBetterFastSim.csproj
```

Configurations: `Debug` (uses NuGet packages from GitLab), `DebugLocal` (uses SecsGemBase DLLs from `%USERPROFILE%\source\repos\SecsGemBase\`), `Release`.

No tests, no linter, no typecheck.

## Where stuff lives

| What | Where |
|---|---|
| DI wiring | `App.xaml.cs:ConfigureServices()` |
| Central VM | `WPF/ViewModels/MainViewModel.cs` |
| Library manager | `WPF/LibraryManager/SecsGemLibraryManager.cs` |
| Settings | `Services/ApplicationSettings.cs` |
| XAML windows | `WPF/Windows/` |
| Event/report/variable mgmt | `WPF/LibraryManager/EventLibraryManager.cs` |

## Gotchas

- **Settings are not written to `appsettings.json`.** Defaults come from `appsettings.json`, runtime edits are saved to `%APPDATA%/EvenBetterFastSim/usersettings.json` only on app close.
- **Add children via `child.SetParent(parent)`**, never assign `Children` directly.
- **DebugLocal** needs the SecsGemBase repo built first at `%USERPROFILE%\source\repos\SecsGemBase\`.
- **Debug** builds pull `SecsGemMessageHandling` and `SecsGemScenarioEngine` from GitLab's NuGet registry. Packages are published by the SecsGemBase CI pipeline on push to master/development.
- **GitLab NuGet auth**: Add to your user-level NuGet config (NOT the project config):
  ```bash
  dotnet nuget add source "http://gitlabserver/api/v4/projects/4/packages/nuget/index.json" --name gitlab --username YOUR_GITLAB_USERNAME --password YOUR_PAT --store-password-in-clear-text --allow-insecure-connections
  ```
  Alternatively, create a **project access token** in the SecsGemBase project settings (`Settings > Access Tokens`) with `Developer` role and `read_api` scope. Then use the bot username as shown in members:
  ```bash
  dotnet nuget add source "http://gitlabserver/api/v4/projects/4/packages/nuget/index.json" --name gitlab --username project_4_bot_xxx --password YOUR_TOKEN --store-password-in-clear-text --allow-insecure-connections
  ```
  Create a PAT at `http://gitlabserver/-/user_settings/personal_access_tokens` with **`api`** scope.
- **`CopyFrom()`** on settings fires `PropertyChanged` which triggers an automatic connection restart.
- **Ordering fix**: If SEND/RECEIVE timestamps appear out of order, add (or check for) `.ConfigureAwait(false)` on `await SendDataMessage(...)` in `CommunicationHandler.SendAndLogMessage()` in SecsGemBase. Without it, the SEND timestamp gets captured on the WPF dispatcher instead of immediately after TCP send, and a fast reply can get an earlier timestamp.
