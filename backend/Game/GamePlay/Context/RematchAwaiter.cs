using Common.Extensions;
using Common.Reactive;

namespace Game.GamePlay;

public interface IRematchAwaiter
{
    Task<bool> ShouldRematch(IReadOnlyLifetime lifetime, TimeSpan timeout);
    void OnRematchAccepted();
}

public class RematchAwaiter : IRematchAwaiter
{
    public RematchAwaiter(IGameContext context)
    {
        _context = context;
    }

    private readonly IGameContext _context;
    private readonly TaskCompletionSource<bool> _completion = new();

    private int _acceptCount;

    public async Task<bool> ShouldRematch(IReadOnlyLifetime lifetime, TimeSpan timeout)
    {
        foreach (var player in _context.Players)
        {
            if (player.User.Lifetime.IsTerminated == true)
                return false;

            player.User.Lifetime.Listen(() =>
                {
                    if (lifetime.IsTerminated == true)
                        return;

                    _completion.TrySetResult(false);
                }
            );
        }

        lifetime.Listen(() => _completion.TrySetResult(false));
        TimeoutAwaiter().NoAwait();

        return await _completion.Task;

        async Task TimeoutAwaiter()
        {
            await Task.Delay(timeout);
            _completion.TrySetResult(false);
        }
    }

    public void OnRematchAccepted()
    {
        _acceptCount++;

        if (_acceptCount >= _context.Players.Count)
            _completion.TrySetResult(true);
    }
}