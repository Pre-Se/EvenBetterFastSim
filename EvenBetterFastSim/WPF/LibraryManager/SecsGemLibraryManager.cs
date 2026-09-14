using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using EvenBetterFastSim.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.LibraryManager;
using SecsGemBaseItems;
using SecsGemBaseItems.Data_Containers.Interfaces;

namespace EvenBetterFastSim.WPF.LibraryManager;

/// <summary>
/// Manages the SECS/GEM library
/// </summary>
internal partial class SecsGemLibraryManager : ObservableObject, ISecsGemLibraryManager
{
    public const string Section = "DefaultLibraryLoadPath";
    /// <summary>
    /// Collection of items in the library
    /// </summary>
    public ObservableCollection<SecsGemTransaction> Library { get; } = [];

    /// <summary>
    /// Currently selected item in the library
    /// </summary>
    [ObservableProperty]
    private IDataItem? selectedItem;
    private ILogger<SecsGemLibraryManager> Logger { get; }
    private ApplicationSettings Settings { get; }
    private LibraryJsonService JsonService { get; }
    private LibraryMessagePackService MessagePackService { get; }

    public SecsGemLibraryManager(
        ApplicationSettings settings,
        LibraryJsonService jsonService,
        LibraryMessagePackService messagePackService,
        ILogger<SecsGemLibraryManager> logger)
    {
        Logger = logger;
        Settings = settings;
        JsonService = jsonService;
        MessagePackService = messagePackService;
        if (!string.IsNullOrEmpty(settings.DefaultLibraryLoadPath))
        {
            try
            {
                LoadLibrary(settings.DefaultLibraryLoadPath);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load library from '{Path}' — starting with empty library", settings.DefaultLibraryLoadPath);
            }
        }
    }

    /// <summary>
    /// Opens a file dialog to select a new library file, old library is cleared
    /// </summary>
    public void OpenLibrary()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select library file",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && dialog.FileName != string.Empty)
        {
            Settings.DefaultLibraryLoadPath = dialog.FileName;
            Library.Clear();
            LoadLibrary(dialog.FileName);
        }
    }

    /// <summary>
    /// Adds a transaction to the library: after the currently selected transaction, or at the end if none is selected.
    /// </summary>
    public void AddItemToLibrary(IDataItem item)
    {
        if (item is not SecsGemTransaction transaction) return;
        if (SelectedItem is SecsGemTransaction selected)
        {
            var selectedIndex = Library.IndexOf(selected);
            if (selectedIndex != -1)
            {
                Library.Insert(selectedIndex + 1, transaction);
                return;
            }
        }
        Library.Add(transaction);
    }

    private void LoadLibrary(string path)
    {
        var ext = Path.GetExtension(path);
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var tx in JsonService.Load(path))
                Library.Add(tx);
        }
        else if (ext.Equals(".msgpack", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var tx in MessagePackService.Load(path))
                Library.Add(tx);
        }
        else
        {
            try
            {
                XmlParser parser = new(path);
                parser.LoadItems(Library);
                Logger.LogInformation("Library \"{Path}\" loaded successfully", path);
            }
            catch (XmlException ex)
            {
                Logger.LogError("Failed to load library from \"{Path}\": file is invalid", path);
            }
        }
    }
}