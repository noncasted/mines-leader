using Game.Session;
using Shared;

namespace Game.GamePlay;

public partial class MoveSnapshot
{
    private readonly List<IMoveSnapshotRecord> _records = new();

    // Ресурсы игрока (мана, здоровье, ходы) не попадают в общий поток в момент мутации:
    // клиент применил бы их до того, как отыграется анимация карты. Вместо этого копим
    // по одной актуальной записи на игрока и ресурс, а отдаём их в Collect() последними —
    // строго после записи о действии, которое их изменило.
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
    /// Последняя запись по ресурсу и есть его финальное состояние, поэтому промежуточные
    /// значения перетираются: клиенту уходит один унифицированный апдейт на игрока.
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
