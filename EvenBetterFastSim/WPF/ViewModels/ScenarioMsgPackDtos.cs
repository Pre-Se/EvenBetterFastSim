using MessagePack;
using SecsGemScenarioEngine.Models;
using System.Collections.Generic;
using System.Linq;

namespace EvenBetterFastSim.WPF.ViewModels;

[MessagePackObject]
public class ScenarioGraphDto
{
    [Key(0)] public string Name { get; set; } = string.Empty;
    [Key(1)] public string Description { get; set; } = string.Empty;
    [Key(2)] public List<ScenarioNodeDto> Nodes { get; set; } = [];
    [Key(3)] public List<ScenarioEdgeDto> Edges { get; set; } = [];

    public static ScenarioGraphDto From(ScenarioGraph g) => new()
    {
        Name = g.Name,
        Description = g.Description,
        Nodes = g.Nodes.Select(ScenarioNodeDto.From).ToList(),
        Edges = g.Edges.Select(ScenarioEdgeDto.From).ToList()
    };

    public ScenarioGraph ToModel() => new()
    {
        Name = Name,
        Description = Description,
        Nodes = Nodes.Select(n => n.ToModel()).ToList(),
        Edges = Edges.Select(e => e.ToModel()).ToList()
    };
}

[MessagePackObject]
public class ScenarioNodeDto
{
    [Key(0)] public string Id { get; set; } = string.Empty;
    [Key(1)] public int Type { get; set; }
    [Key(2)] public string? TransactionName { get; set; }
    [Key(3)] public string? DisplayName { get; set; }
    [Key(4)] public string? TransactionJson { get; set; }
    [Key(5)] public bool UseReplyMessage { get; set; }
    [Key(6)] public double X { get; set; }
    [Key(7)] public double Y { get; set; }
    [Key(8)] public string? MatchConditionsJson { get; set; }
    [Key(9)] public string? ResponseBindingsJson { get; set; }

    public static ScenarioNodeDto From(ScenarioNode n) => new()
    {
        Id = n.Id,
        Type = (int)n.Type,
        TransactionName = n.TransactionName,
        DisplayName = n.DisplayName,
        TransactionJson = n.TransactionJson,
        UseReplyMessage = n.UseReplyMessage,
        X = n.X,
        Y = n.Y,
        MatchConditionsJson = n.MatchConditionsJson,
        ResponseBindingsJson = n.ResponseBindingsJson
    };

    public ScenarioNode ToModel() => new()
    {
        Id = Id,
        Type = (NodeType)Type,
        TransactionName = TransactionName,
        DisplayName = DisplayName,
        TransactionJson = TransactionJson,
        UseReplyMessage = UseReplyMessage,
        X = X,
        Y = Y,
        MatchConditionsJson = MatchConditionsJson,
        ResponseBindingsJson = ResponseBindingsJson
    };
}

[MessagePackObject]
public class ScenarioEdgeDto
{
    [Key(0)] public string SourceNodeId { get; set; } = string.Empty;
    [Key(1)] public string TargetNodeId { get; set; } = string.Empty;
    [Key(2)] public bool IsFailurePath { get; set; }

    public static ScenarioEdgeDto From(ScenarioEdge e) => new()
    {
        SourceNodeId = e.SourceNodeId,
        TargetNodeId = e.TargetNodeId,
        IsFailurePath = e.IsFailurePath
    };

    public ScenarioEdge ToModel() => new()
    {
        SourceNodeId = SourceNodeId,
        TargetNodeId = TargetNodeId,
        IsFailurePath = IsFailurePath
    };
}
