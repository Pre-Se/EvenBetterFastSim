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
}
