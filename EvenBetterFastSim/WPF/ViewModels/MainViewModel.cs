using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvenBetterFastSim.Logging;
using EvenBetterFastSim.Logging.Interfaces;
using EvenBetterFastSim.Services;
using EvenBetterFastSim.Services.JSON;
using EvenBetterFastSim.WPF.LibraryManager;
using Microsoft.Extensions.Logging;
using EvenBetterFastSim.WPF.UIElements;
using EvenBetterFastSim.WPF.ViewModels.Models;
using EvenBetterFastSim.WPF.Windows;
using Logging.Interfaces;
using Microsoft.Win32;
using SecsGemBaseItems;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Data_Containers.Interfaces;
using SecsGemBaseItems.Enums;
using SecsGemBaseItems.LibraryManager;
using SecsGemMessageHandling.Data_Handling;
using SecsGemMessageHandling.Events;

namespace EvenBetterFastSim.WPF.ViewModels;

/// <summary>
/// Contains the data and commands for the main window
/// </summary>
public partial class MainViewModel : ObservableObject, IBaseViewModel
{
    public Action? CloseAction { get; set; }
    public CommunicationHandler MessageHandler { get; }
    private readonly SecsGemEventReportHandler eventReportHandler;

    /// <summary>
    /// UI representation of the current connection status
    /// </summary>
    [ObservableProperty]
    public partial ConnectionButtonValues ConnectionValues { get; private set; } =
        ConnectionButtonValues.ConnectionButtonValuesMap[0];

    /// <summary>
    /// Contains the library of SECS/GEM transactions
    /// </summary>
    public ObservableCollection<SecsGemTransaction> SecsGemMessageLibrary { get; }

    /// <summary>
    /// Contains messages directed at the user for informational purposes
    /// </summary>
    public ObservableCollection<LoggedString> LoggedStringCollection { get; }

    /// <summary>
    /// Contains list of SECS/GEM messages sent and received
    /// </summary>
    public ObservableCollection<ILoggedSecsGemMessage> SecsGemMessagesLog { get; }

    public ReadOnlyObservableCollection<EventReportViewModel> SecsGemEventCollection { get; }
    public ReadOnlyObservableCollection<ReportViewModel> SecsGemReportCollection { get; }
    public ReadOnlyObservableCollection<VariableViewModel> SecsGemVariableCollection { get; }

    public IDataItem? SelectedItem
    {
        get => LibraryManager.SelectedItem;
        set => LibraryManager.SelectedItem = value;
    }

    private ILogger<MainViewModel> Logger { get; }
    private ViewModelLocator ViewModelLocator { get; }
    private ISecsGemLibraryManager LibraryManager { get; }
    private SaveToJsonService SaveToJsonService { get; }
    private LibraryXmlExportService LibraryXmlExportService { get; }
    private LibraryJsonService LibraryJsonService { get; }
    private LibraryMessagePackService LibraryMessagePackService { get; }
    private ApplicationSettings ApplicationSettings { get; }
    private IWindowManager WindowManager { get; }
    private ModelViewModelMapper ModelViewModelMapper { get; }
    public ControlMessageHandling ControlMessageHandling { get; }
    private TransactionHandler TransactionHandler { get; }
    public DataMessageHandler DataMessageHandler { get; }
    public ControlStateInfo ControlStateInfo { get; }
    public ControlStateHandler ControlStateHandler { get; }
    public EventLibraryManager EventLibraryManager { get; }
    public ScenariosViewModel ScenariosVm { get; }

    /// <summary>
    /// Window/taskbar title. Includes the instance profile name and endpoint so multiple
    /// running instances can be told apart; plain "EvenBetterFastSim" when no profile is active.
    /// </summary>
    public string WindowTitle =>
        InstanceContext.ProfileName is { } profile
            ? $"EvenBetterFastSim — {profile}  ({ApplicationSettings.NetworkSettings.ConnectionMode} :{ApplicationSettings.NetworkSettings.Port})"
            : "EvenBetterFastSim";

