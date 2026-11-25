using System;
using Cysharp.Threading.Tasks;
using Internal;

namespace Meta
{
    // The loss of one meta connection while it is being opened. Subscribed before the socket opens: the loss may
    // be reported inside Connect. Race fails the wait for the first data with the loss, or after ReceiveTimeout.
    internal class MetaConnectionLoss
    {
        public MetaConnectionLoss(INetworkConnection connection, IReadOnlyLifetime lifetime)
        {
            connection.Closed.Advise(lifetime, info => {
                _info = info;
                _lost.TrySetResult();
            });
        }

        // The server sends the projections right after the connect: none in this time - the connection is not alive.
        private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(20);

        private readonly UniTaskCompletionSource _lost = new();
        private SocketCloseInfo _info;

        public async UniTask Race(UniTask task)
        {
            var winner = await UniTask.WhenAny(task, _lost.Task).Timeout(ReceiveTimeout, DelayType.Realtime);

            if (winner == 1)
                throw Lost();
        }

        public async UniTask<T> Race<T>(UniTask<T> task)
        {
            var (hasResult, result) = await UniTask.WhenAny(task, _lost.Task).Timeout(ReceiveTimeout, DelayType.Realtime);

            if (hasResult == false)
                throw Lost();

            return result;
        }

        private Exception Lost()
        {
            return new Exception($"[Meta] [Loop] The meta connection is lost while connecting: {_info}");
        }
    }
}
