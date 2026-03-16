using Common.Reactive;
using Infrastructure;

namespace Meta.Bots;

public class ClusterBotsSetup : ICoordinatorSetupCompleted
{
    public ClusterBotsSetup(IBotCollection collection, IBotFactory factory)
    {
        _collection = collection;
        _factory = factory;
    }

    private readonly IBotCollection _collection;
    private readonly IBotFactory _factory;

    public async Task OnCoordinatorSetupCompleted(IReadOnlyLifetime lifetime)
    {
        if (_collection.Count != 0)
            return;

        for (var i = 0; i < 5; i++)
            await _factory.Create($"Bot_{i}");
    }
}