# SecsGemBase Library

Location: `%USERPROFILE%\source\repos\SecsGemBase\` ([GitHub: Pre-Se/SecsGemBase](https://github.com/Pre-Se/SecsGemBase))
NuGet packages (nuget.org, pre-release): `SecsGemBase.MessageHandling`, `SecsGemBase.ScenarioEngine`

Related docs: [SESSION_REFACTORING_LOG.md](../SESSION_REFACTORING_LOG.md), [patterns.md](patterns.md), [CLAUDE.md](../CLAUDE.md)

## Class Hierarchy

```
DataItem (ObservableObject)
  ├── SecsGemItem (abstract)
  │     ├── SecsGemListItem        (FormatType = List)
  │     └── SecsGemValueItem<T>    (all value types; Values: ObservableCollection<T>)
  └── SecsGemDataMessage
```

### DataItem (`SecsGemBaseItems/Data Containers/DataItem.cs`)

- `Name`, `Description` are `[ObservableProperty]`
- `Header` is derived: `"Name - Description"` or just `"Name"` — updated by `SetHeader` subscribed to `PropertyChanged` in constructor
- `Children`: `ObservableCollection<IDataItem>`
- `SetParent(ICanBeParent?)` — removes from old parent, adds to new parent via `TryAddChild`

### SecsGemItem (`SecsGemBaseItems/Data Containers/SecsGemItem.cs`) — abstract

- `FormatType`: `SecsGemItemFormatType` enum (List, ASCII, Binary, U1..U8, I1..I8, Float, Double, Boolean, JIS8, TwoByteCharacter)
- **`Values` moved to `SecsGemValueItem<T>`** — the base exposes non-generic accessors instead:
  - `Create(SecsGemItemFormatType)` static factory → `SecsGemValueItem<T>` / `SecsGemListItem`
  - `GetBoxedValues()` / `GetStringValues()` / `SetValuesFromStrings()`
- Constructor subscribes: `PropertyChanged += SetName`, `Children.CollectionChanged += SetName`
- `Name` is auto-set by `SetName()`:
  - List: `"List(N)"` where N = children count
  - Binary: `"Binary = 0xABCD..."` (hex, concatenated)
  - Others: `"FormatType = firstValue"`

### T → FormatType Mapping (`SecsGemValueItem<T>`)

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

### SecsGemDataMessage (`SecsGemBaseItems/Data Containers/SecsGemDataMessage.cs`)

- `Reply`, `Stream`, `Function`, `IsPrimary` are `[ObservableProperty]`
- Constructor subscribes `PropertyChanged += SetName`; `SetName` sets `Name = "S{Stream}F{Function}"`

## Clone Pattern

**No MemberwiseClone.** `SecsGemItem.Clone()` is abstract; the concrete types build clones explicitly:

```csharp
// SecsGemValueItem<T>.Clone()
public override SecsGemItem Clone()
{
    var clone = new SecsGemValueItem<T>
    {
        FormatType = FormatType,
        Description = Description
    };
    clone.Values.Clear();
    foreach (var v in Values)
        clone.Values.Add(v);
    foreach (var child in Children.OfType<SecsGemItem>().Select(c => c.Clone()))
        child.SetParent(clone);
    return clone;
}

// SecsGemListItem.Clone()
public override SecsGemItem Clone()
{
    var clone = new SecsGemListItem
    {
        Description = Description
    };
    foreach (var child in Children.OfType<SecsGemItem>().Select(c => c.Clone()))
        child.SetParent(clone);
    return clone;
}

// SecsGemDataMessage.Clone() — explicit new + CopyFrom + recurse children
// SecsGemTransaction.Clone() — new SecsGemTransaction + cloned Primary/Reply messages
```

MemberwiseClone is banned: it copies the PropertyChanged invocation list, so clones fire handlers on the original instance (Name/Header never updates on the clone). See [patterns.md](patterns.md).

## CopyFrom Rules

`CopyFrom` must never replace `Values` or `Children` with a new collection — always mutate in-place to preserve existing `CollectionChanged` subscriptions.

```csharp
// CORRECT
Values.Clear();
foreach (var v in source.Values) Values.Add(v);

// WRONG — breaks CollectionChanged subscription set up by constructor
Values = new(source.Values);
```

`SecsGemDataMessage.CopyFrom` copies: `Reply`, `Stream`, `Function`, `IsPrimary`, `Description`.
`SecsGemItem.CopyFrom` copies: `Description`, `FormatType`, Values (in-place, when the source is the same `T`).

## Binary Format

Binary values are stored as **`byte[]`** (`SecsGemValueItem<byte>`) after the generic refactor — no more hex-string round-trip. `GetStringValues()` produces **2-digit uppercase hex strings** (e.g., `"FF"`, `"0A"`) for display and wire encoding.

| Location | Change |
|---|---|
| `ItemFactory.cs` AddBinary | `AddBinary(byte value, ...)` — stores raw byte, no hex |
| `SecsGemItem.GetStringValues()` | `byte.ToString("X2")` for Binary |
| `SecsGemItem` serialization | `Convert.ToByte(value, 16)` |
| `SecsGemItem` display | `"0x" + string.Join("", GetStringValues())` |
| `DataMessageHandler` CheckValue | `["00"]` not `["0"]` |
| `SpecialCasesHandling` parsing | `NumberStyles.HexNumber`, `boxed[0] is byte b ? b.ToString("X2")` |

## ICanBeParent

Exposes plain methods — NOT commands:
```csharp
bool CanAddChild(IDataItem? child);
bool TryAddChild(IDataItem child);
bool TryRemoveChild(IDataItem child);
```
Commands hold delegates bound to a specific instance and break with cloning.

## TCPIPClientBase (`TCPIPBaseLibrary/TCPBase/TCPIPClientBase.cs`)

Connection loop creates a **new `TcpClient` per attempt** — `TcpClient` cannot be reconnected after use. The old singleton instance caused `SocketException(IsConnected)` on the second connect attempt after a drop.

Key rules:
- `OnDisconnected` fires on a mid-loop drop (not just on full dispose) — `wasConnected` flag tracks whether `OnConnect` was actually raised
- `catch (Exception e) when (e is SocketException or IOException)` — `IOException` wrapping `SocketException` must be caught alongside bare `SocketException`
- Retry delay uses `await Task.Delay(timeout, cancellationToken)` — cancellable, not `Thread.Sleep`
- `Dispose` catches `AggregateException` from `receiveDataLoopTask.Wait()` containing expected shutdown exceptions
- `activeTcpClient` uses `Volatile.Read/Write` for cross-thread visibility (single writer: loop; reader: `SendData`)

## SecsGemItem serialization (`ConvertBasicValuesToBytes`)

Pre-allocates output array once — O(n). Old code called `Combine(growingArray, singleByte)` in a loop which is O(n²) and hangs for large binary payloads.

Binary is special-cased to avoid per-element array allocations.

## CommunicationHandler — serialization off UI thread

`BuildMessageData` (which calls `message.ToBytes()`) is wrapped in `Task.Run` before `SendDataAsync`. Keeps UI responsive for large messages since serialization runs entirely on the thread pool before the first network write.

Also: `SendAndLogMessage` awaits `SendDataMessage(...).ConfigureAwait(false)` so the SEND timestamp is captured immediately after the TCP send (see [CLAUDE.md](../CLAUDE.md#message-logging-system)).
