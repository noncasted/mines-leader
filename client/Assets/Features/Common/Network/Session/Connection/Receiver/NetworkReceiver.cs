using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Channels;
using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using Shared;
using Channel = System.Threading.Channels.Channel;

namespace Common.Network
{
    public class NetworkReceiver : INetworkReceiver
    {
        private readonly System.Threading.Channels.Channel<ServerEmptyResponse> _empty
            = Channel.CreateBounded<ServerEmptyResponse>(new BoundedChannelOptions(7)
            {
                SingleWriter = true,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false
            });

        private readonly System.Threading.Channels.Channel<ServerFullResponse> _full
            = Channel.CreateBounded<ServerFullResponse>(new BoundedChannelOptions(7)
            {
                SingleWriter = true,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait,
                AllowSynchronousContinuations = false
            });

        public System.Threading.Channels.Channel<ServerEmptyResponse> Empty => _empty;
        public System.Threading.Channels.Channel<ServerFullResponse> Full => _full;

        public async UniTask Run(IReadOnlyLifetime lifetime, ClientWebSocket webSocket)
        {
            var buffer = new byte[1024 * 1024 * 4].AsMemory();

            while (webSocket.State == WebSocketState.Open && lifetime.IsTerminated == false)
            {
                if (webSocket.CloseStatus != null)
                    break;

                try
                {
                    var receiveResult = await webSocket.ReceiveAsync(
                        buffer,
                        CancellationToken.None
                    );

                    var payload = buffer[..receiveResult.Count];
                    var context = MemoryPackSerializer.Deserialize<IServerResponse>(payload.Span)!;

                    if (context is ServerFullResponse full)
                        await _full.Writer.WriteAsync(full);
                    else if (context is ServerEmptyResponse empty)
                        await _empty.Writer.WriteAsync(empty);
                }
                catch (WebSocketException)
                {
                    break;
                }
            }
        }
    }
}