using Common.Extensions;
using FluentAssertions;
using Meta.Bots;
using Meta.Users;
using Shared;
using Cluster.Configs;
using Tests.Fixtures;
using Xunit;

namespace Tests.Meta;

/// <summary>
/// Tests the bot creation workflow by executing the same grain calls that BotFactory performs.
/// BotFactory is a plain service (not a grain), so we replicate its logic via direct grain calls.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class BotFactoryWorkflowTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task BotCreation_InitializesUserGrain()
    {
        var id = Guid.NewGuid();

        await RunTransaction(async () => {
            var user = GetGrain<IUser>(id);
            await user.Initialize();
            await user.SetName("TestBot");
        });
        UserState? state = null;

        await RunTransaction(async () => {
            state = await GetGrain<IUser>(id).GetState();
        });
        state!.Id.Should().Be(id);
        state.Name.Should().Be("TestBot");
    }

    [Fact]
    public async Task BotCreation_InitializesDeck()
    {
        var id = Guid.NewGuid();

        await RunTransaction(async () => {
            var deck = GetGrain<IUserDeck>(id);
            await deck.Initialize();
        });
        UserDeckState? state = null;

        await RunTransaction(async () => {
            state = await GetGrain<IUserDeck>(id).GetState();
        });
        state!.Entries.Should().HaveCount(DeckOptions.MaxDecks);
    }

    [Fact]
    public async Task BotCreation_RegistersAuth()
    {
        var id = Guid.NewGuid();

        await RunTransaction(async () => {
            var auth = GetGrain<IUserAuth>(id);
            await auth.OnRegistered();
        });
        var exists = false;

        await RunTransaction(async () => {
            exists = await GetGrain<IUserAuth>(id).IsExists();
        });
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task BotCreation_CustomDeckFromProfileConfig()
    {
        var id = Guid.NewGuid();
        var botConfig = GetSiloService<IBotConfig>().Value;
        var profileConfig = botConfig.CurrentProfileConfig;
        var deckSize = profileConfig.DeckSize;
        var decks = profileConfig.Decks;

        decks.Should().NotBeEmpty("profile should have at least one deck configured");

        await RunTransaction(async () => {
            var deck = GetGrain<IUserDeck>(id);
            await deck.Initialize();

            var cards = GetGrain<IUserCards>(id);
            await cards.Initialize();

            var template = decks[0];
            var selectedCards = new List<CardType>(template.Cards);

            while (selectedCards.Count > deckSize)
                selectedCards.RemoveAt(selectedCards.Count - 1);
            while (selectedCards.Count < deckSize)
                selectedCards.Add(CardType.Dud);

            foreach (var card in selectedCards.Distinct())
                await cards.AddCard(card);

            await deck.Update(0, selectedCards);
        });
        IReadOnlyList<CardType>? selected = null;

        await RunTransaction(async () => {
            selected = await GetGrain<IUserDeck>(id).GetSelected();
        });
        selected.Should().HaveCount(deckSize);
        selected.Should().NotBeEquivalentTo(GetSiloService<IUserDeckConfig>().Value.BaseDeck, "bot deck should differ from base deck");
    }

    [Fact]
    public async Task BotCreation_FullWorkflow_InitializesAllGrains()
    {
        var id = Guid.NewGuid();
        var botConfig = GetSiloService<IBotConfig>().Value;
        var profileConfig = botConfig.CurrentProfileConfig;
        var deckSize = profileConfig.DeckSize;
        var decks = profileConfig.Decks;

        decks.Should().NotBeEmpty("profile should have at least one deck configured");

        // Replicate BotFactory.Create() logic
        await RunTransaction(async () => {
            var user = GetGrain<IUser>(id);
            await user.Initialize();
            await user.SetName("FullBot");

            var deck = GetGrain<IUserDeck>(id);
            await deck.Initialize();

            var cards = GetGrain<IUserCards>(id);
            await cards.Initialize();

            var auth = GetGrain<IUserAuth>(id);
            await auth.OnRegistered();

            var template = decks[0];
            var selectedCards = new List<CardType>(template.Cards);

            while (selectedCards.Count > deckSize)
                selectedCards.RemoveAt(selectedCards.Count - 1);
            while (selectedCards.Count < deckSize)
                selectedCards.Add(CardType.Dud);

            foreach (var card in selectedCards.Distinct())
                await cards.AddCard(card);

            await deck.Update(0, selectedCards);

            var bot = GetGrain<IBot>(id);
            await bot.Initialize();
            await bot.OnUpdated();
        });

        // Verify user state
        UserState? userState = null;

        await RunTransaction(async () => {
            userState = await GetGrain<IUser>(id).GetState();
        });
        userState!.Id.Should().Be(id);
        userState.Name.Should().Be("FullBot");

        // Verify auth
        var exists = false;

        await RunTransaction(async () => {
            exists = await GetGrain<IUserAuth>(id).IsExists();
        });
        exists.Should().BeTrue();

        // Verify deck
        IReadOnlyList<CardType>? deck = null;

        await RunTransaction(async () => {
            deck = await GetGrain<IUserDeck>(id).GetSelected();
        });
        deck.Should().HaveCount(deckSize);
    }
}
