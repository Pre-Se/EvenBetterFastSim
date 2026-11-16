# Features

Related docs: [project_overview.md](project_overview.md), [secsgembase_library.md](secsgembase_library.md), [dialog_pattern.md](dialog_pattern.md), [SESSION_REFACTORING_LOG.md](../SESSION_REFACTORING_LOG.md)

## Wait node on the canvas

A Wait node can be made and edited in the app (internal board, issue 18). Until then it only came from an imported
file, its time could only be changed in the file, and a value the engine cannot read ("20 s", "12,6") silently meant
"no wait" while the node only showed its name or the bare number.

**Files:** `WPF/ViewModels/Graph/WaitTime.cs`, `WPF/ViewModels/Graph/ScenarioNodeViewModel.cs` (`CreateStructural`,
title), `WPF/ViewModels/NodeMillisecondsViewModel.cs`, `WPF/Windows/NodeMillisecondsView.xaml`,
`WPF/ViewModels/ScenariosViewModel.cs` (`OpenNodeEditor`, `AddStructuralNode`), `WPF/Windows/MainWindow.xaml` (palette
chip, node colour), `Window Helpers/DialogWindow.xaml` and `.xaml.cs`, `Services/WindowMapper.cs`.

- **Palette:** the chip **WAIT** next to AND. Dropping it on the canvas makes a Wait node with 1000 ms.
- **Editor:** double-click, right-click or Enter on the node opens the dialog "Wait" with the field "Wait time (ms)".
  It accepts a whole number from 1 to 2147483647 written in digits; OK or Enter writes it to the node's
  `TransactionName`, where the engine reads it. Spaces and characters nobody can see at the ends of the text are
  ignored; inside the number the message under the field names them.
- **Title:** "Wait 1500 ms". A value the engine cannot read shows as "Wait (invalid: 20 s)", a missing one as
  "Wait (no time set)". A character nobody can see in such a value (a non-breaking space from a hand-edited file)
  shows as "[U+00A0]". A `DisplayName` from a file no longer replaces the time.
- A time with a leading sign ("+5", only possible in a hand-edited file) shows as valid only if the culture of the
  computer reads it too: the engine up to package `1.0.0-build-0027` reads with that culture, a later one with the
  invariant culture (`WaitTime.TryRead`). Where the plus sign of the regional settings is "+", the two agree.
- `NodeMillisecondsViewModel` takes its title, label and minimum, so other node types with a time in ms can use it.
  It is not an `ObservableValidator`: `DialogWindow` binds its content to the view model, and WPF then draws its red
  validation frame around the whole dialog.
