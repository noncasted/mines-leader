using System;
using System.Collections.Generic;
using System.Linq;
using Internal;

namespace Game.Session
{
    // Session owner: the user who does the work of the whole session on its client (generates the location
    // scheme). The first user in the session, i.e. the lowest index among the connected ones; when the owner
    // leaves - the next one by index. A bot has no client: it is never the owner. Null while no human is connected.
    public interface ISessionOwner
    {
        IViewableProperty<IUser> Owner { get; }
    }

    public class SessionOwner : ISessionOwner
    {
        public SessionOwner(ISessionUsers users, ISessionData data)
        {
            _users = users;
            users.View(data.Lifetime, OnUserAdded);
        }

        private readonly ISessionUsers _users;
        private readonly ViewableProperty<IUser> _owner = new(null);

        public IViewableProperty<IUser> Owner => _owner;

        private void OnUserAdded(IReadOnlyLifetime userLifetime, IUser user)
        {
            if (user.IsBot == true)
                return;

            if (_owner.Value == null)
                _owner.Set(user);

            userLifetime.Listen(() => {
                if (_owner.Value == user)
                    _owner.Set(FindNext(user));
            });
        }

        private IUser FindNext(IUser leaving)
        {
            IUser next = null;

            foreach (var user in _users)
            {
                if (user == leaving || user.IsBot == true)
                    continue;

                if (next == null || user.Index < next.Index)
                    next = user;
            }

            return next;
        }
    }
}
