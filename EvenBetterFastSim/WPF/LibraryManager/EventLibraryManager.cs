using CommunityToolkit.Mvvm.ComponentModel;
using EvenBetterFastSim.WPF.ViewModels.Models;
using MessagePack;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using SecsGemBaseItems.Enums;
using SecsGemMessageHandling.Events.Models;
using SecsGemMessageHandling.Events.Registry;
using SecsGemMessageHandling.Events.Registry.Interface;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using EvenBetterFastSim.WPF.LibraryManager.Serializer;

namespace EvenBetterFastSim.WPF.LibraryManager;
public partial class EventLibraryManager : ObservableObject
{
    public ReadOnlyObservableCollection<EventReportViewModel> EventCollection { get; }
    public ReadOnlyObservableCollection<ReportViewModel> ReportCollection { get; }
    public ReadOnlyObservableCollection<VariableViewModel> VariableCollection { get; }
    public IRegistry<SecsGemEventReport> SecsGemEventRegistry { get; }
    public IRegistry<SecsGemReport> SecsGemReportRegistry { get; }
    public IRegistry<SecsGemEquipmentVariable> SecsGemEquipmentVariableRegistry { get; }
    public LinkEventReportRegistry LinkEventReportRegistry { get; }
    public ObservableRegistry<SecsGemEventReport, EventReportViewModel> ObservableEventRegistry { get; }
    public ObservableRegistry<SecsGemReport, ReportViewModel> ObservableReportRegistry { get; }
    public ObservableRegistry<SecsGemEquipmentVariable, VariableViewModel> ObservableVariableRegistry { get; }
    private readonly ILogger<EventLibraryManager> logger;

    /// <summary>
    /// Currently selected item in the library
    /// </summary>
    [ObservableProperty]
    public partial EventReportViewModel? SelectedEvent { get; set; }
    [ObservableProperty]
    public partial ReportViewModel? SelectedReport { get; set; }
    [ObservableProperty]
    public partial VariableViewModel? SelectedVariable { get; set; }

    public EventLibraryManager(IRegistry<SecsGemEventReport> secsGemEventRegistry,
        IRegistry<SecsGemReport> secsGemReportRegistry,
        IRegistry<SecsGemEquipmentVariable> secsGemEquipmentVariableRegistry,
        LinkEventReportRegistry linkEventReportRegistry,
        ILogger<EventLibraryManager> logger,
        ObservableRegistry<SecsGemEventReport, EventReportViewModel> observableEventRegistry,
        ObservableRegistry<SecsGemReport, ReportViewModel> observableReportRegistry,
        ObservableRegistry<SecsGemEquipmentVariable, VariableViewModel> observableVariableRegistry)
    {
        SecsGemEventRegistry = secsGemEventRegistry;
        SecsGemReportRegistry = secsGemReportRegistry;
        SecsGemEquipmentVariableRegistry = secsGemEquipmentVariableRegistry;
        LinkEventReportRegistry = linkEventReportRegistry;
        this.logger = logger;
        ObservableEventRegistry = observableEventRegistry;
        ObservableReportRegistry = observableReportRegistry;
        ObservableVariableRegistry = observableVariableRegistry;

        EventCollection = ObservableEventRegistry.ItemCollection;
        ReportCollection = ObservableReportRegistry.ItemCollection;
        VariableCollection = ObservableVariableRegistry.ItemCollection;
    }

