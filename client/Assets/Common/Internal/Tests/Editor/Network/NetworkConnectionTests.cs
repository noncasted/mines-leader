using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Shared;
using UnityEngine;
using UnityEngine.TestTools;

namespace Internal.Tests
{
    // A socket lost right after it opens (before the answer to the authorization) closes the connection like any
    // later loss: Writer.IsClosed, a request gets null at once (not after its 20 s timeout), Closed is raised.
    // A socket that never opens: Run throws ConnectionFailedException, the connection is closed.
    // A server of another protocol closes the socket with ProtocolVersion.ClientOutdatedCloseCode: Closed carries
    // that code and the outdated notice is shown.
    public class NetworkConnectionTests
    {
        private static readonly TimeSpan LossTimeout = TimeSpan.FromSeconds(5);

        // The server accepts the upgrade and ends the TCP stream at once. The main thread is held until the end has
        // arrived: the first receive of the socket, started on the main thread right after the connect, then fails
        // synchronously - inside Connect, before the rest of NetworkConnection.Run.
        [UnityTest]
        public IEnumerator SocketLostInsideConnect_ConnectionIsClosed() => UniTask.ToCoroutine(async () =>
        {
            using var server = new EndingServer();
            var lifetime = new Lifetime();

            try
            {
                var connection = CreateConnection();
                var closed = new UniTaskCompletionSource<SocketCloseInfo>();
                connection.Closed.Advise(lifetime, reason => closed.TrySetResult(reason));

                var run = connection.Run(lifetime, server.Url);
                server.WaitEnded();
                await run;

                await closed.Task.Timeout(LossTimeout);
                Assert.IsTrue(connection.Writer.IsClosed);

                LogAssert.Expect(LogType.Error, new Regex("the connection is closed"));
                var response = await connection.Request<SharedSessionAuth.Response>(new SharedSessionAuth.Request())
                                               .Timeout(LossTimeout);

                Assert.IsNull(response);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        [UnityTest]
        public IEnumerator ConnectRefused_RunThrows_ConnectionIsClosed() => UniTask.ToCoroutine(async () =>
        {
            var lifetime = new Lifetime();

            try
            {
                var connection = CreateConnection();
                Exception exception = null;

                try
                {
                    await connection.Run(lifetime, $"ws://127.0.0.1:{GetFreePort()}/");
                }
                catch (Exception caught)
                {
                    exception = caught;
                }

                Assert.IsInstanceOf<ConnectionFailedException>(exception, exception?.ToString());
                Assert.IsTrue(connection.Writer.IsClosed);
            }
            finally
            {
                lifetime.Terminate();
            }
        });

        // The gateways accept the socket of an outdated client and close it at once with 4001 (ClientProtocolCheck).
        // The upgrade carries the hash of this build.
        [UnityTest]
        public IEnumerator ServerClosesAsOutdated_ClosedCarriesCode_NoticeShown() => UniTask.ToCoroutine(async () =>
        {
            var port = GetFreePort();
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();

            var query = new UniTaskCompletionSource<string>();
            var serverTask = ServeOutdated(listener, query);

            var lifetime = new Lifetime();
            var notice = new FakeOutdatedNotice();

            try
            {
                var connection = CreateConnection(notice);
                var closed = new UniTaskCompletionSource<SocketCloseInfo>();
                connection.Closed.Advise(lifetime, info => closed.TrySetResult(info));

                await connection.Run(lifetime, $"ws://127.0.0.1:{port}/?userId=1");

                var info = await closed.Task.Timeout(LossTimeout);

                Assert.AreEqual(ProtocolVersion.ClientOutdatedCloseCode, info.Code, info.ToString());
                Assert.AreEqual(1, notice.ShowCount);
                Assert.IsTrue(connection.Writer.IsClosed);
                Assert.AreEqual($"?userId=1&{ProtocolVersion.QueryKey}={ProtocolVersion.Hash}",
                    await query.Task.Timeout(LossTimeout));
            }
            finally
            {
                lifetime.Terminate();
                listener.Stop();
            }

            await serverTask;
        });

        private static async UniTask ServeOutdated(HttpListener listener, UniTaskCompletionSource<string> query)
        {
            var context = await listener.GetContextAsync();
            query.TrySetResult(context.Request.Url.Query);

            var webSocketContext = await context.AcceptWebSocketAsync(null);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await webSocketContext.WebSocket.CloseOutputAsync((WebSocketCloseStatus)ProtocolVersion.ClientOutdatedCloseCode,
                ProtocolVersion.ClientOutdatedReason,
                timeout.Token);
        }

        private static NetworkConnection CreateConnection(IClientOutdatedNotice notice = null)
        {
            return new NetworkConnection(new NetworkCommandsCollection(),
                new PlatformOptions(),
                notice ?? new FakeOutdatedNotice());
        }

        private class FakeOutdatedNotice : IClientOutdatedNotice
        {
            public int ShowCount;

            public bool IsShown => ShowCount > 0;

            public void Show()
            {
                ShowCount++;
            }
        }

        private static int GetFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            return port;
        }

        // A websocket server on a raw TCP socket: answers the upgrade, then shuts its side down (FIN, no Close frame).
        private class EndingServer : IDisposable
        {
            public EndingServer()
            {
                _listener = new TcpListener(IPAddress.Loopback, 0);
                _listener.Start();
                Url = $"ws://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/";
                _thread = new Thread(Serve) { IsBackground = true };
                _thread.Start();
            }

            private readonly TcpListener _listener;
            private readonly Thread _thread;
            private readonly ManualResetEventSlim _ended = new();
            private Socket _client;

            public string Url { get; }

            // Blocks the calling (main) thread until the stream is ended, plus a margin for the FIN to reach the client.
            public void WaitEnded()
            {
                if (_ended.Wait(TimeSpan.FromSeconds(10)) == false)
                    throw new TimeoutException("The server did not end the socket");

                Thread.Sleep(200);
            }

            private void Serve()
            {
                _client = _listener.AcceptSocket();

                var request = new StringBuilder();
                var buffer = new byte[1024];

                while (request.ToString().Contains("\r\n\r\n") == false)
                {
                    var read = _client.Receive(buffer);

                    if (read == 0)
                        throw new InvalidOperationException("The client closed before the upgrade");

                    request.Append(Encoding.ASCII.GetString(buffer, 0, read));
                }

                var key = ReadHeader(request.ToString(), "Sec-WebSocket-Key");

                using var sha1 = SHA1.Create();
                var accept = Convert.ToBase64String(
                    sha1.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));

                var response = "HTTP/1.1 101 Switching Protocols\r\n" +
                               "Upgrade: websocket\r\n" +
                               "Connection: Upgrade\r\n" +
                               $"Sec-WebSocket-Accept: {accept}\r\n\r\n";

                _client.Send(Encoding.ASCII.GetBytes(response));
                _client.Shutdown(SocketShutdown.Send);
                _ended.Set();
            }

            private static string ReadHeader(string request, string name)
            {
                foreach (var line in request.Split("\r\n"))
                {
                    if (line.StartsWith(name + ":", StringComparison.OrdinalIgnoreCase) == true)
                        return line.Substring(name.Length + 1).Trim();
                }

                throw new InvalidOperationException($"No {name} in the upgrade request");
            }

            public void Dispose()
            {
                _client?.Dispose();
                _listener.Stop();
                _ended.Dispose();
            }
        }
    }
}
