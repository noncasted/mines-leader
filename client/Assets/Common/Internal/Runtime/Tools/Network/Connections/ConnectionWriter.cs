using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Shared;

namespace Internal
{
    public interface IConnectionWriter
    {
        // No request is answered any more: the connection is lost, its socket never opened, or the lifetime it was
        // run with has ended. Requests complete with null at once. Never run is closed too.
        bool IsClosed { get; }

        void WriteOneWay(INetworkContext value);
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
        private const int TimeoutIndex = 1;

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
        private void OnClosed(string reason)
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

            // Копия: TrySetResult синхронно будит ожидающих, а они снимают себя из словаря.
            var pending = new List<UniTaskCompletionSource<INetworkContext>>(_pendingRequests.Values);
            _pendingRequests.Clear();

            foreach (var completion in pending)
                completion.TrySetResult(null);
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
            // Запрос уходит в очередь и ждёт ответа параллельно с чем угодно ещё.
            using var trace = GameProfiler.Concurrent($"Request: {Describe(commandContext)}");

            _requestCounter++;

            var request = new RequestMessageFromClient()
            {
                Context = commandContext,
                RequestId = _requestCounter
            };


            // Nothing answers a closed connection: without this the request waited for its whole timeout.
            if (IsClosed == true)
            {
                UnityEngine.Debug.LogError($"Request {Describe(commandContext)} is not sent: the connection is closed");
                return default;
            }

            var completion = new UniTaskCompletionSource<INetworkContext>();
            _pendingRequests.Add(request.RequestId, completion);

            _writeQueue.Add(request);

            // Обрыв соединения не должен вешать вызывающего: он получит null и покажет ошибку сам.
            _lifetime.Listen(() => completion.TrySetResult(null));

            var (winner, context, _) = await UniTask.WhenAny(completion.Task, AwaitTimeout());

            _pendingRequests.Remove(request.RequestId);

            if (context is T typed)
                return typed;

            var reason = winner == TimeoutIndex ? "timed out" : "got no response";

            UnityEngine.Debug.LogError(
                $"Request {Describe(commandContext)} {reason} after {RequestTimeoutSeconds}s, expected {typeof(T).Name}");

            return default;
        }

        /// <summary>
        /// Ответ может не прийти вовсе: сервер уронил обработчик, сокет переподключился, пакет потерялся.
        /// Без таймаута такой запрос висит вечно, и UI остаётся в состоянии загрузки.
        /// </summary>
        private static async UniTask<INetworkContext> AwaitTimeout()
        {
            await UniTask.Delay(TimeSpan.FromSeconds(RequestTimeoutSeconds), ignoreTimeScale: true);
            return null;
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

        /// <summary>
        /// Контексты объявлены вложенными типами, и у них Name — это голое "Request":
        /// без владельца в трассе не понять, какой именно запрос ждали.
        /// </summary>
        private static string Describe(INetworkContext context)
        {
            var type = context.GetType();

            return type.DeclaringType == null ? type.Name : $"{type.DeclaringType.Name}.{type.Name}";
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