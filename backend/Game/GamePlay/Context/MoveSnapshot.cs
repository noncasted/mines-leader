using Common;
using Shared;

namespace Game.GamePlay;

public class MoveSnapshot
{
    public MoveSnapshot(IGameContext gameContext, IReadOnlyLifetime lifetime)
    {
        _gameContext = gameContext;
        _lifetime = lifetime.Child();
    }

    private readonly IGameContext _gameContext;
    private readonly List<IMoveSnapshotRecord> _records = new();
    
    private readonly ILifetime _lifetime;

    private bool _isLocked = false;

    public void Start()
    {
        HandleBoards(_lifetime);
    }

    public void Lock()
    {
        _isLocked = true;
    }

    public void Unlock()
    {
        _isLocked = false;
    }

    public void RecordCard(Guid playerId, int entityId, CardType type, ICardActionData data)
    {
        _records.Add(new PlayerSnapshotRecord.Card()
        {
            PlayerId = playerId,
            EntityId = entityId,
            Type = type,
            Data = data
        });
    }
    
    public void RecordCardDraw(Guid playerId, CardType type)
    {
        _records.Add(new PlayerSnapshotRecord.CardDraw()
        {
            PlayerId = playerId,
            Type = type
        });
    }
    
    private void HandleBoards(IReadOnlyLifetime lifetime)
    {
        foreach (var (_, board) in _gameContext.Boards)
        {
            var events = board.Events;

            events.CellSet.Advise(lifetime, CellSet);
            events.Flag.Advise(lifetime, Flag);
            events.Mines.Advise(lifetime, Mines);
            events.Record.Advise(lifetime, record => WriteBoardRecord(board, record));
            events.Explode.Advise(lifetime, Explosion);

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
        }

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
        _lifetime.Terminate();

        return new SharedMoveSnapshot
        {
            Records = _records.AsReadOnly()
        };
    }
}