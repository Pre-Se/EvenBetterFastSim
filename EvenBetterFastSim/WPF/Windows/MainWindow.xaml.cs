using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EvenBetterFastSim.WPF.ViewModels;
using EvenBetterFastSim.WPF.ViewModels.Graph;
using SecsGemBaseItems.Data_Containers;
using Wpf.Ui.Controls;

namespace EvenBetterFastSim.WPF.Windows;

internal record ScenarioDragData(SecsGemTransaction Transaction, bool IsPrimary);

public partial class MainWindow
{
    public MainWindow()
    {
        InitializeComponent();
        Closing += (o, i) =>
        {
            if (DataContext is MainViewModel viewModel)
                viewModel.OnClosing();
        };

        MessageLibraryTreeView.PreviewKeyDown += OnMessageDeleteKey;
        EventCollectionTreeView.PreviewKeyDown += OnEventDeleteKey;
        ReportCollectionTreeView.PreviewKeyDown += OnReportDeleteKey;
        VariableCollectionTreeView.PreviewKeyDown += OnVariableDeleteKey;

        // Open a scenario node's editor with a single right-click or a left double-click.
        // Registered handledEventsToo so Nodify's own right-click / selection handling doesn't swallow it.
        ScenarioCanvas.AddHandler(MouseRightButtonDownEvent,
            new MouseButtonEventHandler(ScenarioNode_RightButtonDown), handledEventsToo: true);
        ScenarioCanvas.AddHandler(MouseRightButtonUpEvent,
            new MouseButtonEventHandler(ScenarioNode_RightButtonUp), handledEventsToo: true);
        ScenarioCanvas.AddHandler(System.Windows.Controls.Control.MouseDoubleClickEvent,
            new MouseButtonEventHandler(ScenarioNode_MouseDoubleClick), handledEventsToo: true);
    }

    private Point scenarioRightDownPoint;

    private void ScenarioNode_RightButtonDown(object sender, MouseButtonEventArgs e) =>
        scenarioRightDownPoint = e.GetPosition(ScenarioCanvas);

    private void ScenarioNode_RightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Ignore the release that ends a right-drag pan.
        if ((e.GetPosition(ScenarioCanvas) - scenarioRightDownPoint).Length > 6) return;
        if (OpenNodeEditorFor(e.OriginalSource))
            e.Handled = true;
    }

    private void ScenarioNode_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (OpenNodeEditorFor(e.OriginalSource))
            e.Handled = true;
    }

    private bool OpenNodeEditorFor(object originalSource)
    {
        if (FindNodeViewModel(originalSource as DependencyObject) is not { } node) return false;
        if (DataContext is not MainViewModel vm) return false;

        var command = vm.ScenariosVm.OpenNodeEditorCommand;
        if (!command.CanExecute(node)) return false;
        command.Execute(node);
        return true;
    }

    private static ScenarioNodeViewModel? FindNodeViewModel(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: ScenarioNodeViewModel node })
                return node;
            source = source is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }
        return null;
    }

    private void OnMessageDeleteKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || DataContext is not MainViewModel vm) return;
        if (!vm.DeleteSelectedItemCommand.CanExecute(null)) return;
        vm.DeleteSelectedItemCommand.Execute(null);
        e.Handled = true;
        Dispatcher.BeginInvoke(() => MessageLibraryTreeView.Focus(), DispatcherPriority.Background);
    }

    private void OnEventDeleteKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || DataContext is not MainViewModel vm) return;
        if (!vm.DeleteEventCommand.CanExecute(null)) return;
        vm.DeleteEventCommand.Execute(null);
        e.Handled = true;
        Dispatcher.BeginInvoke(() => EventCollectionTreeView.Focus(), DispatcherPriority.Background);
    }

    private void OnReportDeleteKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || DataContext is not MainViewModel vm) return;
        if (!vm.DeleteReportCommand.CanExecute(null)) return;
        vm.DeleteReportCommand.Execute(null);
        e.Handled = true;
        Dispatcher.BeginInvoke(() => ReportCollectionTreeView.Focus(), DispatcherPriority.Background);
    }

    private void OnVariableDeleteKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete || DataContext is not MainViewModel vm) return;
        if (!vm.DeleteVariableCommand.CanExecute(null)) return;
        vm.DeleteVariableCommand.Execute(null);
        e.Handled = true;
        Dispatcher.BeginInvoke(() => VariableCollectionTreeView.Focus(), DispatcherPriority.Background);
    }

    private Point dragStartPoint;
    private bool dragStarted;

    private void TransactionTree_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var tvi = FindTreeViewItem(e.OriginalSource as DependencyObject);
        if (tvi == null) return;

        var currentPos = e.GetPosition(null);
        if (!dragStarted)
        {
            dragStartPoint = currentPos;
            dragStarted = true;
            return;
        }

        if (Math.Abs(currentPos.X - dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(currentPos.Y - dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        dragStarted = false;

        var dragData = tvi.DataContext switch
        {
            SecsGemTransaction tx => new ScenarioDragData(tx, IsPrimary: true),
            SecsGemDataMessage msg => BuildDragDataFromMessage(tvi, msg),
            _ => null
        };

        if (dragData != null)
            DragDrop.DoDragDrop(tvi, dragData, DragDropEffects.Copy);
    }

    private static ScenarioDragData? BuildDragDataFromMessage(
        System.Windows.Controls.TreeViewItem tvi, SecsGemDataMessage message)
    {
        var parentTvi = FindParentTreeViewItem(tvi);
        if (parentTvi?.DataContext is not SecsGemTransaction parentTx) return null;
        var isPrimary = ReferenceEquals(parentTx.PrimaryMessage, message);
        return new ScenarioDragData(parentTx, isPrimary);
    }

    private static System.Windows.Controls.TreeViewItem? FindTreeViewItem(DependencyObject? element)
    {
        while (element != null)
        {
            if (element is System.Windows.Controls.TreeViewItem tvi)
                return tvi;
            element = VisualTreeHelper.GetParent(element);
        }
        return null;
    }

    private static System.Windows.Controls.TreeViewItem? FindParentTreeViewItem(DependencyObject element)
    {
        var parent = VisualTreeHelper.GetParent(element);
        while (parent != null)
        {
            if (parent is System.Windows.Controls.TreeViewItem tvi)
                return tvi;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    private void ScenarioCanvas_PreviewDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ScenarioDragData))) return;
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void ScenarioCanvas_PreviewDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ScenarioDragData))) return;
        if (DataContext is not MainViewModel vm) return;

        var dragData = (ScenarioDragData)e.Data.GetData(typeof(ScenarioDragData))!;
        var position = e.GetPosition(ScenarioCanvas);

        var transform = ScenarioCanvas.ViewportTransform;
        if (transform != null)
            position = transform.Inverse.Transform(position);

        vm.ScenariosVm.AddNodeFromDrop(dragData.Transaction, position, dragData.IsPrimary);
        e.Handled = true;
    }
}
