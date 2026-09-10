using System;
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SecsGemMessageHandling.Events.Models;
using SecsGemMessageHandling.Events.Registry.Interface;

namespace EvenBetterFastSim.WPF.ViewModels;

public partial class AddReportViewModel(IRegistry<SecsGemReport> reportRegistry) : ObservableObject, IBaseViewModel
{
    private SecsGemReport? originalItem;

    public int Rptid { get; set; }
    public string ReportName { get; set; } = string.Empty;
    public Action? CloseAction { get; set; }
    public string HeaderText => "Add Report";

    public void LoadForEdit(SecsGemReport item)
    {
        originalItem = item;
        Rptid = item.Rptid;
        ReportName = item.ReportName;
    }

    [RelayCommand]
    public void AcceptButtonClick()
    {
        var newReport = new SecsGemReport
        {
            Rptid = Rptid,
            ReportName = ReportName,
            Variables = originalItem?.Variables ?? ImmutableList<int>.Empty
        };

        if (originalItem != null)
            reportRegistry.Update(newReport, originalItem);
        else
            reportRegistry.Add(newReport);

        CloseAction?.Invoke();
    }
}