- The engine in SecsGemBase logs a warning for a time it cannot read once its change for this issue
  ([SecsGemBase pull request 12](https://github.com/Pre-Se/SecsGemBase/pull/12)) is published and the app's packages
  are raised to it. Every package up to `1.0.0-build-0027` skips the wait silently (the app pinned
  `1.0.0-build-0018` when this was written), and the title is then the only sign.

## Scenario runs leave the UI thread

A scenario (Run or a loop) now executes on the thread pool. Until then every node ran on the WPF UI thread, because
the engine awaits without `ConfigureAwait(false)`: the time between two nodes depended on what the UI was busy with,
and a node that blocks froze the window (internal board, issue 26).

**Files:** `Services/ScenarioRun.cs` (`RunAsync`, `Snapshot`, `IsStale`), `WPF/ViewModels/ScenariosViewModel.cs`
(`RunOnceAsync`, `LogInfo`/`LogError`), `Logging/ObservableLogger.cs`.

- `ScenarioRun.RunAsync` takes a `Snapshot` of the graph on the UI thread and starts the engine with `Task.Run`.
  `RunOnceAsync` continues on the UI thread after the await; the `Progress<T>` for the canvas is created there.
- The snapshot is a copy nothing else refers to. The stored graph changes on the UI thread during a run (opening a
  scenario clears `TransactionJson` of its nodes), and a node from an older file that only names its transaction gets
  a copy of the library transaction, so the engine does not read or write the UI-bound library for it. That copy is
  written with the invariant culture (the SecsGemBase converter otherwise turns 1,5 into 15 on a decimal-comma system).
- The System Log logger (`ObservableLogger`) queues its entries with `Dispatcher.InvokeAsync`, like
  `SecsMessageLogger`, instead of waiting for the UI thread. `ScenariosViewModel` writes its own lines through the
  logger too, so they keep their place between the engine's lines. Without a dispatcher (unit tests, shutdown) an
  entry is dropped; pass a dispatcher provider as third constructor argument to observe entries in a test.
- Two things the engine did not need on a single thread: runs enter the engine one at a time (it keeps the run's
  cancellation source in a field while it starts), and an And node's late "1/2 branches" report no longer replaces
  its Succeeded state.

## Responder panel — edit the message shape

The node responder dialog (`NodeResponderView`, opened by double-click on a Send / Receive node) now has a
message-editing toolbar above the field tree: **Add Item** (U1 child of the selected list / message body),
**Add Sibling** (U1 after the selection), **Duplicate**, **Delete**, **Up**, **Down** (also `Alt+Up` /
`Alt+Down`). Each row is a left-aligned strip whose first control is an inline **format dropdown** that
retypes the item in place (values carried over best-effort; children preserved when a value item becomes a
list) — there is no separate text label, the dropdown is the item's identity. Works in both modes — Match
conditions (Receive) and Response values (Send).

**Files:** `WPF/ViewModels/Responders/NodeResponderViewModel.cs` (`workingMessage` clone, `SelectedField`,
`AddItem` / `AddSibling` / `DuplicateItem` / `DeleteItem` / `MoveItem*` commands, `ApplyFormatChange`,
`CommitStructuralChange`), `WPF/ViewModels/Responders/ResponderFieldViewModel.cs` (`Format`, `FormatChoices`,
`FormatChangeRequested`), `WPF/Windows/NodeResponderView.xaml` (toolbar, per-row format combo, selection
binding via `EventToCommandAdaptor.TreeViewSelectedItem`).

- Edits mutate a clone; on **OK** the new shape is written back to the node's `Transaction` (Primary, or
  Reply when `UseReplyMessage`) only if something structural changed, then persisted via `ToModel()` JSON.
- `BuildFields` wraps `workingMessage` directly (no re-clone) so `SecsGemItem` identity is stable across the
  tree rebuild — `CommitStructuralChange` uses it to carry pending condition / binding edits over.

## Inspect received message items

Double-clicking a `SecsGemItem` in the messages log TreeView opens a read-only inspect dialog.

**Files:** `InspectSecsGemItemViewModel.cs`, `InspectSecsGemItemView.xaml/.cs`, `MainViewModel.InspectLoggedItemCommand`, `MainWindow.xaml` (MouseBinding on log TreeView), `WindowMapper`, `DialogWindow.xaml`, `App.xaml.cs`

- `Item` property set after DI resolution
- `UseBase64` toggle for binary display — default hex (`"0xAB CD..."`), toggle Base64
- `IsHex` = inverse of `UseBase64` for RadioButton TwoWay binding
- Format row visibility controlled by `IsBinary`

## Binary values as byte[]

Binary values are stored as `byte[]` (`SecsGemValueItem<byte>`) since the generic refactor; `GetStringValues()` renders them as 2-digit uppercase hex strings. See [secsgembase_library.md](secsgembase_library.md) and [SESSION_REFACTORING_LOG.md](../SESSION_REFACTORING_LOG.md) for all touch points.

## Double-click library items opens edit dialog

`MouseBinding LeftDoubleClick → ModifySelectedItemCommand` on SecsGemItem, SecsGemDataMessage, and SecsGemTransaction templates in the library TreeView.

## Clone/duplicate name update bug — fixed

Replaced MemberwiseClone with explicit `new T() + CopyFrom() + recurse children` in `SecsGemItem.Clone()` and `SecsGemDataMessage.Clone()`.

## SecsGemTransaction edit dialog

Fully wired. Double-clicking a transaction in the library TreeView opens a Name + Description edit dialog.

**Files:** `SecsGemTransactionViewModel.cs`, `SecsGemTransactionView.xaml`, `App.xaml.cs`, `WindowMapper.cs`, `DialogWindow.xaml`, `ModelViewModelMapper.cs`

- VM takes `ISecsGemLibraryManager` in constructor, reads `SelectedItem as SecsGemTransaction`
- Holds a `transactionCopy` (new SecsGemTransaction); copies Name + Description into it
- On Accept: writes `transaction.Name` and `transaction.Description` directly (no SaveService)
- `ModelViewModelMapper` maps `SecsGemTransaction → SecsGemTransactionViewModel` so `ModifySelectedItemCommand` routes it automatically

## Upload file as binary

"File Upload" mode in `SecsGemItemView` — visible only when `FormatType == Binary`.

- `IsFileUploadMode` property on `SecsGemItemViewModel` (RadioButton "File Upload" / "Manual Entry" toggle)
- `UploadFileCommand`: opens `OpenFileDialog`, reads all bytes straight into `SecsGemValueItem<byte>.Values` (no hex string split)
- Button appears inline next to the Value TextBox

## Save library as XML

Library → Save as XML menu item writes the current transaction library to an XML file in the same format that `XmlParser` reads.

**Files:** `Services/LibraryXmlExportService.cs`, `MainViewModel.SaveLibrary()` (bound to `SaveLibraryCommand` in `MainWindow.xaml`)

- Multi-value items write one `<Value>` element per entry; parser reads all of them
- `LibraryXmlExportService` registered as scoped in `App.xaml.cs`

## Inspect dialog — selectable TextBoxes

All fields in `InspectSecsGemItemView` use `IsReadOnly="True"` TextBoxes instead of TextBlocks — text is selectable and copyable. Value box has `MaxHeight="200"` and `VerticalScrollBarVisibility="Auto"` to stay bounded. Value binding uses `Mode=OneWay` on the read-only computed `ValuesDisplay` property.

## XML library — multi-value items round-trip correctly

`XmlParser.ReadItems` now uses `SelectNodes("Value")` instead of `SelectSingleNode("Value")`. First value replaces `Values[0]`, subsequent values are `Add`ed. Pairs with `LibraryXmlExportService` which writes one `<Value>` per entry.

## Auto-reconnect on server drop

When the remote server drops the connection, `TCPIPClientBase` fires `OnDisconnected`, which triggers `CommunicationHandler.RestartConnection`. Status updates immediately to `PortOpen` and the reconnect loop starts fresh. Previously the status stayed `Connected` because `OnDisconnected` only fired when the entire connect task ended (on dispose), not on a mid-loop drop.
