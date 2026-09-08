using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecsGemMessageHandling.Events.Models;
using SecsGemMessageHandling.Events.Registry.Interface;

namespace EvenBetterFastSim.WPF.ViewModels;
public partial class AddEventReportViewModel(IRegistry<SecsGemEventReport> eventRegistry) : ObservableValidator, IBaseViewModel
{
    private SecsGemEventReport? originalItem;

    public int Ceid { get; set; }
    public string EventName { get; set; } = string.Empty;
    public ObservableCollection<int> ReportList { get; set; } = [];
    public bool IsActive { get; set; }
    public int? SelectedReport { get; set; }
    public Action? CloseAction { get; set; }

    public void LoadForEdit(SecsGemEventReport item)
    {
        originalItem = item;
        Ceid = item.Ceid;
        EventName = item.EventName;
        IsActive = item.IsActive;
        ReportList = new ObservableCollection<int>(item.ReportList);
    }

    [RelayCommand]
    public void AcceptButtonClick()
    {
        var newEvent = new SecsGemEventReport
        {
            Ceid = Ceid,
            EventName = EventName,
            IsActive = IsActive,
            ReportList = ReportList.ToImmutableList()
        };

        if (originalItem != null)
            eventRegistry.Update(newEvent, originalItem);
        else
            eventRegistry.Add(newEvent);

        CloseAction?.Invoke();
    }
}
