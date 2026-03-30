using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meta.Users;

public interface IUserCollection : IStateCollection<Guid, UserState>
{
}

public class UserCollection(StateCollectionUtils<Guid, UserState> utils, ILogger<UserCollection> logger)
    : StateCollection<Guid, UserState>(utils, logger), IUserCollection;