using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using Common.Network;
using Internal;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Framework.Tests.Session;

// A client lost without a close keeps its TCP connection open (a frozen client that no longer reads): only the
// protocol ping of the gateway (WebSocketAcceptContext.KeepAliveTimeout) ends the read loop of its Connection. A
// socket leaked by an editor domain reload is not such a client: its pending receive keeps answering pings
// (ClientThatReads_... below).
// A real Kestrel on a free loopback port with short keep-alive times, the server side is the real Connection.
public class SessionKeepAliveTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task ClientThatStopsReading_IsDisconnectedByKeepAliveTimeout()
    {
        await using var server = await KeepAliveServer.Start(Interval, Timeout);
        using var client = await Connect(server);

        // The client never receives: pings stay without a pong.
        var finished = await Task.WhenAny(server.Disconnected, Task.Delay(TimeSpan.FromSeconds(10)));

        finished.Should().Be(server.Disconnected, "the keep-alive timeout must end the read loop");
        server.Stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));

        // An expected loss of a client, not an error of the server.
        server.Logger.Entries.Should().ContainSingle(entry => entry.Message.Contains("keep-alive"), string.Join(" || ", server.Logger.Entries));
        server.Logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ClientThatReads_StaysConnectedLongerThanTimeout()
    {
        await using var server = await KeepAliveServer.Start(Interval, Timeout);
        using var client = await Connect(server);

        // A pending receive answers the pings.
        var receive = client.ReceiveAsync(new ArraySegment<byte>(new byte[1024]), CancellationToken.None);

        await Task.Delay((Interval + Timeout) * 3);

        server.Disconnected.IsCompleted.Should().BeFalse();
        receive.IsCompleted.Should().BeFalse();

        await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        (await Task.WhenAny(server.Disconnected, Task.Delay(TimeSpan.FromSeconds(5)))).Should().Be(server.Disconnected);
    }

    private static async Task<ClientWebSocket> Connect(KeepAliveServer server)
    {
        var client = new ClientWebSocket();

        // No unsolicited pongs from the client side: only answers to the pings of the server count.
        client.Options.KeepAliveInterval = TimeSpan.Zero;

        await client.ConnectAsync(server.Uri, CancellationToken.None);
        await server.Connected;

        return client;
    }

    private class KeepAliveServer : IAsyncDisposable
    {
        private KeepAliveServer(
            WebApplication app,
            Uri uri,
            Task connected,
            Task disconnected,
            Stopwatch stopwatch,
            RecordingLogger<Connection> logger)
        {
            _app = app;
            Logger = logger;
            Uri = uri;
            Connected = connected;
            Disconnected = disconnected;
            Stopwatch = stopwatch;
        }

        private readonly WebApplication _app;

        public Uri Uri { get; }
        public RecordingLogger<Connection> Logger { get; }
        public Task Connected { get; }
        public Task Disconnected { get; }

        // Started when the socket is accepted.
        public Stopwatch Stopwatch { get; }

        public static async Task<KeepAliveServer> Start(TimeSpan interval, TimeSpan timeout)
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));

            var app = builder.Build();
            var connected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var stopwatch = new Stopwatch();
            var logger = new RecordingLogger<Connection>();

            app.UseWebSockets();

            app.Run(async context => {
                var webSocket = await context.WebSockets.AcceptWebSocketAsync(new WebSocketAcceptContext
                {
                    KeepAliveInterval = interval,
                    KeepAliveTimeout = timeout
                });

                stopwatch.Start();
                connected.TrySetResult();

                var lifetime = new Lifetime();
                var connection = new Connection(webSocket, lifetime, logger);
                connection.Reader.Requests.Advise(lifetime, _ => { });

                try
                {
                    await connection.Run();
                }
                finally
                {
                    stopwatch.Stop();
                    disconnected.TrySetResult();
                    lifetime.Terminate();
                }
            });

            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            var uri = new Uri(address.Replace("http://", "ws://"));

            return new KeepAliveServer(app, uri, connected.Task, disconnected.Task, stopwatch, logger);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
