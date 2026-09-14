using CommunityToolkit.Mvvm.ComponentModel;
using SecsGemMessageHandling.Events.Models;

namespace EvenBetterFastSim.WPF.ViewModels.Models;
public partial class VariableViewModel : ObservableObject, IBaseViewModelItem<SecsGemEquipmentVariable>
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Header))]
    public partial SecsGemEquipmentVariable Model { get; set; } = new() { VariableId = 0 };
    public string Header => $"[{Model.VariableId}] - {Model.Item.Header}";
}