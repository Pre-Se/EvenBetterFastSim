using SecsGemMessageHandling.Events.Models;
using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EvenBetterFastSim.WPF.ViewModels.Models;
public partial class EventReportViewModel() : ObservableObject, IBaseViewModelItem<SecsGemEventReport>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header), nameof(ReportList))]
    public partial SecsGemEventReport Model { get; set; } = new() { Ceid = 0, EventName = string.Empty };
    public string Header => $"[{Model.Ceid}] - {Model.EventName} - {(Model.IsActive? "Active" : "Not Active")}";
    public ImmutableList<int> ReportList => Model.ReportList;
}
