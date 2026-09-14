using System.Collections.ObjectModel;
using EvenBetterFastSim.Logging.Interfaces;

namespace EvenBetterFastSim.Logging;

public class LogService<T> : ILogService<T>
{
    public ObservableCollection<T> LogMessages { get; } = [];

    public void ClearLogs()
    {
        LogMessages.Clear();
    }
}