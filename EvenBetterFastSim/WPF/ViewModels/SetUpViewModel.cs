using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvenBetterFastSim.Services;
using SecsGemBaseItems.SecsGemParameters;
using TCPIPBaseLibrary.Interfaces;
using NetworkSettings = TCPIPBaseLibrary.Network.NetworkSettings;


namespace EvenBetterFastSim.WPF.ViewModels;

public partial class SetUpViewModel : ObservableValidator, IBaseViewModel
{
    private readonly SaveServiceAggregator serviceAggregate;

    /// <summary>
    /// Copy of the <see cref="INetworkSettings"/> to be edited, used so that the values can be reverted if the user cancels the edit
    /// </summary>
    [ObservableProperty]
    private INetworkSettings networkSettingsCopy = new NetworkSettings();

    [ObservableProperty]
    private IHSMSParameters hsmsParametersCopy = new HSMSParameters();
    public Action? CloseAction { get; set; }

    /// <summary>
    /// Cast of the <see cref="ConnectionMode"/> enum to an IEnumerable for use in the ComboBox values
    /// </summary>
    public static IEnumerable<ConnectionMode> ConnectionTypeValues =>
        Enum.GetValues(typeof(ConnectionMode))
            .Cast<ConnectionMode>();


    public SetUpViewModel(INetworkSettings networkSettings, IHSMSParameters hsmsParameters)
    {
        NetworkSettingsCopy.ErrorsChanged += (o, i) => { AcceptButtonClickCommand.NotifyCanExecuteChanged();};

        serviceAggregate = new SaveServiceAggregator(
        [
            new SaveService<INetworkSettings>(NetworkSettingsCopy, networkSettings),
            new SaveService<IHSMSParameters>(HsmsParametersCopy, hsmsParameters)
        ]);
    }

    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private void AcceptButtonClick()
    {
        serviceAggregate.Save();
        CloseAction?.Invoke();
    }

    private bool CanSaveChanges()
    {
        return !NetworkSettingsCopy.HasErrors;
    }

    [RelayCommand]
    private void CancelClick()
    {
        CloseAction?.Invoke();
    }
}