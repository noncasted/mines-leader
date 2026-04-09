using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using Internal;

namespace Network
{
    public class DefaultWebSocket : IWebSocket
    {
        private readonly ClientWebSocket _socket;

        private readonly Uri _uri;
        private readonly ViewableDelegate<byte[]> _received = new();

        private readonly IReadOnlyLifetime _lifetime;
        private readonly CancellationToken _cancellation;

        public WebSocketState State => _socket.State;
        public IViewableDelegate<byte[]> Received => _received;

        public DefaultWebSocket(string url, IReadOnlyLifetime lifetime)
        {
            _uri = new Uri(url);
            _lifetime = lifetime.Child();
            _cancellation = _lifetime.Token;

            var protocol = _uri.Scheme;

            if (!protocol.Equals("ws") && !protocol.Equals("wss"))
                throw new ArgumentException("Unsupported protocol: " + protocol);

            _socket = new ClientWebSocket();
        }

        public async UniTask Connect()
        {
            await _socket.ConnectAsync(_uri, _cancellation);
            Receive().Forget();
        }

        public async UniTask Send(byte[] bytes)
        {
            var payload = new ReadOnlyMemory<byte>(bytes);
            await _socket.SendAsync(payload, WebSocketMessageType.Binary, true, _lifetime.Token);
        }

        private async UniTask Receive()
        {
            var buffer = new ArraySegment<byte>(new byte[8192]);

            while (_socket.State == WebSocketState.Open)
            {
                using var stream = new MemoryStream();

                WebSocketReceiveResult result;

                do
                {
                    result = await _socket.ReceiveAsync(buffer, _cancellation);
                    stream.Write(buffer.Array, buffer.Offset, result.Count);
                }
                while (result.EndOfMessage == false);

                stream.Seek(0, SeekOrigin.Begin);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await Close();
                    return;
                }

                var rawValue = stream.ToArray();
                _received.Invoke(rawValue);
            }
        }

        public async UniTask Close()
        {
            if (State != WebSocketState.Open || _lifetime.IsTerminated == true)
                return;

            await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, _cancellation);
        }
    }
}