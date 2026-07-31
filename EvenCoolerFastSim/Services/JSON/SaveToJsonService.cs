using System;
using System.IO;
using System.Text.Json;
using TCPIPBaseLibrary;

namespace EvenBetterFastSim.Services.JSON;

public class SaveToJsonService(ApplicationSettings applicationSettings)
{
    public static string UserSettingsPath { get; } = Path.Combine(
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