    public MainViewModel(CommunicationHandler messageHandler,
        IWindowManager windowManager,
        ViewModelLocator viewModelLocator,
        ILogService<LoggedString> logger,
        ILogger<MainViewModel> mainLogger,
        ISecsMessageLogger secsMessageLogger,
        ISecsGemLibraryManager libraryManager,
        SaveToJsonService saveToJsonService,
        LibraryXmlExportService libraryXmlExportService,
        LibraryJsonService libraryJsonService,
        LibraryMessagePackService libraryMessagePackService,
        ApplicationSettings applicationSettings,
        ModelViewModelMapper modelViewModelMapper,
        ControlMessageHandling controlMessageHandling,
        TransactionHandler transactionHandler,
        DataMessageHandler dataMessageHandler,
        ControlStateInfo controlStateInfo,
        ControlStateHandler controlStateHandler,
        EventLibraryManager eventLibraryManager,
        SecsGemEventReportHandler eventReportHandler,
        ScenariosViewModel scenariosVm,
        SpecialCasesHandling specialCasesHandling)
    {
        Logger = mainLogger;
        MessageHandler = messageHandler;
        SecsGemMessageLibrary = libraryManager.Library;
        LoggedStringCollection = logger.LogMessages;
        SecsGemMessagesLog = secsMessageLogger.MessagesLog;
        ViewModelLocator = viewModelLocator;
        LibraryManager = libraryManager;
        SaveToJsonService = saveToJsonService;
        LibraryXmlExportService = libraryXmlExportService;
        LibraryJsonService = libraryJsonService;
        LibraryMessagePackService = libraryMessagePackService;
        ApplicationSettings = applicationSettings;
        WindowManager = windowManager;
        ModelViewModelMapper = modelViewModelMapper;
        ControlMessageHandling = controlMessageHandling;
        TransactionHandler = transactionHandler;
        DataMessageHandler = dataMessageHandler;
        ControlStateInfo = controlStateInfo;
        ControlStateHandler = controlStateHandler;
        EventLibraryManager = eventLibraryManager;
        this.eventReportHandler = eventReportHandler;
        ScenariosVm = scenariosVm;

        SecsGemEventCollection = eventLibraryManager.EventCollection;
        SecsGemReportCollection = eventLibraryManager.ReportCollection;
        SecsGemVariableCollection = eventLibraryManager.VariableCollection;

        // Subscribe to PropertyChanged event to update commands when the selected item changes
        LibraryManager.PropertyChanged += UpdateSelectedItemChanged;
        MessageHandler.PropertyChanged += UpdateConnectionStatusChanged;
        DataMessageHandler.PropertyChanged += OnCommunicationsEstablishedChanged;
        ControlStateInfo.PropertyChanged += OnControlSubstateChanged;

        ConfigureSecsGemLogger(secsMessageLogger);
        ModelViewModelMapper = modelViewModelMapper;
    }

    /// <summary>
    /// Called by <see cref="MainWindow"/> before closing
    /// </summary>
    public void OnClosing()
    {
        ScenariosVm.SaveAll();
        SaveToJsonService.Save();
    }

