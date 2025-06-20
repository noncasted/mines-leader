using System.Net.WebSockets;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public interface INetworkReceiver
    {
        System.Threading.Channels.Channel<ServerEmptyResponse> Empty { get; }
        System.Threading.Channels.Channel<ServerFullResponse> Full { get; }

        UniTask Run(IReadOnlyLifetime lifetime, ClientWebSocket webSocket);
    }
}