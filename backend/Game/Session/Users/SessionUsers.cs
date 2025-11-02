using Common.Reactive;
using Shared;

namespace Game.Session;

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
    extension(ISessionUsers users)
    {
        public void IterateOthers(IUser exclude, Action<IUser> action)
        {
            foreach (var user in users)
            {
                if (user == exclude)
                    continue;

                action(user);
            }
        }

        public async Task IterateOthers(IUser exclude, Func<IUser, Task> action)
        {
            foreach (var user in users)
            {
                if (user == exclude)
                    continue;

                await action(user);
            }
        }

        public void SendAllExceptSelf(IUser self, INetworkContext context)
        {
            foreach (var user in users)
            {
                if (user == self)
                    continue;

                user.Send(context);
            }
        }

        public void SendAll(INetworkContext context)
        {
            foreach (var user in users)
                user.Send(context);
        }
    }
}