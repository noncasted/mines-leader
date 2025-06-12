using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Common.Network
{
    public interface INetworkSession
    {
        INetworkUsersCollection Users { get; }
        IReadOnlyLifetime Lifetime { get; }

        UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId);
    }

    public static class SessionContextExtensions
    {
        public static int LocalIndex(this INetworkSession context)
        {
            return context.Users.Local.Index;
        }
    }
}