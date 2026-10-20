using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Shared;

namespace Internal
{
    public interface IConnectionWriter
    {
        // No request is answered any more: the connection is lost, its socket never opened, or the lifetime it was
        // run with has ended. Requests fail with OperationCanceledException at once. Never run is closed too.
        // NOT closed: a live connection after ForceSendAll; a request made there is a mistake of the caller.
        bool IsClosed { get; }

        void WriteOneWay(INetworkContext value);

        // Never returns null. The response T, or:
        // NetworkRequestException - the server answered with ErrorResponse (its command threw, is unknown or refused);
        // TimeoutException - no response in RequestTimeoutSeconds;
        // OperationCanceledException - the connection is closed (IsClosed) before the response;
        // InvalidOperationException - a response of another type, or a request after ForceSendAll.
        UniTask<T> WriteRequest<T>(INetworkContext commandContext);
        void WriteResponse(INetworkContext commandContext, int requestId);
        void OnRequestHandled(INetworkContext commandContext, int requestId);
        UniTask ForceSendAll();
    }

    public class ConnectionWriter : IConnectionWriter
    {
        private IWebSocket _webSocket;

        private readonly Dictionary<int, UniTaskCompletionSource<INetworkContext>> _pendingRequests = new();
        private readonly List<IMessageFromClient> _writeQueue = new();

        private const float RequestTimeoutSeconds = 20f;

        private int _requestCounter;
        private IReadOnlyLifetime _connectionLifetime;
        private ILifetime _lifetime;
        private bool _isClosed;

        public bool IsClosed => _connectionLifetime == null || _isClosed == true || _connectionLifetime.IsTerminated == true;

        public void Run(IReadOnlyLifetime lifetime, IWebSocket webSocket)
        {
            if (_connectionLifetime != null && _connectionLifetime.IsTerminated == false)
                throw new InvalidOperationException("[Network] The writer runs again while its previous connection is alive");

            // A connection may be run again after a loss (the meta reconnect): the state of the previous one goes.
            _webSocket = webSocket;
            _connectionLifetime = lifetime;
            _isClosed = false;
            _writeQueue.Clear();
            _lifetime = lifetime.Child();

            webSocket.Closed.Advise(lifetime, OnClosed);

            Loop(lifetime).Forget();
        }

        /// <summary>
        /// Обрыв соединения: ответов больше не будет, поэтому ожидающие запросы завершаются
        /// сразу, а не по таймауту. Очередь отправки останавливается, в сокет писать некуда.
        /// </summary>
        private void OnClosed(SocketCloseInfo info)
        {
            Close();
        }

        /// <summary>
        /// The socket of this run never opened (NetworkConnection.Run: its Connect failed): the connection is closed
        /// as after a loss, so a caller that checks <see cref="IsClosed"/> opens it again instead of taking it alive.
        /// </summary>
        public void Close()
        {
            _isClosed = true;
            _writeQueue.Clear();

            // Copy: TrySetCanceled synchronously wakes the waiters, and they remove themselves from the dictionary.
            var pending = new List<UniTaskCompletionSource<INetworkContext>>(_pendingRequests.Values);
            _pendingRequests.Clear();

            foreach (var completion in pending)
                completion.TrySetCanceled();
        }

        private async UniTask Loop(IReadOnlyLifetime lifetime)
        {
            while (lifetime.IsTerminated == false && _isClosed == false)
            {
                if (_writeQueue.Count == 0)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(0.05f));
                    continue;
                }

                var request = _writeQueue[0];
                _writeQueue.RemoveAt(0);

                try
                {
                    var payload = MemoryPackSerializer.Serialize(request);
                    await _webSocket.Send(payload);
                }
                catch (Exception e)
                {
                    // Упавший цикл отправки молча вешает все последующие запросы, поэтому продолжаем.
                    UnityEngine.Debug.LogError($"Failed to send message to server: {e}");
                }
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
            var name = ErrorResponse.Describe(commandContext);

            // Nothing answers a closed connection: without this the request would wait for its timeout, and a chain
            // of requests (entering a session) - for one timeout per request.
            if (IsClosed == true)
                throw new OperationCanceledException($"Connection is closed: request {name} is not sent");

            if (_lifetime.IsTerminated == true)
                throw new InvalidOperationException($"Request {name} after ForceSendAll");

            // The request goes to the queue and awaits the response in parallel with anything else.
            using var trace = GameProfiler.Concurrent($"Request: {name}");

            _requestCounter++;

            var request = new RequestMessageFromClient()
            {
                Context = commandContext,
                RequestId = _requestCounter
            };

            var completion = new UniTaskCompletionSource<INetworkContext>();
            _pendingRequests.Add(request.RequestId, completion);

            _writeQueue.Add(request);

            // The end of the connection (or ForceSendAll) cancels the request. Removed when the request ends:
            // a listener per request would otherwise stay on the connection lifetime until it ends.
            _lifetime.Listen(OnLifetimeEnded);

            // The response may never arrive: the server handler crashed, the packet was lost. Cancelled by the response.
            var timeout = new CancellationTokenSource();
            LimitTime().Forget();

            INetworkContext context;

            try
            {
                context = await completion.Task;
            }
            finally
            {
                _pendingRequests.Remove(request.RequestId);
                _lifetime.RemoveListener(OnLifetimeEnded);
                timeout.Cancel();
                timeout.Dispose();
            }

            return context switch
            {
                T typed => typed,
                ErrorResponse error => throw new NetworkRequestException(error.Code, error.Message, name),
                _ => throw new InvalidOperationException(
                    $"Request {name} got {context.GetType().Name}, expected {typeof(T).Name}")
            };

            void OnLifetimeEnded()
            {
                completion.TrySetCanceled();
            }

            async UniTaskVoid LimitTime()
            {
                var isCanceled = await UniTask
                                       .Delay(TimeSpan.FromSeconds(RequestTimeoutSeconds),
                                           DelayType.Realtime,
                                           cancellationToken: timeout.Token)
                                       .SuppressCancellationThrow();

                if (isCanceled == true)
                    return;

                completion.TrySetException(new TimeoutException(
                    $"Request {name} got no response in {RequestTimeoutSeconds} s, expected {typeof(T).Name}"));
            }
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
                // Штатная ситуация: ответ пришёл после таймаута или обрыва, запрос уже снят.
                UnityEngine.Debug.LogWarning($"Response for unknown request {requestId} ignored");
            }
        }

        public async UniTask ForceSendAll()
        {
            _lifetime.Terminate();

            // The queue is taken, not read: left in place, the send loop sent it a second time on the next frame
            // (and a message it took during the await below modified the list under the enumeration).
            var messages = new List<IMessageFromClient>(_writeQueue);
            _writeQueue.Clear();

            foreach (var message in messages)
            {
                var payload = MemoryPackSerializer.Serialize(message);
                await _webSocket.Send(payload);
            }
        }
    }
}