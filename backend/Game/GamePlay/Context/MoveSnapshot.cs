using Game.Session;
using Meta.Matches;
using Meta.Users;
using Shared;

namespace Game.GamePlay;

public class MoveSnapshot
{
    private readonly List<IMoveSnapshotRecord> _records = new();

    // Ресурсы игрока (мана, здоровье, ходы) не попадают в общий поток в момент мутации:
    // клиент применил бы их до того, как отыграется анимация карты. Вместо этого копим
    // по одной актуальной записи на игрока и ресурс, а отдаём их в Collect() последними —
    // строго после записи о действии, которое их изменило.
    private readonly List<ResourceKey> _resourceOrder = new();
    private readonly Dictionary<ResourceKey, IMoveSnapshotRecord> _resources = new();

    public ISessionLogger? SessionLogger { get; set; }
    public bool HasDropPosition { get; set; }
    public float DropX { get; set; }
    public float DropY { get; set; }

    public int Count => _records.Count;

    public void RecordCardUse(Guid playerId, Guid cardId, ICardActionData data)
    {
        Append(new PlayerSnapshotRecord.CardUse()
        {
            PlayerId = playerId,
            CardId = cardId,
            Data = data,
            HasDropPosition = HasDropPosition,
            DropX = DropX,
            DropY = DropY
        });
    }

    public ICardActionData? TakeCardUseFrom(int startIndex)
    {
        ICardActionData? data = null;

        for (var i = _records.Count - 1; i >= startIndex; i--)
        {
            if (_records[i] is not PlayerSnapshotRecord.CardUse cardUse)
                continue;

            data = cardUse.Data;
            _records.RemoveAt(i);
        }

        return data;
    }

    public void RecordCardAdd(Guid playerId, Guid cardId, CardType type, bool isStash = false)
    {
        Append(new PlayerSnapshotRecord.CardAdd()
        {
            PlayerId = playerId,
            CardId = cardId,
            Type = type,
            IsStash = isStash
        });
    }

    public void RecordCardRemove(Guid playerId, Guid cardId)
    {
        Append(new PlayerSnapshotRecord.CardRemove()
        {
            PlayerId = playerId,
            CardId = cardId
        });
    }

    public void RecordGameStarted(int cardMovesCost)
    {
        Append(new GameStartedRecord { CardMovesCost = cardMovesCost });
    }

    public void RecordCellTaken(IBoard board, Position position)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.CellTaken { Position = position });
    }

    public void RecordCellFree(IBoard board, Position position)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.CellFree { Position = position });
    }

    public void RecordFlag(IBoard board, Position position, bool isFlagged)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.Flag { Position = position, IsFlagged = isFlagged });
    }

    public void RecordMines(IBoard board, Position position, int count)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.MinesAround { Position = position, Count = count });
    }

    public void RecordMines(IBoard board, IReadOnlyList<BoardSnapshotRecord.MinesAround> records)
    {
        foreach (var record in records)
            AppendBoardRecord(board, record);
    }

    public void RecordExplosion(IBoard board, Position position)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.Explosion { Position = position });
    }

    public void RecordEffectAdded(IBoard board, Position position, CellEffectType type, Guid effectId)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.EffectAdded
        {
            Position = position,
            Type = type,
            EffectId = effectId
        });
    }

    public void RecordEffectRemoved(IBoard board, Position position, Guid effectId)
    {
        AppendBoardRecord(board, new BoardSnapshotRecord.EffectRemoved
        {
            Position = position,
            EffectId = effectId
        });
    }

    public void RecordManaUpdate(IPlayer player)
    {
        var mana = player.Mana;

        AppendResource(player.User.Id, ResourceKind.Mana, new PlayerSnapshotRecord.ManaUpdate
        {
            PlayerId = player.User.Id,
            Current = mana.Current,
            BaseMax = mana.BaseMax,
            ResultMax = mana.ResultMax
        });
    }

    public void RecordHealthUpdate(IPlayer player)
    {
        var health = player.Health;

        AppendResource(player.User.Id, ResourceKind.Health, new PlayerSnapshotRecord.HealthUpdate
        {
            PlayerId = player.User.Id,
            Current = health.Current,
            BaseMax = health.BaseMax,
            ResultMax = health.ResultMax
        });
    }

    public void RecordMovesUpdate(IPlayer player)
    {
        var moves = player.Moves;

        AppendResource(player.User.Id, ResourceKind.Moves, new PlayerSnapshotRecord.MovesUpdate
        {
            PlayerId = player.User.Id,
            Left = moves.Left,
            BaseMax = moves.BaseMax,
            ResultMax = moves.ResultMax,
            IsAvailable = moves.IsAvailable
        });
    }

    public void RecordModifierUpdate(IPlayer player, DurationalModifierOverview overview)
    {
        Append(new PlayerSnapshotRecord.ModifierUpdate
        {
            PlayerId = player.User.Id,
            Overview = overview
        });
    }

    public void RecordDeckUpdate(IPlayer player)
    {
        Append(new PlayerSnapshotRecord.DeckUpdate
        {
            PlayerId = player.User.Id,
            Count = player.Deck.Count
        });
    }

    public void RecordCardsStashed(IPlayer player)
    {
        Append(new PlayerSnapshotRecord.CardsStashed
        {
            PlayerId = player.User.Id
        });
    }

    public void RecordStashUpdate(IPlayer player)
    {
        Append(new PlayerSnapshotRecord.StashUpdate
        {
            PlayerId = player.User.Id,
            Count = player.Stash.Count
        });
    }

    /// <summary>
    /// Подорванные мины входят в общее число: их уже нет на поле, но флагом они не закрыты,
    /// поэтому счётчик оставшихся мин обязан их показывать.
    /// </summary>
    public void RecordBoardStateUpdate(IBoard board)
    {
        Append(new PlayerSnapshotRecord.BoardStateUpdate
        {
            PlayerId = board.OwnerId,
            Mines = board.MinesScanner.Mines + board.DetonatedMines,
            Flags = board.MinesScanner.Flags
        });
    }

    public void RecordGameCompleted(
        Guid winner,
        IReadOnlyList<Guid> players,
        IReadOnlyDictionary<Guid, UserStatsDelta> stats,
        MatchCompletionSummary? summary)
    {
        Append(new GameCompletedRecord
        {
            Winner = winner,
            Duration = summary?.Duration ?? TimeSpan.Zero,
            Players = players.Select(id => CreatePlayerResult(id, stats.GetValueOrDefault(id), summary))
                             .ToList()
        });
    }

    private static MatchPlayerResult CreatePlayerResult(
        Guid id,
        UserStatsDelta? delta,
        MatchCompletionSummary? summary)
    {
        delta ??= new UserStatsDelta();

        return new MatchPlayerResult
        {
            PlayerId = id,
            RatingChange = summary?.RatingChanges.GetValueOrDefault(id) ?? 0,
            Rating = summary?.Ratings.GetValueOrDefault(id) ?? 0,
            Stats = new MatchPlayerStats
            {
                Counters = new Dictionary<UserStatType, long>(delta.Counters),
                CardsPlayedByGroup = new Dictionary<CardGroup, long>(delta.CardsPlayedByGroup)
            }
        };
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