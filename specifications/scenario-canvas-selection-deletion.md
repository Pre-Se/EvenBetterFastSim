# Scenario Canvas — Connection & Node Selection / Deletion

## Overview

The **NodifyEditor** (`ScenarioCanvas`) supports selecting and deleting connections and nodes via left-click + Delete, following the Nodify Playground example pattern.

## Transaction deep-copy on drop

When a transaction is dragged onto the canvas, it is deep-cloned and serialized to JSON, stored in `ScenarioNode.TransactionJson`. During scenario execution, the clone is deserialized — so each node carries its own independent snapshot, immune to library edits. Existing nodes without `TransactionJson` fall back to the old name-based `FirstOrDefault` library lookup.

See also: `SecsGemBaseItems/Data Containers/Serialization/SecsGemTransactionJsonConverter.cs`

## How selection works

### Connection selection (left-click)

Left-clicking a connection selects it. The `BaseConnection` style in `NodifyEditor.Resources` sets `IsSelectable="True"` and `IsSelected="{Binding IsSelected}"`, enabling Nodify's built-in selection mechanism via `ConnectionContainer.OnMouseDown` → `ConnectionsMultiSelector.Select()`.

Selected connections are synced to `ScenariosViewModel.SelectedConnections` (ObservableCollection) via `NodifyEditor.SelectedConnections` binding.

### Node selection (left-click)

Left-clicking a node selects it. The `ItemContainer` style sets `IsSelected="{Binding IsSelected}"`, and `NodifyEditor.SelectedItems` is bound to `ScenariosViewModel.SelectedNodes` (ObservableCollection).

### Visual feedback

Selected connections are highlighted using a `BaseConnection` style trigger:
- `Stroke` changes to accent color
- `StrokeThickness` increases to 3

### Deletion (Delete key)

`KeyBinding Key="Delete"` on the `NodifyEditor` calls `DeleteSelectionCommand`, which iterates `SelectedConnections` and `SelectedNodes` and removes them.

### Right-click context menu deletion

Still available for both connectors ("Disconnect") and connections ("Delete").

## Key files

| File | Role |
|---|---|
| `MainWindow.xaml:513-607` | NodifyEditor with `SelectedItems`, `SelectedConnections` bindings, `KeyBinding`, styles |
| `MainWindow.xaml.cs` | Code-behind (drag-and-drop only now; selection/deletion handled by Nodify + bindings) |
| `ScenariosViewModel.cs:57-61,66-68,94-100` | `SelectedConnections`, `SelectedNodes`, `DeleteSelectionCommand` |
| `ConnectionViewModel.cs:28-31` | `IsSelected` property (two-way bound to `BaseConnection.IsSelected`) |
| `ScenarioNodeViewModel.cs:34-35` | `IsSelected` property (two-way bound to `ItemContainer.IsSelected`) |

## Architecture note

Both `NodifyEditor.SelectedConnections` and `NodifyEditor.SelectedItems` default to `null`. Nodify's internal selection engine adds/removes items from these collections. When unbound, items are silently dropped. Binding them to VM-backed `ObservableCollection`s (like the Playground does) is required for selection to work.
