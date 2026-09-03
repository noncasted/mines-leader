using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Internal
{
    public class DefaultWebSocket : IWebSocket
    {
        private readonly ClientWebSocket _socket;

        private readonly Uri _uri;
        private readonly ViewableDelegate<byte[]> _received = new();
        private readonly ViewableDelegate<string> _closed = new();

        private readonly ILifetime _lifetime;
        private readonly CancellationToken _cancellation;

        private bool _isClosedNotified;

        private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(2);

        public WebSocketState State => _socket.State;
        public IViewableDelegate<byte[]> Received => _received;
        public IViewableDelegate<string> Closed => _closed;

        public DefaultWebSocket(string url, IReadOnlyLifetime lifetime)
        {
            _uri = new Uri(url);
            _lifetime = lifetime.Child();
            _cancellation = _lifetime.Token;

            var protocol = _uri.Scheme;

            if (!protocol.Equals("ws") && !protocol.Equals("wss"))
                throw new ArgumentException("Unsupported protocol: " + protocol);

            _socket = new ClientWebSocket();
        }

        public async UniTask Connect()
        {
            await _socket.ConnectAsync(_uri, _cancellation);

            // ClientWebSocket продолжает на пуле потоков, а всё, что выше сокета,
            // работает с Unity-объектами.
            await UniTask.SwitchToMainThread(_cancellation);

            _lifetime.Listen(() => Shutdown().Forget());
            Receive().Forget();
        }

        public async UniTask Send(byte[] bytes)
        {
            var payload = new ReadOnlyMemory<byte>(bytes);
            await _socket.SendAsync(payload, WebSocketMessageType.Binary, true, _cancellation);
            await UniTask.SwitchToMainThread();
        }

        /// <summary>
        /// Приём без токена лайфтайма: отмена ожидающего ReceiveAsync переводит ClientWebSocket
        /// в Aborted, и корректно закрыться после этого уже нельзя. Цикл завершается либо
        /// Close-кадром, либо Dispose сокета из <see cref="Shutdown"/>.
        /// </summary>
        private async UniTask Receive()
        {
            var buffer = new ArraySegment<byte>(new byte[8192]);
            string closeReason;

            try
            {
                while (true)
                {
                    using var stream = new MemoryStream();

                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _socket.ReceiveAsync(buffer, CancellationToken.None);
                        stream.Write(buffer.Array, buffer.Offset, result.Count);
                    }
                    while (result.EndOfMessage == false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        closeReason = $"closed by server: {result.CloseStatus} {result.CloseStatusDescription}";
                        await AcknowledgeClose();
                        break;
                    }

                    var rawValue = stream.ToArray();

                    await UniTask.SwitchToMainThread(_cancellation);
                    _received.Invoke(rawValue);
                }
            }
            catch (OperationCanceledException)
            {
                // Лайфтайм завершён локально: это не обрыв.
                return;
            }
            catch (ObjectDisposedException)
            {
                // Сокет уничтожен из Shutdown.
                return;
            }
            catch (WebSocketException exception)
            {
                closeReason = $"socket error: {exception.WebSocketErrorCode} {exception.Message}";
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                closeReason = $"receive failed: {exception.GetType().Name} {exception.Message}";
            }

            await NotifyClosed(closeReason);
        }

        /// <summary>
        /// Сервер прислал Close: отвечаем своим Close-кадром, чтобы завершить рукопожатие.
        /// CloseOutputAsync не ждёт встречного кадра и не конфликтует с циклом приёма.
        /// </summary>
        private async UniTask AcknowledgeClose()
        {
            if (_socket.State != WebSocketState.CloseReceived)
                return;

            try
            {
                using var timeout = new CancellationTokenSource(CloseTimeout);
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Network] [DefaultWebSocket] Close acknowledge failed: {exception.Message}");
            }
        }

        private async UniTask NotifyClosed(string reason)
        {
            await UniTask.SwitchToMainThread();

            if (_lifetime.IsTerminated == true || _isClosedNotified == true)
                return;

            _isClosedNotified = true;

            Debug.LogWarning($"[Network] [DefaultWebSocket] Connection lost: {reason}");
            _closed.Invoke(reason);
        }

        /// <summary>
        /// Локальное завершение: отправляем Close-кадр, если соединение ещё живо, и уничтожаем
        /// сокет. Dispose обрывает ожидающий ReceiveAsync, цикл приёма выходит без уведомления.
        /// </summary>
        private async UniTask Shutdown()
        {
            try
            {
                if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                {
                    using var timeout = new CancellationTokenSource(CloseTimeout);
                    await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Network] [DefaultWebSocket] Close failed: {exception.Message}");
            }
            finally
            {
                _socket.Dispose();
                _received.Dispose();
                _closed.Dispose();
            }
        }

        public UniTask Close()
        {
            _lifetime.Terminate();
            return UniTask.CompletedTask;
        }
    }
}