    /// <summary>
    /// Opens a file dialog to select a new library file, old library is cleared
    /// </summary>
    public string GetPath()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select library file",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            Multiselect = false
        };

        if (dialog.ShowDialog() == true && dialog.FileName != string.Empty)
        {
            return dialog.FileName;
        }

        return string.Empty;
    }

    public void Load()
    {
        var path = GetPath();

        if (path == string.Empty) return;

        SecsGemEventRegistry.DeleteAll();
        SecsGemReportRegistry.DeleteAll();
        SecsGemEquipmentVariableRegistry.DeleteAll();

        if (Path.GetExtension(path).Equals(".msgpack", StringComparison.OrdinalIgnoreCase))
        {
            LoadMsgPack(path);
        }
        else
        {
            LoadJson(path);
        }
    }

    private void LoadJson(string path)
    {
        var text = File.ReadAllText(path);
        var loaded = JsonSerializer.Deserialize<EventLibrarySaveModel>(text);
        if (loaded == null) return;

        if (loaded.Events is not null)
            foreach (var e in loaded.Events)
                SecsGemEventRegistry.Add(e);

        if (loaded.Reports is not null)
            foreach (var r in loaded.Reports)
                SecsGemReportRegistry.Add(r);

        if (loaded.Variables is not null)
            foreach (var v in loaded.Variables)
                SecsGemEquipmentVariableRegistry.Add(v);
    }

    private void LoadMsgPack(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var dto = MessagePackSerializer.Deserialize<EventLibrarySaveDtoM>(bytes);

        if (dto.Events is not null)
            foreach (var e in dto.Events)
                SecsGemEventRegistry.Add(new SecsGemEventReport
                {
                    Ceid = e.Ceid,
                    EventName = e.EventName,
                    ReportList = [.. e.ReportList],
                    IsActive = e.IsActive
                });

        if (dto.Reports is not null)
            foreach (var r in dto.Reports)
                SecsGemReportRegistry.Add(new SecsGemReport
                {
                    Rptid = r.Rptid,
                    ReportName = r.ReportName,
                    Variables = [.. r.Variables]
                });

        if (dto.Variables is not null)
            foreach (var v in dto.Variables)
                SecsGemEquipmentVariableRegistry.Add(new SecsGemEquipmentVariable
                {
                    VariableId = v.VariableId,
                    Name = v.Name,
                    Value = v.Value,
                    Description = v.Description,
                    DataType = (SecsGemItemFormatType)v.DataType,
                    VariableClass = (SecsGemMessageHandling.Events.Enums.SecsGemVariableClass)v.VariableClass
                });
    }

    public void Save()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save library file",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Personal),
            Filter = "JSON Files (*.json)|*.json|MessagePack Files (*.msgpack)|*.msgpack|All Files (*.*)|*.*",
            DefaultExt = "json",
            AddExtension = true,
            OverwritePrompt = true
        };

        if (dialog.ShowDialog() != true || dialog.FileName == string.Empty) return;

        var path = dialog.FileName;

        if (Path.GetExtension(path).Equals(".msgpack", StringComparison.OrdinalIgnoreCase))
        {
            SaveMsgPack(path);
        }
        else
        {
            SaveJson(path);
        }
    }

    private void SaveJson(string path)
    {
        var saveModel = new EventLibrarySaveModel
        {
            Events = EventCollection.Select(ev => ev.Model).ToList(),
            Reports = ReportCollection.Select(ev => ev.Model).ToList(),
            Variables = VariableCollection.Select(ev => ev.Model).ToList(),
        };
        var json = JsonSerializer.Serialize(saveModel, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    private void SaveMsgPack(string path)
    {
        var dto = new EventLibrarySaveDtoM
        {
            Events = EventCollection.Select(ev => new EventReportDtoM
            {
                Ceid = ev.Model.Ceid,
                EventName = ev.Model.EventName,
                ReportList = [.. ev.Model.ReportList],
                IsActive = ev.Model.IsActive
            }).ToList(),
            Reports = ReportCollection.Select(r => new ReportDtoM
            {
                Rptid = r.Model.Rptid,
                ReportName = r.Model.ReportName,
                Variables = [.. r.Model.Variables]
            }).ToList(),
            Variables = VariableCollection.Select(v => new VariableDtoM
            {
                VariableId = v.Model.VariableId,
                Name = v.Model.Name,
                Value = v.Model.Value,
                Description = v.Model.Description,
                DataType = (int)v.Model.DataType,
                VariableClass = (int)v.Model.VariableClass
            }).ToList()
        };
        File.WriteAllBytes(path, MessagePackSerializer.Serialize(dto));
    }
}
