using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using MemoryPack;
using NativeWebSocket;
using Shared;

namespace Global.Backend
{
    public interface ISocketSender
    {
        UniTask Run(IReadOnlyLifetime lifetime);
        UniTask Send<T>(T value);
        UniTask<T> SendFull<T>(INetworkContext commandContext);
        UniTask ForceSendAll();
    }

    public class SocketSender : ISocketSender
    {
        public SocketSender(WebSocket webSocket, ISocketReceiver receiver)
        {
            _webSocket = webSocket;
            _receiver = receiver;
        }

        private readonly WebSocket _webSocket;
        private readonly ISocketReceiver _receiver;
        private readonly Dictionary<int, UniTaskCompletionSource<INetworkContext>> _pending = new();

        private int _requestCounter;
        private IReadOnlyLifetime _lifetime;

        public UniTask Run(IReadOnlyLifetime lifetime)
        {
            _lifetime = lifetime;
            _receiver.Full.Advise(lifetime, OnFullReceived);

            return UniTask.CompletedTask;
        }

        public UniTask Send<T>(T value)
        {
            var payload = MemoryPackSerializer.Serialize(value);
            _webSocket.Send(payload);
            return UniTask.CompletedTask;
        }

        public async UniTask<T> SendFull<T>(INetworkContext commandContext)
        {
            var payload = MemoryPackSerializer.Serialize(commandContext);
            _requestCounter++;

            var request = new ServerFullRequest()
            {
                Context = commandContext,
                RequestId = _requestCounter
            };

            var completion = new UniTaskCompletionSource<INetworkContext>();
            _pending.Add(request.RequestId, completion);
            _webSocket.Send(payload);

            _lifetime.Listen(() => completion.TrySetCanceled());
            
            var context = await completion.Task;

            _pending.Remove(request.RequestId);

            return (T)context;
        }

        public UniTask ForceSendAll()
        {
            return UniTask.CompletedTask;
        }

        private void OnFullReceived(ServerFullResponse response)
        {
            var context = response.Context;
            var pending = _pending[response.RequestId];
            pending.TrySetResult(context);
            _pending.Remove(response.RequestId);
        }
    }
}