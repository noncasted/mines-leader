using Infrastructure;
using Meta.Users;
using Shared;

namespace Meta.Bots;

public interface IBotFactory {
    Task<Guid> Create(string name);
}

public class BotFactory : IBotFactory {
    public BotFactory(IOrleans orleans) {
        _orleans = orleans;
    }

    private readonly IOrleans _orleans;

    public Task<Guid> Create(string name) {
        return _orleans.OldTransactions.Create(async () => {
            var id = Guid.NewGuid();
            var handle = _orleans.CreateUserHandle(id);

            await handle.Entity.Initialize();
            await handle.Entity.SetName(name);
            await handle.Deck.Initialize();
            
            var cards = new List<CardType>(DeckOptions.BotPool).Shuffle();
            var selectedCards = cards.Take(DeckOptions.DeckSize).ToList();
            await handle.Deck.Update(0, selectedCards);

            var collection = _orleans.GetGrain<IBotCollection>(Guid.Empty);
            await collection.AddOrUpdate(new BotState { Id = id, Name = name });

            return id;
        });
    }
}
