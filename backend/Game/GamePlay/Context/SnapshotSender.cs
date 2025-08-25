namespace Game.GamePlay;

public interface ISnapshotSender
{
    void Send(MoveSnapshot snapshot);
}

public class SnapshotSender : ISnapshotSender
{
    public SnapshotSender(IGameContext context)
    {
        _context = context;
    }

    private readonly IGameContext _context;
    
    public void Send(MoveSnapshot snapshot)
    {
        var context = snapshot.Collect();
        
        foreach (var (user, _) in _context.UserToPlayer)
            user.Send(context);
    }
}