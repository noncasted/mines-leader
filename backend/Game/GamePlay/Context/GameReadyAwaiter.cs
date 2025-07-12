using Common;

namespace Game.GamePlay;

public interface IGameReadyAwaiter
{
    Task Await(IReadOnlyLifetime lifetime);
    void OnPlayerReady(Guid id);
}

public class GameReadyAwaiter : IGameReadyAwaiter
{
    private readonly HashSet<Guid> _ready = new();

    public async Task Await(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            if (_ready.Count == 2)
                break;
            
            await Task.Delay(100);
        }
    }

    public void OnPlayerReady(Guid id)
    {
        _ready.Add(id);
    }
}