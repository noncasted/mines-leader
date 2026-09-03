using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MemoryPack;
using Shared;

namespace Internal
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
        private IWebSocket _webSocket;

        private readonly Dictionary<int, UniTaskCompletionSource<INetworkContext>> _pendingRequests = new();
        private readonly List<IMessageFromClient> _writeQueue = new();

        private const float RequestTimeoutSeconds = 20f;
        private const int TimeoutIndex = 1;

        private int _requestCounter;
        private ILifetime _lifetime;
        private bool _isClosed;

        public void Run(IReadOnlyLifetime lifetime, IWebSocket webSocket)
        {
            _webSocket = webSocket;
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

            foreach (var message in _writeQueue)
            {
                var payload = MemoryPackSerializer.Serialize(message);
                await _webSocket.Send(payload);
            }
        }
    }
}