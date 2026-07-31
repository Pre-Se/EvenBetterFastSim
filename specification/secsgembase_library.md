# SecsGemBase Library

Location: `%USERPROFILE%\source\repos\SecsGemBase\`
NuGet package: `SecsGemMessageHandling`

## Class Hierarchy

`DataItem` (ObservableObject) → `SecsGemItem`, `SecsGemDataMessage`

### DataItem (`SecsGemBaseItems/Data Containers/DataItem.cs`)

- `Name`, `Description` are `[ObservableProperty]`
- `Header` is derived: `"Name - Description"` or just `"Name"` — updated by `SetHeader` subscribed to `PropertyChanged` in constructor
- `Children`: `ObservableCollection<IDataItem>`
- `SetParent(ICanBeParent?)` — removes from old parent, adds to new parent via `TryAddChild`

### SecsGemItem (`SecsGemBaseItems/Data Containers/SecsGemItem.cs`)

- `FormatType`: `SecsGemItemFormatType` enum (List, ASCII, Binary, U1..U8, I1..I8, Float, Double, Boolean, JIS8, TwoByteCharacter)
- `Values`: `ObservableCollection<string>` — initialized to `[string.Empty]` in field initializer
- Constructor subscribes: `PropertyChanged += SetName`, `Values.CollectionChanged += SetName`, `Children.CollectionChanged += SetName`
- `Name` is auto-set by `SetName()`:
  - List: `"List(N)"` where N = children count
  - Binary: `"Binary = 0xABCD..."` (hex, concatenated)
  - Others: `"FormatType = firstValue"`

### SecsGemDataMessage (`SecsGemBaseItems/Data Containers/SecsGemDataMessage.cs`)

- `Reply`, `Stream`, `Function`, `IsPrimary` are `[ObservableProperty]`
- Constructor subscribes `PropertyChanged += SetName`; `SetName` sets `Name = "S{Stream}F{Function}"`

## Clone Pattern

No MemberwiseClone. Both Clone methods use explicit construction:

```csharp
// SecsGemItem.Clone()
public SecsGemItem Clone()
{
    var clone = new SecsGemItem();
    clone.CopyFrom(this);
    foreach (var child in Children.OfType<SecsGemItem>().Select(c => c.Clone()))
        child.SetParent(clone);
    return clone;
}

// SecsGemDataMessage.Clone()
public ISecsGemDataMessage Clone()
{
    var clone = new SecsGemDataMessage();
    clone.CopyFrom(this);
    foreach (var child in Children.OfType<SecsGemItem>().Select(c => c.Clone()))
        child.SetParent(clone);
    return clone;
}
```

MemberwiseClone is banned: it copies the PropertyChanged invocation list, so clones fire handlers on the original instance (Name/Header never updates on the clone).

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
`SecsGemItem.CopyFrom` copies: `Description`, `FormatType`, Values (in-place).

## Binary Format

Binary values are stored as **2-digit uppercase hex strings** (e.g., `"FF"`, `"0A"`).

| Location | Change |
|---|---|
| `MessageParsing.cs` parsing | `itemDataBytes[0].ToString("X2")` |
| `ItemFactory.cs` AddBinary | `value.ToString("X2")` |
| `SecsGemItem` serialization | `Convert.ToByte(value, 16)` |
| `SecsGemItem` display | `"0x" + string.Join("", Values)` |
| `DataMessageHandler` CheckValue | `["00"]` not `["0"]` |
| `SpecialCasesHandling` parsing | `NumberStyles.HexNumber` |

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

Binary is special-cased to avoid per-element array allocations:

## CommunicationHandler — serialization off UI thread

`BuildMessageData` (which calls `message.ToBytes()`) is wrapped in `Task.Run` before `SendDataAsync`. Keeps UI responsive for large messages since serialization runs entirely on the thread pool before the first network write.
