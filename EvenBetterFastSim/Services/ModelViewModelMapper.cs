using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using EvenBetterFastSim.WPF.ViewModels;
using Microsoft.Extensions.Logging;
using SecsGemBaseItems.Data_Containers;
using SecsGemBaseItems.Data_Containers.Interfaces;

namespace EvenBetterFastSim.Services;

/// <summary>
/// Maps models to view models
/// </summary>
public class ModelViewModelMapper
{
    private readonly Dictionary<Type, Type> mappings = [];
    private readonly ILogger logger;

    public ModelViewModelMapper(ILogger<ModelViewModelMapper> logger)
    {
        RegisterMapping<SecsGemTransaction, SecsGemTransactionViewModel>();
        RegisterMapping<SecsGemDataMessage, SecsGemDataMessageViewModel>();
        RegisterMapping<SecsGemItem, SecsGemItemViewModel>();
        this.logger = logger;
    }

    /// <summary>
    /// Registers a mapping between a <see cref="IDataItem"/> and a <see cref="IBaseViewModel"/>
    /// </summary>
    private void RegisterMapping<TModel, TViewModel>() where TModel : IDataItem where TViewModel : IBaseViewModel
    {
        mappings[typeof(TModel)] = typeof(TViewModel);
    }

    public bool GetViewModel(IDataItem? dataItem, [NotNullWhen(true)]out Type? viewModelType)
    {
        if (dataItem != null)
        {
            var type = dataItem.GetType();
            while (type != null)
            {
                if (mappings.TryGetValue(type, out viewModelType))
                    return true;
                type = type.BaseType;
            }
        }
        logger.LogError($"No view model registered for selected item: {dataItem?.GetType().Name}");
        viewModelType = null;
        return false;
    }
}
