using EvenBetterFastSim.Logging.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Windows;

namespace EvenBetterFastSim.Logging;

/// <summary>
/// Logs messages to the UI
/// </summary>
public class ObservableLogger(ILogService<LoggedString> logService, string _) : ILogger
{
    /// <summary>
    /// Gets the associated ObservableCollection of logged strings
    /// </summary>
    private readonly ILogService<LoggedString> logService = logService;
    private readonly string _ = _;

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
        // Invoke the UI thread to add the message to the collection
        Application.Current.Dispatcher.Invoke(() =>
        {
            logService.LogMessages.Add(messageToLog);
        });
    }
}