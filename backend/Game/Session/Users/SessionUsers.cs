using Common;
using Shared;

namespace Game;

public interface ISessionUsers : IViewableList<IUser>
{
    int GetNextIndex();
    void AddUser(IUser user);
}

public class SessionUsers : ViewableList<IUser>, ISessionUsers
{
    private int _index;

    public int GetNextIndex()
    {
        _index++;
        return _index;
    }

    public void AddUser(IUser user)
    {
        Add(user);
        user.Lifetime.Listen(() => Remove(user));
    }
}

public static class SessionUsersExtensions
{
    public static void IterateOthers(this ISessionUsers users, IUser exclude, Action<IUser> action)
    {
        foreach (var user in users)
        {
            if (user == exclude)
                continue;

            action(user);
        }
    }

    public static async Task IterateOthers(this ISessionUsers users, IUser exclude, Func<IUser, Task> action)
    {
        foreach (var user in users)
        {
            if (user == exclude)
                continue;

            await action(user);
        }
    }

    public static void SendAllExceptSelf(this ISessionUsers users, IUser self, INetworkContext context)
    {
        foreach (var user in users)
        {
            if (user == self)
                continue;

            user.Send(context);
        }
    }
}