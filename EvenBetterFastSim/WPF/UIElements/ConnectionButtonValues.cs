using System.Collections.Generic;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using SecsGemMessageHandling.Enums;

namespace EvenBetterFastSim.WPF.UIElements;

public partial class ConnectionButtonValues : ObservableObject
{
    public static Dictionary<ConnectionStatus, ConnectionButtonValues> ConnectionButtonValuesMap { get; }
    static ConnectionButtonValues()
    {
        ConnectionButtonValuesMap = new Dictionary<ConnectionStatus, ConnectionButtonValues>
        {
            [ConnectionStatus.PortClosed] = new(){BackgroundColor = Brushes.Red, ActionText = "Connect", Text = "Port Closed"},
            [ConnectionStatus.PortOpen] = new(){BackgroundColor = Brushes.Yellow, ActionText = "Disconnect", Text = "Port Open"},
            [ConnectionStatus.Connected] = new(){BackgroundColor = Brushes.Green, ActionText = "Disconnect", Text = "Connected"}
        };
        ConnectionButtonValuesMap.AsReadOnly();
    }


    [ObservableProperty]
    private SolidColorBrush backgroundColor = new();
    [ObservableProperty]
    private string text = string.Empty;
    [ObservableProperty]
    private string actionText = string.Empty;
}