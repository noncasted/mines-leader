using Cysharp.Threading.Tasks;
using Internal;
using NativeWebSocket;
using Shared;

namespace Global.Backend
{
    public interface INetworkConnection
    {
        IReadOnlyLifetime Lifetime { get; }

        IConnectionReader Reader { get; }
        IConnectionWriter Writer { get; }

        UniTask Run(IReadOnlyLifetime lifetime, string url);
    }

    public class NetworkConnection : INetworkConnection
    {
        public NetworkConnection(INetworkCommandsCollection commands)
        {
            _reader = new ConnectionReader();
            _writer = new ConnectionWriter(Reader);
            _dispatcher = new NetworkCommandsDispatcher(commands, this);
        }

        private WebSocket _webSocket;

        private readonly NetworkCommandsDispatcher _dispatcher;
        private readonly ConnectionReader _reader;
        private readonly ConnectionWriter _writer;

        private IReadOnlyLifetime _lifetime;

        public IReadOnlyLifetime Lifetime => _lifetime;
        public IConnectionReader Reader => _reader;
        public IConnectionWriter Writer => _writer;

        public async UniTask Run(IReadOnlyLifetime lifetime, string url)
        {
            _webSocket = new WebSocket(url);
            await _webSocket.Connect(lifetime);

            lifetime.Listen(() => _webSocket.Close());
            _lifetime = _webSocket.AttachLifetime(lifetime);

            _dispatcher.Run(lifetime);
            _reader.Run(lifetime, _webSocket).Forget();
            _writer.Run(lifetime, _webSocket);
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

            builder.AddPingCommand();
            
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

        public static UniTask ForceSendAll(this INetworkConnection connection)
        {
            return connection.Writer.ForceSendAll();
        }
    }
}