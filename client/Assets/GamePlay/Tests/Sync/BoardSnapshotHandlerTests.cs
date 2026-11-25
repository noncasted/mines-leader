using System;
using System.Collections.Generic;
using GamePlay.Boards;
using GamePlay.Services;
using NUnit.Framework;
using Shared;
using UnityEngine;

namespace GamePlay.Tests
{
    // Reference of BoardSnapshotHandler: what every board record does to the cell at its position.
    public class BoardSnapshotHandlerTests
    {
        private FakeBoard _board;
        private BoardSnapshotHandler.PlayerRecordResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            _board = new FakeBoard(new Vector2Int(5, 4), 1f, Vector2.zero);
            _resolver = new BoardSnapshotHandler.PlayerRecordResolver(_board);
        }

        [Test]
        public void Generated_BoardIsGenerated()
        {
            _resolver.Resolve(new BoardSnapshotRecord.Generated());

            Assert.IsTrue(_board.IsGenerated.Value);
        }

        [Test]
        public void CellFree_TakenCellBecomesFree()
        {
            _resolver.Resolve(new BoardSnapshotRecord.CellFree { Position = new Position(3, 1) });

            Assert.IsTrue(_board.Cell(3, 1).IsFree());
            Assert.IsTrue(_board.Cell(1, 3).IsTaken());
        }

        [Test]
        public void CellTaken_FreeCellBecomesTakenWithoutAFlag()
        {
            _board.Free(new Vector2Int(3, 1));

            _resolver.Resolve(new BoardSnapshotRecord.CellTaken { Position = new Position(3, 1) });

            Assert.IsTrue(_board.Cell(3, 1).IsTaken());
            Assert.IsFalse(_board.Cell(3, 1).HasFlag());
        }

        [Test]
        public void CellTaken_TakenCell_KeepsItsStateAndFlag()
        {
            var cell = _board.Cell(3, 1);
            var state = cell.EnsureTaken();
            state.OnFlagUpdated(true);

            _resolver.Resolve(new BoardSnapshotRecord.CellTaken { Position = new Position(3, 1) });

            Assert.AreSame(state, cell.State.Value);
            Assert.IsTrue(cell.HasFlag());
        }

        [Test]
        public void Flag_SetAndRemoved_OnATakenCell()
        {
            var cell = _board.Cell(3, 1);

            _resolver.Resolve(new BoardSnapshotRecord.Flag { Position = new Position(3, 1), IsFlagged = true });
            Assert.IsTrue(cell.HasFlag());

            _resolver.Resolve(new BoardSnapshotRecord.Flag { Position = new Position(3, 1), IsFlagged = false });
            Assert.IsTrue(cell.IsTaken());
            Assert.IsFalse(cell.HasFlag());
        }

        [Test]
        public void Flag_OnAFreeCell_CellBecomesTakenAndFlagged()
        {
            _board.Free(new Vector2Int(3, 1));

            _resolver.Resolve(new BoardSnapshotRecord.Flag { Position = new Position(3, 1), IsFlagged = true });

            Assert.IsTrue(_board.Cell(3, 1).IsTaken());
            Assert.IsTrue(_board.Cell(3, 1).HasFlag());
        }

        [Test]
        public void MinesAround_TakenCellBecomesFreeWithTheCount()
        {
            _resolver.Resolve(new BoardSnapshotRecord.MinesAround { Position = new Position(3, 1), Count = 4 });

            var cell = _board.Cell(3, 1);
            Assert.IsTrue(cell.IsFree());
            Assert.AreEqual(4, ((ICellFreeState)cell.State.Value).MinesAround.Value);
        }

        [Test]
        public void CellFreeThenMinesAround_OneFreeStateWithTheCount()
        {
            _resolver.Resolve(new BoardSnapshotRecord.CellFree { Position = new Position(3, 1) });
            var free = _board.Cell(3, 1).State.Value;

            _resolver.Resolve(new BoardSnapshotRecord.MinesAround { Position = new Position(3, 1), Count = 2 });

            Assert.AreSame(free, _board.Cell(3, 1).State.Value);
            Assert.AreEqual(2, ((ICellFreeState)free).MinesAround.Value);
        }

