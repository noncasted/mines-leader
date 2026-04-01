using Common.Extensions;
using Game.GamePlay;
using Game.Session;
using Shared;

namespace Benchmarks;

public class DeckHandStashTest
{
    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils, BenchmarkStorage benchmarkStorage) : base(utils, benchmarkStorage)
        {
        }

        public override string Group => TestGroups.Game;
        public override string Title => "deck-hand-stash";
        public override string MetricName => "ms";

        protected override Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            TestDeck(handle);
            handle.Progress.SetProgress(0.33f);

            TestHand(handle);
            handle.Progress.SetProgress(0.66f);

            TestStash(handle);
            handle.Progress.SetProgress(1f);

            handle.Progress.Log("All deck/hand/stash tests passed");
            return Task.CompletedTask;
        }

        private void TestDeck(ClusterTestNodeHandle handle)
        {
            var deckState = new PlayerDeckState { Queue = new List<CardType>() };
            var state = new ValueProperty<PlayerDeckState>(0, deckState).ForTest();
            var selected = new List<CardType> { CardType.Bloodhound, CardType.Trebuchet, CardType.ZipZap };
            var deck = new Deck(state, selected);

            // Init — fills deck by cycling through selected cards
            deck.Init(7);

            if (deck.Count != 7)
                throw new Exception($"Deck init: expected 7, got {deck.Count}");

            // Draw all cards — each should be one of the selected types
            var drawnTypes = new HashSet<CardType>();

            for (var i = 0; i < 7; i++)
            {
                var card = deck.DrawCard();
                drawnTypes.Add(card);
            }

            if (deck.Count != 0)
                throw new Exception($"Deck after draw all: expected 0, got {deck.Count}");

            // All selected types should appear
            foreach (var selectedType in selected)
            {
                if (!drawnTypes.Contains(selectedType))
                    throw new Exception($"Card type {selectedType} was not drawn");
            }

            // Draw from empty — should throw
            var threw = false;

            try
            {
                deck.DrawCard();
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            if (!threw)
                throw new Exception("Drawing from empty deck should throw");

            // Add card manually
            deck.AddCard(CardType.Smoke);

            if (deck.Count != 1)
                throw new Exception($"Deck after add: expected 1, got {deck.Count}");

            var drawn = deck.DrawCard();

            if (drawn != CardType.Smoke)
                throw new Exception($"Expected Smoke, got {drawn}");

            handle.Progress.Log("Deck tests passed");
        }

        private void TestHand(ClusterTestNodeHandle handle)
        {
            var state = new ValueProperty<PlayerHandState>(1).ForTest();
            var hand = new Hand(state);

            hand.SetSize(5);

            // Add cards
            var card1 = hand.Add(CardType.Bloodhound);
            var card2 = hand.Add(CardType.Trebuchet);

            if (hand.Entries.Count != 2)
                throw new Exception($"Hand count: expected 2, got {hand.Entries.Count}");

            if (card1.Type != CardType.Bloodhound)
                throw new Exception($"Card 1 type: expected Bloodhound, got {card1.Type}");

            // Remove card
            hand.Remove(card1.Id);

            if (hand.Entries.Count != 1)
                throw new Exception($"Hand after remove: expected 1, got {hand.Entries.Count}");

            if (hand.Entries[0].Id != card2.Id)
                throw new Exception("Wrong card remaining after remove");

            // Remove non-existent card — should be safe
            hand.Remove(Guid.NewGuid());

            if (hand.Entries.Count != 1)
                throw new Exception("Remove of non-existent card changed count");

            handle.Progress.Log("Hand tests passed");
        }

        private void TestStash(ClusterTestNodeHandle handle)
        {
            var state = new ValueProperty<PlayerStashState>(2).ForTest();
            var stash = new Stash(state);

            // Add cards
            stash.Add(CardType.Bloodhound);
            stash.Add(CardType.Trebuchet);
            stash.Add(CardType.ZipZap);

            if (stash.Count != 3)
                throw new Exception($"Stash count: expected 3, got {stash.Count}");

            // Pick — LIFO (last added first)
            var picked = stash.Pick();

            if (picked != CardType.ZipZap)
                throw new Exception($"Pick: expected ZipZap, got {picked}");

            if (stash.Count != 2)
                throw new Exception($"Stash after pick: expected 2, got {stash.Count}");

            // Collect — returns all remaining, clears stash
            var collected = stash.Collect();

            if (collected.Count != 2)
                throw new Exception($"Collected: expected 2, got {collected.Count}");

            if (stash.Count != 0)
                throw new Exception($"Stash after collect: expected 0, got {stash.Count}");

            // Pick from empty — should throw
            var threw = false;

            try
            {
                stash.Pick();
            }
            catch
            {
                threw = true;
            }

            if (!threw)
                throw new Exception("Pick from empty stash should throw");

            handle.Progress.Log("Stash tests passed");
        }
    }
}
