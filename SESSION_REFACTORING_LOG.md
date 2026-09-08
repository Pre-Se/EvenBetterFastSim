# Refactoring Session Log — June 2026

## Summary

Refactored `SecsGemItem` from a single string-typed class to a generic type hierarchy (`SecsGemItem` abstract base → `SecsGemValueItem<T>` + `SecsGemListItem`). Eliminates the hex-string round-trip for binary file uploads, making 500KB+ binary sends near-instant instead of hundreds of milliseconds.

**~25 files changed across SecsGemBase and EvenBetterFastSim repos.**

---

## Architecture Change

### Before
```
DataItem → SecsGemItem (concrete)
           Values: ObservableCollection<string>  // "FF", "00", "A3", ...
```
Binary values stored as 500K heap-allocated hex strings. Send path: join 500K strings → parse hex → bytes. ~26MB GC pressure.

### After
```
DataItem → SecsGemItem (abstract)
            ├── SecsGemListItem  (FormatType = List)
            └── SecsGemValueItem<T>  (all value types)
                  Values: ObservableCollection<T>
```
Binary uses `T=byte`. 500KB = 500K entries in a contiguous `byte[]` backing array. No per-element GC allocations. Send path: `Values.CopyTo(result, 0)` — single memcpy.

### T → FormatType Mapping

| `T` | FormatTypes |
|---|---|
| `byte` | Binary, U1 |
| `sbyte` | I1 |
| `bool` | Boolean |
| `ushort` | U2 |
| `short` | I2 |
| `uint` | U4 |
| `int` | I4 |
| `ulong` | U8 |
| `long` | I8 |
| `float` | Float |
| `double` | Double |
| `string` | ASCII, JIS8, TwoByteCharacter |

`FormatType` stays on the base to distinguish wire-level encoding (e.g., Binary vs U1 both use `T=byte`, but have different SECS II format bytes).

---

## All Files Changed

### SecsGemBase Repo (`%USERPROFILE%\source\repos\SecsGemBase\`)

#### NEW FILES
- `SecsGemBaseItems/Data Containers/SecsGemValueItem.cs` — Generic value item
- `SecsGemBaseItems/Data Containers/SecsGemListItem.cs` — List item type

#### REWRITTEN FILES
- `SecsGemBaseItems/Data Containers/SecsGemItem.cs` — Made abstract. Added:
  - `Create(SecsGemItemFormatType)` factory method
  - `GetBoxedValues()` / `GetStringValues()` / `SetValuesFromStrings()` non-generic accessors
  - `CheckValue()` uses `GetStringValues()` (produces hex for Binary)
  - `CreateItemHeaderBytes()`, `GetLengthBytes()` moved to base
  - `IsEquivalent()` uses `GetBoxedValues()`

- `SecsGemBaseItems/Data Containers/ItemFactory.cs` — Each `AddXxx()` creates typed `SecsGemValueItem<T>`

- `SecsGemMessageHandling/Data Handling/MessageParsing.cs` — `ReadItem` uses `SecsGemItem.Create()`, `ReadItemData` dispatches by runtime type

- `SecsGemBaseItems/Data Containers/SecsGemDataMessage.cs` — Uses `OfType<SecsGemItem>()` instead of `Cast<SecsGemItem>()`

- `SecsGemBaseItems/Data Containers/Serialization/SecsGemTransactionJsonConverter.cs` — Uses `Create()`, `GetStringValues()`, `SetValuesFromStrings()`

- `SecsGemBaseItems/XMLParser.cs` — Uses `Create()`, `SetValuesFromStrings()`

#### MODIFIED FILES
- `SecsGemMessageHandling/Data Handling/DataMessageHandler.cs`:
  - `SendCommunicationsRequestAsync` — COMMACK denial logs error + disconnects (was retry loop)
  - `HsmsStatusChanged` — added `!CommunicationsEstablished` guard to prevent timer restart after comms established
  - `CheckValue` usage in S1F14 validation now works correctly with hex strings

- `SecsGemMessageHandling/Data Handling/SpecialCasesHandling.cs` — `CheckIfVariableExist` uses `GetBoxedValues()` instead of `Values.Count`

- `SecsGemMessageHandling/Events/Models/SecsGemEquipmentVariable.cs` — `Item` default is `SecsGemValueItem<string>`, `Value` getter/setter uses `GetStringValues()`/`SetValuesFromStrings()`

- `SecsGemMessageHandling/Events/SecsGemEventReportHandler.cs` — `Values[0]` → `GetStringValues().FirstOrDefault()`, `Values is [var first, ..]` → `GetBoxedValues().FirstOrDefault()`, `new SecsGemItem()` → `SecsGemItem.Create()`

### EvenBetterFastSim Repo

#### REWRITTEN FILES
- `WPF/ViewModels/SecsGemItemViewModel.cs` — File upload sets `SecsGemValueItem<byte>.Values` directly, no hex string split. Added `EnsureCorrectCopyType()` for FormatType changes.

- `WPF/ViewModels/InspectSecsGemItemViewModel.cs` — Display uses `GetStringValues()` instead of `item.Values`