        [Test]
        public void Explosion_TakenCellExplodesAsAMineAndBecomesFree()
        {
            _resolver.Resolve(new BoardSnapshotRecord.Explosion { Position = new Position(3, 1) });

            var cell = _board.Cell(3, 1);
            Assert.AreEqual(CellExplosionType.Mine, cell.Exploded);
            Assert.IsTrue(cell.IsFree());
        }

        [Test]
        public void Explosion_FlaggedCell_Explodes()
        {
            _board.Cell(3, 1).EnsureTaken().OnFlagUpdated(true);

            _resolver.Resolve(new BoardSnapshotRecord.Explosion { Position = new Position(3, 1) });

            Assert.AreEqual(CellExplosionType.Mine, _board.Cell(3, 1).Exploded);
            Assert.IsTrue(_board.Cell(3, 1).IsFree());
        }

        [Test]
        public void EffectAddedAndRemoved_OnTheCellOfThePosition()
        {
            var id = Guid.NewGuid();
            var cell = _board.Cell(3, 1);

            _resolver.Resolve(new BoardSnapshotRecord.EffectAdded { Position = new Position(3, 1), EffectId = id, Type = CellEffectType.Smoke });

            CollectionAssert.AreEqual(new[] { CellEffectType.Smoke }, cell.Effects.Active.Values);
            Assert.IsEmpty(_board.Cell(1, 3).Effects.Active.Values);

            _resolver.Resolve(new BoardSnapshotRecord.EffectRemoved { Position = new Position(3, 1), EffectId = id });

            Assert.IsEmpty(cell.Effects.Active.Values);
        }

        [Test]
        public void EffectAdded_TwoEffects_BothActive()
        {
            var cell = _board.Cell(0, 0);

            _resolver.Resolve(new BoardSnapshotRecord.EffectAdded { Position = new Position(0, 0), EffectId = Guid.NewGuid(), Type = CellEffectType.Fog });
            _resolver.Resolve(new BoardSnapshotRecord.EffectAdded { Position = new Position(0, 0), EffectId = Guid.NewGuid(), Type = CellEffectType.MineHighlight });

            CollectionAssert.AreEquivalent(new[] { CellEffectType.Fog, CellEffectType.MineHighlight }, cell.Effects.Active.Values);
        }

        [Test]
        public void UnknownRecord_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => _resolver.Resolve(new UnknownBoardRecord()));
        }

        [Test]
        public void PositionOutsideTheBoard_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() =>
                _resolver.Resolve(new BoardSnapshotRecord.CellTaken { Position = new Position(5, 0) }));
        }

        [Test]
        public void Handle_RecordsGoToTheBoardOfTheOwnerInTheirOrder()
        {
            var self = new FakePlayer(Guid.NewGuid(), new FakeBoard(new Vector2Int(5, 4), 1f, Vector2.zero));
            var other = new FakePlayer(Guid.NewGuid(), new FakeBoard(new Vector2Int(5, 4), 1f, new Vector2(0f, 10f)));
            var handler = new BoardSnapshotHandler(new FakeGameContext(self, other), new SilentAudioPlayer());
            var selfBoard = (FakeBoard)self.Board;
            var otherBoard = (FakeBoard)other.Board;

            handler.Handle(new SharedBoardSnapshot
            {
                BoardOwnerId = other.Id,
                Records = new List<IBoardSnapshotRecord>
                {
                    new BoardSnapshotRecord.Generated(),
                    new BoardSnapshotRecord.MinesAround { Position = new Position(2, 2), Count = 1 },
                    new BoardSnapshotRecord.CellTaken { Position = new Position(2, 2) },
                    new BoardSnapshotRecord.Flag { Position = new Position(2, 2), IsFlagged = false },
                }
            });
            handler.Handle(new SharedBoardSnapshot
            {
                BoardOwnerId = self.Id,
                Records = new List<IBoardSnapshotRecord>
                {
                    new BoardSnapshotRecord.MinesAround { Position = new Position(4, 0), Count = 3 },
                }
            });

            Assert.IsTrue(otherBoard.IsGenerated.Value);
            Assert.IsTrue(otherBoard.Cell(2, 2).IsTaken());
            Assert.IsTrue(otherBoard.Cell(4, 0).IsTaken());

            Assert.IsFalse(selfBoard.IsGenerated.Value);
            Assert.IsTrue(selfBoard.Cell(2, 2).IsTaken());
            Assert.IsTrue(selfBoard.Cell(4, 0).IsFree());
        }
    }
}
