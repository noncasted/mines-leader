using Internal;
using MemoryPack;
using Shared;

namespace Common.Network
{
    public interface IConnectionReader
    {
        IViewableDelegate<OneWayMessageFromServer> OneWay { get; }
        IViewableDelegate<ResponseMessageFromServer> Response { get; }
        IViewableDelegate<RequestMessageFromServer> Request { get; }
    }

    public class ConnectionReader : IConnectionReader
    {
        private readonly ViewableDelegate<OneWayMessageFromServer> _oneWay = new();
        private readonly ViewableDelegate<ResponseMessageFromServer> _response = new();
        private readonly ViewableDelegate<RequestMessageFromServer> _request = new();

        public IViewableDelegate<OneWayMessageFromServer> OneWay => _oneWay;
        public IViewableDelegate<ResponseMessageFromServer> Response => _response;
        public IViewableDelegate<RequestMessageFromServer> Request => _request;

        public void Run(IReadOnlyLifetime lifetime, IWebSocket webSocket)
        {
            webSocket.Received.Advise(lifetime, OnMessage);
            return;

            void OnMessage(byte[] bytes)
            {
                var context = MemoryPackSerializer.Deserialize<IMessageFromServer>(bytes)!;

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