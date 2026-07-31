using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows;

namespace EvenBetterFastSim.Helpers;

public class EventToCommandAdaptor
{
    public static readonly DependencyProperty TreeViewSelectedItemChangedCommandDpProp =
        DependencyProperty.RegisterAttached(
            "TreeViewSelectedItemChangedCommand",
            typeof(ICommand),
            typeof(EventToCommandAdaptor), // owner type
            new PropertyMetadata(new PropertyChangedCallback(AttachOrRemoveTreeViewSelectedItemChangedEvent))
        );

    public static readonly DependencyProperty TreeViewSelectedItemProperty =
        DependencyProperty.RegisterAttached(
            "TreeViewSelectedItem",
            typeof(object),
            typeof(EventToCommandAdaptor),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTreeViewSelectedItemChanged));

    private static readonly HashSet<TreeView> SubscribedTreeViews = [];
    private static bool _updatingSelection;

    public static void SetTreeViewSelectedItemChangedCommand(DependencyObject obj, ICommand value)
    {
        obj.SetValue(TreeViewSelectedItemChangedCommandDpProp, value);
    }

    public static object GetTreeViewSelectedItem(DependencyObject d)
        => d.GetValue(TreeViewSelectedItemProperty);

    public static void SetTreeViewSelectedItem(DependencyObject d, object value)
        => d.SetValue(TreeViewSelectedItemProperty, value);

    public static void AttachOrRemoveTreeViewSelectedItemChangedEvent(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        if (obj is not TreeView treeview) return;

        if (args.OldValue == null && args.NewValue != null)
        {
            treeview.SelectedItemChanged += ExecuteTreeViewSelectedItemChanged;
        }
        else if (args.OldValue != null && args.NewValue == null)
        {
            treeview.SelectedItemChanged -= ExecuteTreeViewSelectedItemChanged;
        }
    }

    private static void OnTreeViewSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TreeView treeView) return;
        if (SubscribedTreeViews.Add(treeView))
            treeView.SelectedItemChanged += TreeView_SelectedItemChanged;
        if (_updatingSelection) return;
        if (e.NewValue == null || Equals(e.NewValue, treeView.SelectedItem)) return;
        treeView.UpdateLayout();
        var tvi = FindTreeViewItemContainer(treeView, e.NewValue);
        if (tvi == null) return;
        _updatingSelection = true;
        tvi.IsSelected = true;
        _updatingSelection = false;
    }

    private static TreeViewItem? FindTreeViewItemContainer(ItemsControl parent, object item)
    {
        if (parent.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
            return tvi;
        for (var i = 0; i < parent.Items.Count; i++)
        {
            if (parent.ItemContainerGenerator.ContainerFromIndex(i) is TreeViewItem child)
            {
                child.UpdateLayout();
                var found = FindTreeViewItemContainer(child, item);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        var treeView = (TreeView)sender;
        _updatingSelection = true;
        SetTreeViewSelectedItem(treeView, e.NewValue);
        _updatingSelection = false;
    }

    private static void ExecuteTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> args)
    {
        if (sender is not DependencyObject obj) return;
        if (obj.GetValue(TreeViewSelectedItemChangedCommandDpProp) is not ICommand cmd) return;

        if (cmd.CanExecute(args.NewValue))
        {
            cmd.Execute(args.NewValue);
        }
    }
}