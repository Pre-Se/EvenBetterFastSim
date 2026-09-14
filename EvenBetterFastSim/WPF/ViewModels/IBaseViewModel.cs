using System;
using System.ComponentModel;

namespace EvenBetterFastSim.WPF.ViewModels;

public interface IBaseViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// Enables the view model to close the view
    /// </summary>
    public Action? CloseAction { get; set; }
}