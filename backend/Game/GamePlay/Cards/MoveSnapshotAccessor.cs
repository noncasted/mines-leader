namespace Game.GamePlay;

public interface IMoveSnapshotAccessor
{
    MoveSnapshot Snapshot { get; }
    Guid CardId { get; }
}

public class MoveSnapshotAccessor : IMoveSnapshotAccessor
{
    private MoveSnapshot? _snapshot;
    private Guid _cardId;

    public MoveSnapshot Snapshot => _snapshot ?? throw new InvalidOperationException("Snapshot not set");
    public Guid CardId => _cardId;

    public void Set(MoveSnapshot snapshot, Guid cardId)
    {
        _snapshot = snapshot;
        _cardId = cardId;
    }
}