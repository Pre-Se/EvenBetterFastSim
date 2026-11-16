using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using EvenBetterFastSim.Services;
using Logging.Interfaces;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Data_Containers.Serialization;
using SecsGemScenarioEngine.Models;
using SecsGemScenarioEngine.Services;
using Xunit;

namespace EvenBetterFastSim.Tests;

/// <summary>
/// Scenarios run on the thread pool instead of the WPF UI thread, with a copy of the graph that nothing on the UI
/// thread can change under the engine (GitLab secsgem/evencoolerfastsim#26).
/// </summary>
public class ScenarioRunTests
{
    /// <summary>
    /// The caller is a thread with a running Dispatcher, like the UI thread. The graph is copied on that thread, where
    /// the library may be read. The engine then runs on the thread pool and must not see the caller's context: not when
    /// it starts and not after its first await, or its continuations would go back to that thread.
    /// </summary>
    /// <remarks>
    /// A plain SynchronizationContext would not do as the caller's context: await treats the base type like no context,
    /// so an implementation that awaits before it calls the engine would end up on the thread pool in the test and on
    /// the UI thread in the app.
    /// </remarks>
    [Fact]
    public async Task Graph_is_copied_on_the_callers_thread_and_the_engine_runs_on_the_thread_pool()
    {
        using var ui = new UiThread();
        var engine = new RecordingEngine();
        var transaction = Transaction(6, 11);
        var library = new RecordingLibrary(transaction);
        var graph = new ScenarioGraph
        {
            Nodes = [new ScenarioNode { Type = NodeType.Send, TransactionName = transaction.PrimaryMessage.Name }]
        };
        using var cancellation = new CancellationTokenSource();
        var consumed = new ConcurrentDictionary<ILoggedDataMessage, byte>();
        var deadline = TimeSpan.FromSeconds(7);
        var progress = new Progress<ScenarioNodeProgress>();
        SynchronizationContext? callerContext = null;

        var run = ui.Dispatcher.Invoke(() =>
        {
            callerContext = SynchronizationContext.Current;
            return ScenarioRun.RunAsync(engine, graph, library, cancellation.Token, consumed, deadline, progress);
        });
        var result = await run.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        Assert.IsType<DispatcherSynchronizationContext>(callerContext);
        Assert.Same(engine.Result, result);
        Assert.Equal(ui.ThreadId, library.EnumeratedOnThread);
        Assert.True(engine.OnThreadPool);
        Assert.NotEqual(ui.ThreadId, engine.ThreadId);
        Assert.Null(engine.ContextAtStart);
        Assert.Null(engine.ContextAfterAwait);
        // The engine got the copy, with the transaction the node only named.
        Assert.NotSame(graph, engine.Graph);
        Assert.NotNull(engine.Graph!.Nodes.Single().TransactionJson);
        Assert.Null(graph.Nodes.Single().TransactionJson);
        Assert.Equal(cancellation.Token, engine.Token);
        Assert.Same(consumed, engine.Consumed);
        Assert.Equal(deadline, engine.Deadline);
        Assert.Same(progress, engine.Progress);
    }

    /// <summary>
    /// The engine keeps a run's cancellation source in a field while the run starts. Runs that start at the same time
    /// enter the engine one after the other.
    /// </summary>
    [Fact]
    public async Task Runs_enter_the_engine_one_at_a_time()
    {
        var engine = new SlowStartEngine();
        var graph = new ScenarioGraph();

        var runs = Enumerable.Range(0, 4)
            .Select(_ => ScenarioRun.RunAsync(engine, graph, [], CancellationToken.None, null, null, null))
            .ToArray();
        await Task.WhenAll(runs).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(4, engine.Starts);
        Assert.Equal(1, engine.MostAtOnce);
    }

