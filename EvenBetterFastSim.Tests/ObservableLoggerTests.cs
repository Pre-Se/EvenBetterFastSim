using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EvenBetterFastSim.Logging;
using Microsoft.Extensions.Logging;
using Xunit;

namespace EvenBetterFastSim.Tests;

/// <summary>
/// The System Log logger queues its entries for the UI thread instead of waiting for it
/// (GitLab secsgem/evencoolerfastsim#26). With Dispatcher.Invoke every log call from the scenario engine or the receive
/// loop took as long as the UI was busy, and a caller the UI thread itself was waiting for blocked for seconds.
/// </summary>
public class ObservableLoggerTests
{
    [Fact]
    public async Task Log_does_not_wait_for_a_busy_UI_thread()
    {
        using var ui = new UiThread();
        var log = new LogService<LoggedString>();
        var logger = new ObservableLogger(log, "test", () => ui.Dispatcher);
        using var busy = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        _ = ui.Dispatcher.InvokeAsync(() =>
        {
            busy.Set();
            release.Wait(TimeSpan.FromSeconds(30));
        });
        var cancellation = TestContext.Current.CancellationToken;
        Assert.True(busy.Wait(TimeSpan.FromSeconds(10), cancellation), "the UI thread did not start");
        try
        {
            var logging = Task.Run(() => logger.LogWarning("while the UI thread is busy"), cancellation);
            var finished = await Task.WhenAny(logging, Task.Delay(TimeSpan.FromSeconds(5), cancellation));

            Assert.True(finished == logging, "the log call waited for the UI thread");
            await logging;
            // The UI thread is still busy, so nothing has reached the collection yet.
            Assert.Empty(log.LogMessages);
        }
        finally
        {
            release.Set();
        }

        var entry = Assert.Single(ui.AfterQueuedWork(() => log.LogMessages.ToList()));
        Assert.Equal("while the UI thread is busy", entry.Message);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Fact]
    public void Entries_arrive_on_the_UI_thread_in_the_order_of_the_calls()
    {
        using var ui = new UiThread();
        var log = new LogService<LoggedString>();
        var addedOn = new System.Collections.Generic.HashSet<int>();
        log.LogMessages.CollectionChanged += (_, _) => addedOn.Add(Environment.CurrentManagedThreadId);
        var logger = new ObservableLogger(log, "test", () => ui.Dispatcher);

        for (var i = 0; i < 300; i++)
            logger.LogInformation("entry {Number}", i);

        var messages = ui.AfterQueuedWork(() => log.LogMessages.Select(m => m.Message).ToList());
        Assert.Equal(Enumerable.Range(0, 300).Select(i => $"entry {i}"), messages);
        Assert.Equal([ui.ThreadId], addedOn);
    }

    /// <summary>No Application (unit tests, shutdown): the entry is dropped; until #26 the log call threw.</summary>
    [Fact]
    public void Without_a_dispatcher_the_entry_is_dropped()
    {
        var log = new LogService<LoggedString>();

        new ObservableLogger(log, "test", () => null).LogError("nobody shows this");
        new ObservableLogger(log, "test").LogError("no Application in the test process");

        Assert.Empty(log.LogMessages);
    }
}
