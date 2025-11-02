using Infrastructure;

namespace Meta.Users;

[GenerateSerializer]
public class UsersCollectionState : AddressableDictionaryState<Guid, UserState>
{
}

public interface IUsersCollection : IGrainWithGuidKey
{
    [Transaction(TransactionOption.Join)]
    Task AddOrUpdate(UserState user);
    

    [Transaction(TransactionOption.Join)]
    Task Remove(Guid id);
}

public interface IUsersCollectionView : IAddressableDictionaryView<Guid, UserState>
{
}

public class UsersCollection : AddressableDictionary<UsersCollectionState, Guid, UserState>, IUsersCollection
{
    public UsersCollection(
        [States.UserCollection] IPersistentState<UsersCollectionState> state,
        IMessaging messaging) : base(state, messaging)
    {
    }

    protected override AddressableDictionaryMessageQueueId QueueId { get; } = new("users");

    public Task AddOrUpdate(UserState user)
    {
        return Write(user.Id, user);
    }

    public Task Remove(Guid id)
    {
        return Erase(id);
    }
}

public class UsersCollectionView : AddressableDictionaryView<Guid, UserState>, IUsersCollectionView
{
    public UsersCollectionView(IMessaging messaging) : base(messaging)
    {
    }

    protected override string Name => "users";
}