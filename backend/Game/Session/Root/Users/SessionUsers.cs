using Common;
using Microsoft.Extensions.Logging;

namespace Game;

public class SessionUsers : ViewableList<IUser>, ISessionUsers
{
    public SessionUsers(ILogger<SessionUsers> logger)
    {
        _logger = logger;
    }

    private readonly Dictionary<Guid, IUser> _users = new();
    private readonly Dictionary<int, IUser> _indexedUsers = new();
    private readonly ILogger<SessionUsers> _logger;

    private int _index;

    public int GetNextIndex()
    {
        _index++;
        return _index;
    }

    public void AddUser(IUser user)
    {
        Add(user);

        _users.Add(user.Id, user);
        _indexedUsers.Add(user.Index, user);

        user.Lifetime.Listen(() =>
        {
            _users.Remove(user.Id);
            _indexedUsers.Remove(user.Index);
            Remove(user);
        });
    }
}