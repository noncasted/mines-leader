using System;
using System.Collections.Generic;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay.Boards
{
    public class BoardSnapshotHandler : ISnapshotHandler
    {
        public BoardSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;
        private readonly Dictionary<Guid, PlayerRecordResolver> _resolvers = new();

        public Type Target => typeof(SharedBoardSnapshot);

        public void Handle(IMoveSnapshotRecord record)
        {
            if (record is not SharedBoardSnapshot boardRecord)
            {
                throw new ArgumentException(
                    $"Expected record of type {nameof(IBoardSnapshotRecord)}, but got {record.GetType().Name}.",
                    nameof(record)
                );
            }

            if (_resolvers.TryGetValue(boardRecord.BoardOwnerId, out var resolver) == false)
            {
                var player = _gameContext.GetPlayer(boardRecord.BoardOwnerId);
                resolver = new PlayerRecordResolver(player.Board);
                _resolvers[boardRecord.BoardOwnerId] = resolver;
            }

            foreach (var snapshotRecord in boardRecord.Records)
                resolver.Resolve(snapshotRecord);
        }

        public class PlayerRecordResolver
        {
            public PlayerRecordResolver(IBoard board)
            {
                _cellTaken = new CellTaken(board);
                _cellFree = new CellFree(board);
                _flag = new Flag(board);
                _minesAround = new MinesAround(board);
            }

            private readonly CellTaken _cellTaken;
            private readonly CellFree _cellFree;
            private readonly Flag _flag;
            private readonly MinesAround _minesAround;

            public void Resolve(IBoardSnapshotRecord record)
            {
                switch (record)
                {
                    case BoardSnapshotRecord.CellTaken cellTaken:
                        _cellTaken.Execute(cellTaken);
                        break;
                    case BoardSnapshotRecord.CellFree cellFree:
                        _cellFree.Execute(cellFree);
                        break;
                    case BoardSnapshotRecord.Flag flag:
                        _flag.Execute(flag);
                        break;
                    case BoardSnapshotRecord.MinesAround minesAround:
                        _minesAround.Execute(minesAround);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(record), record, null);
                }
            }
        }

        public class CellTaken
        {
            public CellTaken(IBoard board)
            {
                _board = board;
            }

            private readonly IBoard _board;

            public void Execute(BoardSnapshotRecord.CellTaken record)
            {
                var vector = record.Position.ToVector();
                _board.Cells[vector].EnsureTaken();
            }
        }

        public class CellFree
        {
            public CellFree(IBoard board)
            {
                _board = board;
            }

            private readonly IBoard _board;

            public void Execute(BoardSnapshotRecord.CellFree record)
            {
                var vector = record.Position.ToVector();
                _board.Cells[vector].EnsureFree();
            }
        }

        public class Flag
        {
            public Flag(IBoard board)
            {
                _board = board;
            }

            private readonly IBoard _board;

            public void Execute(BoardSnapshotRecord.Flag record)
            {
                var vector = record.Position.ToVector();
                _board.Cells[vector].EnsureTaken().OnFlagUpdated(record.IsFlagged);
            }
        }

        public class MinesAround
        {
            public MinesAround(IBoard board)
            {
                _board = board;
            }

            private readonly IBoard _board;

            public void Execute(BoardSnapshotRecord.MinesAround record)
            {
                var vector = record.Position.ToVector();
                _board.Cells[vector].EnsureFree().OnMinesUpdated(record.Count);
            }
        }
    }
}