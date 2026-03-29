using Common.Extensions;
using Infrastructure;
using Meta.Users;
using Shared;

namespace Tests;

public class UserDeckTest
{
    public class Root : ClusterTestRoot<StateMigrationTest.EmptyPayload>
    {
        public Root(ClusterTestUtils utils, IOrleans orleans, ITransactions transactions, IUserFactory userFactory)
            : base(utils)
        {
            _orleans = orleans;
            _transactions = transactions;
            _userFactory = userFactory;
        }

        private readonly IOrleans _orleans;
        private readonly ITransactions _transactions;
        private readonly IUserFactory _userFactory;

        public override string Group => TestGroups.Meta;
        public override string Title => "user-deck";

        protected override async Task Run(ClusterTestNodeHandle handle, StateMigrationTest.EmptyPayload payload)
        {
            handle.Progress.SetStatus(OperationStatus.InProgress);

            // Create user — Initialize() sets up default decks
            var userId = await _userFactory.Create(new UserCreateOptions());
            Cleanup.TrackUser(userId);
            var user = _orleans.CreateUserHandle(userId);

            handle.Progress.Log($"User created: {userId}");
            handle.Progress.SetProgress(0.2f);

            // Read deck state — should have default decks initialized
            var deckState = await _transactions.Run(() => user.Deck.GetState());

            if (deckState.Entries.Count == 0)
                throw new Exception("No decks after initialization");

            handle.Progress.Log($"Decks initialized: {deckState.Entries.Count} decks");
            handle.Progress.SetProgress(0.4f);

            // Get selected deck
            var selected = await _transactions.Run(() => user.Deck.GetSelected());

            if (selected.Count == 0)
                throw new Exception("Selected deck is empty");

            handle.Progress.Log($"Selected deck: {selected.Count} cards");
            handle.Progress.SetProgress(0.5f);

            // Update deck 0 with custom cards
            var customCards = new List<CardType>
            {
                CardType.Trebuchet,
                CardType.Bloodhound,
                CardType.ZipZap,
                CardType.ErosionDozer,
                CardType.Gravedigger,
                CardType.TrebuchetAimer
            };

            await _transactions.Run(() => user.Deck.Update(0, customCards));

            handle.Progress.SetProgress(0.7f);

            // Verify updated deck
            var updatedState = await _transactions.Run(() => user.Deck.GetState());

            if (!updatedState.Entries.ContainsKey(0))
                throw new Exception("Deck 0 not found after update");

            var deck0 = updatedState.Entries[0];

            if (deck0.Cards.Count != customCards.Count)
                throw new Exception(
                    $"Deck 0 card count: expected {customCards.Count}, got {deck0.Cards.Count}");

            for (var i = 0; i < customCards.Count; i++)
            {
                if (deck0.Cards[i] != customCards[i])
                    throw new Exception(
                        $"Deck 0 card {i}: expected {customCards[i]}, got {deck0.Cards[i]}");
            }

            handle.Progress.SetProgress(0.9f);

            // Verify persistence — deck survives across grain calls
            var rereadState = await _transactions.Run(() => user.Deck.GetState());

            if (rereadState.Entries[0].Cards.Count != customCards.Count)
                throw new Exception("Deck not persisted correctly");

            handle.Progress.Log("Deck tests passed");
            handle.Progress.SetProgress(1f);
        }
    }
}
