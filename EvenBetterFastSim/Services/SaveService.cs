using SecsGemHelperClasses.Copy;

namespace EvenBetterFastSim.Services;

public class SaveService<T> : ISaveService
{
    private readonly ICopy<T> destination;
    private readonly ICopy<T> source;

    public SaveService(ICopy<T> source, ICopy<T> destination)
    {
        source.CopyFrom(destination.GetCopySource());
        this.source = source;
        this.destination = destination;
    }

    public void Save()
    {
        destination.CopyFrom(source.GetCopySource());
    }
}

public interface ISaveService
{
    public void Save();
}