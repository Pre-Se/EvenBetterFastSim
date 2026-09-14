using System.Collections.ObjectModel;

namespace EvenBetterFastSim.Logging.Interfaces;
public interface ILogService<T>
{
    ObservableCollection<T> LogMessages { get; }
    void ClearLogs();
}