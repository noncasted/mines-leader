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

            using (GameProfiler.Scope("Socket connect"))
                await _webSocket.Connect();

            _writer.Run(lifetime, _webSocket);

            // Writer подписан раньше: к моменту, когда обрыв дойдёт до игрового кода,
            // ожидающие запросы уже завершены.
            _webSocket.Closed.Advise(lifetime, reason => _closed.Invoke(reason));

            return;

            IWebSocket CreateWebSocket()
            {
                if (_platformOptions.IsEditor == true)
                    return new DefaultWebSocket(url, lifetime);

                switch (_platformOptions.PlatformType)
                {
                    case PlatformType.Website:
                    case PlatformType.ItchIO:
                        return new JsWebSocket(url, lifetime);
                    case PlatformType.IOS:
                    case PlatformType.Android:
                        return new DefaultWebSocket(url, lifetime);
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
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