using EvenBetterFastSim.WPF.ViewModels;

namespace EvenBetterFastSim.Services;

public interface IWindowManager
{
    bool? ShowDialog(IBaseViewModel viewModel);
    void ShowWindow(IBaseViewModel viewModel);
}