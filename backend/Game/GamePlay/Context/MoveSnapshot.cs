using Game.Session;
using Shared;

namespace Game.GamePlay;

public class MoveSnapshot
{
    private readonly List<IMoveSnapshotRecord> _records = new();
    private int? _insertAt;

    public ISessionLogger? SessionLogger { get; set; }

    public int Count => _records.Count;

    public IDisposable BeginInsertAt(int index)
    {
        if (index < 0 || index > _records.Count)
            throw new ArgumentOutOfRangeException(nameof(index));

        _insertAt = index;
        return new InsertScope(this);
    }

    public void RecordCardUse(Guid playerId, Guid cardId, ICardActionData data)
    {
        Append(new PlayerSnapshotRecord.CardUse()
        {
            PlayerId = playerId,
            CardId = cardId,
            Data = data
        });
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

        Append(new PlayerSnapshotRecord.ManaUpdate
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

        Append(new PlayerSnapshotRecord.HealthUpdate
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

        Append(new PlayerSnapshotRecord.MovesUpdate
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

    public void RecordGameCompleted(Guid winner)
    {
        Append(new GameCompletedRecord
        {
            Winner = winner
        });
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
        return new SharedMoveSnapshot
        {
            Records = _records.AsReadOnly()
        };
    }

    private void Append(IMoveSnapshotRecord record)
    {
        if (_insertAt.HasValue == true)
        {
            _records.Insert(_insertAt.Value, record);
            _insertAt = _insertAt.Value + 1;
        }
        else
        {
            _records.Add(record);
        }
    }

    private void AppendBoardRecord(IBoard board, IBoardSnapshotRecord record)
    {
        if (_insertAt.HasValue == true)
        {
            var container = new SharedBoardSnapshot
            {
                BoardOwnerId = board.OwnerId,
                Records = new List<IBoardSnapshotRecord> { record }
            };

            _records.Insert(_insertAt.Value, container);
            _insertAt = _insertAt.Value + 1;
            return;
        }

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

    private sealed class InsertScope : IDisposable
    {
        public InsertScope(MoveSnapshot owner)
        {
            _owner = owner;
        }

        private readonly MoveSnapshot _owner;

        public void Dispose()
        {
            _owner._insertAt = null;
        }
    }
}