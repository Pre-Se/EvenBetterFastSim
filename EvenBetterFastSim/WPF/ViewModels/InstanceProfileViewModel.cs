using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TCPIPBaseLibrary.Interfaces;

namespace EvenBetterFastSim.WPF.ViewModels;

/// <summary>
/// Editor for a single <c>InstanceProfile</c>, hosted in the shared <c>DialogWindow</c>
/// (its OK button binds to <see cref="AcceptButtonClickCommand"/>). Also used in
/// "linked pair" mode, where the connection-mode selector is hidden and the name field
/// captures the shared base name of the two profiles the hub will create.
/// </summary>
public partial class InstanceProfileViewModel : ObservableValidator, IBaseViewModel
{
    private readonly Func<string, bool> isNameValid;

    public Action? CloseAction { get; set; }

    /// <summary>True when the user confirmed with OK (vs. cancelling / closing the dialog).</summary>
    public bool Accepted { get; private set; }

    public string Title { get; }
    public string NameLabel { get; }
    public bool ShowConnectionMode { get; }

    /// <summary>
    /// True when the "open instance(s) after creation" checkbox should be shown
    /// (i.e. this dialog is creating profiles, not editing an existing one).
    /// </summary>
    public bool ShowLaunchAfterCreate { get; }

    public string LaunchAfterCreateLabel { get; }

    /// <summary>
    /// When <see cref="ShowLaunchAfterCreate"/> is true, whether the hub should launch the
    /// newly created instance(s) immediately after the dialog is accepted.
    /// </summary>
    [ObservableProperty]
    private bool launchAfterCreate;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(AcceptButtonClickCommand))]
    [CustomValidation(typeof(InstanceProfileViewModel), nameof(ValidateName))]
    private string name;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [NotifyCanExecuteChangedFor(nameof(AcceptButtonClickCommand))]
    [CustomValidation(typeof(InstanceProfileViewModel), nameof(ValidateIpAddress))]
    private string ipAddress;

    [ObservableProperty]
    private ushort port;

    [ObservableProperty]
    private ConnectionMode connectionMode;

    public static IEnumerable<ConnectionMode> ConnectionModeValues => Enum.GetValues<ConnectionMode>();

    public InstanceProfileViewModel(
        string title,
        string nameLabel,
        bool showConnectionMode,
        string name,
        string ipAddress,
        ushort port,
        ConnectionMode connectionMode,
        Func<string, bool> isNameValid,
        bool showLaunchAfterCreate = false,
        string launchAfterCreateLabel = "Open instance after creation")
    {
        Title = title;
        NameLabel = nameLabel;
        ShowConnectionMode = showConnectionMode;
        ShowLaunchAfterCreate = showLaunchAfterCreate;
        LaunchAfterCreateLabel = launchAfterCreateLabel;
        launchAfterCreate = showLaunchAfterCreate;
        this.name = name;
        this.ipAddress = ipAddress;
        this.port = port;
        this.connectionMode = connectionMode;
        this.isNameValid = isNameValid;
        ValidateAllProperties();
    }

    public static ValidationResult? ValidateName(string value, ValidationContext context)
    {
        var vm = (InstanceProfileViewModel)context.ObjectInstance;
        return vm.isNameValid(value)
            ? ValidationResult.Success
            : new ValidationResult("Name is empty, has invalid characters, or is already used");
    }

    public static ValidationResult? ValidateIpAddress(string value, ValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(value)) return ValidationResult.Success; // Passive may listen on any
        return IPAddress.TryParse(value, out _)
            ? ValidationResult.Success
            : new ValidationResult("Not a valid IP address");
    }

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void AcceptButtonClick()
    {
        Accepted = true;
        CloseAction?.Invoke();
    }

    private bool CanAccept() => !HasErrors;
}