    /// <summary>
    /// Opening a scenario while it runs clears TransactionJson of the stored graph's nodes (LoadGraphToCanvas). The
    /// engine reads a node only when it reaches it, so it must have nodes of its own.
    /// </summary>
    [Fact]
    public void Snapshot_does_not_change_when_the_stored_graph_changes()
    {
        var json = SecsGemTransactionJsonConverter.Serialize(Transaction(1, 13));
        var start = new ScenarioNode { Type = NodeType.Start };
        var send = new ScenarioNode { Type = NodeType.Send, TransactionName = "S1F13", TransactionJson = json };
        var edge = new ScenarioEdge { SourceNodeId = start.Id, TargetNodeId = send.Id, IsFailurePath = true };
        var graph = new ScenarioGraph { Name = "n", Description = "d", Nodes = [start, send], Edges = [edge] };

        var run = ScenarioRun.Snapshot(graph, []);

        Assert.NotSame(graph, run);
        Assert.Equal(("n", "d"), (run.Name, run.Description));
        Assert.Equal([start.Id, send.Id], run.Nodes.Select(n => n.Id));
        Assert.All(run.Nodes, node => Assert.DoesNotContain(node, graph.Nodes));
        var runEdge = Assert.Single(run.Edges);
        Assert.NotSame(edge, runEdge);
        Assert.Equal((start.Id, send.Id, true), (runEdge.SourceNodeId, runEdge.TargetNodeId, runEdge.IsFailurePath));

        send.TransactionJson = null;
        send.TransactionName = "changed";
        edge.TargetNodeId = "changed";
        graph.Nodes.Clear();
        graph.Edges.Clear();

        Assert.Equal(json, run.Nodes[1].TransactionJson);
        Assert.Equal("S1F13", run.Nodes[1].TransactionName);
        Assert.Equal(send.Id, run.Edges.Single().TargetNodeId);
        Assert.Equal(2, run.Nodes.Count);
    }

    [Theory]
    [InlineData(NodeType.Send)]
    [InlineData(NodeType.SendAndWait)]
    [InlineData(NodeType.Receive)]
    public void Node_that_only_names_its_transaction_gets_its_own_copy(NodeType type)
    {
        var transaction = Transaction(6, 11);
        // Found like in the engine's own lookup: by the primary message's name, whatever its case.
        var name = transaction.PrimaryMessage.Name.ToLowerInvariant();
        Assert.NotEqual(transaction.PrimaryMessage.Name, name);
        var named = new ScenarioNode { Type = type, TransactionName = name };

        var run = ScenarioRun.Snapshot(new ScenarioGraph { Nodes = [named] }, [Transaction(1, 1), transaction]);

        // The graph that gets saved keeps the name-only node.
        Assert.Null(named.TransactionJson);
        var copy = SecsGemTransactionJsonConverter.Deserialize(run.Nodes.Single().TransactionJson!);
        Assert.NotNull(copy);
        Assert.NotSame(transaction, copy);
        Assert.Equal((6, 11), (copy.PrimaryMessage.Stream, copy.PrimaryMessage.Function));
        Assert.Equal((6, 12), (copy.ReplyMessage.Stream, copy.ReplyMessage.Function));
    }

    /// <summary>
    /// The converter writes values with the current culture and reads F4/F8 invariant: with a decimal comma the copy of
    /// 1,5 would hold 15 (GitLab secsgem/secsgembase#68).
    /// </summary>
    [Fact]
    public void Copy_holds_the_same_float_values_with_a_decimal_comma_culture()
    {
        var transaction = new SecsGemTransaction
        {
            PrimaryMessage = new MessageFactory(2, 41, true, "primary", new ItemFactory()
                .AddList(list => list.AddFloat(1.5f).AddDouble(-2.25).AddI4(-7).AddU4(1000))).Build(),
            ReplyMessage = new MessageFactory(2, 42, false, "reply").Build()
        };
        var named = new ScenarioNode { Type = NodeType.Send, TransactionName = transaction.PrimaryMessage.Name };
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        ScenarioGraph run;
        try
        {
            Assert.Equal("1,5", 1.5f.ToString());
            run = ScenarioRun.Snapshot(new ScenarioGraph { Nodes = [named] }, [transaction]);
            Assert.Equal(CultureInfo.GetCultureInfo("de-DE"), CultureInfo.CurrentCulture);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }

        var copy = SecsGemTransactionJsonConverter.Deserialize(run.Nodes.Single().TransactionJson!);
        var values = ((SecsGemItem)copy!.PrimaryMessage.Children.Single()).Children.Cast<SecsGemItem>()
            .Select(item => item.GetBoxedValues().Single()).ToList();
        Assert.Equal([1.5f, -2.25, -7, 1000u], values);
    }

    /// <summary>The copied node differs from the original at most in TransactionJson, whatever ScenarioNode holds.</summary>
    [Fact]
    public void Copy_keeps_every_other_property_of_the_node()
    {
        var transaction = Transaction(6, 11);
        var node = new ScenarioNode();
        FillWithDistinctValues(node);
        node.Type = NodeType.Send;
        node.TransactionJson = null;
        node.TransactionName = transaction.PrimaryMessage.Name;

        var copy = ScenarioRun.Snapshot(new ScenarioGraph { Nodes = [node] }, [transaction]).Nodes.Single();

        Assert.NotSame(node, copy);
        Assert.NotNull(copy.TransactionJson);
        foreach (var property in typeof(ScenarioNode).GetProperties().Where(p => p.Name != nameof(ScenarioNode.TransactionJson)))
            Assert.Equal(property.GetValue(node), property.GetValue(copy));
    }

