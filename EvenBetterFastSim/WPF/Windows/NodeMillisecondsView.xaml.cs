using System.Windows.Controls;

namespace EvenBetterFastSim.WPF.Windows;

/// <summary>
/// Interaction logic for NodeMillisecondsView.xaml — editor content for a node parameter in milliseconds,
/// hosted inside the shared <c>DialogWindow</c>.
/// </summary>
public partial class NodeMillisecondsView : UserControl
{
    public NodeMillisecondsView()
    {
        InitializeComponent();
        // Ready to type: the value is selected, Enter confirms (DialogWindow).
        Loaded += (_, _) =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        };
    }
}
