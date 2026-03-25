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
    public BotFactory(IOrleans orleans, IBotCollection botCollection)
    {
        _orleans = orleans;
        _botCollection = botCollection;
    }

    private readonly IOrleans _orleans;
    private readonly IBotCollection _botCollection;

    public Task<Guid> Create(string name)
    {
        return _orleans.Transactions.Run(async () =>
            {
                var id = Guid.NewGuid();
                var handle = _orleans.CreateUserHandle(id);

                await handle.Entity.Initialize();
                await handle.Entity.SetName(name);
                await handle.Deck.Initialize();

                var cards = new List<CardType>(DeckOptions.BotPool).Shuffle();
                var selectedCards = cards.Take(DeckOptions.DeckSize).ToList();
                await handle.Deck.Update(0, selectedCards);

                var bot = _orleans.GetGrain<IBot>(id);
                await bot.Initialize();
                await bot.OnUpdated();

                return id;
            }
        );
    }
}