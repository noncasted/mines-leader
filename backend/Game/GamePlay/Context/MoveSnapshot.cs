using Common;
using Context;
using Shared;

namespace Game.GamePlay;

public class MoveSnapshot
{
    public MoveSnapshot(IGameContext gameContext)
    {
        _gameContext = gameContext;
    }

    private readonly IGameContext _gameContext;
    private readonly List<IMoveSnapshotRecord> _records = new();

    public void Start(IReadOnlyLifetime lifetime)
    {
        HandleBoards(lifetime);
    }

    public void RecordCard(Guid playerId, CardType card)
    {
        _records.Add(new PlayerSnapshotRecord.Card()
        {
            PlayerId = playerId,
            Type = card
        });
    }
    
    public void RecordReshuffleFromStash(Guid playerId, int count)
    {
        _records.Add(new PlayerSnapshotRecord.ReshuffleFromStash()
        {
            PlayerId = playerId,
            CardsCount = count
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

            void CellSet(ICell cell)
            {
                IBoardSnapshotRecord record = cell.Status switch
                {
                    CellStatus.Free => new BoardSnapshotRecord.CellTaken() { Position = cell.Position },
                    CellStatus.Taken => new BoardSnapshotRecord.CellFree() { Position = cell.Position },
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
        }

        void WriteBoardRecord(IBoard board, IBoardSnapshotRecord record)
        {
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

    private void ListenPlayers(IReadOnlyLifetime lifetime)
    {
        foreach (var (_, player) in _gameContext.UserToPlayer)
        {
            
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