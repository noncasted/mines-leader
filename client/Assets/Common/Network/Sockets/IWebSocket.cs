using Cysharp.Threading.Tasks;
using Internal;

namespace Common.Network
{
    public interface IWebSocket
    {
        IViewableDelegate<byte[]> Received { get; }

        UniTask Connect();
        UniTask Send(byte[] bytes);
    }
}