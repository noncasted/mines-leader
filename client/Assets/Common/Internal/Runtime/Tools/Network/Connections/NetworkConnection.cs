using System;
using Cysharp.Threading.Tasks;
using Shared;

namespace Internal
{
    public interface INetworkConnection
    {
        IConnectionReader Reader { get; }
        IConnectionWriter Writer { get; }

        /// <summary>
        /// Соединение оборвалось не по нашей инициативе. Переподключения нет: ожидающие
        /// запросы уже завершены с null, дальше решает вызывающий код.
        /// </summary>
        IViewableDelegate<string> Closed { get; }

        // Opens the socket. ConnectionFailedException when it does not open (the connection is closed then); a loss
        // at any moment after it opens - even before Run returns - closes the connection and raises Closed.
        UniTask Run(IReadOnlyLifetime lifetime, string url);
    }

    public class NetworkConnection : INetworkConnection
    {
        public NetworkConnection(INetworkCommandsCollection commands, PlatformOptions platformOptions)
        {
            _platformOptions = platformOptions;
            _reader = new ConnectionReader();
            _writer = new ConnectionWriter();
            _dispatcher = new NetworkCommandsDispatcher(commands, this);
        }

        private IWebSocket _webSocket;

        private readonly NetworkCommandsDispatcher _dispatcher;
        private readonly ConnectionReader _reader;
        private readonly ConnectionWriter _writer;
        private readonly PlatformOptions _platformOptions;
        private readonly ViewableDelegate<string> _closed = new();

        // A connect that hangs (a proxy that accepts TCP but has no server behind it, a stalled upgrade) is a failed
        // one: without a bound the caller waited forever.
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

        public IConnectionReader Reader => _reader;
        public IConnectionWriter Writer => _writer;
        public IViewableDelegate<string> Closed => _closed;

        public async UniTask Run(IReadOnlyLifetime lifetime, string url)
        {
            _webSocket = CreateWebSocket();

            // Чтение подписано до коннекта: авторизация едет в апгрейде, и сервер шлёт проекции
            // сразу. Первые кадры могут прийти вместе с ответом на апгрейд и попасть в Received
            // ещё внутри Connect (или между onopen и следующим кадром в WebGL) — без подписчика
            // они терялись, и профиль оставался null.
            _dispatcher.Run(lifetime);
            _reader.Run(lifetime, _webSocket);

            // The writer and the listeners of the loss are subscribed before connecting too: the socket may be lost
            // inside Connect (DefaultWebSocket starts its receive loop there, and a socket dropped between the upgrade
            // and the next frame fails its first receive synchronously). Subscribed after it, they missed that loss:
            // the connection stayed open forever, the authorization waited for its timeout or for projections that
            // never came. The writer goes first: by the time the loss reaches game code, pending requests are
            // already completed.
            _writer.Run(lifetime, _webSocket);
            _webSocket.Closed.Advise(lifetime, reason => _closed.Invoke(reason));

            try
            {
                // Коннект идёт параллельно загрузке меню: отрезок на стек не встаёт, иначе чужие
                // этапы вложились бы в ожидание сокета.
                using (GameProfiler.Concurrent("Socket connect"))
                    await _webSocket.Connect().Timeout(ConnectTimeout, DelayType.Realtime);
            }
            // The socket never opened (refused, dropped during the upgrade): nothing is answered on this connection.
            // The end of the lifetime is not a failure: its cancellation goes up as it is.
            catch (Exception exception) when (lifetime.IsTerminated == false)
            {
                _writer.Close();
                throw new ConnectionFailedException(url, exception);
            }

            return;

            IWebSocket CreateWebSocket()
            {
                // ClientWebSocket в WebGL не работает, а ссылка на него тянет в билд System.Net, TLS и Mono.Security.
#if UNITY_EDITOR || !UNITY_WEBGL
                if (_platformOptions.IsEditor == true)
                    return new DefaultWebSocket(url, lifetime);
#endif

                switch (_platformOptions.PlatformType)
                {
                    case PlatformType.Website:
                    case PlatformType.ItchIO:
                        return new JsWebSocket(url, lifetime);
#if UNITY_EDITOR || !UNITY_WEBGL
                    case PlatformType.IOS:
                    case PlatformType.Android:
                        return new DefaultWebSocket(url, lifetime);
#endif
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }

    // NetworkConnection.Run: the socket did not open (connect refused, the network dropped it during the upgrade).
    // The connection is closed (IConnectionWriter.IsClosed); INetworkConnection.Closed is not raised - it is for a
    // connection that was established.
    public class ConnectionFailedException : Exception
    {
        public ConnectionFailedException(string url, Exception inner)
            : base($"[Network] The socket to {url} did not open: {inner.GetType().Name} {inner.Message}", inner)
        {
        }
    }

    public static class NetworkConnectionExtensions
    {
        public static IScopeBuilder AddNetworkConnection(this IScopeBuilder builder)
        {
            builder.Register<NetworkConnection>()
                   .As<INetworkConnection>()
                   .AsSelf();

            builder.Register<NetworkCommandsCollection>()
                   .AsSelfResolvable()
                   .As<INetworkCommandsCollection>();

            builder.Register<NetworkCommandsDispatcher>()
                   .As<INetworkCommandsDispatcher>();

            return builder;
        }

        public static void OneWay(this INetworkConnection connection, INetworkContext value)
        {
            connection.Writer.WriteOneWay(value);
        }

        public static UniTask<T> Request<T>(this INetworkConnection connection, INetworkContext commandContext)
        {
            return connection.Writer.WriteRequest<T>(commandContext);
        }

        public static UniTask<EmptyResponse> Request(this INetworkConnection connection, INetworkContext commandContext)
        {
            return connection.Writer.WriteRequest<EmptyResponse>(commandContext);
        }

        public static UniTask ForceSendAll(this INetworkConnection connection)
        {
            return connection.Writer.ForceSendAll();
        }
    }
}