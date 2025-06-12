namespace Game;

public interface ISessionServices
{
    IReadOnlyDictionary<string, IService> Entries { get; }
    
    void Add(IService service);
}

public class SessionServices : ISessionServices
{
    private readonly Dictionary<string, IService> _services = new();

    public IReadOnlyDictionary<string, IService> Entries => _services;
    
    public void Add(IService service)
    {
        _services.Add(service.Key, service);
    }
}