    /// <summary>
    /// Snapshot copies exactly these. A property that a later engine package adds to ScenarioGraph would be dropped
    /// without notice, so it has to show up here first.
    /// </summary>
    [Fact]
    public void Snapshot_knows_every_property_of_a_graph() =>
        Assert.Equal(["Description", "Edges", "Name", "Nodes"],
            typeof(ScenarioGraph).GetProperties().Select(property => property.Name).Order());

    [Fact]
    public void Copy_keeps_every_property_of_an_edge()
    {
        var edge = new ScenarioEdge();
        FillWithDistinctValues(edge);

        var copy = ScenarioRun.Snapshot(new ScenarioGraph { Edges = [edge] }, []).Edges.Single();

        Assert.NotSame(edge, copy);
        foreach (var property in typeof(ScenarioEdge).GetProperties())
            Assert.Equal(property.GetValue(edge), property.GetValue(copy));
    }

    /// <summary>
    /// No library transaction is copied into nodes that carry their own, nodes whose TransactionName is a parameter
    /// (Wait, Drop connection, Wait for communication: ms), and nodes with a name the library does not know (the
    /// engine reports those).
    /// </summary>
    [Fact]
    public void Other_nodes_keep_what_they_have()
    {
        var transaction = Transaction(6, 11);
        var own = SecsGemTransactionJsonConverter.Serialize(Transaction(1, 13));
        var graph = new ScenarioGraph
        {
            Nodes =
            [
                new ScenarioNode { Type = NodeType.Send, TransactionName = transaction.PrimaryMessage.Name, TransactionJson = own },
                new ScenarioNode { Type = NodeType.Wait, TransactionName = transaction.PrimaryMessage.Name },
                new ScenarioNode { Type = NodeType.DropConnection, TransactionName = transaction.PrimaryMessage.Name },
                new ScenarioNode { Type = NodeType.WaitForCommunication, TransactionName = transaction.PrimaryMessage.Name },
                new ScenarioNode { Type = NodeType.Receive, TransactionName = "not in the library" },
                new ScenarioNode { Type = NodeType.Send, TransactionName = "  " }
            ]
        };

        var run = ScenarioRun.Snapshot(graph, [transaction]);

        Assert.Equal(own, run.Nodes[0].TransactionJson);
        Assert.All(run.Nodes.Skip(1), node => Assert.Null(node.TransactionJson));
        Assert.Equal(graph.Nodes.Select(n => (n.Id, n.Type, n.TransactionName)),
            run.Nodes.Select(n => (n.Id, n.Type, n.TransactionName)));
    }

    /// <summary>
    /// With branches running in parallel, an And node's "1/2 branches" report can arrive after its Succeeded report.
    /// Only that case is dropped; every other report is shown.
    /// </summary>
    [Theory]
    [InlineData(ScenarioNodeRunState.Succeeded, ScenarioNodeRunState.Waiting, true)]
    [InlineData(ScenarioNodeRunState.Succeeded, ScenarioNodeRunState.Succeeded, false)]
    [InlineData(ScenarioNodeRunState.Succeeded, ScenarioNodeRunState.Failed, false)]
    [InlineData(ScenarioNodeRunState.Succeeded, ScenarioNodeRunState.Running, false)]
    [InlineData(ScenarioNodeRunState.Waiting, ScenarioNodeRunState.Waiting, false)]
    [InlineData(ScenarioNodeRunState.Running, ScenarioNodeRunState.Waiting, false)]
    [InlineData(ScenarioNodeRunState.Idle, ScenarioNodeRunState.Waiting, false)]
    [InlineData(ScenarioNodeRunState.Waiting, ScenarioNodeRunState.Succeeded, false)]
    public void Only_a_waiting_report_after_success_is_stale(ScenarioNodeRunState shown, ScenarioNodeRunState reported,
        bool stale) =>
        Assert.Equal(stale, ScenarioRun.IsStale(shown, new ScenarioNodeProgress("node", reported, "1/2 branches")));

