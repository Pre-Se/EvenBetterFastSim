using MessagePack;
using System.Collections.Generic;

namespace EvenBetterFastSim.WPF.LibraryManager.Serializer;

[MessagePackObject]
public class EventLibrarySaveDtoM
{
    [Key(0)] public List<EventReportDtoM>? Events { get; set; }
    [Key(1)] public List<ReportDtoM>? Reports { get; set; }
    [Key(2)] public List<VariableDtoM>? Variables { get; set; }
}

[MessagePackObject]
public class EventReportDtoM
{
    [Key(0)] public int Ceid { get; set; }
    [Key(1)] public string EventName { get; set; } = string.Empty;
    [Key(2)] public List<int> ReportList { get; set; } = [];
    [Key(3)] public bool IsActive { get; set; }
}

[MessagePackObject]
public class ReportDtoM
{
    [Key(0)] public int Rptid { get; set; }
    [Key(1)] public string ReportName { get; set; } = string.Empty;
    [Key(2)] public List<int> Variables { get; set; } = [];
}

[MessagePackObject]
public class VariableDtoM
{
    [Key(0)] public int VariableId { get; set; }
    [Key(1)] public string Name { get; set; } = string.Empty;
    [Key(2)] public string Value { get; set; } = string.Empty;
    [Key(3)] public string Description { get; set; } = string.Empty;
    [Key(4)] public int DataType { get; set; }
    [Key(5)] public int VariableClass { get; set; }
}
