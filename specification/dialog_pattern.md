# Dialog Pattern

## How a dialog opens

1. Command in `MainViewModel` calls `ViewModelLocator.GetViewModel<TViewModel>()` (resolves from DI)
2. Set data on the VM after resolution: `vm.Item = item` or `vm.Initialize(item)`
3. `WindowManager.ShowDialog(vm)` → `WindowMapper` maps VM type → Window → creates window, sets DataContext, shows modal

## Three files to update for every new dialog

| File | What to add |
|---|---|
| `App.xaml.cs` | `services.AddTransient<TViewModel>()` |
| `Services/WindowMapper.cs` | `TViewModel → DialogWindow` mapping |
| `Window Helpers/DialogWindow.xaml` | `DataTemplate DataType=TViewModel` → UserControl |

## Current dialog mappings

| ViewModel | View | Status |
|---|---|---|
| `SecsGemItemViewModel` | `SecsGemItemView` | Done |
| `SecsGemDataMessageViewModel` | `SecsGemDataMessageView` | Done |
| `InspectSecsGemItemViewModel` | `InspectSecsGemItemView` | Done |
| `SecsGemTransactionViewModel` | `SecsGemTransactionView` | Done |

## ViewModel conventions

- `CloseAction` callback — set by the Window, invoked by VM on Accept/Cancel
- Data passed via constructor injection (`ISecsGemLibraryManager.SelectedItem`) — VM reads the selected item in its constructor
- On Accept: write directly to the item (`item.CopyFrom(copy)` or set properties) then `CloseAction?.Invoke()`
- On Cancel: `CloseAction?.Invoke()` only

## Double-click bindings (MainWindow.xaml)

`MouseBinding LeftDoubleClick` on a `StackPanel.InputBindings` inside each TreeView `DataTemplate`. Command bound via `RelativeSource AncestorType=TreeView` to reach the TreeView's `DataContext` (MainViewModel).

- Library TreeView templates → `ModifySelectedItemCommand`
- Messages Log TreeView SecsGemItem → `InspectLoggedItemCommand`
