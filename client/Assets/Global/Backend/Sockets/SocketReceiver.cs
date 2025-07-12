using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using NativeWebSocket;
using Shared;

namespace Global.Backend
{
    public interface ISocketReceiver
    {
        IViewableDelegate<OneWayMessageFromServer> Empty { get; }
        IViewableDelegate<ResponseMessageFromServer> Full { get; }
    }

    public class SocketReceiver : ISocketReceiver
    {
        private WebSocket _webSocket;

        private readonly ViewableDelegate<OneWayMessageFromServer> _empty = new();
        private readonly ViewableDelegate<ResponseMessageFromServer> _full = new();

        public IViewableDelegate<OneWayMessageFromServer> Empty => _empty;
        public IViewableDelegate<ResponseMessageFromServer> Full => _full;

        public UniTask Run(IReadOnlyLifetime lifetime, WebSocket webSocket)
        {
            _webSocket = webSocket;
            _webSocket.OnMessage += OnMessage;
            lifetime.Listen(() => _webSocket.OnMessage -= OnMessage);

            return UniTask.CompletedTask;

            void OnMessage(byte[] bytes)
            {
                Handle(bytes).Forget();
                
                async UniTask Handle(byte[] raw)
                {
                    await UniTask.SwitchToMainThread();
                    
                    var context = MemoryPackSerializer.Deserialize<IMessageFromServer>(raw)!;

                    if (context is ResponseMessageFromServer full)
                        _full.Invoke(full);
                    else if (context is OneWayMessageFromServer empty)
                        _empty.Invoke(empty);
                }
            }
        }
    }
}