using System;
using System.Collections.Generic;
using System.Windows;
using EvenBetterFastSim.Window_Helpers;
using EvenBetterFastSim.WPF.ViewModels;
using EvenBetterFastSim.WPF.Windows;

namespace EvenBetterFastSim.Services;

/// <summary>
/// Maps all <see cref="IBaseViewModel"/> to its corresponding <see cref="Window"/>
/// </summary>
public class WindowMapper
{
    private readonly Dictionary<Type, Type> mappings = [];

    public WindowMapper()
    {
        RegisterMapping<MainViewModel, MainWindow>();
        RegisterMapping<SetUpViewModel, DialogWindow>();
        RegisterMapping<SecsGemDataMessageViewModel, DialogWindow>();
        RegisterMapping<SecsGemItemViewModel, DialogWindow>();
        RegisterMapping<AddEventReportViewModel, DialogWindow>();
        RegisterMapping<AddReportViewModel, DialogWindow>();
        RegisterMapping<AddEquipmentVariableViewModel, DialogWindow>();
        RegisterMapping<InspectSecsGemItemViewModel, DialogWindow>();
        RegisterMapping<SecsGemTransactionViewModel, DialogWindow>();
    }

    /// <summary>
    /// Registers a mapping between a <see cref="IBaseViewModel"/> and a <see cref="Window"/>
    /// </summary>
    public void RegisterMapping<TViewModel, TWindow>() where TViewModel : IBaseViewModel where TWindow : Window
    {
        mappings[typeof(TViewModel)] = typeof(TWindow);
    }

    /// <summary>
    /// Gets the <see cref="Window"/> type for the given <see cref="IBaseViewModel"/> type
    /// </summary>
    public Type? GetWindowTypeForViewModel(IBaseViewModel viewModel)
    {
        mappings.TryGetValue(viewModel.GetType(), out var windowType);
        return windowType;
    }
}