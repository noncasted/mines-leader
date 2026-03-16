using Infrastructure;

namespace Meta.Users;

public interface IUserCollection : IStateCollection<Guid, UserState>
{
}

public class UserCollection(StateCollectionUtils<Guid, UserState> utils)
    : StateCollection<Guid, UserState>(utils), IUserCollection;