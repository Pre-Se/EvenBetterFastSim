using System.Collections.Generic;
using SecsGemMessageHandling.Events.Models;

namespace EvenBetterFastSim.WPF.LibraryManager.Serializer;
public class EventLibrarySaveModel
{
    public List<SecsGemEventReport>? Events { get; init; }
    public List<SecsGemReport>? Reports { get; init; }
    public List<SecsGemEquipmentVariable>? Variables { get; init; }
}
