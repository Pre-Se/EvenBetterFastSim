using EvenBetterFastSim.Logging.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Windows;
using System.Windows.Threading;

namespace EvenBetterFastSim.Logging;

/// <summary>
/// Logs messages to the UI
/// </summary>
/// <param name="dispatcher">The UI thread's dispatcher at the time of a log call; by default the application's.
/// Without one (no <see cref="Application"/>: unit tests, shutdown) the entry is dropped.</param>
public class ObservableLogger(ILogService<LoggedString> logService, string _, Func<Dispatcher?>? dispatcher = null) : ILogger
{
    /// <summary>
    /// Gets the associated ObservableCollection of logged strings
    /// </summary>
    private readonly ILogService<LoggedString> logService = logService;
    private readonly string _ = _;
    private readonly Func<Dispatcher?> dispatcher = dispatcher ?? (static () => Application.Current?.Dispatcher);

    public void Log<TState>(LogLevel logLevel, 
        EventId eventId, 
        TState state, 
        Exception? exception, 
        Func<TState, Exception?, 
            string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = $"{formatter(state, exception)}";
        LoggedString logString = new()
        {
            Message = message,
            Level = logLevel
        };
        AddItemToCollection(logString);
    }

    public bool IsEnabled(LogLevel logLevel) => true;

    public IDisposable BeginScope<TState>(TState state) where TState : notnull => null!;

    private void AddItemToCollection(LoggedString messageToLog)
    {
        // Queued for the UI thread, like SecsMessageLogger; entries keep the order of their calls. With Invoke every
        // caller waited for the UI thread: the scenario engine and the receive loop took as long as the UI was busy,
        // and a caller that the UI thread itself was waiting for (a connection being closed) blocked for seconds
        // (GitLab secsgem/evencoolerfastsim#26).
        dispatcher()?.InvokeAsync(() => logService.LogMessages.Add(messageToLog));
    }
}