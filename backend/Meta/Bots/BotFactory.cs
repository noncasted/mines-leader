using Cluster.Configs;
using Infrastructure;
using Meta.Users;
using Shared;

namespace Meta.Bots;

public interface IBotFactory
{
    Task<Guid> Create(string name);
}

public class BotFactory : IBotFactory
{
    public BotFactory(IOrleans orleans, IBotCollection botCollection, IBotConfig botConfig)
    {
        _orleans = orleans;
        _botCollection = botCollection;
        _botConfig = botConfig;
    }

    private readonly IOrleans _orleans;
    private readonly IBotCollection _botCollection;
    private readonly IBotConfig _botConfig;

    public Task<Guid> Create(string name)
    {
        return _orleans.Transactions.Run(async () => {
            var id = Guid.NewGuid();
            var handle = _orleans.CreateUserHandle(id);

            await handle.Entity.Initialize();
            await handle.Entity.SetName(name);
            await handle.Deck.Initialize();
            await handle.Cards.Initialize();

            var profileConfig = _botConfig.Value.CurrentProfileConfig;
            var decks = profileConfig.Decks;
            var deckSize = profileConfig.DeckSize;

            List<CardType> selectedCards;

            if (decks.Count > 0)
            {
                var deck = decks[Random.Shared.Next(decks.Count)];
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
                await handle.Cards.AddCard(card);

            await handle.Deck.Update(0, selectedCards);

            var bot = _orleans.GetGrain<IBot>(id);
            await bot.Initialize();
            await bot.OnUpdated();

            return id;
        });
    }
}
