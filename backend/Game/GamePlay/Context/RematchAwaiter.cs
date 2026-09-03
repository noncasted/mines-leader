using Common.Extensions;
using Common.Reactive;
using Game.Session;

namespace Game.GamePlay;

public interface IRematchAwaiter
{
    Task<bool> ShouldRematch(IReadOnlyLifetime lifetime, TimeSpan timeout);
    void OnRematchAccepted(IUser user);
}

public class RematchAwaiter : IRematchAwaiter
{
    public RematchAwaiter(IGameContext context)
    {
        _context = context;
    }

    private readonly IGameContext _context;
    private readonly TaskCompletionSource<bool> _completion = new();
    private readonly HashSet<Guid> _accepted = new();

    public async Task<bool> ShouldRematch(IReadOnlyLifetime lifetime, TimeSpan timeout)
    {
        // Боты всегда согласны на реванш, ждём решения только живых игроков.
        var humans = _context.Players
                             .Where(t => t.User.IsBot == false)
                             .ToList();

        if (humans.Count == 0)
            return false;

        foreach (var player in humans)
        {
            if (player.User.Lifetime.IsTerminated == true)
                return false;

            player.User.Lifetime.Listen(() => {
                if (lifetime.IsTerminated == true)
                    return;

                _completion.TrySetResult(false);
            });
        }

        lifetime.Listen(() => _completion.TrySetResult(false));
        TimeoutAwaiter().NoAwait();

        TryComplete();

        return await _completion.Task;

        async Task TimeoutAwaiter()
        {
            await Task.Delay(timeout);
            _completion.TrySetResult(false);
        }
    }

    public void OnRematchAccepted(IUser user)
    {
        if (user.IsBot)
            return;

        _accepted.Add(user.Id);

        TryComplete();
    }

    private void TryComplete()
    {
        var required = _context.Players.Count(t => t.User.IsBot == false);

        if (required == 0)
            return;

        if (_accepted.Count >= required)
            _completion.TrySetResult(true);
    }
}
