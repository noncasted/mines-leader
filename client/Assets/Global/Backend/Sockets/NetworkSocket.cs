using Cysharp.Threading.Tasks;
using Internal;
using NativeWebSocket;
using Shared;

namespace Global.Backend
{
    public interface INetworkSocket
    {
        IReadOnlyLifetime Lifetime { get; }

        ISocketReceiver Receiver { get; }
        ISocketSender Sender { get; }

        UniTask Run(IReadOnlyLifetime lifetime, string url);
    }

    public class NetworkSocket : INetworkSocket
    {
        public NetworkSocket()
        {
            _receiver = new SocketReceiver();
            _sender = new SocketSender(Receiver);
        }

        private WebSocket _webSocket;

        private readonly SocketReceiver _receiver;
        private readonly SocketSender _sender;

        private IReadOnlyLifetime _lifetime;

        public IReadOnlyLifetime Lifetime => _lifetime;
        public ISocketReceiver Receiver => _receiver;
        public ISocketSender Sender => _sender;

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
            builder.Register<NetworkSocket>()
                .As<INetworkSocket>()
                .AsSelf();
            
            return builder;
        }

        public static UniTask Send(this INetworkSocket socket, INetworkContext value)
        {
            return socket.Sender.Send(value);
        }

        public static UniTask<T> SendFull<T>(this INetworkSocket socket, INetworkContext commandContext)
        {
            return socket.Sender.SendFull<T>(commandContext);
        }

        public static UniTask ForceSendAll(this INetworkSocket socket)
        {
            return socket.Sender.ForceSendAll();
        }
    }
}