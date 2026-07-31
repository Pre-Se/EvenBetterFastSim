using System;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Enums;

namespace EvenBetterFastSim.WPF.ViewModels;

public partial class InspectSecsGemItemViewModel : ObservableObject, IBaseViewModel
{
    private const int BinaryDisplayMaxBytes = 256;

    public Action? CloseAction { get; set; }

    [ObservableProperty]
    private SecsGemItem? item;

    [ObservableProperty]
    private bool useBase64 = false;

    public bool IsHex
    {
        get => !UseBase64;
        set => UseBase64 = !value;
    }

    public bool IsBinary => Item?.FormatType is SecsGemItemFormatType.Binary;
    public bool ValueVisibility => Item?.FormatType is not (null or SecsGemItemFormatType.List);

    public string ValuesDisplay
    {
        get
        {
            if (Item is null) return string.Empty;
            if (Item.FormatType is SecsGemItemFormatType.List) return string.Empty;
            if (Item.FormatType is SecsGemItemFormatType.Binary)
                return UseBase64 ? ToBinaryBase64(Item) : ToBinaryHex(Item);
            return string.Join(" ", Item.GetStringValues());
        }
    }

    partial void OnItemChanged(SecsGemItem? value)
    {
        OnPropertyChanged(nameof(ValuesDisplay));
        OnPropertyChanged(nameof(ValueVisibility));
        OnPropertyChanged(nameof(IsBinary));
    }

    partial void OnUseBase64Changed(bool value)
    {
        OnPropertyChanged(nameof(ValuesDisplay));
        OnPropertyChanged(nameof(IsHex));
    }

    private static string ToBinaryHex(SecsGemItem item)
    {
        var strings = item.GetStringValues().ToList();
        var count = strings.Count;
        if (count == 0) return string.Empty;

        if (count > BinaryDisplayMaxBytes)
        {
            var sb = new StringBuilder((BinaryDisplayMaxBytes + 3) * 5);
            sb.Append("0x");
            sb.Append(strings[0]);
            for (var i = 1; i < BinaryDisplayMaxBytes; i++)
            {
                sb.Append(" 0x");
                sb.Append(strings[i]);
            }
            sb.Append($" ... ({count:N0} bytes total)");
            return sb.ToString();
        }

        var sb2 = new StringBuilder(count * 5);
        sb2.Append("0x");
        sb2.Append(strings[0]);
        for (var i = 1; i < count; i++)
        {
            sb2.Append(" 0x");
            sb2.Append(strings[i]);
        }
        return sb2.ToString();
    }

    private static string ToBinaryBase64(SecsGemItem item)
    {
        var strings = item.GetStringValues().ToList();
        var hex = string.Concat(strings);
        var bytes = Convert.FromHexString(hex);
        if (bytes.Length > BinaryDisplayMaxBytes)
        {
            var preview = Convert.ToBase64String(bytes, 0, BinaryDisplayMaxBytes);
            return preview + $" ... ({bytes.Length:N0} bytes total)";
        }
        return Convert.ToBase64String(bytes);
    }

    [RelayCommand]
    private void DownloadItem()
    {
        if (Item is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "Save binary data",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            Filter = "Binary Files (*.bin)|*.bin|All Files (*.*)|*.*",
            DefaultExt = "bin",
            AddExtension = true,
            OverwritePrompt = true,
            FileName = Item.Name ?? "data"
        };
        if (dialog.ShowDialog() != true) return;
        var strings = Item.GetStringValues().ToList();
        var hex = string.Concat(strings);
        var bytes = Convert.FromHexString(hex);
        File.WriteAllBytes(dialog.FileName, bytes);
    }

    [RelayCommand]
    private void AcceptButtonClick() => CloseAction?.Invoke();
}
