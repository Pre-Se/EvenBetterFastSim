using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using EvenBetterFastSim.WPF.ViewModels.Responders;

namespace EvenBetterFastSim.WPF.Windows;

public partial class NodeResponderView : UserControl
{
    public NodeResponderView()
    {
        InitializeComponent();
    }

    private void FieldTree_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right)) return;
        if (sender is not TreeView tree) return;

        var combo = FindComboBoxAncestor(e.OriginalSource as DependencyObject);
        if (combo is null || combo.IsDropDownOpen) return;

        e.Handled = true;
        MoveTreeSelection(tree, e.Key);
    }

    private static ComboBox? FindComboBoxAncestor(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ComboBox combo) return combo;
            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }
        return null;
    }

    private static void MoveTreeSelection(TreeView tree, Key key)
    {
        if (tree.SelectedItem is not ResponderFieldViewModel selected) return;

        var flat = new List<ResponderFieldViewModel>();
        var parents = new Dictionary<ResponderFieldViewModel, ResponderFieldViewModel?>();
        foreach (var root in (IEnumerable<ResponderFieldViewModel>?)tree.ItemsSource ?? [])
            Flatten(root, null, flat, parents);

        var index = flat.IndexOf(selected);
        if (index < 0) return;

        var container = FindContainer(tree, selected);

        switch (key)
        {
            case Key.Up:
                SelectItem(tree, flat, index - 1);
                break;
            case Key.Down:
                SelectItem(tree, flat, index + 1);
                break;
            case Key.Left:
                if (container is { HasItems: true, IsExpanded: true })
                {
                    container.IsExpanded = false;
                    return;
                }
                if (parents.TryGetValue(selected, out var parent) && parent is not null)
                    SelectItem(tree, flat, flat.IndexOf(parent));
                break;
            case Key.Right:
                if (container is { HasItems: true } && !container.IsExpanded)
                {
                    container.IsExpanded = true;
                    return;
                }
                SelectItem(tree, flat, index + 1);
                break;
        }
    }

    private static void SelectItem(TreeView tree, List<ResponderFieldViewModel> flat, int index)
    {
        if (index < 0 || index >= flat.Count) return;

        if (FindContainer(tree, flat[index]) is { } container)
        {
            container.IsSelected = true;
            container.Focus();
            container.BringIntoView();
        }
    }

    private static void Flatten(
        ResponderFieldViewModel node,
        ResponderFieldViewModel? parent,
        List<ResponderFieldViewModel> flat,
        Dictionary<ResponderFieldViewModel, ResponderFieldViewModel?> parents)
    {
        flat.Add(node);
        parents[node] = parent;
        foreach (var child in node.Children)
            Flatten(child, node, flat, parents);
    }

    private static TreeViewItem? FindContainer(ItemsControl parent, object item)
    {
        if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
            return tvi;

        for (var i = 0; i < parent.Items.Count; i++)
        {
            if (parent.ItemContainerGenerator.ContainerFromIndex(i) is not TreeViewItem child) continue;

            child.UpdateLayout();
            var found = FindContainer(child, item);
            if (found is not null) return found;
        }
        return null;
    }
}
