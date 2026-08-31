using Cysharp.Threading.Tasks;

namespace Internal
{
    public interface IWebSocket
    {
        IViewableDelegate<byte[]> Received { get; }

        UniTask Connect();
        UniTask Send(byte[] bytes);
    }
}