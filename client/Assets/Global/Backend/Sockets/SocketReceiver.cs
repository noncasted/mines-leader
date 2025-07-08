using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using NativeWebSocket;
using Shared;

namespace Global.Backend
{
    public interface ISocketReceiver
    {
        IViewableDelegate<ServerEmptyResponse> Empty { get; }
        IViewableDelegate<ServerFullResponse> Full { get; }
    }

    public class SocketReceiver : ISocketReceiver
    {
        private WebSocket _webSocket;

        private readonly ViewableDelegate<ServerEmptyResponse> _empty = new();
        private readonly ViewableDelegate<ServerFullResponse> _full = new();

        public IViewableDelegate<ServerEmptyResponse> Empty => _empty;
        public IViewableDelegate<ServerFullResponse> Full => _full;

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
                    
                    var context = MemoryPackSerializer.Deserialize<IServerResponse>(raw)!;

                    if (context is ServerFullResponse full)
                        _full.Invoke(full);
                    else if (context is ServerEmptyResponse empty)
                        _empty.Invoke(empty);
                }
            }
        }
    }
}