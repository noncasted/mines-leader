using Game.Session;
using Shared;

namespace Game.GamePlay;

public partial class MoveSnapshot
{
    private readonly List<IMoveSnapshotRecord> _records = new();

    // Player resources (mana, health, moves) do not go into the common stream at the moment of mutation:
    // the client would apply them before the card animation plays. Instead we keep
    // one current record per player and resource, and hand them out last in Collect(),
    // strictly after the record of the action that changed them.
    private readonly List<ResourceKey> _resourceOrder = new();
    private readonly Dictionary<ResourceKey, IMoveSnapshotRecord> _resources = new();

    // A snapshot without a logger (the completion of the match, card previews, tests) logs nothing.
    public MoveSnapshot()
    {
    }

    public MoveSnapshot(ISessionLogger sessionLogger)
    {
        SessionLogger = sessionLogger;
    }

    public ISessionLogger? SessionLogger { get; }

    public int Count => _records.Count;

    public void RecordGameStarted(int cardMovesCost)
    {
        Append(new GameStartedRecord { CardMovesCost = cardMovesCost });
    }

    public void RecordTimeLimitedRound(Guid currentPlayer, Dictionary<Guid, long> secondsLeft)
    {
        Append(new TimeLimitedRoundRecord
        {
            CurrentPlayer = currentPlayer,
            SecondsLeft = new Dictionary<Guid, long>(secondsLeft)
        });
    }

    public void RecordLastManStandingRound(Guid currentPlayer, int currentRound, int secondsLeft)
    {
        Append(new LastManStandingRoundRecord
        {
            CurrentPlayer = currentPlayer,
            CurrentRound = currentRound,
            SecondsLeft = secondsLeft
        });
    }

    public SharedMoveSnapshot Collect()
    {
        var records = new List<IMoveSnapshotRecord>(_records.Count + _resourceOrder.Count);

        records.AddRange(_records);

        foreach (var key in _resourceOrder)
            records.Add(_resources[key]);

        return new SharedMoveSnapshot
        {
            Records = records
        };
    }

    private void Append(IMoveSnapshotRecord record)
    {
        _records.Add(record);
    }

    /// <summary>
    /// The last record of a resource is its final state, so intermediate
    /// values are overwritten: the client gets one unified update per player.
    /// </summary>
    private void AppendResource(Guid playerId, ResourceKind kind, IMoveSnapshotRecord record)
    {
        var key = new ResourceKey(playerId, kind);

        if (_resources.ContainsKey(key) == false)
            _resourceOrder.Add(key);

        _resources[key] = record;
    }

    private void AppendBoardRecord(IBoard board, IBoardSnapshotRecord record)
    {
        if (_records.Count == 0 ||
            _records[^1] is not SharedBoardSnapshot boardRecord ||
            boardRecord.BoardOwnerId != board.OwnerId)
        {
            boardRecord = new SharedBoardSnapshot
            {
                BoardOwnerId = board.OwnerId,
                Records = new List<IBoardSnapshotRecord>()
            };

            _records.Add(boardRecord);
        }

        boardRecord.Records.Add(record);
    }

    private enum ResourceKind
    {
        Mana,
        Health,
        Moves
    }

    private readonly record struct ResourceKey(Guid PlayerId, ResourceKind Kind);
}
