using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace EvenBetterFastSim.WPF.ViewModels.Models;

public partial class CheckableItem(string header, bool isLinked, Action<bool> onChange) : ObservableObject
{
    public string Header { get; } = header;

    [ObservableProperty]
    private bool isLinked = isLinked;

    partial void OnIsLinkedChanged(bool value) => onChange(value);
}
