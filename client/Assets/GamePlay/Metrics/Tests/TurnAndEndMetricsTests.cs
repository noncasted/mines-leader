using System.Linq;
using GamePlay.Loop;
using NUnit.Framework;
using Shared;
using UnityEngine;
using static GamePlay.Metrics.Tests.MetricsTestKit;

namespace GamePlay.Metrics.Tests
{
    public class TurnAndEndMetricsTests
    {
        private static double Field(MetricsTestKit kit, int index, string key)
        {
            return kit.Metrics.Events[index].fields.Single(f => f.Key == key).Number;
        }

        [Test]
        public void TheOwnTurnsWithTheirLengthCellsAndCards()
        {
            using var kit = new MetricsTestKit();
            new TurnMetrics(kit.Session, kit.Snapshots, kit.Context, kit.Clock).OnSetup(kit.Lifetime);

            kit.Clock.Receive(1000, Time.realtimeSinceStartupAsDouble);
            kit.Push(new LastManStandingRoundRecord { CurrentPlayer = kit.Self, CurrentRound = 0 });

            // The same record again (the time left of the turn) does not start a new turn.
            kit.Push(new LastManStandingRoundRecord { CurrentPlayer = kit.Self, CurrentRound = 0 });
            kit.Push(Board(kit.Self, Mines(0, 0, 1), Free(0, 0)));
            kit.Push(Board(kit.Other, Free(5, 5)));
            kit.Push(new PlayerSnapshotRecord.CardUse
            {
                PlayerId = kit.Self,
                Data = new CardActionSnapshot.ZipZap
                {
                    TargetPlayer = kit.Self,
                    OpenedCells = new[] { new OpenedCell(), new OpenedCell() }
                }
            });

            Assert.That(kit.Metrics.Value("turns"), Is.EqualTo(1));
            Assert.That(kit.Metrics.Events, Is.Empty);

            kit.Clock.Receive(5000, Time.realtimeSinceStartupAsDouble);
            kit.Push(new LastManStandingRoundRecord { CurrentPlayer = kit.Other, CurrentRound = 1 });

            Assert.That(kit.Metrics.Events.Count, Is.EqualTo(1));
            Assert.That(kit.Metrics.Events[0].name, Is.EqualTo("turn.end"));
            Assert.That(Field(kit, 0, "number"), Is.EqualTo(1));
            Assert.That(Field(kit, 0, "seconds"), Is.EqualTo(4).Within(0.5));
            Assert.That(Field(kit, 0, "cells"), Is.EqualTo(3));
            Assert.That(Field(kit, 0, "cards"), Is.EqualTo(1));

            // The next own turn is open when the scope ends: Ending closes it before the end of the stream.
            kit.Push(new LastManStandingRoundRecord { CurrentPlayer = kit.Self, CurrentRound = 2 });
            kit.Dispose();

            Assert.That(kit.Metrics.Value("turns"), Is.EqualTo(2));
            Assert.That(kit.Metrics.Events.Count, Is.EqualTo(2));
            Assert.That(Field(kit, 1, "number"), Is.EqualTo(2));
            Assert.That(Field(kit, 1, "cells"), Is.EqualTo(0));
            Assert.That(kit.Metrics.IsEnded, Is.True);
        }

        [Test]
        public void TheRoundNumberOfTheEndOfATurnIsNotANewTurn()
        {
            using var kit = new MetricsTestKit();
            new TurnMetrics(kit.Session, kit.Snapshots, kit.Context, kit.Clock).OnSetup(kit.Lifetime);

            // The order of the server (LastManStandingRound): the start of a turn, the end of the same turn with the next
            // round number and the same player, then the start of the turn of the other player.
            kit.Push(new LastManStandingRoundRecord { CurrentPlayer = kit.Self, CurrentRound = 0 });
            kit.Push(new LastManStandingRoundRecord { CurrentPlayer = kit.Self, CurrentRound = 1 });
            kit.Push(new LastManStandingRoundRecord { CurrentPlayer = kit.Other, CurrentRound = 1 });
            kit.Push(new LastManStandingRoundRecord { CurrentPlayer = kit.Other, CurrentRound = 2 });
            kit.Push(new LastManStandingRoundRecord { CurrentPlayer = kit.Self, CurrentRound = 2 });

            Assert.That(kit.Metrics.Value("turns"), Is.EqualTo(2));
            Assert.That(kit.Metrics.Events.Count, Is.EqualTo(1));
            Assert.That(Field(kit, 0, "number"), Is.EqualTo(1));
        }

        [Test]
        public void TheCompletionEndsTheOpenTurn()
        {
            using var kit = new MetricsTestKit();
            new TurnMetrics(kit.Session, kit.Snapshots, kit.Context, kit.Clock).OnSetup(kit.Lifetime);

            kit.Push(new TimeLimitedRoundRecord { CurrentPlayer = kit.Self });
            kit.Push(new GameCompletedRecord { Winner = kit.Self });

            Assert.That(kit.Metrics.Events.Count, Is.EqualTo(1));
            Assert.That(kit.Metrics.Events[0].name, Is.EqualTo("turn.end"));

            kit.Dispose();
            Assert.That(kit.Metrics.Events.Count, Is.EqualTo(1));
        }

        [TestCase(MatchResultType.Win, "win")]
        [TestCase(MatchResultType.Lose, "lose")]
        [TestCase(MatchResultType.Leave, "leave")]
        public void TheResultOfTheMatch(MatchResultType type, string result)
        {
            using var kit = new MetricsTestKit();
            var state = new TestGameState();
            new MatchEndMetrics(kit.Session, state, kit.Clock).OnSetup(kit.Lifetime);

            kit.Clock.Receive(42000, 0d);
            kit.Clock.Freeze(0d);
            state.Complete(type);

            Assert.That(kit.Metrics.Events.Count, Is.EqualTo(1));
            Assert.That(kit.Metrics.Events[0].name, Is.EqualTo("match.end"));
            Assert.That(kit.Metrics.Events[0].fields.Single(f => f.Key == "result").Text, Is.EqualTo(result));
            Assert.That(Field(kit, 0, "seconds"), Is.EqualTo(42).Within(1e-3));
        }
    }
}
