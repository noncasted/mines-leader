using Cysharp.Threading.Tasks;
using Internal;
using NativeWebSocket;

namespace Global.Backend
{
    public static class NativeWebsocketExtensions
    {
        public static async UniTask Connect(this WebSocket webSocket, IReadOnlyLifetime lifetime)
        {
            var completion = new UniTaskCompletionSource();
            var listenLifetime = lifetime.Child();

            webSocket.OnOpen += OnOpen;
            webSocket.Connect();

            listenLifetime.Listen(() =>
            {
                webSocket.OnOpen -= OnOpen;

                if (completion.Task.Status == UniTaskStatus.Pending)
                    completion.TrySetCanceled();
            });

            await completion.Task;

            listenLifetime.Terminate();


            void OnOpen()
            {
                completion.TrySetResult();
            }
        }

        public static IReadOnlyLifetime AttachLifetime(this WebSocket webSocket, IReadOnlyLifetime parent)
        {
            var lifetime = parent.Child();
            webSocket.OnClose += OnClose;
            return lifetime;
            

            void OnClose(WebSocketCloseCode closeCode)
            {
                lifetime.Terminate();
                webSocket.OnClose -= OnClose;
            }
        }
    }
}