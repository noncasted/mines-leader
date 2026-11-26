using Game.Session;

namespace Game.GamePlay;

public interface ISnapshotSender
{
    void Send(MoveSnapshot snapshot);
}

public class SnapshotSender : ISnapshotSender
{
    public SnapshotSender(IGameContext context, IMatchClock clock)
    {
        _context = context;
        _clock = clock;
    }

    private readonly IGameContext _context;
    private readonly IMatchClock _clock;

    // The only way a snapshot goes to the clients: each one carries the clock of the match, the last one too.
    public void Send(MoveSnapshot snapshot)
    {
        var context = snapshot.Collect();
        context.MatchMs = _clock.Ms;

        foreach (var (user, _) in _context.UserToPlayer)
            user.Send(context);
    }
}