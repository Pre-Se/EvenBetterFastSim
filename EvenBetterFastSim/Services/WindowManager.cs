using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using EvenBetterFastSim.WPF.ViewModels;

namespace EvenBetterFastSim.Services;

/// <summary>
/// Displays a window with the given <see cref="IBaseViewModel"/>.
/// </summary>
internal class WindowManager(WindowMapper windowMapper) : IWindowManager
{
    /// <summary>
    /// Shows a window as a dialog with the given <see cref="IBaseViewModel"/>.
    /// </summary>
    /// <returns><inheritdoc cref="Window.ShowDialog"/></returns>
    public bool? ShowDialog(IBaseViewModel viewModel)
    {
        var window = GetWindow(viewModel);

        // Anchor the modal to the currently active window so it can never open behind its
        // parent (which would leave the parent modally disabled and looking frozen).
        var owner = Application.Current?.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive && !ReferenceEquals(w, window));
        if (owner != null)
            window.Owner = owner;

        return window.ShowDialog();
    }

    /// <summary>
    /// Shows a window with the given <see cref="IBaseViewModel"/>.
    /// </summary>
    public void ShowWindow(IBaseViewModel viewModel)
    {
        var window = GetWindow(viewModel);
        window.Show();
    }

    private Window GetWindow(IBaseViewModel viewModel)
    {
        var windowType = windowMapper.GetWindowTypeForViewModel(viewModel) ?? throw new Exception();
        var window = Activator.CreateInstance(windowType) as Window;
        Debug.Assert(window != null, nameof(window) + " != null");
        window.DataContext = viewModel;
        viewModel.CloseAction = window.Close;
        return window;
    }
}