    /// <summary>
    /// Modifies the selected IDataItem
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanModifySelectedItem))]
    private void ModifySelectedItem()
    {
        if (!ModelViewModelMapper.GetViewModel(LibraryManager.SelectedItem, out var viewModelType)) return;
        var viewModelInstance = CreateViewModelInstance(viewModelType);
        if (viewModelInstance != null) WindowManager.ShowDialog(viewModelInstance);
    }

    private IBaseViewModel? CreateViewModelInstance(Type viewModelType)
    {
        var genericMethodInfo = typeof(ViewModelLocator).GetMethod(nameof(ViewModelLocator.GetViewModel));
        var makeGenericMethod = genericMethodInfo?.MakeGenericMethod(viewModelType);
        var viewModelInstance = makeGenericMethod?.Invoke(ViewModelLocator, null);
        return viewModelInstance as IBaseViewModel;
    }

    private bool CanModifySelectedItem()
    {
        return (LibraryManager.SelectedItem is not null);
    }

    [RelayCommand(CanExecute = nameof(CanSendTransaction))]
    private void SendTransaction()
    {
        if (LibraryManager.SelectedItem is SecsGemTransaction transaction)
            DataMessageHandler.SendDataMessage(transaction.PrimaryMessage);
    }

    private bool CanSendTransaction()
    {
        if (LibraryManager.SelectedItem is SecsGemTransaction transaction)
        {
            return DataMessageHandler.CanSendMessage(transaction.PrimaryMessage);
        }

        return false;
    }

    [RelayCommand]
    private void AddTransaction()
    {
        LibraryManager.AddItemToLibrary(new SecsGemTransaction());
    }

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private void AddItem()
    {
        if (LibraryManager.SelectedItem is ICanBeParent parent)
            SecsGemItem.Create(SecsGemItemFormatType.U1).SetParent(parent);
    }

    private bool CanAddItem()
    {
        return LibraryManager.SelectedItem is SecsGemDataMessage ||
               LibraryManager.SelectedItem is SecsGemItem { FormatType: SecsGemItemFormatType.List };
    }

    [RelayCommand(CanExecute = nameof(CanDuplicateSelectedItem))]
    private void DuplicateSelectedItem()
    {
        if (LibraryManager.SelectedItem is SecsGemTransaction transaction)
            LibraryManager.AddItemToLibrary(transaction.Clone());
        else if (LibraryManager.SelectedItem is SecsGemItem item)
            item.AddSibling(item.Clone());
    }

    private bool CanDuplicateSelectedItem()
    {
        return LibraryManager.SelectedItem is SecsGemTransaction or SecsGemItem;
    }

    [RelayCommand(CanExecute = nameof(CanDeleteSelectedItem))]
    private void DeleteSelectedItem()
    {
        if (LibraryManager.SelectedItem is SecsGemTransaction transaction)
        {
            var idx = SecsGemMessageLibrary.IndexOf(transaction);
            SecsGemMessageLibrary.Remove(transaction);
            SelectedItem = GetAdjacentAfterRemoval(idx, SecsGemMessageLibrary);
        }
        else if (LibraryManager.SelectedItem is SecsGemItem item)
        {
            var container = FindItemContainer(item);
            item.SetParent(null);
            if (container.HasValue)
            {
                var (children, idx, parent) = container.Value;
                var count = children.Count;
                SelectedItem = count > 0 ? children[Math.Min(idx, count - 1)] as IDataItem : parent;
            }
        }
    }

    private bool CanDeleteSelectedItem()
    {
        return LibraryManager.SelectedItem is SecsGemTransaction or SecsGemItem;
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedItemUp))]
    private void MoveSelectedItemUp()
    {
        if (LibraryManager.SelectedItem is SecsGemTransaction transaction)
        {
            var idx = SecsGemMessageLibrary.IndexOf(transaction);
            if (idx > 0)
                SecsGemMessageLibrary.Move(idx, idx - 1);
        }
        else if (LibraryManager.SelectedItem is SecsGemItem item)
        {
            var container = FindItemContainer(item);
            if (container is var (children, idx, _) && idx > 0 && children is ObservableCollection<IDataItem> col)
                col.Move(idx, idx - 1);
        }
        MoveSelectedItemUpCommand.NotifyCanExecuteChanged();
        MoveSelectedItemDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveSelectedItemUp()
    {
        if (LibraryManager.SelectedItem is SecsGemTransaction t)
            return SecsGemMessageLibrary.IndexOf(t) > 0;
        if (LibraryManager.SelectedItem is SecsGemItem item)
        {
            var container = FindItemContainer(item);
            return container is var (_, idx, _) && idx > 0;
        }
        return false;
    }

    [RelayCommand(CanExecute = nameof(CanMoveSelectedItemDown))]
    private void MoveSelectedItemDown()
    {
        if (LibraryManager.SelectedItem is SecsGemTransaction transaction)
        {
            var idx = SecsGemMessageLibrary.IndexOf(transaction);
            if (idx < SecsGemMessageLibrary.Count - 1)
                SecsGemMessageLibrary.Move(idx, idx + 1);
        }
        else if (LibraryManager.SelectedItem is SecsGemItem item)
        {
            var container = FindItemContainer(item);
            if (container is var (children, idx, _) && idx < children.Count - 1 && children is ObservableCollection<IDataItem> col)
                col.Move(idx, idx + 1);
        }
        MoveSelectedItemUpCommand.NotifyCanExecuteChanged();
        MoveSelectedItemDownCommand.NotifyCanExecuteChanged();
    }

    private bool CanMoveSelectedItemDown()
    {
        if (LibraryManager.SelectedItem is SecsGemTransaction t)
            return SecsGemMessageLibrary.IndexOf(t) < SecsGemMessageLibrary.Count - 1;
        if (LibraryManager.SelectedItem is SecsGemItem item)
        {
            var container = FindItemContainer(item);
            return container is var (children, idx, _) && idx < children.Count - 1;
        }
        return false;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task Connect()
    {
        try
        {
            await Task.Run(ToggleConnection).WaitAsync(TimeSpan.FromSeconds(10));
        }
        catch
        {
            // Connection toggle timed out or failed
        }
    }

    private void ToggleConnection()
    {
        if (MessageHandler.ConnectionOn)
            MessageHandler.ClosePort();
        else
            MessageHandler.OpenPort();
        
    }

    [RelayCommand]
    private void OpenSetUpWindow()
    {
        WindowManager.ShowDialog(ViewModelLocator.GetViewModel<SetUpViewModel>());
    }

    [RelayCommand]
    private void InspectLoggedItem(object? parameter)
    {
        if (parameter is not SecsGemItem item) return;
        var vm = ViewModelLocator.GetViewModel<InspectSecsGemItemViewModel>();
        vm.Item = item;
        WindowManager.ShowDialog(vm);
    }

    [RelayCommand]
    private void OpenLibrary()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Open library",
            Filter = "All library files (*.xml;*.json;*.msgpack)|*.xml;*.json;*.msgpack|XML Files (*.xml)|*.xml|JSON Files (*.json)|*.json|MessagePack Files (*.msgpack)|*.msgpack"
        };
        if (dialog.ShowDialog() != true) return;
        var path = dialog.FileName;
        var ext = System.IO.Path.GetExtension(path);
        SecsGemMessageLibrary.Clear();
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
            foreach (var tx in LibraryJsonService.Load(path))
                SecsGemMessageLibrary.Add(tx);
        else if (ext.Equals(".msgpack", StringComparison.OrdinalIgnoreCase))
            foreach (var tx in LibraryMessagePackService.Load(path))
                SecsGemMessageLibrary.Add(tx);
        else
        {
            try
            {
                var parser = new XmlParser(path);
                parser.LoadItems(SecsGemMessageLibrary);
                Logger.LogInformation("Library \"{Path}\" loaded successfully", path);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to load library from \"{Path}\": file is invalid", path);
            }
        }
        ApplicationSettings.DefaultLibraryLoadPath = path;
    }

    [RelayCommand]
    private void SaveLibrary()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save library",
            Filter = "XML Files (*.xml)|*.xml|JSON Files (*.json)|*.json|MessagePack Files (*.msgpack)|*.msgpack",
            FilterIndex = 3,
            AddExtension = true
        };
        if (dialog.ShowDialog() != true) return;
        var path = dialog.FileName;
        var ext = System.IO.Path.GetExtension(path);
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
            LibraryJsonService.Save(path, SecsGemMessageLibrary);
        else if (ext.Equals(".msgpack", StringComparison.OrdinalIgnoreCase))
            LibraryMessagePackService.Save(path, SecsGemMessageLibrary);
        else
            LibraryXmlExportService.Save(path, SecsGemMessageLibrary);

        // Remember the just-saved file so this instance re-opens it on next launch
        // instead of the previously loaded library (persisted on window close).
        ApplicationSettings.DefaultLibraryLoadPath = path;
    }

    [RelayCommand]
    private void ClearMessageLog() => SecsGemMessagesLog.Clear();

    [RelayCommand]
    private void ClearLog() => LoggedStringCollection.Clear();

    [RelayCommand]
    private void SelectedItemChanged(IDataItem selectedTreeItem)
    {
        LibraryManager.SelectedItem = selectedTreeItem;
    }
    [RelayCommand]
    private void TurnOnline()
    {
        ControlStateHandler.TurnOnlineSwitch();
    }
    private bool CanTurnOnline()
    {
        return ControlStateInfo.ControlSubstate == ControlSubstate.EquipmentOffline;
    }
    [RelayCommand]
    private void TurnOffline()
    {
        ControlStateHandler.TurnOfflineSwitch();
    }
    private bool CanTurnOffline()
    {
        return ControlStateInfo.ControlSubstate is not ControlSubstate.EquipmentOffline and not ControlSubstate.AttemptOnline;
    }
    /// <summary>
    /// True when the online substate (current or next time the equipment goes online) is Remote.
    /// Drives the Local/Remote toggle button.
    /// </summary>
    public bool IsOnlineRemote => ControlStateInfo.OnlineSubstate == ControlSubstate.OnlineRemote;

    /// <summary>Caption for the Local/Remote toggle button.</summary>
    public string OnlineModeLabel => IsOnlineRemote ? "Remote" : "Local";

    /// <summary>
    /// Toggles the online substate: Local → Remote → Local. If the equipment is already
    /// online the change takes effect immediately; otherwise it sets which substate the
    /// equipment will enter next time it goes online.
    /// </summary>
    [RelayCommand]
    private async Task ToggleOnlineMode()
    {
        if (IsOnlineRemote)
            await ControlStateHandler.TurnLocalSwitch();
        else
            await ControlStateHandler.TurnRemoteSwitch();

        OnPropertyChanged(nameof(IsOnlineRemote));
        OnPropertyChanged(nameof(OnlineModeLabel));
    }

    /// <summary>
    /// When true, the simulator automatically requests ON-LINE once communications are
    /// established after connecting. Persisted via <see cref="ApplicationSettings"/>.
    /// </summary>
    public bool AutoStartOnline
    {
        get => ApplicationSettings.AutoStartOnline;
        set
        {
            if (ApplicationSettings.AutoStartOnline == value) return;
            ApplicationSettings.AutoStartOnline = value;
            OnPropertyChanged();
        }
    }

    private void UpdateSelectedItemChanged(object? o, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ISecsGemLibraryManager.SelectedItem)) return;

        OnPropertyChanged(nameof(SelectedItem));
        AddItemCommand.NotifyCanExecuteChanged();
        ModifySelectedItemCommand.NotifyCanExecuteChanged();
        SendTransactionCommand.NotifyCanExecuteChanged();
        DuplicateSelectedItemCommand.NotifyCanExecuteChanged();
        DeleteSelectedItemCommand.NotifyCanExecuteChanged();
        MoveSelectedItemUpCommand.NotifyCanExecuteChanged();
        MoveSelectedItemDownCommand.NotifyCanExecuteChanged();
    }

    private void UpdateConnectionStatusChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MessageHandler.ConnectionStatus)) return;

        ConnectionValues = ConnectionButtonValues.ConnectionButtonValuesMap[MessageHandler.ConnectionStatus];
    }

    private void OnCommunicationsEstablishedChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(DataMessageHandler.CommunicationsEstablished)) return;
        if (!DataMessageHandler.CommunicationsEstablished || !AutoStartOnline) return;

        // TurnOnlineSwitch is a no-op unless the equipment is currently offline.
        _ = ControlStateHandler.TurnOnlineSwitch();
    }

    private void OnControlSubstateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ControlStateInfo.ControlSubstate)) return;

        OnPropertyChanged(nameof(IsOnlineRemote));
        OnPropertyChanged(nameof(OnlineModeLabel));
    }

    private void ConfigureSecsGemLogger(ISecsMessageLogger logger)
    {
        MessageHandler.OnDataMessageIn.Subscribe(logger.MessageIn);
        MessageHandler.OnDataMessageOut.Subscribe(logger.MessageOut);
        MessageHandler.OnControlMessageIn.Subscribe(logger.ControlMessageIn);
        MessageHandler.OnControlMessageOut.Subscribe(logger.ControlMessageOut);
    }

    // ── Event ↔ Report linking ────────────────────────────────────────────

    private static T? GetAdjacentAfterRemoval<T>(int removedIndex, IReadOnlyList<T> collection)
    {
        var count = collection.Count;
        return count > 0 ? collection[Math.Min(removedIndex, count - 1)] : default;
    }

    private (IList container, int index, IDataItem parent)? FindItemContainer(IDataItem target)
    {
        foreach (var tx in SecsGemMessageLibrary)
        {
            if (tx.PrimaryMessage.Children is IList pc)
            {
                var r = FindInList(pc, target, tx.PrimaryMessage);
                if (r != null) return r;
            }
            if (tx.PrimaryMessage.Reply && tx.ReplyMessage.Children is IList rc)
            {
                var r = FindInList(rc, target, tx.ReplyMessage);
                if (r != null) return r;
            }
        }
        return null;
    }

    private static (IList container, int index, IDataItem parent)? FindInList(
        IList children, IDataItem target, IDataItem parent)
    {
        for (var i = 0; i < children.Count; i++)
        {
            if (ReferenceEquals(children[i], target))
                return (children, i, parent);
            if (children[i] is SecsGemItem item && item.Children is IList nested)
            {
                var r = FindInList(nested, target, item);
                if (r != null) return r;
            }
        }
        return null;
    }

    public ObservableCollection<CheckableItem> LinkableReports { get; } = [];

    private void RefreshLinkedReports(ImmutableList<int> linkedIds)
    {
        LinkableReports.Clear();
        foreach (var report in EventLibraryManager.ReportCollection)
        {
            var linked = linkedIds.Contains(report.Model.Rptid);
            LinkableReports.Add(new CheckableItem(report.Header, linked, isNowLinked =>
                ToggleReportLink(report, isNowLinked)));
        }
    }

    private void ToggleReportLink(ReportViewModel report, bool link)
    {
        if (EventLibraryManager.SelectedEvent == null) return;
        var oldEvent = EventLibraryManager.SelectedEvent.Model;
        var newIds = link
            ? oldEvent.ReportList.Add(report.Model.Rptid)
            : oldEvent.ReportList.Remove(report.Model.Rptid);
        EventLibraryManager.SecsGemEventRegistry.Update(oldEvent with { ReportList = newIds }, oldEvent);
    }

    // ── Report ↔ Variable linking ─────────────────────────────────────────

    public ObservableCollection<CheckableItem> LinkableVariables { get; } = [];

    private void RefreshLinkedVariables(ImmutableList<int> linkedIds)
    {
        LinkableVariables.Clear();
        foreach (var variable in EventLibraryManager.VariableCollection)
        {
            var linked = linkedIds.Contains(variable.Model.VariableId);
            LinkableVariables.Add(new CheckableItem(variable.Header, linked, isNowLinked =>
                ToggleVariableLink(variable, isNowLinked)));
        }
    }

    private void ToggleVariableLink(VariableViewModel variable, bool link)
    {
        if (EventLibraryManager.SelectedReport == null) return;
        var oldReport = EventLibraryManager.SelectedReport.Model;
        var newIds = link
            ? oldReport.Variables.Add(variable.Model.VariableId)
            : oldReport.Variables.Remove(variable.Model.VariableId);
        EventLibraryManager.SecsGemReportRegistry.Update(oldReport with { Variables = newIds }, oldReport);
    }

    [RelayCommand(CanExecute = nameof(ValidSelectedEvent))]
    private void ModifyEvent()
    {
        if (EventLibraryManager.SelectedEvent == null) return;
        var vm = ViewModelLocator.GetViewModel<AddEventReportViewModel>();
        vm.LoadForEdit(EventLibraryManager.SelectedEvent.Model);
        WindowManager.ShowDialog(vm);
    }

    [RelayCommand]
    private void AddEvent()
    {
        WindowManager.ShowDialog(ViewModelLocator.GetViewModel<AddEventReportViewModel>());
    }

    [RelayCommand(CanExecute = nameof(ValidSelectedEvent))]
    private void DeleteEvent()
    {
        var selected = EventLibraryManager.SelectedEvent;
        if (selected == null) return;
        var idx = EventLibraryManager.EventCollection.IndexOf(selected);
        EventLibraryManager.SecsGemEventRegistry.Delete(selected.Model);
        EventLibraryManager.SelectedEvent = GetAdjacentAfterRemoval(idx, EventLibraryManager.EventCollection);
    }
    [RelayCommand(CanExecute = nameof(ValidSelectedEvent))]
    private void SendEvent()
    {
        if (EventLibraryManager.SelectedEvent != null)
            eventReportHandler.SendS6F11Message(EventLibraryManager.SelectedEvent.Model.Ceid);
    }

    [RelayCommand]
    private void SelectedEventChanged(object? selectedTreeItem)
    {
        if (selectedTreeItem is not EventReportViewModel eventReport)
            return;
        EventLibraryManager.SelectedEvent = eventReport;
        DeleteEventCommand.NotifyCanExecuteChanged();
        ModifyEventCommand.NotifyCanExecuteChanged();
        SendEventCommand.NotifyCanExecuteChanged();
        RefreshLinkedReports(eventReport.Model.ReportList);
    }
    private bool ValidSelectedEvent()
    {
        return EventLibraryManager.SelectedEvent is not null;
    }

    [RelayCommand]
    private void SaveEvents()
    {
        EventLibraryManager.Save();
    }
    [RelayCommand]
    private void LoadEvents()
    {
        EventLibraryManager.Load();
    }
    [RelayCommand(CanExecute = nameof(ValidSelectedReport))]
    private void ModifyReport()
    {
        if (EventLibraryManager.SelectedReport == null) return;
        var vm = ViewModelLocator.GetViewModel<AddReportViewModel>();
        vm.LoadForEdit(EventLibraryManager.SelectedReport.Model);
        WindowManager.ShowDialog(vm);
    }

    [RelayCommand]
    private void AddReport()
    {
        WindowManager.ShowDialog(ViewModelLocator.GetViewModel<AddReportViewModel>());
    }

    [RelayCommand(CanExecute = nameof(ValidSelectedReport))]
    private void DeleteReport()
    {
        var selected = EventLibraryManager.SelectedReport;
        if (selected == null) return;
        var idx = EventLibraryManager.ReportCollection.IndexOf(selected);
        EventLibraryManager.SecsGemReportRegistry.Delete(selected.Model);
        EventLibraryManager.SelectedReport = GetAdjacentAfterRemoval(idx, EventLibraryManager.ReportCollection);
    }
    [RelayCommand]
    private void SelectedReportChanged(object? selectedTreeItem)
    {
        if (selectedTreeItem is not ReportViewModel report) return;
        EventLibraryManager.SelectedReport = report;
        DeleteReportCommand.NotifyCanExecuteChanged();
        ModifyReportCommand.NotifyCanExecuteChanged();
        RefreshLinkedVariables(report.Model.Variables);
    }
    private bool ValidSelectedReport()
    {
        return EventLibraryManager.SelectedReport is not null;
    }
    [RelayCommand(CanExecute = nameof(ValidSelectedVariable))]
    private void ModifyVariable()
    {
        if (EventLibraryManager.SelectedVariable == null) return;
        var vm = ViewModelLocator.GetViewModel<AddEquipmentVariableViewModel>();
        vm.LoadForEdit(EventLibraryManager.SelectedVariable.Model);
        WindowManager.ShowDialog(vm);
    }

    [RelayCommand]
    private void AddVariable()
    {
        WindowManager.ShowDialog(ViewModelLocator.GetViewModel<AddEquipmentVariableViewModel>());
    }

    [RelayCommand(CanExecute = nameof(ValidSelectedVariable))]
    private void DeleteVariable()
    {
        var selected = EventLibraryManager.SelectedVariable;
        if (selected == null) return;
        var idx = EventLibraryManager.VariableCollection.IndexOf(selected);
        EventLibraryManager.SecsGemEquipmentVariableRegistry.Delete(selected.Model);
        EventLibraryManager.SelectedVariable = GetAdjacentAfterRemoval(idx, EventLibraryManager.VariableCollection);
    }
    [RelayCommand]
    private void SelectedVariableChanged(object? selectedTreeItem)
    {
        if (selectedTreeItem is not VariableViewModel variable) return;
        EventLibraryManager.SelectedVariable = variable;
        DeleteVariableCommand.NotifyCanExecuteChanged();
        ModifyVariableCommand.NotifyCanExecuteChanged();
    }
    private bool ValidSelectedVariable()
    {
        return EventLibraryManager.SelectedVariable is not null;
    }
}