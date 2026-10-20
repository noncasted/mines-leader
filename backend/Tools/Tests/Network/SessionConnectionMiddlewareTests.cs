using System.Net.WebSockets;
using Common.Network;
using Common.Reactive;
using FluentAssertions;
using Game.Global;
using Game.Session;
using GameGateway;
using Infrastructure.Startup;
using MemoryPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared;
using Tests.Security;
using Xunit;

namespace Tests.Network;

/// <summary>
/// The game gateway refuses a connection to a session that is not in the collection (closed after the search,
/// unknown id) explicitly: the client gets a failed auth response and a close, not a dropped socket.
/// </summary>
public class SessionConnectionMiddlewareTests
{
    [Fact]
    public async Task UnknownSession_RespondsFailureAndCloses()
    {
        var sessions = Substitute.For<ISessionsCollection>();
        sessions.TryGet(Arg.Any<Guid>(), out Arg.Any<ISession>()).Returns(false);

        await using var host = await Start(sessions);
        using var socket = await Connect(host);
        var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

        await Send(socket, new SharedSessionAuth.Request
        {
            SessionId = Guid.NewGuid(),
            UserId = Guid.NewGuid()
        }, cancellation);

        var reader = new WebSocketMessageReader();
        (await reader.Read(socket, cancellation)).Should().Be(WebSocketReadStatus.Message);

        var message = MemoryPackSerializer.Deserialize<IMessageFromServer>(reader.Payload.Span);
        var response = message.Should().BeOfType<ResponseMessageFromServer>().Subject;
        response.RequestId.Should().Be(1);
        response.Context.Should().BeOfType<SharedSessionAuth.Response>().Which.IsSuccess.Should().BeFalse();

        (await reader.Read(socket, cancellation)).Should().Be(WebSocketReadStatus.Close);
        socket.CloseStatus.Should().Be(WebSocketCloseStatus.PolicyViolation);
    }

    private static async Task<TestWebHost> Start(ISessionsCollection sessions)
    {
        var participant = Substitute.For<IClusterParticipantContext>();
        participant.IsInitialized.Returns(new ViewableProperty<bool>(true));

        var builder = TestWebHost.CreateBuilder("Production");
        builder.Services.AddSingleton(sessions);
        builder.Services.AddSingleton(participant);

        var app = builder.Build();
        app.UseWebSockets();
        app.UseMiddleware<SessionConnectionMiddleware>();

        return await TestWebHost.Start(app);
    }

    private static async Task<ClientWebSocket> Connect(TestWebHost host)
    {
        var socket = new ClientWebSocket();
        var uri = new UriBuilder(host.Address) { Scheme = "ws", Path = "/" }.Uri;
        await socket.ConnectAsync(uri, CancellationToken.None);
        return socket;
    }

    private static Task Send(ClientWebSocket socket, INetworkContext context, CancellationToken cancellation)
    {
        IMessageFromClient message = new RequestMessageFromClient
        {
            Context = context,
            RequestId = 1
        };

        var bytes = MemoryPackSerializer.Serialize(message);
        return socket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellation);
    }
}
