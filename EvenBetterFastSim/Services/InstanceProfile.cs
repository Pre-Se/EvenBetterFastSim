using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using TCPIPBaseLibrary.Interfaces;

namespace EvenBetterFastSim.Services;

/// <summary>
/// A named, launchable simulator instance. The hub keeps a list of these and starts one
/// OS process per profile, passing <c>--profile &lt;Name&gt;</c>. The network fields are the
/// profile's identity — they are written into the profile's isolated <c>usersettings.json</c>
/// every time it is launched.
/// </summary>
public partial class InstanceProfile : ObservableObject
{
    [ObservableProperty]
    private string name = "New instance";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Endpoint))]
    private string ipAddress = "127.0.0.1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Endpoint))]
    private ushort port = 5000;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Endpoint))]
    private ConnectionMode connectionMode = ConnectionMode.Passive;

    /// <summary>Human-readable endpoint summary for the hub list, e.g. "Passive · 127.0.0.1:5000".</summary>
    [JsonIgnore]
    public string Endpoint => $"{ConnectionMode} · {IpAddress}:{Port}";
}
