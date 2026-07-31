using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using EvenBetterFastSim.WPF.ViewModels;
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
