using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using AOT;
using Cysharp.Threading.Tasks;
using Internal;
using UnityEngine;

namespace Common.Network
{
    /// <summary>
    /// WebSocket implementation for WebGL platform using JavaScript interop.
    /// Uses handler-based architecture with unique IDs for each connection.
    /// </summary>
    public class JsWebSocket : IWebSocket
    {
        // Static dictionary to map handler IDs to C# instances for callbacks
        private static readonly Dictionary<int, JsWebSocket> Instances = new();
        private static bool _callbacksInitialized;

        private readonly ViewableDelegate<byte[]> _received = new();
        private readonly IReadOnlyLifetime _lifetime;
        private readonly int _handlerId;

        private bool _isConnected;
        private bool _isDisposed;

        public IViewableDelegate<byte[]> Received => _received;

        public JsWebSocket(string url, IReadOnlyLifetime lifetime)
        {
            _lifetime = lifetime.Child();

            // Initialize global callbacks once
            if (!_callbacksInitialized)
            {
                InitializeCallbacks();
                _callbacksInitialized = true;
            }

            // Create handler and get unique ID
            _handlerId = JsWebSocketCreate(url);

            // Register this instance for callbacks
            Instances[_handlerId] = this;

            // Setup cleanup on lifetime termination
            _lifetime.Listen(Dispose);

            Debug.Log($"[Network] [JsWebSocket] Created: id={_handlerId}, url={url}");
        }

        public async UniTask Connect()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(JsWebSocket));

            if (_isConnected)
            {
                Debug.LogWarning($"[Network] [JsWebSocket] Already connected: id={_handlerId}");
                return;
            }

            Debug.Log($"[Network] [JsWebSocket] Connecting: id={_handlerId}");

            var result = JsWebSocketConnect(_handlerId);
            if (result != 0)
            {
                throw new Exception($"Failed to connect WebSocket: error={result}");
            }

            // Wait for connection to be established
            await UniTask.WaitUntil(() => _isConnected || _isDisposed, cancellationToken: _lifetime.Token);

            if (_isDisposed)
                throw new ObjectDisposedException(nameof(JsWebSocket));

            Debug.Log($"[Network] [JsWebSocket] Connected: id={_handlerId}");
        }

        public async UniTask Send(byte[] bytes)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(JsWebSocket));

            if (!_isConnected)
                throw new InvalidOperationException("WebSocket is not connected");

            // Pin the byte array and get pointer for JS interop
            var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            
            try
            {
                var ptr = handle.AddrOfPinnedObject();
                var result = JsWebSocketSend(_handlerId, ptr, bytes.Length);

                if (result != 0)
                    throw new Exception($"Failed to send data: error={result}");
            }
            finally
            {
                handle.Free();
            }

            await UniTask.Yield();
        }

        private void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            Debug.Log($"[Network] [JsWebSocket] Disposing: id={_handlerId}");

            // Close connection if still open
            if (_isConnected)
            {
                JsWebSocketClose(_handlerId, 1000, "Normal closure");
            }

            // Free handler resources
            JsWebSocketFree(_handlerId);

            // Remove from instances
            Instances.Remove(_handlerId);

            // Clear event listeners
            _received.Dispose();

            Debug.Log($"[Network] [JsWebSocket] Disposed: id={_handlerId}");
        }

        #region JavaScript Interop

        // Import jslib functions
        [DllImport("__Internal")]
        private static extern void JsWebSocketSetOnOpen(Action<int> callback);

        [DllImport("__Internal")]
        private static extern void JsWebSocketSetOnMessage(Action<int, IntPtr, int> callback);

        [DllImport("__Internal")]
        private static extern void JsWebSocketSetOnError(Action<int, IntPtr> callback);

        [DllImport("__Internal")]
        private static extern void JsWebSocketSetOnClose(Action<int, int> callback);

        [DllImport("__Internal")]
        private static extern int JsWebSocketCreate(string url);

        [DllImport("__Internal")]
        private static extern int JsWebSocketConnect(int id);

        [DllImport("__Internal")]
        private static extern int JsWebSocketSend(int id, IntPtr buffer, int length);

        [DllImport("__Internal")]
        private static extern int JsWebSocketClose(int id, int code, string reason);

        [DllImport("__Internal")]
        private static extern int JsWebSocketFree(int id);

        [DllImport("__Internal")]
        private static extern int JsWebSocketGetState(int id);

        #endregion

        #region Callbacks (must be static for AOT)

        private static void InitializeCallbacks()
        {
            JsWebSocketSetOnOpen(OnOpen);
            JsWebSocketSetOnMessage(OnMessage);
            JsWebSocketSetOnError(OnError);
            JsWebSocketSetOnClose(OnClose);

            Debug.Log("[Network] [JsWebSocket] Global callbacks initialized");
        }

        [MonoPInvokeCallback(typeof(Action<int>))]
        private static void OnOpen(int handlerId)
        {
            if (Instances.TryGetValue(handlerId, out var instance))
            {
                Debug.Log($"[Network] [JsWebSocket] OnOpen: id={handlerId}");
                instance._isConnected = true;
            }
            else
            {
                Debug.LogError($"[Network] [JsWebSocket] OnOpen: instance not found for id={handlerId}");
            }
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr, int>))]
        private static void OnMessage(int handlerId, IntPtr bufferPtr, int length)
        {
            if (Instances.TryGetValue(handlerId, out var instance))
            {
                try
                {
                    // Copy data from unmanaged memory to managed byte array
                    var data = new byte[length];
                    Marshal.Copy(bufferPtr, data, 0, length);

                    Debug.Log($"[Network] [JsWebSocket] OnMessage: id={handlerId}, size={length}");

                    // Invoke the received event
                    instance._received.Invoke(data);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Network] [JsWebSocket] OnMessage error: {ex}");
                }
            }
            else
            {
                Debug.LogError($"[Network] [JsWebSocket] OnMessage: instance not found for id={handlerId}");
            }
        }

        [MonoPInvokeCallback(typeof(Action<int, IntPtr>))]
        private static void OnError(int handlerId, IntPtr errorPtr)
        {
            if (Instances.TryGetValue(handlerId, out var instance))
            {
                var errorMessage = Marshal.PtrToStringAnsi(errorPtr);
                Debug.LogError($"[Network] [JsWebSocket] OnError: id={handlerId}, error={errorMessage}");

                // Mark as not connected
                instance._isConnected = false;
            }
            else
            {
                Debug.LogError($"[Network] [JsWebSocket] OnError: instance not found for id={handlerId}");
            }
        }

        [MonoPInvokeCallback(typeof(Action<int, int>))]
        private static void OnClose(int handlerId, int code)
        {
            if (Instances.TryGetValue(handlerId, out var instance))
            {
                Debug.Log($"[Network] [JsWebSocket] OnClose: id={handlerId}, code={code}");
                instance._isConnected = false;
            }
            else
            {
                Debug.LogError($"[Network] [JsWebSocket] OnClose: instance not found for id={handlerId}");
            }
        }

        #endregion
    }
}