    /// <summary>
    /// Sets every property to a value that differs from its default and from the other properties. A property type this
    /// does not know fails the test: then the copy in ScenarioRun.Snapshot needs a look as well.
    /// </summary>
    private static void FillWithDistinctValues(object target)
    {
        var index = 0;
        foreach (var property in target.GetType().GetProperties())
        {
            index++;
            Assert.True(property.CanWrite, $"{target.GetType().Name}.{property.Name} cannot be set: check how Snapshot copies it");
            object value = property.PropertyType switch
            {
                var t when t == typeof(string) => $"value {index} of {property.Name}",
                var t when t == typeof(bool) => !(bool)property.GetValue(target)!,
                var t when t == typeof(double) => 12.5 + index,
                var t when t == typeof(NodeType) => NodeType.Receive,
                var t => throw new InvalidOperationException(
                    $"{target.GetType().Name}.{property.Name} has the type {t}: extend this test and check Snapshot")
            };
            property.SetValue(target, value);
        }
    }

    private static SecsGemTransaction Transaction(byte stream, byte function) => new()
    {
        PrimaryMessage = new MessageFactory(stream, function, true, "primary").Build(),
        ReplyMessage = new MessageFactory(stream, (byte)(function + 1), false, "reply").Build()
    };

    /// <summary>A library that notes the thread it is read on.</summary>
    private sealed class RecordingLibrary(params SecsGemTransaction[] transactions) : IEnumerable<SecsGemTransaction>
    {
        public int EnumeratedOnThread { get; private set; }

        public IEnumerator<SecsGemTransaction> GetEnumerator()
        {
            EnumeratedOnThread = Environment.CurrentManagedThreadId;
            return ((IEnumerable<SecsGemTransaction>)transactions).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class RecordingEngine : IScenarioExecutionService
    {
        public ScenarioExecutionResult Result { get; } = new() { Success = true, CompletedSteps = 3 };
        public bool OnThreadPool { get; private set; }
        public int ThreadId { get; private set; }
        public SynchronizationContext? ContextAtStart { get; private set; } = new();
        public SynchronizationContext? ContextAfterAwait { get; private set; } = new();
        public ScenarioGraph? Graph { get; private set; }
        public CancellationToken Token { get; private set; }
        public ConcurrentDictionary<ILoggedDataMessage, byte>? Consumed { get; private set; }
        public TimeSpan? Deadline { get; private set; }
        public IProgress<ScenarioNodeProgress>? Progress { get; private set; }

        public async Task<ScenarioExecutionResult> ExecuteAsync(ScenarioGraph scenario,
            CancellationToken cancellation = default,
            ConcurrentDictionary<ILoggedDataMessage, byte>? consumedAcrossRuns = null, TimeSpan? deadline = null,
            IProgress<ScenarioNodeProgress>? progress = null)
        {
            OnThreadPool = Thread.CurrentThread.IsThreadPoolThread;
            ThreadId = Environment.CurrentManagedThreadId;
            ContextAtStart = SynchronizationContext.Current;
            (Graph, Token, Consumed, Deadline, Progress) = (scenario, cancellation, consumedAcrossRuns, deadline, progress);
            // Like the engine: no ConfigureAwait(false).
            await Task.Delay(20, CancellationToken.None);
            ContextAfterAwait = SynchronizationContext.Current;
            return Result;
        }

        public void Cancel() { }
    }

    /// <summary>Its synchronous start takes a while and counts how many callers are in it at once.</summary>
    private sealed class SlowStartEngine : IScenarioExecutionService
    {
        private int inside;
        private int mostAtOnce;
        private int starts;

        public int MostAtOnce => Volatile.Read(ref mostAtOnce);
        public int Starts => Volatile.Read(ref starts);

        public Task<ScenarioExecutionResult> ExecuteAsync(ScenarioGraph scenario,
            CancellationToken cancellation = default,
            ConcurrentDictionary<ILoggedDataMessage, byte>? consumedAcrossRuns = null, TimeSpan? deadline = null,
            IProgress<ScenarioNodeProgress>? progress = null)
        {
            var now = Interlocked.Increment(ref inside);
            Interlocked.Increment(ref starts);
            int seen;
            while ((seen = Volatile.Read(ref mostAtOnce)) < now && Interlocked.CompareExchange(ref mostAtOnce, now, seen) != seen)
            {
            }

            Thread.Sleep(150);
            Interlocked.Decrement(ref inside);
            return Task.FromResult(new ScenarioExecutionResult { Success = true });
        }

        public void Cancel() { }
    }
}
