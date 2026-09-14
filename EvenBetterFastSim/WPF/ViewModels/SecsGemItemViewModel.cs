using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvenBetterFastSim.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Data_Containers.Interfaces;
using SecsGemBaseItems.Enums;
using SecsGemBaseItems.LibraryManager;

namespace EvenBetterFastSim.WPF.ViewModels;

public partial class SecsGemItemViewModel : ObservableObject, IBaseViewModel
{
    private const int BinaryDisplayThreshold = 256;

    public Action? CloseAction { get; set; }
    public string HeaderText => "Modify Item";
    public bool ValueVisibility => GetValueVisibility();
    public bool DelimiterVisibility => GetDelimiterVisibility();
    public bool ShowBinaryModeSelector => SecsGemItemCopy.FormatType is SecsGemItemFormatType.Binary;
    public bool TextBoxVisibility => ValueVisibility && (!ShowBinaryModeSelector || IsManualEntry);
    public bool FileEntryVisibility => ShowBinaryModeSelector && !IsManualEntry;
    public bool IsFileUploadMode
    {
        get => !IsManualEntry;
        set => IsManualEntry = !value;
    }
    public string BinaryFileSummary => rawBinaryData is not null
        ? $"{rawBinaryData.Length:N0} bytes binary data loaded"
        : "No file loaded";

    public string BinaryHexError
    {
        get
        {
            if (SecsGemItemCopy.FormatType is not SecsGemItemFormatType.Binary || !IsManualEntry || string.IsNullOrEmpty(ItemValues))
                return string.Empty;
            if (ItemValues.Length % 2 != 0)
                return "Hex string must have an even number of characters.";
            return !ItemValues.All(Uri.IsHexDigit) ? "Invalid characters — only hex digits (0–9, A–F) are allowed." : string.Empty;
        }
    }

    public bool HasBinaryHexError => BinaryHexError.Length > 0;
    public bool ShowBinaryHexError => HasBinaryHexError && submitAttempted;

    public static IEnumerable<SecsGemItemFormatType> ItemFormatTypes =>
        Enum.GetValues<SecsGemItemFormatType>();

    private SaveServiceAggregator ServiceAggregate { get; } = new();
    private ILogger<SecsGemItemViewModel> Logger { get; }
    private readonly SecsGemItem? originalItem;
    private readonly ISecsGemLibraryManager libraryManager;

