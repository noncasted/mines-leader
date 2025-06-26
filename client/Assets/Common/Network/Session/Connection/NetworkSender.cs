using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Channels;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using Shared;
using Channel = System.Threading.Channels.Channel;

namespace Common.Network
{
    public interface INetworkSender
    {
        UniTask Run(IReadOnlyLifetime lifetime, ClientWebSocket webSocket);
        ValueTask SendEmpty(INetworkContext commandContext);
        UniTask<T> SendFull<T>(IReadOnlyLifetime lifetime, INetworkContext commandContext);
    }
    
    public class NetworkSender : INetworkSender
    {
        public NetworkSender(INetworkResponsesDispatcher responsesDispatcher)
        {
            _responsesDispatcher = responsesDispatcher;
        }

        private readonly INetworkResponsesDispatcher _responsesDispatcher;

        private int _requestCounter;

        private readonly System.Threading.Channels.Channel<IServerRequest> _pending
            = Channel.CreateBounded<IServerRequest>(new BoundedChannelOptions(7)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false
            });

        public async UniTask Run(IReadOnlyLifetime lifetime, ClientWebSocket webSocket)
        {
            var reader = _pending.Reader;
            var buffer = new MemoryStream();
            var cancellation = lifetime.Token;

            while (await reader.WaitToReadAsync(cancellation))
            {
                while (reader.TryRead(out var message))
                {
                    await MemoryPackSerializer.SerializeAsync(buffer, message, cancellationToken: cancellation);
                    var sendBuffer = new ReadOnlyMemory<byte>(buffer.GetBuffer(), 0, (int)buffer.Length);
                    await webSocket.SendAsync(sendBuffer, WebSocketMessageType.Binary, true, cancellation);
                    buffer.Position = 0;
                }
            }
        }

        public ValueTask SendEmpty(INetworkContext commandContext)
        {
            var request = new ServerEmptyRequest()
            {
                Context = commandContext
            };

            return _pending.Writer.WriteAsync(request);
        }

        public async UniTask<T> SendFull<T>(IReadOnlyLifetime lifetime, INetworkContext commandContext)
        {
            _requestCounter++;
            
            var request = new ServerFullRequest()
            {
                Context = commandContext,
                RequestId = _requestCounter
            };

            var responseTask = _responsesDispatcher.AwaitResponse<T>(request);
            await _pending.Writer.WriteAsync(request);
            return await responseTask;
        }
    }
}