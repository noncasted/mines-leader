using Infrastructure;
using Meta.Users;

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
        return _orleans.Transactions.Create(async () => {
            var id = Guid.NewGuid();
            var handle = _orleans.CreateUserHandle(id);

            await handle.Entity.Initialize();
            await handle.Entity.SetName(name);
            await handle.Deck.Initialize();

            var collection = _orleans.GetGrain<IBotCollection>(Guid.Empty);
            await collection.AddOrUpdate(new BotState { Id = id, Name = name });

            return id;
        });
    }
}
