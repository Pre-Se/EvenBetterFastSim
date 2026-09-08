# Features

Related docs: [project_overview.md](project_overview.md), [secsgembase_library.md](secsgembase_library.md), [dialog_pattern.md](dialog_pattern.md), [SESSION_REFACTORING_LOG.md](../SESSION_REFACTORING_LOG.md)

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
