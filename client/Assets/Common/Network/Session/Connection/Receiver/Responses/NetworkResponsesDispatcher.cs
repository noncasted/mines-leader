using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Internal;
using Shared;

namespace Common.Network
{
    public class NetworkResponsesDispatcher : INetworkResponsesDispatcher
    {
        public NetworkResponsesDispatcher(INetworkReceiver receiver)
        {
            _receiver = receiver;
        }

        private readonly INetworkReceiver _receiver;

        private readonly Dictionary<int, UniTaskCompletionSource<INetworkContext>> _pending = new();

        public async UniTask Run(IReadOnlyLifetime lifetime)
        {
            var reader = _receiver.Full.Reader;
            var cancellation = lifetime.Token;

            while (await reader.WaitToReadAsync(cancellation) && lifetime.IsTerminated == false)
            {
                while (reader.TryRead(out var response))
                {
                    var context = response.Context;
                    var pending = _pending[response.RequestId];
                    pending.TrySetResult(context);
                    _pending.Remove(response.RequestId);
                }
            }
        }

        public async UniTask<T> AwaitResponse<T>(ServerFullRequest request)
        {
            var completion = new UniTaskCompletionSource<INetworkContext>();
            _pending.Add(request.RequestId, completion);
            var context = await completion.Task;
            return (T)context;
        }
    }
}