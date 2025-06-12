using System;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Common.Network
{
    public class NetworkSession : INetworkSession
    {
        public NetworkSession(
            INetworkSessionCallbacks callbacks,
            INetworkUsersCollection users,
            INetworkConnection connection)
        {
            _callbacks = callbacks;
            _connection = connection;
            Users = users;
        }

        private readonly INetworkSessionCallbacks _callbacks;
        private readonly INetworkConnection _connection;

        private ILifetime _lifetime;

        public INetworkUsersCollection Users { get; }
        public IReadOnlyLifetime Lifetime => _lifetime;

        public async UniTask Start(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId)
        {
            _lifetime = lifetime.Child();

            await _connection.Connect(_lifetime, serverUrl, sessionId);
            await UniTask.WaitUntil(() => Users.Local != null);

            await _callbacks.InvokeSessionSetupCompleted(lifetime);
        }
    }
}