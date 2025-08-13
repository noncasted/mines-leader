using System;
using System.Collections.Generic;
using GamePlay.Boards;
using GamePlay.Loop;
using GamePlay.Services;
using Shared;

namespace GamePlay
{
    public class BoardSnapshotHandler : ISnapshotHandler<SharedBoardSnapshot>
    {
        public BoardSnapshotHandler(IGameContext gameContext)
        {
            _gameContext = gameContext;
        }

        private readonly IGameContext _gameContext;
        private readonly Dictionary<Guid, PlayerRecordResolver> _resolvers = new();

        public void Handle(SharedBoardSnapshot record)
        {
            if (_resolvers.TryGetValue(record.BoardOwnerId, out var resolver) == false)
            {
                var player = _gameContext.GetPlayer(record.BoardOwnerId);
                resolver = new PlayerRecordResolver(player.Board);
                _resolvers[record.BoardOwnerId] = resolver;
            }

            foreach (var snapshotRecord in record.Records)
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