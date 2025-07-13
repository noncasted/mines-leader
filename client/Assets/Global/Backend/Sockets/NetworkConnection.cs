using Cysharp.Threading.Tasks;
using Internal;
using NativeWebSocket;
using Shared;

namespace Global.Backend
{
    public interface INetworkConnection
    {
        IReadOnlyLifetime Lifetime { get; }

        INetworkReader Receiver { get; }
        INetworkWriter Sender { get; }

        UniTask Run(IReadOnlyLifetime lifetime, string url);
    }

    public class NetworkConnection : INetworkConnection
    {
        public NetworkConnection()
        {
            _receiver = new NetworkReader();
            _sender = new NetworkWriter(Receiver);
        }

        private WebSocket _webSocket;

        private readonly NetworkReader _receiver;
        private readonly NetworkWriter _sender;

        private IReadOnlyLifetime _lifetime;

        public IReadOnlyLifetime Lifetime => _lifetime;
        public INetworkReader Receiver => _receiver;
        public INetworkWriter Sender => _sender;

        public async UniTask Run(IReadOnlyLifetime lifetime, string url)
        {
            _webSocket = new WebSocket(url);
            await _webSocket.Connect(lifetime);

            lifetime.Listen(() => _webSocket.Close());
            _lifetime = _webSocket.AttachLifetime(lifetime);

            _receiver.Run(lifetime, _webSocket).Forget();
            _sender.Run(lifetime, _webSocket).Forget();
        }
    }

    public static class NetworkSocketExtensions
    {
        public static IScopeBuilder AddNetworkSocket(this IScopeBuilder builder)
        {
            builder.Register<NetworkConnection>()
                .As<INetworkConnection>()
                .AsSelf();
            
            return builder;
        }

        public static UniTask Send(this INetworkConnection connection, INetworkContext value)
        {
            return connection.Sender.Send(value);
        }

        public static UniTask<T> SendFull<T>(this INetworkConnection connection, INetworkContext commandContext)
        {
            return connection.Sender.SendFull<T>(commandContext);
        }

        public static UniTask ForceSendAll(this INetworkConnection connection)
        {
            return connection.Sender.ForceSendAll();
        }
    }
}