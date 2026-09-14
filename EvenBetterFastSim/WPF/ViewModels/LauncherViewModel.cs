using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EvenBetterFastSim.Services;
using Microsoft.Extensions.Logging;
using TCPIPBaseLibrary.Interfaces;

namespace EvenBetterFastSim.WPF.ViewModels;

/// <summary>
/// The hub window shown when the app starts without <c>--profile</c>. Manages the list of
/// <see cref="InstanceProfile"/>s and launches each as its own OS process.
/// </summary>
public partial class LauncherViewModel : ObservableObject, IBaseViewModel
{
    private readonly InstanceProfileStore store;
    private readonly IWindowManager windowManager;
    private readonly ILogger<LauncherViewModel> logger;

    public Action? CloseAction { get; set; }

    public ObservableCollection<InstanceProfile> Profiles { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LaunchCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateProfileCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteProfileCommand))]
    private InstanceProfile? selectedProfile;

    public LauncherViewModel(InstanceProfileStore store, IWindowManager windowManager,
        ILogger<LauncherViewModel> logger)
    {
        this.store = store;
        this.windowManager = windowManager;
        this.logger = logger;

        Profiles = new ObservableCollection<InstanceProfile>(store.Load());
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private bool HasSelection() => SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Launch() => LaunchProfile(SelectedProfile!);

    /// <summary>Seeds the profile's isolated settings then starts a new simulator process for it.</summary>
    public void LaunchProfile(InstanceProfile profile)
    {
        store.SeedSettingsFile(profile);
        try
        {
            Process.Start(BuildStartInfo(profile.Name));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch instance '{Profile}'", profile.Name);
        }
    }

    private static ProcessStartInfo BuildStartInfo(string profileName)
    {
        var host = Environment.ProcessPath ?? throw new InvalidOperationException("No process path");
        var psi = new ProcessStartInfo
        {
            UseShellExecute = false,
            WorkingDirectory = AppContext.BaseDirectory,
            FileName = host
        };

        // When running under `dotnet` (dev), the managed dll must be the first argument.
        if (string.Equals(Path.GetFileNameWithoutExtension(host), "dotnet", StringComparison.OrdinalIgnoreCase))
            psi.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "EvenBetterFastSim.dll"));

        psi.ArgumentList.Add(InstanceContext.ProfileArgument);
        psi.ArgumentList.Add(profileName);
        return psi;
    }

    [RelayCommand]
    private void NewProfile()
    {
        var draft = new InstanceProfile { Name = UniqueName("instance") };
        var editor = new InstanceProfileViewModel(
            "New instance", "Name", showConnectionMode: true,
            draft.Name, draft.IpAddress, draft.Port, draft.ConnectionMode,
            name => InstanceProfileStore.IsValidName(name, Profiles),
            showLaunchAfterCreate: true,
            launchAfterCreateLabel: "Open instance after creation");

        windowManager.ShowDialog(editor);
        if (!editor.Accepted) return;

        Apply(draft, editor);
        Profiles.Add(draft);
        Persist();
        SelectedProfile = draft;

        if (editor.LaunchAfterCreate)
            LaunchProfile(draft);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void EditProfile()
    {
        var target = SelectedProfile!;
        var editor = new InstanceProfileViewModel(
            "Edit instance", "Name", showConnectionMode: true,
            target.Name, target.IpAddress, target.Port, target.ConnectionMode,
            name => InstanceProfileStore.IsValidName(name, Profiles, target));

        windowManager.ShowDialog(editor);
        if (!editor.Accepted) return;

        Apply(target, editor);
        Persist();
        store.SeedSettingsFile(target);
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DuplicateProfile()
    {
        var src = SelectedProfile!;
        var copy = new InstanceProfile
        {
            Name = UniqueName(src.Name + "-copy"),
            IpAddress = src.IpAddress,
            Port = src.Port,
            ConnectionMode = src.ConnectionMode
        };
        Profiles.Add(copy);
        Persist();
        SelectedProfile = copy;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void DeleteProfile()
    {
        Profiles.Remove(SelectedProfile!);
        Persist();
        SelectedProfile = Profiles.FirstOrDefault();
    }

    [RelayCommand]
    private void NewLinkedPair()
    {
        var editor = new InstanceProfileViewModel(
            "New linked pair", "Base name", showConnectionMode: false,
            UniqueName("link"), "127.0.0.1", 5000, ConnectionMode.Passive,
            baseName => InstanceProfileStore.IsValidName($"{baseName}-passive", Profiles)
                        && InstanceProfileStore.IsValidName($"{baseName}-active", Profiles),
            showLaunchAfterCreate: true,
            launchAfterCreateLabel: "Open both instances after creation");

        windowManager.ShowDialog(editor);
        if (!editor.Accepted) return;

        var passive = new InstanceProfile
        {
            Name = $"{editor.Name}-passive",
            IpAddress = editor.IpAddress,
            Port = editor.Port,
            ConnectionMode = ConnectionMode.Passive
        };
        var active = new InstanceProfile
        {
            Name = $"{editor.Name}-active",
            IpAddress = editor.IpAddress,
            Port = editor.Port,
            ConnectionMode = ConnectionMode.Active
        };

        Profiles.Add(passive);
        Profiles.Add(active);
        store.SeedSettingsFile(passive);
        store.SeedSettingsFile(active);
        Persist();
        SelectedProfile = passive;

        if (editor.LaunchAfterCreate)
        {
            // Start the passive (listening) side first so it is ready when the active side connects.
            LaunchProfile(passive);
            LaunchProfile(active);
        }
    }

    private static void Apply(InstanceProfile profile, InstanceProfileViewModel editor)
    {
        profile.Name = editor.Name.Trim();
        profile.IpAddress = editor.IpAddress.Trim();
        profile.Port = editor.Port;
        profile.ConnectionMode = editor.ConnectionMode;
    }

    private string UniqueName(string baseName)
    {
        if (InstanceProfileStore.IsValidName(baseName, Profiles)) return baseName;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseName}-{i}";
            if (InstanceProfileStore.IsValidName(candidate, Profiles)) return candidate;
        }
    }

    private void Persist() => store.Save(Profiles);
}
