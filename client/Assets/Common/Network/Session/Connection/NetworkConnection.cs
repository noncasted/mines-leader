using System;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using Shared;
using UnityEngine;

namespace Common.Network
{
    public interface INetworkConnection
    {
        UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId, Guid userId);
    }

    public class NetworkConnection : INetworkConnection
    {
        public NetworkConnection(
            INetworkSocket socket,
            INetworkCommandsDispatcher commandsDispatcher)
        {
            _socket = socket;
            _commandsDispatcher = commandsDispatcher;
        }

        private readonly INetworkSocket _socket;
        private readonly INetworkCommandsDispatcher _commandsDispatcher;

        public async UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId, Guid userId)
        {
            var auth = new GameConnectionAuth.Request
            {
                SessionId = sessionId,
                UserId = userId
            };

            Debug.Log($"User {userId} connecting to session {sessionId} at {serverUrl}");

            await _socket.Run(lifetime, serverUrl);

            var response = await _socket.SendFull<GameConnectionAuth.Response>(auth);

            if (response.IsSuccess == false)
            {
                Debug.LogError($"Failed to authenticate user {userId} for session {sessionId}");
                throw new Exception("Authentication failed");
            }

            _commandsDispatcher.Run(lifetime).Forget();
        }
    }
}