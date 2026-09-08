using Logging.Interfaces;
using System.Collections.ObjectModel;
using System.Windows;

namespace EvenBetterFastSim.Logging;

public class SecsMessageLogger : ISecsMessageLogger
{
    public ObservableCollection<ILoggedSecsGemMessage> MessagesLog { get; } = [];

    public void MessageIn(ILoggedDataMessage loggedSecsGemMessage) =>
        AddItemToCollection(loggedSecsGemMessage);

    public void MessageOut(ILoggedDataMessage loggedSecsGemMessage) =>
        AddItemToCollection(loggedSecsGemMessage);

    public void ControlMessageIn(ILoggedControlMessage loggedControlMessage) =>
        AddItemToCollection(loggedControlMessage);

    public void ControlMessageOut(ILoggedControlMessage loggedControlMessage) =>
        AddItemToCollection(loggedControlMessage);

    private void AddItemToCollection(ILoggedSecsGemMessage messageToLog)
    {
        Application.Current.Dispatcher.InvokeAsync(() =>
        {
            MessagesLog.Add(messageToLog);
        });
    }
}
