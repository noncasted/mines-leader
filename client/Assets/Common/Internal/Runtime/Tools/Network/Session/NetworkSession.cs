using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    public interface INetworkSession
    {
        INetworkUsersCollection Users { get; }
        IReadOnlyLifetime Lifetime { get; }

        // The ticket (from the meta: MatchResult.Ticket, LobbyResult.Ticket, or from the game server:
        // RematchContexts.Success.Ticket) names the user and the session; it is accepted once, within a minute of its
        // issue (S4).
        UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, string ticket);
    }

    public class NetworkSession : INetworkSession
    {
        public NetworkSession(
            INetworkSessionCallbacks callbacks,
            INetworkUsersCollection users,
            ISessionConnection connection)
        {
            _callbacks = callbacks;
            _connection = connection;
            Users = users;
        }

        private readonly INetworkSessionCallbacks _callbacks;
        private readonly ISessionConnection _connection;

        private ILifetime _lifetime;

        public INetworkUsersCollection Users { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;

        public async UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, string ticket)
        {
            _lifetime = lifetime.Child();

            Debug.Log("[Network] [Session] Starting session connection...");
            await _connection.Connect(_lifetime, serverUrl, ticket);
            Debug.Log("[Network] [Session] Session connection established.");
            Debug.Log("[Network] [Session] Waiting for local user data...");
            await UniTask.WaitUntil(() => Users.Local != null);

            Debug.Log("[Network] [Session] Local user data received.");
            Debug.Log("[Network] [Session] Starting session services");
            await _callbacks.InvokeSessionSetupCompleted(lifetime);
        }
    }

    public static class SessionContextExtensions
    {
        public static int LocalIndex(this INetworkSession context)
        {
            return context.Users.Local.Index;
        }
    }
}