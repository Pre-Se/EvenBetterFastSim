using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EvenBetterFastSim.WPF.ViewModels.Graph;

public partial class PendingConnectionViewModel : ObservableObject
{
    private ConnectorViewModel? source;

    public ICommand StartCommand { get; }
    public ICommand FinishCommand { get; }

    public PendingConnectionViewModel()
    {
        StartCommand = new RelayCommand<ConnectorViewModel?>(source => this.source = source);
        FinishCommand = new RelayCommand<ConnectorViewModel?>(target =>
        {
            if (source != null && target != null && target != source)
                ConnectionCompleted?.Invoke(source, target);
            source = null;
        });
    }

    public event Action<ConnectorViewModel, ConnectorViewModel>? ConnectionCompleted;
}
