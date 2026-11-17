using System;
using Internal;

namespace Game.Session
{
    // A user of a host without a container (SessionCore): the seat and the connection given by the host.
    public class SessionUser : IUser
    {
        public SessionUser(ISessionSeat seat, bool isBot, ILifetime lifetime, IUserConnection connection)
        {
            Seat = seat;
            IsBot = isBot;
            Lifetime = lifetime;
            Connection = connection;
        }

        public ISessionSeat Seat { get; }
        public Guid Id => Seat.Id;
        public Guid BackendId => Seat.UserId;
        public int Index => Seat.Index;
        public bool IsBot { get; }
        public ILifetime Lifetime { get; }
        public IUserConnection Connection { get; }
    }
}
