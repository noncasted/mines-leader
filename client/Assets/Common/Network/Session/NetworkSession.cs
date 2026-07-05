using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Network
{
    public interface INetworkSession
    {
        INetworkUsersCollection Users { get; }
        IReadOnlyLifetime Lifetime { get; }
        Guid LocalUserId { get; }

        UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId, Guid userId);
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
        private Guid _userId;

        public INetworkUsersCollection Users { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;
        public Guid LocalUserId => _userId;

        public async UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId, Guid userId)
        {
            _userId = userId;
            _lifetime = lifetime.Child();

            Debug.Log("[Network] [Session] Starting session connection...");
            await _connection.Connect(_lifetime, serverUrl, sessionId, userId);
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