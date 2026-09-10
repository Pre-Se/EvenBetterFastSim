using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvenBetterFastSim.Services;
using Microsoft.Extensions.Logging;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.LibraryManager;
using System;

namespace EvenBetterFastSim.WPF.ViewModels;
public partial class SecsGemDataMessageViewModel : ObservableObject, IBaseViewModel
{
    public Action? CloseAction { get; set; }
    public string HeaderText => "Modify Data Message";

    /// <summary>
    /// Copy of the <see cref="SecsGemDataMessage"/> to be edited, used so that the values can be reverted if the user cancels the edit
    /// </summary>
    [ObservableProperty]
    private SecsGemDataMessage dataMessageCopy = new();
    private SaveServiceAggregator ServiceAggregate { get; } = new();
    private ILogger<SecsGemDataMessageViewModel> Logger { get; }

    public SecsGemDataMessageViewModel(ISecsGemLibraryManager libraryManager, ILogger<SecsGemDataMessageViewModel> logger)
    {
        Logger = logger;
        if (libraryManager.SelectedItem is SecsGemDataMessage message)
        {
            ServiceAggregate = new SaveServiceAggregator([new SaveService<SecsGemDataMessage>(DataMessageCopy, message)
            ]);
        }
        else
        {
            Logger.LogError("Selected item is not a SECS/GEM DataMessage");
            CloseAction?.Invoke();
        }
    }
    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private void AcceptButtonClick()
    {
        ServiceAggregate.Save();
        CloseAction?.Invoke();
    }

    private bool CanSaveChanges()
    {
        return true;
    }

    [RelayCommand]
    private void CancelClick()
    {
        CloseAction?.Invoke();
    }
}
