using Infrastructure;
using Orleans.Concurrency;
using Orleans.Transactions.Abstractions;

namespace Meta.Bots;

[GenerateSerializer]
public class BotState
{
    [Id(0)] public Guid Id { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
}

[GenerateSerializer]
public class BotCollectionState : AddressableDictionaryState<Guid, BotState>
{
}

public interface IBotCollection : IAddressableDictionary<Guid, BotState>
{
    [Transaction(TransactionOption.Join)]
    Task AddOrUpdate(BotState bot);

    [Transaction(TransactionOption.Join)]
    Task Remove(Guid id);
}

public interface IBotCollectionView : IAddressableDictionaryView<Guid, BotState>
{
}

public class BotCollection : AddressableDictionary<BotCollectionState, Guid, BotState>, IBotCollection
{
    public BotCollection(
        [States.BotCollection] IPersistentState<BotCollectionState> state,
        IMessaging messaging) : base(state, messaging)
    {
    }

    public Task AddOrUpdate(BotState bot)
    {
        return Write(bot.Id, bot);
    }

    public Task Remove(Guid id)
    {
        return Erase(id);
    }
}

public class BotCollectionView : AddressableDictionaryView<Guid, BotState, IBotCollection>, IBotCollectionView
{
    public BotCollectionView(IOrleans orleans, IMessaging messaging) : base(orleans, messaging)
    {
    }
}