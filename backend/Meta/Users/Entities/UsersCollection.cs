using Infrastructure;

namespace Meta.Users;

[GenerateSerializer]
public class UsersCollectionState : AddressableDictionaryState<Guid, UserState>
{
}

public interface IUsersCollection : IAddressableDictionary<Guid, UserState>
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

    public Task AddOrUpdate(UserState user)
    {
        return Write(user.Id, user);
    }

    public Task Remove(Guid id)
    {
        return Erase(id);
    }
}

public class UsersCollectionView : AddressableDictionaryView<Guid, UserState, IUsersCollection>, IUsersCollectionView
{
    public UsersCollectionView(IOrleans orleans, IMessaging messaging) : base(orleans, messaging)
    {
    }
}