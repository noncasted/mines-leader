using System;
using System.Collections.Generic;
using System.Linq;
using Internal;
using Shared;

namespace Game.Session
{
    public interface ISessionUsers : IViewableList<IUser>
    {
        int GetNextIndex();
        void AddUser(IUser user);
    }

    public class SessionUsers : ViewableList<IUser>, ISessionUsers
    {
        private int _index;

        // Every new seat (ISessionSeats.Take) takes a new index; a reconnect keeps the index of its seat. Past
        // SharedEntityIds.MaxUserIndex the user would have no entity id range: the join fails (the gateway logs it and closes the socket).
        public int GetNextIndex()
        {
            if (_index >= SharedEntityIds.MaxUserIndex)
            {
                throw new InvalidOperationException(
                    $"The session has given out all {SharedEntityIds.MaxUserIndex} user indices: no entity id range is left");
            }

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
        // The meta ids of the players: the users with a client (no bots), each once whatever number of its
        // connections are joined (a stale one is dropped a moment after a return).
        public static IEnumerable<Guid> GetPlayerIds(this ISessionUsers users)
        {
            return users.Where(user => user.IsBot == false).Select(user => user.BackendId).Distinct();
        }

        public static int CountPlayers(this ISessionUsers users)
        {
            return users.GetPlayerIds().Count();
        }

        public static void IterateOthers(this ISessionUsers users, IUser exclude, Action<IUser> action)
        {
            foreach (var user in users)
            {
                if (user == exclude)
                    continue;

                action(user);
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

        public static void SendAll(this ISessionUsers users, INetworkContext context)
        {
            foreach (var user in users)
                user.Send(context);
        }
    }
}
