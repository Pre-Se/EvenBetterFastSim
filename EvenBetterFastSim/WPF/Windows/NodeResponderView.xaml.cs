using System;
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
        if (sender is not TreeView tree) return;

        if (e.Key == Key.Tab)
        {
            if (FindAncestor<TextBox>(e.OriginalSource as DependencyObject) is { } textBox
                && MoveFocusToNextTextBox(tree, textBox, (Keyboard.Modifiers & ModifierKeys.Shift) != 0))
            {
                e.Handled = true;
            }
            return;
        }

        if (e.Key is not (Key.Up or Key.Down or Key.Left or Key.Right)) return;

        var combo = FindAncestor<ComboBox>(e.OriginalSource as DependencyObject);
        if (combo is null || combo.IsDropDownOpen) return;

        e.Handled = true;
        MoveTreeSelection(tree, e.Key);
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = VisualTreeHelper.GetParent(source) ?? LogicalTreeHelper.GetParent(source);
        }
        return null;
    }

    /// <summary>
    /// Moves the selection by re-issuing the arrow key with the selected TreeViewItem focused, so the
    /// TreeView's own keyboard navigation handles it — including virtualization (unrealized rows are
    /// realized and scrolled into view) and Left/Right expand/collapse semantics.
    /// </summary>
    private static void MoveTreeSelection(TreeView tree, Key key)
    {
        if (tree.SelectedItem is null) return;

        var container = FindContainer(tree, tree.SelectedItem);
        if (container is null) return;

        container.Focus();

        if (PresentationSource.FromVisual(tree) is not { } source) return;

        var keyEventArgs = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
        InputManager.Current.ProcessInput(keyEventArgs);
    }

    private static bool MoveFocusToNextTextBox(TreeView tree, TextBox current, bool backwards)
    {
        var textBoxes = new List<TextBox>();
        CollectVisibleTextBoxes(tree, textBoxes);
        textBoxes.Sort((a, b) =>
            a.TranslatePoint(new Point(0, 0), tree).Y.CompareTo(b.TranslatePoint(new Point(0, 0), tree).Y));

        var index = textBoxes.IndexOf(current);
        if (index < 0) return false;

        var targetIndex = index + (backwards ? -1 : 1);
        if (targetIndex < 0 || targetIndex >= textBoxes.Count) return false;

        var next = textBoxes[targetIndex];
        next.Focus();
        next.SelectAll();
        next.BringIntoView();
        return true;
    }

    private static void CollectVisibleTextBoxes(DependencyObject root, List<TextBox> result)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is TextBox textBox && textBox.IsVisible && textBox.IsEnabled && textBox.Focusable)
            {
                result.Add(textBox);
                continue;
            }
            CollectVisibleTextBoxes(child, result);
        }
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
