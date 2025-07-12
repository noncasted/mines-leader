using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using NativeWebSocket;
using Shared;

namespace Global.Backend
{
    public interface IConnectionWriter
    {
        void WriteOneWay(INetworkContext value);
        UniTask<T> WriteRequest<T>(INetworkContext commandContext);
        void WriteResponse(INetworkContext commandContext, int requestId);
        void OnRequestHandled(INetworkContext commandContext, int requestId);
        UniTask ForceSendAll();
    }

    public class ConnectionWriter : IConnectionWriter
    {
        public ConnectionWriter(IConnectionReader receiver)
        {
            _receiver = receiver;
        }

        private WebSocket _webSocket;
        
        private readonly IConnectionReader _receiver;
        private readonly Dictionary<int, UniTaskCompletionSource<INetworkContext>> _pendingRequests = new();
        private readonly List<IMessageFromClient> _writeQueue = new();

        private int _requestCounter;
        private ILifetime _lifetime;

        public void Run(IReadOnlyLifetime lifetime, WebSocket webSocket)
        {
            _webSocket = webSocket;
            _lifetime = lifetime.Child();
            
            Loop(lifetime).Forget();
        }

        private async UniTask Loop(IReadOnlyLifetime lifetime)
        {
            while (lifetime.IsTerminated == false)
            {
                if (_writeQueue.Count == 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.05f));
                    continue;
                }

                var request = _writeQueue[0];
                _writeQueue.RemoveAt(0);

                var payload = MemoryPackSerializer.Serialize(request);
                await _webSocket.Send(payload);
                await UniTask.Delay(TimeSpan.FromSeconds(0.05f));
            }
        }

        public void WriteOneWay(INetworkContext value)
        {
            var oneWay = new OneWayMessageFromClient()
            {
                Context = value,
            };

            _writeQueue.Add(oneWay);
        }

        public async UniTask<T> WriteRequest<T>(INetworkContext commandContext)
        {
            _requestCounter++;

            var request = new RequestMessageFromClient()
            {
                Context = commandContext,
                RequestId = _requestCounter
            };


            var completion = new UniTaskCompletionSource<INetworkContext>();
            _pendingRequests.Add(request.RequestId, completion);

            _writeQueue.Add(request);

            _lifetime.Listen(() => completion.TrySetCanceled());

            var context = await completion.Task;

            _pendingRequests.Remove(request.RequestId);

            return (T)context;
        }

        public void WriteResponse(INetworkContext commandContext, int requestId)
        {
            var response = new ResponseMessageFromClient()
            {
                RequestId = requestId,
                Context = commandContext
            };

            _writeQueue.Add(response);
        }

        public void OnRequestHandled(INetworkContext commandContext, int requestId)
        {
            if (_pendingRequests.TryGetValue(requestId, out var pending))
            {
                pending.TrySetResult(commandContext);
                _pendingRequests.Remove(requestId);
            }
            else
            {
                throw new InvalidOperationException($"No pending request found for request ID: {requestId}");
            }
        }

        public async UniTask ForceSendAll()
        {
            _lifetime.Terminate();

            foreach (var message in _writeQueue)
            {
                var payload = MemoryPackSerializer.Serialize(message);
                await _webSocket.Send(payload);
            }
        }
    }
}