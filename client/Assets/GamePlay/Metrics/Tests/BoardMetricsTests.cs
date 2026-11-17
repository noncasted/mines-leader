using GamePlay.Loop;
using NUnit.Framework;
using Shared;
using static GamePlay.Metrics.Tests.MetricsTestKit;

namespace GamePlay.Metrics.Tests
{
    public class BoardMetricsTests
    {
        private static MetricsTestKit Create()
        {
            var kit = new MetricsTestKit();
            new CellOpenMetrics(kit.Session, kit.Snapshots, kit.Context).OnSetup(kit.Lifetime);
            new FlagMetrics(kit.Session, kit.Snapshots, kit.Context).OnSetup(kit.Lifetime);
            new MineHitMetrics(kit.Session, kit.Snapshots, kit.Context).OnSetup(kit.Lifetime);
            return kit;
        }

        [Test]
        public void ClickOnANumberOpensOneCell()
        {
            using var kit = Create();
            kit.Push(Board(kit.Self, Mines(3, 3, 2), Free(3, 3)));

            Assert.That(kit.Metrics.Value("cells.opened", "source", "click"), Is.EqualTo(1));
            Assert.That(kit.Metrics.Value("cells.opened", "source", "cascade"), Is.EqualTo(0));
            Assert.That(kit.Metrics.Value("cascades"), Is.EqualTo(0));
        }

        [Test]
        public void ClickOnAnEmptyCellOpensACascade()
        {
            using var kit = Create();

            // (0,0) and (1,0) are empty, the rest of the wave has numbers.
            kit.Push(Board(kit.Self,
                Mines(0, 1, 1), Mines(1, 1, 2), Mines(2, 0, 1), Mines(2, 1, 1),
                Free(0, 0), Free(1, 0), Free(0, 1), Free(1, 1), Free(2, 0), Free(2, 1)));

            Assert.That(kit.Metrics.Value("cells.opened", "source", "click"), Is.EqualTo(1));
            Assert.That(kit.Metrics.Value("cells.opened", "source", "cascade"), Is.EqualTo(5));
            Assert.That(kit.Metrics.Value("cascades"), Is.EqualTo(1));
        }

        [Test]
        public void ChordWithoutEmptyCellsOpensEveryCellDirectly()
        {
            using var kit = Create();
            kit.Push(Board(kit.Self, Mines(0, 0, 1), Mines(0, 1, 1), Mines(0, 2, 2), Free(0, 0), Free(0, 1), Free(0, 2)));

            Assert.That(kit.Metrics.Value("cells.opened", "source", "click"), Is.EqualTo(3));
            Assert.That(kit.Metrics.Value("cascades"), Is.EqualTo(0));
        }

        [Test]
        public void TheBoardOfTheOtherPlayerIsNotCounted()
        {
            using var kit = Create();
            kit.Push(Board(kit.Other, Free(0, 0), new BoardSnapshotRecord.Explosion { Position = new Position(1, 1) },
                new BoardSnapshotRecord.Flag { Position = new Position(2, 2), IsFlagged = true }));

            Assert.That(kit.Metrics.Value("cells.opened", "source", "click"), Is.EqualTo(0));
            Assert.That(kit.Metrics.Value("mines.hit"), Is.EqualTo(0));
            Assert.That(kit.Metrics.Value("flags.set"), Is.EqualTo(0));
        }

        [Test]
        public void FlagsAndMinesOfTheOwnBoard()
        {
            using var kit = Create();
            kit.Push(Board(kit.Self, new BoardSnapshotRecord.Flag { Position = new Position(2, 2), IsFlagged = true }));
            kit.Push(Board(kit.Self, new BoardSnapshotRecord.Flag { Position = new Position(2, 3), IsFlagged = true }));
            kit.Push(Board(kit.Self, new BoardSnapshotRecord.Flag { Position = new Position(2, 2), IsFlagged = false }));
            kit.Push(Board(kit.Self, new BoardSnapshotRecord.Explosion { Position = new Position(4, 4) }, Mines(4, 4, 3),
                Free(4, 4)));

            Assert.That(kit.Metrics.Value("flags.set"), Is.EqualTo(2));
            Assert.That(kit.Metrics.Value("flags.removed"), Is.EqualTo(1));
            Assert.That(kit.Metrics.Value("mines.hit"), Is.EqualTo(1));
            Assert.That(kit.Metrics.Value("cells.opened", "source", "click"), Is.EqualTo(1));
        }

        [Test]
        public void CardsOpenCellsOfTheOwnBoard()
        {
            using var kit = Create();
            var opened = new[] { new OpenedCell(), new OpenedCell(), new OpenedCell() };

            kit.Push(new PlayerSnapshotRecord.CardUse
            {
                PlayerId = kit.Self,
                Data = new CardActionSnapshot.ZipZap { TargetPlayer = kit.Self, OpenedCells = opened }
            });

            // A card aimed at the other board opens nothing here.
            kit.Push(new PlayerSnapshotRecord.CardUse
            {
                PlayerId = kit.Self,
                Data = new CardActionSnapshot.OpponentBomb { TargetPlayer = kit.Other, OpenedCells = opened }
            });

            Assert.That(kit.Metrics.Value("cells.opened", "source", "card"), Is.EqualTo(3));
            Assert.That(kit.Metrics.Value("cells.opened", "source", "click"), Is.EqualTo(0));
        }
    }
}
