using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using EvenBetterFastSim.Services;
using EvenBetterFastSim.WPF.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Data_Containers.Interfaces;
using SecsGemBaseItems.LibraryManager;

namespace EvenBetterFastSim.Tests;

/// <summary>
/// Minimal fake; <see cref="SecsGemItemViewModel"/> only reads <see cref="SelectedItem"/>.
/// </summary>
internal sealed class FakeLibraryManager : ISecsGemLibraryManager
{
    public ObservableCollection<SecsGemTransaction> Library { get; } = new();
    public IDataItem? SelectedItem { get; set; }
    public void OpenLibrary() { }
    public void AddItemToLibrary(IDataItem item) { }
    public event PropertyChangedEventHandler? PropertyChanged { add { } remove { } }
}

internal static class Editor
{
    /// <summary>
    /// Builds a <see cref="SecsGemItemViewModel"/> editing <paramref name="item"/>, exactly as the
    /// "Modify Item" dialog does when that item is selected in the tree.
    /// </summary>
    public static (SecsGemItemViewModel vm, FakeLibraryManager lib) Open(SecsGemItem item)
    {
        // ApplicationSettings' ctor needs 3 services; the view model only touches ItemDelimiter.
        var settings = (ApplicationSettings)RuntimeHelpers.GetUninitializedObject(typeof(ApplicationSettings));
        settings.ItemDelimiter = ";";

        var lib = new FakeLibraryManager { SelectedItem = item };
        var vm = new SecsGemItemViewModel(settings, lib, NullLogger<SecsGemItemViewModel>.Instance);
        return (vm, lib);
    }

    /// <summary>Hex of the fully serialized SECS-II item (header + value bytes).</summary>
    public static string ToHex(SecsGemItem item) => Convert.ToHexString(item.ToBytes().ToArray());

    public static SecsGemItem? FindByDescription(IEnumerable<IDataItem> items, string description)
    {
        foreach (var it in items)
        {
            if (it is SecsGemItem si && si.Description == description)
                return si;
            if (FindByDescription(it.Children, description) is { } found)
                return found;
        }
        return null;
    }
}

internal static class TestPaths
{
    public static string LibraryDir { get; } = ResolveLibraryDir();
    public static string DefaultLibraryMsgpack => Path.Combine(LibraryDir, "DefaultLibrary.msgpack");

    private static string ResolveLibraryDir()
    {
        // Walk up from the test output folder until "EvenBetterFastSim\Library" appears.
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "EvenBetterFastSim", "Library");
            if (Directory.Exists(candidate))
                return candidate;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException(
            @"Could not locate 'EvenBetterFastSim\Library' above " + AppContext.BaseDirectory);
    }
}
