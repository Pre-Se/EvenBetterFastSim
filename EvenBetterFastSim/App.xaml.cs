using EvenBetterFastSim.Logging;
using EvenBetterFastSim.Logging.Interfaces;
using EvenBetterFastSim.Services;
using EvenBetterFastSim.Services.JSON;
using EvenBetterFastSim.WPF.LibraryManager;
using EvenBetterFastSim.WPF.ViewModels;
using EvenBetterFastSim.WPF.ViewModels.Responders;
using Logging.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecsGemBaseItems.LibraryManager;
using SecsGemBaseItems.SecsGemParameters;
using SecsGemMessageHandling.Data_Handling;
using SecsGemMessageHandling.Helpers;
using System;
using System.Windows;
using Microsoft.Extensions.Logging;
using SecsGemHelperClasses;
using SecsGemHelperClasses.Interfaces;
using SecsGemMessageHandling.Events;
using SecsGemMessageHandling.Events.Builders;
using SecsGemMessageHandling.Events.Registry;
using SecsGemMessageHandling.Events.Registry.Interface;
using SecsGemScenarioEngine.Services;
using TCPIPBaseLibrary;
using TCPIPBaseLibrary.Interfaces;
using TCPIPBaseLibrary.Network;
using TCPIPBaseLibrary.TCPBase;

namespace EvenBetterFastSim;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App
{
    private readonly IServiceProvider serviceProvider = ConfigureServices();

    private static ServiceProvider ConfigureServices()
    {
        // Decide which instance profile (if any) this process runs as before any
        // settings path is resolved.
        InstanceContext.InitFromCommandLine();

        var services = new ServiceCollection();

        services.AddSingleton<ILogService<LoggedString>, LogService<LoggedString>>();

        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.Services.AddSingleton<ILoggerProvider, ObservableLoggerProvider>();
        });

        services.AddScoped<MainViewModel>();
        services.AddScoped<LauncherViewModel>();
        services.AddScoped<InstanceProfileStore>();
        services.AddTransient<SetUpViewModel>();
        services.AddTransient<SecsGemDataMessageViewModel>();
        services.AddTransient<SecsGemItemViewModel>();

        services.AddScoped<ViewModelLocator>();
        services.AddScoped<WindowMapper>();
        services.AddScoped<IWindowManager, WindowManager>();
        services.AddScoped<ModelViewModelMapper>();

        services.AddScoped<INetworkSettings, NetworkSettings>();
        services.AddScoped<IHSMSParameters, HSMSParameters>();
        services.AddTransient<INetworkConnectionFactory, NetworkConnectionFactory>();

        services.AddScoped<CommunicationHandler>();
        services.AddScoped<ControlMessageHandling>();
        services.AddScoped<DataMessageHandler>();
        services.AddScoped<TransactionHandler>();
        services.AddScoped<ControlMessageFactory>();

        services.AddScoped<SecsGemEventBuilder>();
        services.AddScoped<SecsGemReportBuilder>();

        services.AddScoped(typeof(IRegistry<>), typeof(SecsGemRegistry<>));
        services.AddScoped<LinkEventReportRegistry>();

        services.AddScoped<EventLibraryManager>();

        services.AddScoped(typeof(ObservableRegistry<,>));

        services.AddTransient<AddEventReportViewModel>();
        services.AddTransient<AddReportViewModel>();
        services.AddTransient<AddEquipmentVariableViewModel>();
        services.AddTransient<InspectSecsGemItemViewModel>();
        services.AddTransient<SecsGemTransactionViewModel>();

        services.AddScoped<SecsGemEventReportHandler>();
        services.AddScoped<SecsGemLinkEventReportBuilder>();

        services.AddScoped<SpecialCasesHandling>();
        services.AddScoped<ControlStateHandler>();
        services.AddScoped<ControlStateInfo>();

        services.AddTransient<TCPIPClientBase>();
        services.AddTransient<TCPIPServerBase>();

        services.AddScoped<ISecsMessageLogger, SecsMessageLogger>();
        services.AddScoped<ISecsGemLibraryManager, SecsGemLibraryManager>();

        services.AddScoped<SaveToJsonService>();
        services.AddScoped<LibraryXmlExportService>();
        services.AddScoped<LibraryJsonService>();
        services.AddScoped<LibraryMessagePackService>();
        services.AddScoped<ApplicationSettings>();

        services.AddScoped<ScenariosViewModel>();
        services.AddScoped<ScenarioExecutionService>();
        services.AddTransient<NodeResponderViewModel>();

        services.AddScoped<IEventBusFactory, EventBusFactory>();

        //TODO: Handle what happens if Max decides to delete my appsettings.json :'(
        IConfiguration configuration = new ConfigurationBuilder()
            .AddJsonFile(SaveToJsonService.UserSettingsPath, optional: true)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);

        return services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        var serviceScope = serviceProvider.CreateScope();
        var viewModelLocator = serviceScope.ServiceProvider.GetRequiredService<ViewModelLocator>();
        var windowManager = serviceScope.ServiceProvider.GetRequiredService<IWindowManager>();

        // No --profile => show the hub that launches profile instances.
        // With --profile => this process *is* an instance; open the simulator directly.
        if (InstanceContext.ProfileName is null)
            windowManager.ShowWindow(viewModelLocator.GetViewModel<LauncherViewModel>());
        else
            windowManager.ShowWindow(viewModelLocator.GetViewModel<MainViewModel>());

        base.OnStartup(e);
    }
}