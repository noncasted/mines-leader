using System;
using System.Collections.Generic;
using System.Linq;
using Internal;
using Shared;

namespace Game.Session
{
    public interface ISessionData
    {
        SessionType Type { get; }
        int ExpectedUsers { get; }
        Guid Id { get; }
        IReadOnlyLifetime Lifetime { get; }
    }

    public class SessionData : ISessionData
    {
        public SessionData(SessionType type, int expectedUsers, Guid id, IReadOnlyLifetime lifetime)
        {
            Type = type;
            ExpectedUsers = expectedUsers;
            Id = id;
            Lifetime = lifetime;
        }

        public SessionType Type { get; }
        public int ExpectedUsers { get; }
        public Guid Id { get; }
        public IReadOnlyLifetime Lifetime { get; }
    }
}
