using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.LibraryManager;
using System;

namespace EvenBetterFastSim.WPF.ViewModels;

public partial class SecsGemTransactionViewModel : ObservableObject, IBaseViewModel
{
    public Action? CloseAction { get; set; }

    [ObservableProperty]
    private SecsGemTransaction transactionCopy = new();

    private SecsGemTransaction? transaction;
    private ILogger<SecsGemTransactionViewModel> Logger { get; }

    public SecsGemTransactionViewModel(ISecsGemLibraryManager libraryManager, ILogger<SecsGemTransactionViewModel> logger)
    {
        Logger = logger;
        if (libraryManager.SelectedItem is SecsGemTransaction t)
        {
            transaction = t;
            TransactionCopy.Name = t.Name;
            TransactionCopy.Description = t.Description;
        }
        else
        {
            Logger.LogError("Selected item is not a SECS/GEM Transaction");
            CloseAction?.Invoke();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private void AcceptButtonClick()
    {
        if (transaction is not null)
        {
            transaction.Name = TransactionCopy.Name;
            transaction.Description = TransactionCopy.Description;
        }
        CloseAction?.Invoke();
    }

    private static bool CanSaveChanges() => true;

    [RelayCommand]
    private void CancelClick() => CloseAction?.Invoke();
}
