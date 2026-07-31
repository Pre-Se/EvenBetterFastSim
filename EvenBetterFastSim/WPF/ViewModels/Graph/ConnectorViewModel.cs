using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace EvenBetterFastSim.WPF.ViewModels.Graph;

public class ConnectorViewModel : INotifyPropertyChanged
{
    private Point anchor;
    private bool isConnected;
    private string title = string.Empty;

    public Point Anchor
    {
        get => anchor;
        set { anchor = value; OnPropertyChanged(); }
    }

    public bool IsConnected
    {
        get => isConnected;
        set { isConnected = value; OnPropertyChanged(); }
    }

    public string Title
    {
        get => title;
        set { title = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
