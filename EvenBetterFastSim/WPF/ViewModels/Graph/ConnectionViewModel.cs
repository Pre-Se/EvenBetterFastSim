using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EvenBetterFastSim.WPF.ViewModels.Graph;

public class ConnectionViewModel : INotifyPropertyChanged
{
    private ConnectorViewModel? source;
    private ConnectorViewModel? target;
    private bool isSelected;

    public ConnectorViewModel? Source
    {
        get => source;
        set { source = value; OnPropertyChanged(); }
    }

    public ConnectorViewModel? Target
    {
        get => target;
        set { target = value; OnPropertyChanged(); }
    }

    public bool IsSelected
    {
        get => isSelected;
        set { isSelected = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
