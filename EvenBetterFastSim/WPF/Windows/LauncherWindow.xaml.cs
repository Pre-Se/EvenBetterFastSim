using System.Windows;
using System.Windows.Input;
using EvenBetterFastSim.WPF.ViewModels;
using Wpf.Ui.Controls;

namespace EvenBetterFastSim.WPF.Windows;

/// <summary>
/// Interaction logic for LauncherWindow.xaml — the hub shown when the app starts with no
/// <c>--profile</c> argument.
/// </summary>
public partial class LauncherWindow : FluentWindow
{
    public LauncherWindow()
    {
        InitializeComponent();
    }

    // Launch on double-click handled per-item: a MouseBinding on the ListBox is unreliable because
    // ListBoxItem marks the first click handled for selection.
    private void InstanceItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (sender is not FrameworkElement { DataContext: Services.InstanceProfile } ) return;
        if (DataContext is LauncherViewModel vm && vm.LaunchCommand.CanExecute(null))
            vm.LaunchCommand.Execute(null);
    }
}
