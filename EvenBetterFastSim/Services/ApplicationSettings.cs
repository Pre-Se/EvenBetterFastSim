using EvenBetterFastSim.WPF.LibraryManager;
using Microsoft.Extensions.Configuration;
using SecsGemBaseItems.SecsGemParameters;
using TCPIPBaseLibrary.Interfaces;

namespace EvenBetterFastSim.Services;
public class ApplicationSettings
{
    public string? DefaultLibraryLoadPath { get; set; }
    public string ItemDelimiter { get; set; }

    /// <summary>
    /// When true, the simulator automatically requests ON-LINE once communications are
    /// established after a connection. Persisted to usersettings.json.
    /// </summary>
    public bool AutoStartOnline { get; set; }
    public INetworkSettings NetworkSettings { get; }
    public IHSMSParameters HsmsParameters { get; }

    public ApplicationSettings(INetworkSettings networkSettings, IHSMSParameters hsmsParameters, IConfiguration configuration)
    {
        NetworkSettings = networkSettings;
        HsmsParameters = hsmsParameters;
        DefaultLibraryLoadPath = configuration.GetSection(SecsGemLibraryManager.Section).Value;
        ItemDelimiter = configuration.GetSection(nameof(ItemDelimiter)).Value ?? ";";
        AutoStartOnline = bool.TryParse(configuration.GetSection(nameof(AutoStartOnline)).Value, out var autoStartOnline) && autoStartOnline;
        configuration.GetSection(TCPIPBaseLibrary.Network.NetworkSettings.Section).Bind(NetworkSettings);
        configuration.GetSection(HSMSParameters.Section).Bind(HsmsParameters);
    }
}
