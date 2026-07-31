using CommunityToolkit.Mvvm.ComponentModel;
using SecsGemMessageHandling.Events.Models;
using System.Collections.Immutable;

namespace EvenBetterFastSim.WPF.ViewModels.Models;
public partial class ReportViewModel : ObservableObject, IBaseViewModelItem<SecsGemReport>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header), nameof(VariableList))]
    public partial SecsGemReport Model { get; set; } = new() { Rptid = 0, ReportName = string.Empty };
    public string Header => $"[{Model.Rptid}] - {Model.ReportName}";
    public ImmutableList<int> VariableList => Model.Variables;
}
