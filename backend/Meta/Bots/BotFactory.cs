using Cluster.Configs;
using Infrastructure;
using Meta.Users;
using Microsoft.Extensions.Logging;
using Shared;

namespace Meta.Bots;

public interface IBotFactory
{
    Task<Guid> Create(string name);

    /// <summary>
    /// Выдаёт боту случайную колоду из текущего профиля конфига. Вызывается при создании
    /// и перед каждым матчем: колода хранится в состоянии бота, иначе правка конфига
    /// не доходила бы до ботов, созданных при старте кластера.
    /// </summary>
    Task AssignDeck(Guid botId);
}

public class BotFactory : IBotFactory
{
    public BotFactory(IOrleans orleans, IBotCollection botCollection, IBotConfig botConfig, ILogger<BotFactory> logger)
    {
        _orleans = orleans;
        _botCollection = botCollection;
        _botConfig = botConfig;
        _logger = logger;
    }

    private readonly IOrleans _orleans;
    private readonly IBotCollection _botCollection;
    private readonly IBotConfig _botConfig;
    private readonly ILogger<BotFactory> _logger;

    public Task<Guid> Create(string name)
    {
        return _orleans.Transactions.Run(async () => {
            var id = Guid.NewGuid();
            var handle = _orleans.CreateUserHandle(id);

            await handle.Entity.Initialize();
            await handle.Entity.SetName(name);
            await handle.Deck.Initialize();
            await handle.Cards.Initialize();

            await AssignDeckInTransaction(id);

            var bot = _orleans.GetGrain<IBot>(id);
            await bot.Initialize();
            await bot.OnUpdated();

            return id;
        });
    }

    public Task AssignDeck(Guid botId)
    {
        return _orleans.Transactions.Run(() => AssignDeckInTransaction(botId));
    }

    private async Task AssignDeckInTransaction(Guid botId)
    {
        var handle = _orleans.CreateUserHandle(botId);
        var decks = _botConfig.Value.CurrentProfileConfig.Decks;
        var deckSize = DeckOptions.DeckSize;

        List<CardType> selectedCards;
        var deckName = "none";

        if (decks.Count > 0)
        {
            var deck = decks[Random.Shared.Next(decks.Count)];
            deckName = deck.Name;
            selectedCards = new List<CardType>(deck.Cards);

            while (selectedCards.Count > deckSize)
                selectedCards.RemoveAt(selectedCards.Count - 1);
            while (selectedCards.Count < deckSize)
                selectedCards.Add(CardType.Dud);
        }
        else
        {
            selectedCards = Enumerable.Repeat(CardType.Dud, deckSize).ToList();
        }

        foreach (var card in selectedCards.Distinct())
        {
            if (await handle.Cards.HasCard(card) == false)
                await handle.Cards.AddCard(card);
        }

        await handle.Deck.Update(0, selectedCards);

        _logger.LogInformation("[Bot] Deck assigned | Bot={BotId} Deck={Deck} Cards=[{Cards}]",
            botId, deckName, string.Join(", ", selectedCards.Where(c => c != CardType.Dud)));
    }
}
