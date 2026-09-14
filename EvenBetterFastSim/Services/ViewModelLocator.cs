using System;
using EvenBetterFastSim.WPF.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace EvenBetterFastSim.Services;

public class ViewModelLocator(IServiceProvider serviceProvider)
{
    public TViewModel GetViewModel<TViewModel>() where TViewModel : IBaseViewModel
    {
        return serviceProvider.GetRequiredService<TViewModel>();
    }
}