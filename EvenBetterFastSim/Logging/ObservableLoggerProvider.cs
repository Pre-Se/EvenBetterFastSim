using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using EvenBetterFastSim.Logging.Interfaces;

namespace EvenBetterFastSim.Logging;
internal class ObservableLoggerProvider(ILogService<LoggedString> logService) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, ObservableLogger> loggers =
        new(StringComparer.OrdinalIgnoreCase);
    public ILogger CreateLogger(string categoryName) =>
        loggers.GetOrAdd(categoryName, name => new ObservableLogger(logService, name));
    public void Dispose()
    {
        throw new NotImplementedException();
    }
}