- `WPF/ViewModels/AddEquipmentVariableViewModel.cs` — `SetValuesFromStrings()`, `GetStringValues()`, `EnsureCorrectCopyType()`

- `Services/LibraryJsonService.cs` — `GetStringValues()`, `SetValuesFromStrings()`, `SecsGemItem.Create()`

- `Services/LibraryMessagePackService.cs` — `GetStringValues()`, `SetValuesFromStrings()`, `SecsGemItem.Create()`

- `Services/LibraryXmlExportService.cs` — `GetStringValues()` instead of `item.Values`

#### MODIFIED FILES
- `WPF/ViewModels/MainViewModel.cs` — `new SecsGemItem()` → `SecsGemItem.Create(SecsGemItemFormatType.U1)`

- `Services/ModelViewModelMapper.cs` — `GetViewModel()` walks inheritance chain (`SecsGemValueItem<byte>` → `SecsGemItem` → found) instead of exact type match

---

## Bugs Found and Fixed

### 1. S1F13/S1F14 Infinite Loop
**Root cause:** `CheckValue(SecsGemItemFormatType.Binary, ["00"])` compared `byte.ToString()` = `"0"` against expected hex `"00"`. Always failed → handshake retry forever.
**Fix:** `CheckValue` now uses `GetStringValues()` which returns `"00"` for Binary byte 0.

### 2. "No view model registered" when opening item dialog
**Root cause:** `ModelViewModelMapper.GetViewModel()` used `dataItem.GetType()` as dictionary key. Runtime types (`SecsGemValueItem<byte>`) not registered.
**Fix:** Walk inheritance chain until a registered mapping is found.

### 3. Missing JIS8/TwoByteCharacter in Create()
**Root cause:** `SecsGemItem.Create()` switch didn't handle JIS8/TwoByteCharacter → `ArgumentOutOfRangeException`.
**Fix:** Added to switch, map to `SecsGemValueItem<string>`.

### 4. COMMACK denial retry loop
**Root cause:** `SendCommunicationsRequestAsync` returned `false` on COMMACK != 0, causing infinite retry.
**Fix:** COMMACK denial now logs error, calls `RestartConnection()`, returns `true` (stops timer). Also: `HsmsStatusChanged` guarded with `!CommunicationsEstablished` to prevent timer restart after comms is established.

### 5. Self-induced bug from EventReportHandler rewrite
**Root cause:** Originally attempted full rewrite of `SecsGemEventReportHandler.cs` with incorrect types (`IRegistry<SecsGemEvent>` instead of `IRegistry<SecsGemEventReport>`).
**Fix:** Reverted to original via `git checkout`, applied only minimal changes to `.Values` access.

---

## Files That Need to Be RE-APPLIED (Current State)

The following EvenBetterFastSim files were reverted to their pre-refactoring state and need the refactored code re-applied:

| File | What's broken |
|---|---|
| `WPF/ViewModels/SecsGemItemViewModel.cs` | `new SecsGemItem()` (abstract), `.Values` on base (doesn't exist) |
| `WPF/ViewModels/InspectSecsGemItemViewModel.cs` | `.Values` on 4 lines |
| `WPF/ViewModels/AddEquipmentVariableViewModel.cs` | `new SecsGemItem()`, `.Values` |
| `WPF/ViewModels/MainViewModel.cs` | `new SecsGemItem()` at line 203 |
| `Services/LibraryJsonService.cs` | `new SecsGemItem()`, `.Values` |
| `Services/LibraryMessagePackService.cs` | `new SecsGemItem()`, `.Values` |
| `Services/LibraryXmlExportService.cs` | `.Values` at line 67 |

---

## Known Remaining Issue: Message Log Out of Order

**Problem:** Sent messages sometimes appear after received messages in the message log tree view.

**Root cause:** `EventBusT.cs` uses `ObserveOn(ThreadPoolScheduler.Instance)`. `SecsMessageLogger` subscribes to both `OnDataMessageIn` and `OnDataMessageOut` (filtered views of same bus). Both subscriptions run on arbitrary ThreadPool threads. Both call `Dispatcher.Invoke`. If the "Received" subscriber work item runs before "Sent", the UI dispatcher queue has `[Receive, Send]` — out of order.

**Planned fix (not yet implemented):**
Add `SequenceNumber` to `ILoggedSecsGemMessage` (auto-incrementing `Interlocked.Increment` at construction time). Change `SecsMessageLogger.AddItemToCollection` from `MessagesLog.Add(msg)` (append) to binary-search-based `MessagesLog.Insert(position, msg)`, sorted by `TimeStamp` + `SequenceNumber` tiebreak.

**Files to change:**
- `SecsGemBase/Logging/Interfaces/ILoggedSecsGemMessage.cs` — add `long SequenceNumber { get; }`
- `SecsGemBase/Logging/LoggedSecsGemMessage.cs` — add auto-increment property
- `EvenBetterFastSim/Logging/SecsMessageLogger.cs` — sorted insert instead of append
