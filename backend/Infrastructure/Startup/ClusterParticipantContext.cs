using Common.Reactive;

namespace Infrastructure.Startup;

public interface IClusterParticipantContext
{
    IViewableProperty<bool> IsInitialized { get; }
    
    void Initialize();
}

public class ClusterParticipantContext : IClusterParticipantContext
{
    private readonly ViewableProperty<bool> _isInitialized = new(false);

    public IViewableProperty<bool> IsInitialized => _isInitialized;

    public void Initialize()
    {
        _isInitialized.Set(true);
    }
}
