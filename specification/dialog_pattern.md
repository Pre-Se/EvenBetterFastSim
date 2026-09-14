# Dialog Pattern

Related docs: [patterns.md](patterns.md), [project_overview.md](project_overview.md), [CLAUDE.md](../CLAUDE.md)

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

## Current dialog mappings (`Services/WindowMapper.cs`)

| ViewModel | View | Status |
|---|---|---|
| `SecsGemItemViewModel` | `SecsGemItemView` | Done |
| `SecsGemDataMessageViewModel` | `SecsGemDataMessageView` | Done |
| `InspectSecsGemItemViewModel` | `InspectSecsGemItemView` | Done |
| `SecsGemTransactionViewModel` | `SecsGemTransactionView` | Done |
| `AddEventReportViewModel` | `AddEventReportView` | Done |
| `AddReportViewModel` | `AddReportView` | Done |
| `AddEquipmentVariableViewModel` | `AddEquipmentVariableView` | Done |
| `InstanceProfileViewModel` | `InstanceProfileView` | Done |
| `SetUpViewModel` | `SetUpWindow` | Done |

## ViewModel conventions

- `CloseAction` callback — set by the Window, invoked by VM on Accept/Cancel
- **Standard (per [patterns.md](patterns.md))**: don't read `ISecsGemLibraryManager.SelectedItem` in the constructor — resolve the VM, then pass the item after (`vm.Item = item` / `vm.Initialize(item)`). Shared mutable state in DI is unreliable.
- **Legacy (still present in several VMs)**: constructor injection of `ISecsGemLibraryManager`, reading `SelectedItem` directly in the constructor (`SecsGemItemViewModel`, `SecsGemTransactionViewModel`, `AddEquipmentVariableViewModel`). Prefer the standard for new dialogs.
- On Accept: write directly to the item (`item.CopyFrom(copy)` or set properties) then `CloseAction?.Invoke()`
- On Cancel: `CloseAction?.Invoke()` only

## Double-click bindings (MainWindow.xaml)

`MouseBinding LeftDoubleClick` on a `StackPanel.InputBindings` inside each TreeView `DataTemplate`. Command bound via `RelativeSource AncestorType=TreeView` to reach the TreeView's `DataContext` (MainViewModel).

- Library TreeView templates → `ModifySelectedItemCommand`
- Messages Log TreeView SecsGemItem → `InspectLoggedItemCommand`
