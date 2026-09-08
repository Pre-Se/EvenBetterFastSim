using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using EvenBetterFastSim.Services.JSON;
using Microsoft.Extensions.Logging;

namespace EvenBetterFastSim.Services;

/// <summary>
/// Persists the list of <see cref="InstanceProfile"/>s the hub can launch and seeds each
/// profile's isolated <c>usersettings.json</c> so the launched process starts on the right
/// endpoint.
/// </summary>
public class InstanceProfileStore(ILogger<InstanceProfileStore> logger)
{
    private static readonly string ProfilesIndexPath =
        Path.Combine(InstanceContext.ProfilesRoot, "profiles.json");

    private static readonly JsonSerializerOptions IndexOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILogger<InstanceProfileStore> logger = logger;

    /// <summary>Loads the saved profiles, or an empty list on first run / read failure.</summary>
    public List<InstanceProfile> Load()
    {
        try
        {
            if (!File.Exists(ProfilesIndexPath))
                return [];

            var json = File.ReadAllText(ProfilesIndexPath);
            return JsonSerializer.Deserialize<List<InstanceProfile>>(json, IndexOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load instance profiles from {Path}", ProfilesIndexPath);
            return [];
        }
    }

    /// <summary>Writes the profile list back to disk.</summary>
    public void Save(IEnumerable<InstanceProfile> profiles)
    {
        try
        {
            Directory.CreateDirectory(InstanceContext.ProfilesRoot);
            var json = JsonSerializer.Serialize(profiles.ToList(), IndexOptions);
            File.WriteAllText(ProfilesIndexPath, json);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save instance profiles to {Path}", ProfilesIndexPath);
        }
    }

    /// <summary>True when <paramref name="name"/> is a usable, unique profile name.</summary>
    public static bool IsValidName(string? name, IEnumerable<InstanceProfile> existing, InstanceProfile? self = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (name.Any(c => Path.GetInvalidFileNameChars().Contains(c))) return false;
        return !existing.Any(p => !ReferenceEquals(p, self) &&
                                  string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Writes the profile's endpoint into <c>profiles\&lt;Name&gt;\usersettings.json</c>,
    /// preserving any other settings the user changed from inside that instance. Creates
    /// the file (seeded from the legacy settings when present) on first launch.
    /// </summary>
    public void SeedSettingsFile(InstanceProfile profile)
    {
        var dir = InstanceContext.SettingsDirectoryFor(profile.Name);
        var path = Path.Combine(dir, "usersettings.json");

        var firstSeed = !File.Exists(path);

        try
        {
            Directory.CreateDirectory(dir);

            JsonObject root = ReadObject(path)
                              ?? ReadObject(SaveToJsonService.LegacyUserSettingsPath)
                              ?? new JsonObject();

            root["NetworkSettings"] = new JsonObject
            {
                ["IpAddress"] = profile.IpAddress,
                ["Port"] = profile.Port.ToString(),
                ["ConnectionMode"] = profile.ConnectionMode.ToString()
            };

            root["HsmsParameters"] ??= DefaultHsmsParameters();
            root["DefaultLibraryLoadPath"] ??= "Library\\SECSGEM_Library.xml";

            // New instances default to sending the HSMS Select.req on connect so they
            // reach the SELECTED state without the user toggling it. Later edits made from
            // inside the instance are preserved (only forced on first creation).
            if (firstSeed && root["HsmsParameters"] is JsonObject hsms)
                hsms["InitiateSelectRequest"] = true;

            File.WriteAllText(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to seed settings for profile {Profile} at {Path}", profile.Name, path);
        }
    }

    private static JsonObject? ReadObject(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            return JsonNode.Parse(File.ReadAllText(path)) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static JsonObject DefaultHsmsParameters() => new()
    {
        ["T3"] = 45000,
        ["T5"] = 10000,
        ["T6"] = 5000,
        ["T7"] = 10000,
        ["T8"] = 10000,
        ["IgnoreState"] = false,
        ["SessionId"] = 0,
        ["InitiateSelectRequest"] = true
    };
}
