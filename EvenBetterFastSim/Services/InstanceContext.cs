using System;
using System.IO;
using System.Linq;

namespace EvenBetterFastSim.Services;

/// <summary>
/// Identifies which "instance profile" this process is running as. Set once at startup
/// from the <c>--profile &lt;name&gt;</c> command-line argument (see <see cref="InitFromCommandLine"/>).
///
/// When no profile is given the process behaves exactly as before: settings live in the
/// legacy <c>%APPDATA%\EvenBetterFastSim</c> directory. When a profile is given every
/// per-instance file (usersettings.json, scenarios.json, ...) is redirected under
/// <c>%APPDATA%\EvenBetterFastSim\profiles\&lt;name&gt;</c> so multiple instances never
/// clobber each other.
/// </summary>
public static class InstanceContext
{
    /// <summary>Argument that selects a profile, e.g. <c>EvenBetterFastSim.exe --profile demo-active</c>.</summary>
    public const string ProfileArgument = "--profile";

    private static readonly string RootDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EvenBetterFastSim");

    /// <summary>Directory that holds every profile's isolated settings folder.</summary>
    public static string ProfilesRoot { get; } = Path.Combine(RootDirectory, "profiles");

    /// <summary>The active profile name, or <c>null</c> when running without a profile.</summary>
    public static string? ProfileName { get; private set; }

    /// <summary>
    /// Directory this process reads/writes its per-instance settings from. Legacy
    /// <c>%APPDATA%\EvenBetterFastSim</c> when no profile is active, otherwise the
    /// profile's own sub-folder.
    /// </summary>
    public static string SettingsDirectory =>
        ProfileName is { Length: > 0 } name
            ? Path.Combine(ProfilesRoot, name)
            : RootDirectory;

    /// <summary>Absolute settings folder for an arbitrary profile (used by the hub when seeding).</summary>
    public static string SettingsDirectoryFor(string profileName) => Path.Combine(ProfilesRoot, profileName);

    /// <summary>
    /// Reads <c>--profile &lt;name&gt;</c> from the process command line. Safe to call
    /// more than once; later calls are ignored.
    /// </summary>
    public static void InitFromCommandLine()
    {
        if (ProfileName is not null) return;

        var args = Environment.GetCommandLineArgs();
        var index = Array.FindIndex(args, a =>
            string.Equals(a, ProfileArgument, StringComparison.OrdinalIgnoreCase));

        if (index >= 0 && index + 1 < args.Length)
        {
            var name = args[index + 1].Trim();
            if (name.Length > 0)
                ProfileName = name;
        }
    }
}
