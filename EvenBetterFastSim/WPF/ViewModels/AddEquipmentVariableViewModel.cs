using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvenBetterFastSim.Services;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Enums;
using SecsGemMessageHandling.Events.Models;
using SecsGemMessageHandling.Events.Registry.Interface;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace EvenBetterFastSim.WPF.ViewModels;
public partial class AddEquipmentVariableViewModel : ObservableObject, IBaseViewModel
{
    private readonly IRegistry<SecsGemEquipmentVariable> variableRegistry;
    private SecsGemEquipmentVariable? originalItem;
    public Action? CloseAction { get; set; }
    public string HeaderText => "Add Variable";
    public bool ValueVisibility => GetValueVisibility();
    public bool DelimiterVisibility => GetDelimiterVisibility();

    public static IEnumerable<SecsGemItemFormatType> ItemFormatTypes =>
        Enum.GetValues<SecsGemItemFormatType>();

    public string Delimiter {  get; set; }
    public string ItemValues { get; set; } = string.Empty;
    private ApplicationSettings Settings { get; }
    public int Id { get; set; }
    [ObservableProperty] private SecsGemItem secsGemItemCopy = SecsGemItem.Create(SecsGemItemFormatType.U4);

    public AddEquipmentVariableViewModel(ApplicationSettings settings, IRegistry<SecsGemEquipmentVariable> variableRegistry)
    {
        this.variableRegistry = variableRegistry;
        Settings = settings;

        Delimiter = Settings.ItemDelimiter;
        GetItems();
        secsGemItemCopy.PropertyChanged += UpdateVisibility;
    }

    public void LoadForEdit(SecsGemEquipmentVariable item)
    {
        originalItem = item;
        Id = item.VariableId;
        var oldCopy = SecsGemItemCopy;
        SecsGemItemCopy = item.Item.Clone();
        oldCopy.PropertyChanged -= UpdateVisibility;
        SecsGemItemCopy.PropertyChanged += UpdateVisibility;
        GetItems();
    }

    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private void AcceptButtonClick()
    {
        SaveItem();
        Settings.ItemDelimiter = Delimiter;
        CloseAction?.Invoke();
    }

    private static bool CanSaveChanges()
    {
        return true;
    }

    [RelayCommand]
    private void CancelClick()
    {
        CloseAction?.Invoke();
    }

    private void SaveItem()
    {
        string[] values = [string.Empty];
        switch (SecsGemItemCopy.FormatType)
        {
            case SecsGemItemFormatType.List:
                return;
            case SecsGemItemFormatType.ASCII or SecsGemItemFormatType.TwoByteCharacter
                or SecsGemItemFormatType.JIS8:
                values[0] = ItemValues;
                break;
            default:
                values = ItemValues.Split(Delimiter);
                break;
        }

        if (values.Length == 0) return;

        EnsureCorrectCopyType();
        SecsGemItemCopy.SetValuesFromStrings(values);

        var newVariable = new SecsGemEquipmentVariable
        {
            VariableId = Id,
            Item = SecsGemItemCopy
        };

        if (originalItem != null)
            variableRegistry.Update(newVariable, originalItem);
        else
            variableRegistry.Add(newVariable);
    }

    private void EnsureCorrectCopyType()
    {
        var expectedType = SecsGemItem.Create(SecsGemItemCopy.FormatType).GetType();
        if (SecsGemItemCopy.GetType() != expectedType)
        {
            var oldCopy = SecsGemItemCopy;
            var newCopy = SecsGemItem.Create(SecsGemItemCopy.FormatType);
            newCopy.Description = SecsGemItemCopy.Description;
            SecsGemItemCopy = newCopy;
            oldCopy.PropertyChanged -= UpdateVisibility;
            SecsGemItemCopy.PropertyChanged += UpdateVisibility;
        }
    }

    private void GetItems()
    {
        switch (SecsGemItemCopy.FormatType)
        {
            case SecsGemItemFormatType.List:
                return;
            case SecsGemItemFormatType.ASCII or SecsGemItemFormatType.TwoByteCharacter
                or SecsGemItemFormatType.JIS8:
                ItemValues = SecsGemItemCopy.GetStringValues().FirstOrDefault() ?? string.Empty;
                break;
            default:
                ItemValues = string.Join(Delimiter, SecsGemItemCopy.GetStringValues());
                break;
        }
    }

    private void UpdateVisibility(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SecsGemItem.FormatType)) return;
        EnsureCorrectCopyType();
        GetItems();
        OnPropertyChanged(nameof(ValueVisibility));
        OnPropertyChanged(nameof(DelimiterVisibility));
    }

    private bool GetValueVisibility()
    {
        return SecsGemItemCopy.FormatType is not SecsGemItemFormatType.List;
    }

    private bool GetDelimiterVisibility()
    {
        return SecsGemItemCopy.FormatType is not (SecsGemItemFormatType.List or SecsGemItemFormatType.ASCII
            or SecsGemItemFormatType.TwoByteCharacter
            or SecsGemItemFormatType.JIS8);
    }
}
