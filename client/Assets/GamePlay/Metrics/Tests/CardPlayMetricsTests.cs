using System;
using System.Linq;
using NUnit.Framework;
using Shared;

namespace GamePlay.Metrics.Tests
{
    public class CardPlayMetricsTests
    {
        [Test]
        public void CountsTheOwnCardsByTypeAndWritesThePlay()
        {
            using var kit = new MetricsTestKit();
            new CardPlayMetrics(kit.Session, kit.Snapshots, kit.Context).OnSetup(kit.Lifetime);

            var trebuchet = Guid.NewGuid();
            var medic = Guid.NewGuid();
            var enemyCard = Guid.NewGuid();

            kit.Push(
                new PlayerSnapshotRecord.CardAdd { PlayerId = kit.Self, CardId = trebuchet, Type = CardType.Trebuchet_Max },
                new PlayerSnapshotRecord.CardAdd { PlayerId = kit.Self, CardId = medic, Type = CardType.Medic },
                new PlayerSnapshotRecord.CardAdd { PlayerId = kit.Other, CardId = enemyCard, Type = CardType.Medic });

            kit.Push(new PlayerSnapshotRecord.CardUse
            {
                PlayerId = kit.Self,
                CardId = trebuchet,
                Data = new CardActionSnapshot.Trebuchet
                {
                    TargetPlayer = kit.Other,
                    TargetCells = new[] { new Position(1, 1), new Position(1, 2) }
                }
            });

            kit.Push(new PlayerSnapshotRecord.CardUse
            {
                PlayerId = kit.Self,
                CardId = medic,
                Data = new CardActionSnapshot.Medic { TargetPlayer = kit.Self }
            });

            kit.Push(new PlayerSnapshotRecord.CardUse
            {
                PlayerId = kit.Other,
                CardId = enemyCard,
                Data = new CardActionSnapshot.Medic { TargetPlayer = kit.Other }
            });

            Assert.That(kit.Metrics.Value("cards.played", "card", "trebuchet_max"), Is.EqualTo(1));
            Assert.That(kit.Metrics.Value("cards.played", "card", "medic"), Is.EqualTo(1));
            Assert.That(kit.Metrics.Events.Count, Is.EqualTo(2));

            var first = kit.Metrics.Events[0];
            Assert.That(first.name, Is.EqualTo("card.play"));
            Assert.That(first.fields.Single(f => f.Key == "card").Text, Is.EqualTo("trebuchet_max"));
            Assert.That(first.fields.Single(f => f.Key == "board").Text, Is.EqualTo("enemy"));
            Assert.That(first.fields.Single(f => f.Key == "cells").Number, Is.EqualTo(2));

            // A card without cells on a board: the field is left out.
            var second = kit.Metrics.Events[1];
            Assert.That(second.fields.Single(f => f.Key == "board").Text, Is.EqualTo("own"));
            Assert.That(second.fields.Any(f => f.Key == "cells"), Is.False);
        }
    }
}
