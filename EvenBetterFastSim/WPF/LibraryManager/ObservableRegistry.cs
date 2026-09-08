using Microsoft.Extensions.Logging;
using SecsGemMessageHandling.Events.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using EvenBetterFastSim.WPF.ViewModels.Models;
using SecsGemMessageHandling.Events.Registry.Interface;

namespace EvenBetterFastSim.WPF.LibraryManager;
public class ObservableRegistry<TModel,TViewModel> 
    where TModel : IKeyedItem
    where TViewModel : IBaseViewModelItem<TModel>, new()
{
    private readonly IRegistry<TModel> baseRegistry;
    private readonly ObservableCollection<TViewModel> itemCollection = [];
    private readonly ILogger<ObservableRegistry<TModel, TViewModel>> logger;

    public ObservableRegistry(IRegistry<TModel> baseRegistry, ILogger<ObservableRegistry<TModel, TViewModel>> logger)
    {
        this.logger = logger;
        this.baseRegistry = baseRegistry;
        ItemCollection = new(itemCollection);

        foreach (var events in baseRegistry.GetAll())
        {
            AddEvent(events);
        }

        baseRegistry.OnAdded.Subscribe(ev => AddEvent(ev.NewEvent));
        baseRegistry.OnUpdated.Subscribe(ev => UpdateEvent(ev.NewEvent, ev.OldEvent!));
        baseRegistry.OnRemoved.Subscribe(ev => RemovedEvent(ev.NewEvent));
    }

    public ReadOnlyObservableCollection<TViewModel> ItemCollection { get; }
    private void AddEvent(TModel addedItem)
    {
        var viewModel = new TViewModel
        {
            Model = addedItem
        };
        Application.Current.Dispatcher.Invoke(() =>
        {
            itemCollection.Add(viewModel);
        });
    }

    private void UpdateEvent(TModel newEvent, TModel oldEvent)
    {
        var vm = itemCollection
            .FirstOrDefault(e => Equals(e.Model, oldEvent));

        if (vm is null)
        {
            logger.LogError("Event with CEID [{ceid}] not present in collection, disparity detected", oldEvent.Id);
            return;
        }

        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            vm.Model = newEvent;
        });
    }

    private void RemovedEvent(TModel deletedEvent)
    {
        var vm = itemCollection
            .FirstOrDefault(e => Equals(e.Model, deletedEvent));

        if (vm is null)
        {
            logger.LogError("Event with CEID [{ceid}] not present in collection, disparity in collection detected", deletedEvent.Id);
        }
        else
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                itemCollection.Remove(vm);
            });
        }
    }
}