    [ObservableProperty]
    public partial string Delimiter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ItemValues { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsManualEntry { get; set; } = true;

    private byte[]? rawBinaryData;
    private bool submitAttempted;
    private ApplicationSettings Settings { get; }

    [ObservableProperty]
    public partial SecsGemItem SecsGemItemCopy { get; set; } = SecsGemItem.Create(SecsGemItemFormatType.U1);

    public SecsGemItemViewModel(ApplicationSettings settings, ISecsGemLibraryManager libraryManager, ILogger<SecsGemItemViewModel> logger)
    {
        Logger = logger;
        Settings = settings;
        this.libraryManager = libraryManager;
        if (libraryManager.SelectedItem is SecsGemItem item)
        {
            originalItem = item;
            SecsGemItemCopy = SecsGemItem.Create(item.FormatType);
            Delimiter = Settings.ItemDelimiter;
            ServiceAggregate = new SaveServiceAggregator([new SaveService<SecsGemItem>(SecsGemItemCopy, item)]);
            GetItems();
            SecsGemItemCopy.PropertyChanged += UpdateVisibility;
        }
        else
        {
            Logger.LogError("Selected item is not a SECS/GEM Item");
            CloseAction?.Invoke();
        }
    }

    [RelayCommand(CanExecute = nameof(CanSaveChanges))]
    private void AcceptButtonClick()
    {
        if (HasBinaryHexError)
        {
            submitAttempted = true;
            OnPropertyChanged(nameof(ShowBinaryHexError));
            return;
        }

        Settings.ItemDelimiter = Delimiter;
        EnsureCorrectCopyType();
        CreateItems();

        if (originalItem.GetType() != SecsGemItemCopy.GetType())
        {
            var parent = originalItem.Parent;
            if (parent is IDataItem parentItem)
            {
                var children = parentItem.Children;
                var index = children.IndexOf(originalItem);
                originalItem.SetParent(null);
                SecsGemItemCopy.SetParent(parent);
                if (index >= 0 && index < children.Count - 1)
                    children.Move(children.Count - 1, index);
            }
            else if (libraryManager.SelectedItem == originalItem)
            {
                libraryManager.SelectedItem = SecsGemItemCopy;
            }
        }
        else
        {
            originalItem.CopyFrom(SecsGemItemCopy);
        }

        CloseAction?.Invoke();
    }

    private static bool CanSaveChanges() => true;

    [RelayCommand]
    private void CancelClick()
    {
        CloseAction?.Invoke();
    }

    private void CreateItems()
    {
        string[] values = [string.Empty];
        switch (SecsGemItemCopy.FormatType)
        {
            case SecsGemItemFormatType.List:
                return;
            case SecsGemItemFormatType.Binary:
                if (rawBinaryData is not null)
                {
                    PopulateValuesFromBytes(rawBinaryData);
                    rawBinaryData = null;
                    return;
                }
                var bytes = Convert.FromHexString(ItemValues);
                PopulateValuesFromBytes(bytes);
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

        SecsGemItemCopy.SetValuesFromStrings(values);
    }

    private void GetItems()
    {
        switch (SecsGemItemCopy.FormatType)
        {
            case SecsGemItemFormatType.List:
                return;
            case SecsGemItemFormatType.Binary:
                rawBinaryData = null;
                if (SecsGemItemCopy is SecsGemValueItem<byte> byteItem)
                {
                    if (byteItem.Values.Count > BinaryDisplayThreshold)
                    {
                        rawBinaryData = new byte[byteItem.Values.Count];
                        byteItem.Values.CopyTo(rawBinaryData, 0);
                        ItemValues = $"{rawBinaryData.Length:N0} bytes binary data";
                        IsManualEntry = false;
                    }
                    else
                    {
                        ItemValues = string.Concat(byteItem.Values.Select(b => b.ToString("X2")));
                        IsManualEntry = true;
                    }
                }
                break;
            case SecsGemItemFormatType.ASCII or SecsGemItemFormatType.TwoByteCharacter
                or SecsGemItemFormatType.JIS8:
                ItemValues = SecsGemItemCopy.GetStringValues().FirstOrDefault() ?? string.Empty;
                break;
            default:
                ItemValues = string.Join(Delimiter, SecsGemItemCopy.GetStringValues());
                break;
        }
    }

    [RelayCommand]
    private void UploadFile()
    {
        var dialog = new OpenFileDialog { Title = "Select file to upload as binary" };
        if (dialog.ShowDialog() != true) return;
        var bytes = File.ReadAllBytes(dialog.FileName);
        if (bytes.Length == 0) return;
        if (SecsGemItemCopy.FormatType is SecsGemItemFormatType.Binary)
        {
            rawBinaryData = bytes;
            IsManualEntry = false;
            OnPropertyChanged(nameof(BinaryFileSummary));
            return;
        }
        var hex = Convert.ToHexString(bytes);
        if (bytes.Length == 1)
        {
            ItemValues = hex;
            return;
        }
        var delim = Delimiter;
        var sb = new System.Text.StringBuilder(bytes.Length * (delim.Length + 2) - delim.Length);
        sb.Append(hex[0]);
        sb.Append(hex[1]);
        for (var i = 2; i < hex.Length; i += 2)
        {
            sb.Append(delim);
            sb.Append(hex[i]);
            sb.Append(hex[i + 1]);
        }
        ItemValues = sb.ToString();
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

    private void UpdateVisibility(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SecsGemItem.FormatType))
        {
            submitAttempted = false;
            EnsureCorrectCopyType();
            GetItems();
            OnPropertyChanged(nameof(ValueVisibility));
            OnPropertyChanged(nameof(DelimiterVisibility));
            OnPropertyChanged(nameof(ShowBinaryModeSelector));
            OnPropertyChanged(nameof(TextBoxVisibility));
            OnPropertyChanged(nameof(FileEntryVisibility));
            OnPropertyChanged(nameof(BinaryHexError));
            OnPropertyChanged(nameof(HasBinaryHexError));
            OnPropertyChanged(nameof(ShowBinaryHexError));
            return;
        }
    }

    partial void OnItemValuesChanged(string value)
    {
        OnPropertyChanged(nameof(BinaryHexError));
        OnPropertyChanged(nameof(HasBinaryHexError));
        OnPropertyChanged(nameof(ShowBinaryHexError));
    }

    partial void OnIsManualEntryChanged(bool value)
    {
        switch (value)
        {
            case true when rawBinaryData is not null:
                ItemValues = Convert.ToHexString(rawBinaryData);
                rawBinaryData = null;
                break;
            case false when rawBinaryData is null && !string.IsNullOrEmpty(ItemValues):
                try
                {
                    rawBinaryData = Convert.FromHexString(ItemValues);
                    ItemValues = $"{rawBinaryData.Length:N0} bytes binary data loaded";
                }
                catch
                {
                    ItemValues = string.Empty;
                }

                break;
        }

        submitAttempted = false;
        OnPropertyChanged(nameof(FileEntryVisibility));
        OnPropertyChanged(nameof(TextBoxVisibility));
        OnPropertyChanged(nameof(IsFileUploadMode));
        OnPropertyChanged(nameof(BinaryFileSummary));
        OnPropertyChanged(nameof(BinaryHexError));
        OnPropertyChanged(nameof(HasBinaryHexError));
        OnPropertyChanged(nameof(ShowBinaryHexError));
    }

    private bool GetValueVisibility()
    {
        return SecsGemItemCopy.FormatType is not SecsGemItemFormatType.List;
    }

    private bool GetDelimiterVisibility()
    {
        return SecsGemItemCopy.FormatType is not (SecsGemItemFormatType.List or SecsGemItemFormatType.ASCII
            or SecsGemItemFormatType.TwoByteCharacter
            or SecsGemItemFormatType.JIS8 or SecsGemItemFormatType.Binary);
    }

    private void PopulateValuesFromBytes(byte[] bytes)
    {
        if (SecsGemItemCopy is not SecsGemValueItem<byte> byteItem) return;
        byteItem.Values.Clear();
        foreach (var b in bytes)
            byteItem.Values.Add(b);
    }
}
