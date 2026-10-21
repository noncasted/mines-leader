using System.Net;
using System.Net.WebSockets;
using Common.Network;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Tests.Protocol;

/// <summary>
/// F3.2: a socket upgrade with another protocol hash (or none) is accepted and closed with
/// ClientOutdatedCloseCode, the current hash passes. A real Kestrel on a free loopback port, the check is the one both
/// gateways call before reading anything.
/// </summary>
public class ClientProtocolCheckTests
{
    private const string PassedReason = "passed";

    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task CurrentHash_Passes()
    {
        await using var server = await Server.Start();

        var result = await ConnectAndReceive(server.Uri($"?{ProtocolVersion.QueryKey}={ProtocolHash.Value}"));

        result.CloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
        result.CloseStatusDescription.Should().Be(PassedReason);
    }

    [Theory]
    [InlineData("?protocol=0123456789abcdef")]
    [InlineData("")]
    [InlineData("?userId=00000000-0000-0000-0000-000000000001")]
    public async Task OtherOrMissingHash_ClosedAsOutdated(string query)
    {
        await using var server = await Server.Start();

        var result = await ConnectAndReceive(server.Uri(query));

        ((int?)result.CloseStatus).Should().Be(ProtocolVersion.ClientOutdatedCloseCode);
        result.CloseStatusDescription.Should().Be(ProtocolVersion.ClientOutdatedReason);
    }

    private static async Task<WebSocketReceiveResult> ConnectAndReceive(Uri uri)
    {
        using var client = new ClientWebSocket();
        using var timeout = new CancellationTokenSource(Wait);

        await client.ConnectAsync(uri, timeout.Token);

        var result = await client.ReceiveAsync(new ArraySegment<byte>(new byte[1024]), timeout.Token);
        result.MessageType.Should().Be(WebSocketMessageType.Close);

        await client.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, timeout.Token);
        return result;
    }

    private sealed class Server : IAsyncDisposable
    {
        private Server(WebApplication app, string address)
        {
            _app = app;
            _address = address;
        }

        private readonly WebApplication _app;
        private readonly string _address;

        public Uri Uri(string query)
        {
            return new Uri(_address.Replace("http://", "ws://") + "/" + query);
        }

        public static async Task<Server> Start()
        {
            var builder = WebApplication.CreateSlimBuilder();
            builder.WebHost.UseKestrel(options => options.Listen(IPAddress.Loopback, 0));

            var app = builder.Build();
            app.UseWebSockets();

            app.Run(async context => {
                if (ClientProtocolCheck.IsCurrent(context, ProtocolHash.Value) == false)
                {
                    await ClientProtocolCheck.RejectOutdated(context, ProtocolHash.Value, NullLogger.Instance);
                    return;
                }

                using var socket = await context.WebSockets.AcceptWebSocketAsync();
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, PassedReason, CancellationToken.None);
            });

            await app.StartAsync();

            var address = app.Services.GetRequiredService<IServer>()
                             .Features.Get<IServerAddressesFeature>()!
                             .Addresses.First();

            return new Server(app, address);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
