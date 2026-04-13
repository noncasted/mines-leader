using Common.Reactive;
using Shared;

namespace Game.GamePlay;

public class MoveSnapshot
{
    private readonly List<IMoveSnapshotRecord> _records = new();

    private bool _isLocked = false;

    public void Lock()
    {
        _isLocked = true;
    }

    public void Unlock()
    {
        _isLocked = false;
    }

    public void RecordCardUse(Guid playerId, Guid cardId, ICardActionData data)
    {
        var record = new PlayerSnapshotRecord.CardUse()
        {
            PlayerId = playerId,
            CardId = cardId,
            Data = data
        };

        if (_records.Count != 0)
            _records.Insert(0, record);
        else
            _records.Add(record);
    }

    public void RecordCardAdd(Guid playerId, Guid cardId, CardType type)
    {
        _records.Add(new PlayerSnapshotRecord.CardAdd()
        {
            PlayerId = playerId,
            CardId = cardId,
            Type = type
        });
    }

    public void RecordCardRemove(Guid playerId, Guid cardId)
    {
        _records.Add(new PlayerSnapshotRecord.CardRemove()
        {
            PlayerId = playerId,
            CardId = cardId
        });
    }

    public void RecordGameStarted()
    {
        _records.Add(new GameStartedRecord());
    }


    public void HandlePlayers(IReadOnlyLifetime lifetime, IGameContext gameContext)
    {
        foreach (var player in gameContext.Players)
        {
            var playerId = player.User.Id;
            var mana = player.Mana;
            var health = player.Health;
            var moves = player.Moves;

            mana.Updated.Advise(lifetime, () => {
                _records.Add(new PlayerSnapshotRecord.ManaUpdate
                {
                    PlayerId = playerId,
                    Current = mana.Current,
                    Max = mana.Max
                });
            });

            health.Updated.Advise(lifetime, () => {
                _records.Add(new PlayerSnapshotRecord.HealthUpdate
                {
                    PlayerId = playerId,
                    Current = health.Current.Value,
                    Max = health.Max
                });
            });

            moves.Updated.Advise(lifetime, () => {
                _records.Add(new PlayerSnapshotRecord.MovesUpdate
                {
                    PlayerId = playerId,
                    Left = moves.Left,
                    Max = moves.Max,
                    IsAvailable = moves.IsAvailable
                });
            });
        }
    }

    public void HandleBoards(IReadOnlyLifetime lifetime, IGameContext gameContext)
    {
        foreach (var (_, board) in gameContext.Boards)
        {
            var events = board.Events;

            events.CellSet.Advise(lifetime, CellSet);
            events.Flag.Advise(lifetime, Flag);
            events.Mines.Advise(lifetime, Mines);
            events.Record.Advise(lifetime, record => WriteBoardRecord(board, record));
            events.Explode.Advise(lifetime, Explosion);
            events.EffectAdded.Advise(lifetime, (cell, effect) => EffectAdded(board, cell, effect));
            events.EffectRemoved.Advise(lifetime, (cell, effectId) => EffectRemoved(board, cell, effectId));

            continue;

            void CellSet(ICell cell)
            {
                IBoardSnapshotRecord record = cell.Status switch
                {
                    CellStatus.Free => new BoardSnapshotRecord.CellFree() { Position = cell.Position },
                    CellStatus.Taken => new BoardSnapshotRecord.CellTaken() { Position = cell.Position },
                    _ => throw new ArgumentOutOfRangeException()
                };

                WriteBoardRecord(board, record);
            }

            void Flag(ICell cell, bool isFlagged)
            {
                var record = new BoardSnapshotRecord.Flag()
                {
                    Position = cell.Position,
                    IsFlagged = isFlagged
                };

                WriteBoardRecord(board, record);
            }

            void Mines(ICell cell, int count)
            {
                var record = new BoardSnapshotRecord.MinesAround()
                {
                    Position = cell.Position,
                    Count = count
                };

                WriteBoardRecord(board, record);
            }

            void Explosion(ICell cell)
            {
                var record = new BoardSnapshotRecord.Explosion()
                {
                    Position = cell.Position,
                };

                WriteBoardRecord(board, record);
            }

            void EffectAdded(IBoard targetBoard, ICell cell, ICellEffect effect)
            {
                var record = new BoardSnapshotRecord.EffectAdded()
                {
                    Position = cell.Position,
                    Type = effect.Type,
                    EffectId = effect.Id
                };

                WriteBoardRecord(targetBoard, record);
            }

            void EffectRemoved(IBoard targetBoard, ICell cell, Guid effectId)
            {
                var record = new BoardSnapshotRecord.EffectRemoved()
                {
                    Position = cell.Position,
                    EffectId = effectId
                };

                WriteBoardRecord(targetBoard, record);
            }
        }

        return;

        void WriteBoardRecord(IBoard board, IBoardSnapshotRecord record)
        {
            if (_isLocked == true)
                return;

            if (_records.Count == 0 ||
                _records.Last() is not SharedBoardSnapshot boardRecord ||
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
    }

    public SharedMoveSnapshot Collect()
    {
        return new SharedMoveSnapshot
        {
            Records = _records.AsReadOnly()
        };
    }
}