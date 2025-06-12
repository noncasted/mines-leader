using System.Net.WebSockets;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public interface INetworkSender
    {
        UniTask Run(IReadOnlyLifetime lifetime, ClientWebSocket webSocket);
        ValueTask SendEmpty(INetworkContext commandContext);
        UniTask<T> SendFull<T>(IReadOnlyLifetime lifetime, INetworkContext commandContext);
    }
}