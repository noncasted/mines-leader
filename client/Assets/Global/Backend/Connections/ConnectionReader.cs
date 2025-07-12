using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using NativeWebSocket;
using Shared;

namespace Global.Backend
{
    public interface IConnectionReader
    {
        IViewableDelegate<OneWayMessageFromServer> OneWay { get; }
        IViewableDelegate<ResponseMessageFromServer> Response { get; }
        IViewableDelegate<RequestMessageFromServer> Request { get; }
    }

    public class ConnectionReader : IConnectionReader
    {
        private WebSocket _webSocket;

        private readonly ViewableDelegate<OneWayMessageFromServer> _oneWay = new();
        private readonly ViewableDelegate<ResponseMessageFromServer> _response = new();
        private readonly ViewableDelegate<RequestMessageFromServer> _request = new();

        public IViewableDelegate<OneWayMessageFromServer> OneWay => _oneWay;
        public IViewableDelegate<ResponseMessageFromServer> Response => _response;
        public IViewableDelegate<RequestMessageFromServer> Request => _request;

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

                    switch (context)
                    {
                        case OneWayMessageFromServer oneWay:
                            _oneWay.Invoke(oneWay);
                            break;
                        case ResponseMessageFromServer response:
                            _response.Invoke(response);
                            break;
                        case RequestMessageFromServer request:
                            _request.Invoke(request);
                            break;
                    }
                }
            }
        }
    }
}