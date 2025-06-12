using System;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using Global.Backend;
using Internal;
using Newtonsoft.Json;
using Shared;
using UnityEngine;

namespace Common.Network
{
    public class NetworkConnection : INetworkConnection
    {
        public NetworkConnection(
            IBackendUser user,
            INetworkReceiver receiver,
            INetworkSender sender,
            INetworkCommandsDispatcher commandsDispatcher,
            INetworkResponsesDispatcher responsesDispatcher)
        {
            _receiver = receiver;
            _sender = sender;
            _commandsDispatcher = commandsDispatcher;
            _responsesDispatcher = responsesDispatcher;
            _user = user;
        }

        private readonly INetworkReceiver _receiver;
        private readonly INetworkSender _sender;
        private readonly INetworkCommandsDispatcher _commandsDispatcher;
        private readonly INetworkResponsesDispatcher _responsesDispatcher;
        private readonly IBackendUser _user;

        public async UniTask Connect(IReadOnlyLifetime lifetime, string serverUrl, Guid sessionId)
        {
            var webSocket = new ClientWebSocket();
            webSocket.Options.KeepAliveInterval = TimeSpan.FromMinutes(10);

            var auth = new ServerUserAuth
            {
                SessionId = sessionId,
                UserId = _user.Id
            };
            
            Debug.Log($"User {_user.Id} connecting to session {sessionId} at {serverUrl}");

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