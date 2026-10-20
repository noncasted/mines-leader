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
        IViewableDelegate<SocketCloseInfo> Closed { get; }

        // Opens the socket. ConnectionFailedException when it does not open (the connection is closed then); a loss
        // at any moment after it opens - even before Run returns - closes the connection and raises Closed.
        UniTask Run(IReadOnlyLifetime lifetime, string url);
    }

    public class NetworkConnection : INetworkConnection
    {
        public NetworkConnection(
            INetworkCommandsCollection commands,
            PlatformOptions platformOptions,
            IClientOutdatedNotice outdatedNotice)
        {
            _platformOptions = platformOptions;
            _outdatedNotice = outdatedNotice;
            _reader = new ConnectionReader();
            _writer = new ConnectionWriter();
            _dispatcher = new NetworkCommandsDispatcher(commands, this);
        }

        private IWebSocket _webSocket;

        private readonly NetworkCommandsDispatcher _dispatcher;
        private readonly ConnectionReader _reader;
        private readonly ConnectionWriter _writer;
        private readonly PlatformOptions _platformOptions;
        private readonly IClientOutdatedNotice _outdatedNotice;
        private readonly ViewableDelegate<SocketCloseInfo> _closed = new();

        // The protocol this client was built with, sent in the query of the upgrade. A field, not the constant at
        // the call: a play test replaces it to check the outdated client.
        private readonly string _protocolHash = ProtocolVersion.Hash;

        // A connect that hangs (a proxy that accepts TCP but has no server behind it, a stalled upgrade) is a failed
        // one: without a bound the caller waited forever.
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(15);

        public IConnectionReader Reader => _reader;
        public IConnectionWriter Writer => _writer;
        public IViewableDelegate<SocketCloseInfo> Closed => _closed;

        public async UniTask Run(IReadOnlyLifetime lifetime, string url)
        {
            _webSocket = CreateWebSocket(AppendProtocol(url));

            // A server of another protocol accepts the socket and closes it at once, maybe inside Connect: the notice
            // is subscribed first, before whatever else listens to the loss (the meta stops its retries on it).
            _webSocket.Closed.Advise(lifetime, OnSocketClosed);

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
            _webSocket.Closed.Advise(lifetime, info => _closed.Invoke(info));

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

            IWebSocket CreateWebSocket(string socketUrl)
            {
                // ClientWebSocket в WebGL не работает, а ссылка на него тянет в билд System.Net, TLS и Mono.Security.
#if UNITY_EDITOR || !UNITY_WEBGL
                if (_platformOptions.IsEditor == true)
                    return new DefaultWebSocket(socketUrl, lifetime);
#endif

                switch (_platformOptions.PlatformType)
                {
                    case PlatformType.Website:
                    case PlatformType.ItchIO:
                        return new JsWebSocket(socketUrl, lifetime);
#if UNITY_EDITOR || !UNITY_WEBGL
                    case PlatformType.IOS:
                    case PlatformType.Android:
                        return new DefaultWebSocket(socketUrl, lifetime);
#endif
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        private string AppendProtocol(string url)
        {
            var separator = url.Contains('?') == true ? '&' : '?';
            return $"{url}{separator}{ProtocolVersion.QueryKey}={_protocolHash}";
        }

        private void OnSocketClosed(SocketCloseInfo info)
        {
            if (info.Code == ProtocolVersion.ClientOutdatedCloseCode)
                _outdatedNotice.Show();
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