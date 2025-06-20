using System;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using Internal;
using Newtonsoft.Json;
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
            INetworkReceiver receiver,
            INetworkSender sender,
            INetworkCommandsDispatcher commandsDispatcher,
            INetworkResponsesDispatcher responsesDispatcher)
        {
            _receiver = receiver;
            _sender = sender;
            _commandsDispatcher = commandsDispatcher;
            _responsesDispatcher = responsesDispatcher;
        }

        private readonly INetworkReceiver _receiver;
        private readonly INetworkSender _sender;
        private readonly INetworkCommandsDispatcher _commandsDispatcher;
        private readonly INetworkResponsesDispatcher _responsesDispatcher;

        public async UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId, Guid userId)
        {
            var webSocket = new ClientWebSocket();
            webSocket.Options.KeepAliveInterval = TimeSpan.FromMinutes(10);

            var auth = new ServerUserAuth
            {
                SessionId = sessionId,
                UserId = userId
            };
            
            Debug.Log($"User {userId} connecting to session {sessionId} at {serverUrl}");

            var authJson = JsonConvert.SerializeObject(auth);

            webSocket.Options.SetRequestHeader("Authorization", authJson);

            var uri = new Uri(serverUrl);

            await webSocket.ConnectAsync(uri, CancellationToken.None);

            _receiver.Run(lifetime, webSocket).Forget();
            _sender.Run(lifetime, webSocket).Forget();
            _commandsDispatcher.Run(lifetime).Forget();
            _responsesDispatcher.Run(lifetime).Forget();

            lifetime.Listen(() =>
            {
                Debug.Log("Close session connection");
                webSocket.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "closing websocket",
                    CancellationToken.None);
            });
        }
    }
}