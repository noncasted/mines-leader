using Common.Reactive;

namespace Game.GamePlay;

public interface IPlayersReadyAwaiter
{
    Task Await(IReadOnlyLifetime lifetime);
    void OnPlayerLoaded(Guid id);
}

public class PlayersReadyAwaiter : IPlayersReadyAwaiter
{
    private readonly HashSet<Guid> _loaded = new();

    public async Task Await(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            if (_loaded.Count == 2)
                break;

            await Task.Delay(100);
        }
    }

    public void OnPlayerLoaded(Guid id)
    {
        _loaded.Add(id);
    }
}

public class BotPlayersReadyAwaiter : IPlayersReadyAwaiter
{
    private readonly HashSet<Guid> _loaded = new();

    public async Task Await(IReadOnlyLifetime lifetime)
    {
        while (lifetime.IsTerminated == false)
        {
            if (_loaded.Count == 1)
                break;

            await Task.Delay(100);
        }
    }

    public void OnPlayerLoaded(Guid id)
    {
        _loaded.Add(id);
    }
}