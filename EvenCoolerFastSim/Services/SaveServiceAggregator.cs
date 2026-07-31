using System.Collections.Generic;

namespace EvenBetterFastSim.Services;

public class SaveServiceAggregator
{
    private readonly IList<ISaveService> services = [];

    public SaveServiceAggregator(ISaveService[] services)
    {
        this.services = services;
    }

    public SaveServiceAggregator()
    {

    }

    public void AddService(ISaveService service)
    {
        services.Add(service);
    }

    public void Save()
    {
        foreach (var service in services)
        {
            service.Save();
        }
    }
}