using System;
using System.IO;
using System.Text.Json;
using EvenBetterFastSim.Services;
using TCPIPBaseLibrary;

namespace EvenBetterFastSim.Services.JSON;

public class SaveToJsonService(ApplicationSettings applicationSettings)
{
    /// <summary>
    /// Settings file for this process. Redirected per <see cref="InstanceContext"/> so each
    /// launched instance keeps its own copy; falls back to <see cref="LegacyUserSettingsPath"/>
    /// when no profile is active.
    /// </summary>
    public static string UserSettingsPath =>
        Path.Combine(InstanceContext.SettingsDirectory, "usersettings.json");

    /// <summary>The pre-hub, shared settings location (%APPDATA%\EvenBetterFastSim\usersettings.json).</summary>
    public static string LegacyUserSettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EvenBetterFastSim",
        "usersettings.json");

    private ApplicationSettings ApplicationSettings { get; } = applicationSettings;

    public void Save()
    {
        var json = JsonSerializer.Serialize(ApplicationSettings, new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters = { new NetworkSettingsConverter() }
        });

        Directory.CreateDirectory(Path.GetDirectoryName(UserSettingsPath)!);
        File.WriteAllText(UserSettingsPath, json);
    }
}
