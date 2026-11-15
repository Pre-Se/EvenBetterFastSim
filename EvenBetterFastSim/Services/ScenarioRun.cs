using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Logging.Interfaces;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Data_Containers.Serialization;
using SecsGemScenarioEngine.Models;
using SecsGemScenarioEngine.Services;

namespace EvenBetterFastSim.Services;

/// <summary>
/// Hands a scenario to the engine so that it runs on the thread pool instead of the WPF UI thread
/// (GitLab secsgem/evencoolerfastsim#26).
/// </summary>
/// <remarks>
/// The engine awaits without ConfigureAwait(false). Started on the UI thread, every node therefore ran there: the
/// time between two nodes depended on what the UI was busy with, and a node that blocks froze the window.
/// </remarks>
public static class ScenarioRun
{
    /// <summary>
    /// One run enters the engine at a time. While it starts, the engine keeps the run's cancellation source in a field
    /// and reads it back; two runs that start at the same instant on two threads could end up with each other's source
    /// and ignore their own Stop and deadline (GitLab secsgem/secsgembase#69). On the UI thread that could not happen.
    /// Only the synchronous start is covered, not the run.
    /// </summary>
    private static readonly Lock EngineStart = new();

    /// <summary>
    /// Takes a <see cref="Snapshot"/> of the graph on the calling thread (the UI thread), then runs the engine with it on
    /// the thread pool, where the engine has no SynchronizationContext to return to. An awaiting caller continues on its
    /// own context as usual, and a <see cref="Progress{T}"/> created on the UI thread still reports there.
    /// </summary>
    /// <remarks>
    /// The token is not passed to Task.Run on purpose: a run that is cancelled before it starts still goes through the
    /// engine, which reports it as cancelled like any other cancelled run.
    /// </remarks>
    public static Task<ScenarioExecutionResult> RunAsync(
        IScenarioExecutionService engine,
        ScenarioGraph graph,
        IEnumerable<SecsGemTransaction> library,
        CancellationToken token,
        ConcurrentDictionary<ILoggedDataMessage, byte>? consumed,
        TimeSpan? deadline,
        IProgress<ScenarioNodeProgress>? progress)
    {
        var run = Snapshot(graph, library);
        return Task.Run(() =>
        {
            lock (EngineStart)
                return engine.ExecuteAsync(run, token, consumed, deadline, progress);
        });
    }

    /// <summary>
    /// The graph for one run: a copy that nothing else refers to, in which a Send or Receive node that only names its
    /// transaction has its own copy of that library transaction. Call it on the UI thread.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>The stored graph keeps changing on the UI thread while a run is in flight: opening a scenario clears
    /// <see cref="ScenarioNode.TransactionJson"/> of its nodes. The engine reads a node when it reaches it, so it gets
    /// nodes of its own.</item>
    /// <item>Nodes made on the canvas carry their transaction as TransactionJson. A node from an older file has only
    /// <see cref="ScenarioNode.TransactionName"/>; the engine then looks the transaction up in the library and writes
    /// response bindings into it. The library is bound to the UI, where such a write from another thread throws. With
    /// the copy the engine does not touch the library for that node, and the run no longer changes the library
    /// transaction.</item>
    /// <item>A name the library does not know stays unresolved: the engine reports it when it reaches the node. For
    /// that it still searches the library, from its own thread.</item>
    /// </list>
    /// </remarks>
    public static ScenarioGraph Snapshot(ScenarioGraph graph, IEnumerable<SecsGemTransaction> library)
    {
        List<SecsGemTransaction>? transactions = null;
        var run = new ScenarioGraph
        {
            Name = graph.Name,
            Description = graph.Description,
            Edges = graph.Edges.Select(edge => new ScenarioEdge
            {
                SourceNodeId = edge.SourceNodeId,
                TargetNodeId = edge.TargetNodeId,
                IsFailurePath = edge.IsFailurePath
            }).ToList()
        };

        foreach (var node in graph.Nodes)
        {
            var copy = new ScenarioNode
            {
                Id = node.Id,
                Type = node.Type,
                TransactionName = node.TransactionName,
                DisplayName = node.DisplayName,
                TransactionJson = node.TransactionJson,
                UseReplyMessage = node.UseReplyMessage,
                MatchConditionsJson = node.MatchConditionsJson,
                ResponseBindingsJson = node.ResponseBindingsJson,
                X = node.X,
                Y = node.Y
            };
            run.Nodes.Add(copy);

            if (node.Type is not (NodeType.Send or NodeType.SendAndWait or NodeType.Receive) ||
                !string.IsNullOrWhiteSpace(node.TransactionJson) ||
                string.IsNullOrWhiteSpace(node.TransactionName))
                continue;

            transactions ??= library.ToList();
            // Same match as the engine's own lookup.
            var transaction = transactions.FirstOrDefault(t =>
                string.Equals(t.PrimaryMessage?.Name, node.TransactionName, StringComparison.OrdinalIgnoreCase));
            if (transaction is not null)
                copy.TransactionJson = Serialize(transaction);
        }

        return run;
    }

    /// <summary>
    /// A node keeps "Succeeded" for the rest of the run. The engine's branches really run in parallel now, so the report
    /// of a branch that reached an And node early ("1/2 branches", Waiting) can come in after the report that the And
    /// has fired (GitLab secsgem/secsgembase#69); the node would end the run with the hourglass.
    /// </summary>
    public static bool IsStale(ScenarioNodeRunState shown, ScenarioNodeProgress report) =>
        shown is ScenarioNodeRunState.Succeeded && report.State is ScenarioNodeRunState.Waiting;

    /// <summary>
    /// The converter writes item values with the current culture but reads F4/F8 values with the invariant culture: with
    /// a decimal comma, 1,5 came back as 15 (GitLab secsgem/secsgembase#68). Written invariant, the copy holds what the
    /// library transaction holds.
    /// </summary>
    private static string Serialize(SecsGemTransaction transaction)
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            return SecsGemTransactionJsonConverter.Serialize(transaction);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
