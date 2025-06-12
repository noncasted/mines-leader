using Common;

namespace Game;

public interface ISessionUsers : IViewableList<IUser>
{
    int GetNextIndex();
    void AddUser(IUser user);
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
}