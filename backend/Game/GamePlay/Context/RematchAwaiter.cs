using Common;

namespace Game.GamePlay;

public interface IRematchAwaiter
{
    Task<bool> ShouldRematch(IReadOnlyLifetime lifetime, TimeSpan timeout);
    void OnRematchAccepted(Guid playerId);
    void OnRematchDeclined(Guid playerId);
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
        }

        return await _completion.Task;
    }

    public void OnRematchAccepted(Guid playerId)
    {
        _acceptCount++;
        
        if (_acceptCount >= _context.Players.Count)
            _completion.TrySetResult(true);
    }

    public void OnRematchDeclined(Guid playerId)
    {
        _completion.TrySetResult(false);
    }
}