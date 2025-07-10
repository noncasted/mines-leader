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
            ISocketReceiver receiver,
            ISocketSender sender,
            INetworkCommandsDispatcher commandsDispatcher)
        {
            _receiver = receiver;
            _sender = sender;
            _commandsDispatcher = commandsDispatcher;
        }

        private readonly ISocketReceiver _receiver;
        private readonly ISocketSender _sender;
        private readonly INetworkCommandsDispatcher _commandsDispatcher;

        public async UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId, Guid userId)
        {
            var webSocket = new NetworkSocket(serverUrl);

            var auth = new GameConnectionAuth.Request
            {
                SessionId = sessionId,
                UserId = userId
            };

            Debug.Log($"User {userId} connecting to session {sessionId} at {serverUrl}");

            await webSocket.Run(lifetime);

            var response = await webSocket.SendFull<GameConnectionAuth.Response>(auth);

            if (response.IsSuccess == false)
            {
                Debug.LogError($"Failed to authenticate user {userId} for session {sessionId}");
                throw new Exception("Authentication failed");
            }

            // _receiver.Run(lifetime, webSocket).Forget();
            // _sender.Run(lifetime, webSocket).Forget();
            _commandsDispatcher.Run(lifetime).Forget();
        }
    }
}