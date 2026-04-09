using Cysharp.Threading.Tasks;
using Internal;

namespace Network
{
    public interface IWebSocket
    {
        IViewableDelegate<byte[]> Received { get; }

        UniTask Connect();
        UniTask Send(byte[] bytes);
    }
}