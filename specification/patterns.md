# Patterns and Conventions

Related docs: [dialog_pattern.md](dialog_pattern.md), [secsgembase_library.md](secsgembase_library.md), [SESSION_REFACTORING_LOG.md](../SESSION_REFACTORING_LOG.md), [CLAUDE.md](../CLAUDE.md)

## No MemberwiseClone

Use `new T() + CopyFrom() + recurse children` for all `Clone()` methods. MemberwiseClone copies the entire `PropertyChanged` invocation list — clones fire `SetName`/`SetHeader` on the original, so Name/Header on the clone never updates.

## No collection replacement after construction

Never assign a new collection to `Values` or `Children`. Always mutate in-place (Clear + Add each). The constructor subscribes `CollectionChanged` on the original instance — a replacement collection has no subscribers.

## No LibraryManager.SelectedItem in VM constructors

Resolve the VM from DI, then pass the item after: `vm.Item = item` or `vm.Initialize(item)`. Shared mutable state in DI is unreliable — the item can change between registration and use.

## No IRelayCommand on ICanBeParent

Exposes plain methods only. Commands hold delegates bound to a specific instance and silently execute on the original after cloning.

## No SaveServiceAggregator in dialog VMs

Write directly to the item on Accept. Reserve the save service pattern for the settings flow where it's already established.
