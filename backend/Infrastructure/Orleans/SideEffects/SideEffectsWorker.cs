using Common.Reactive;

namespace Infrastructure;

public class SideEffectsWorker : ICoordinatorSetupCompleted
{
    public SideEffectsWorker(ISideEffectsStorage storage)
    {
        _storage = storage;
    }

    private readonly ISideEffectsStorage _storage;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        
    }

    private async Task Loop(IReadOnlyLifetime lifetime)
    {
            
    }
}