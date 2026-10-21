using System.Net.WebSockets;
using Cluster.State;
using Common.Network;
using Common.Reactive;
using FluentAssertions;
using Infrastructure.Startup;
using MemoryPack;
using Meta.Users;
using MetaGateway.UserFlow;
using MetaGateway.UserFlow.Connection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared;
using Tests.Security;
using Xunit;

namespace Tests.Network;

/// <summary>
/// S3: the user of the meta socket comes only from its first frame. A refusal is a response with the status and a
/// close with SharedBackendSocketAuth.RejectedCloseCode; the user id in the query (a client before S3) is not
/// looked at.
/// </summary>
public class MetaSocketHandshakeTests
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task WrongCredentials_RespondsRefusal_ClosesWith4003_NoUserConnected()
    {
        var world = new World();
        var guestId = Guid.NewGuid();
        world.Users.ResolveGuest(guestId, "wrong").Returns(Task.FromResult<ResolvedUser?>(null));

        await using var host = await world.Start();
        using var socket = await Connect(host, string.Empty);

        await Send(socket, new SharedBackendSocketAuth.Request { GuestId = guestId, GuestSecret = "wrong" });

        using var cancellation = new CancellationTokenSource(Wait);
        var reader = new WebSocketMessageReader();
        var response = await ReadResponse(socket, reader, cancellation.Token);

        response.Status.Should().Be(SharedBackendSocketAuth.AuthStatus.InvalidCredentials);
        response.UserId.Should().BeEmpty();

        (await reader.Read(socket, cancellation.Token)).Should().Be(WebSocketReadStatus.Close);
        ((int)socket.CloseStatus!).Should().Be(SharedBackendSocketAuth.RejectedCloseCode);
        await world.EntryPoint.DidNotReceive().OnConnected(Arg.Any<IUserSession>(), Arg.Any<IReadOnlyList<IProjectionPayload>>());
    }

    // A client before S3 put the user id into the query: it is ignored, an empty first frame is a new guest.
    [Fact]
    public async Task QueryUserId_IsIgnored_EmptyFrameIsNewGuest()
    {
        var world = new World();
        var queryUserId = Guid.NewGuid();
        var created = new ResolvedUser { Id = Guid.NewGuid(), Projections = [], IssuedSecret = "issued" };
        world.Users.CreateGuest().Returns(created);

        var connected = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);

        world.EntryPoint.OnConnected(Arg.Any<IUserSession>(), Arg.Any<IReadOnlyList<IProjectionPayload>>())
             .Returns(call => {
                 connected.TrySetResult(call.Arg<IUserSession>().UserId);
                 return Task.CompletedTask;
             });

        await using var host = await world.Start();
        using var socket = await Connect(host, $"&{"userId"}={queryUserId}");

        await Send(socket, new SharedBackendSocketAuth.Request());

        using var cancellation = new CancellationTokenSource(Wait);
        var response = await ReadResponse(socket, new WebSocketMessageReader(), cancellation.Token);

        response.Status.Should().Be(SharedBackendSocketAuth.AuthStatus.Ok);
        response.UserId.Should().Be(created.Id);
        response.IssuedSecret.Should().Be("issued");
        (await connected.Task.WaitAsync(Wait)).Should().Be(created.Id);
        await world.Users.DidNotReceive().ResolveGuest(Arg.Any<Guid>(), Arg.Any<string>());

        socket.Abort();
    }

    private sealed class World
    {
        public IUserFactory Users { get; } = Substitute.For<IUserFactory>();
        public IUserConnectionEntryPoint EntryPoint { get; } = Substitute.For<IUserConnectionEntryPoint>();

        public async Task<TestWebHost> Start()
        {
            var participant = Substitute.For<IClusterParticipantContext>();
            participant.IsInitialized.Returns(new ViewableProperty<bool>(true));

            var features = Substitute.For<IClusterFeatures>();
            features.IsAcceptingConnections.Returns(true);

            var builder = TestWebHost.CreateBuilder("Production");
            builder.Services.AddSingleton(participant);
            builder.Services.AddSingleton(features);
            builder.Services.AddSingleton(Users);
            builder.Services.AddSingleton(EntryPoint);
            builder.Services.AddSingleton<MetaSocketAuth>();

            var app = builder.Build();
            app.UseWebSockets();
            app.UseMiddleware<BackendConnectionMiddleware>();

            return await TestWebHost.Start(app);
        }
    }

    private static async Task<SharedBackendSocketAuth.Response> ReadResponse(
        ClientWebSocket socket,
        WebSocketMessageReader reader,
        CancellationToken cancellation)
    {
        (await reader.Read(socket, cancellation)).Should().Be(WebSocketReadStatus.Message);

        var message = MemoryPackSerializer.Deserialize<IMessageFromServer>(reader.Payload.Span);
        var response = message.Should().BeOfType<ResponseMessageFromServer>().Subject;
        response.RequestId.Should().Be(1);

        return response.Context.Should().BeOfType<SharedBackendSocketAuth.Response>().Subject;
    }

    private static async Task<ClientWebSocket> Connect(TestWebHost host, string extraQuery)
    {
        var socket = new ClientWebSocket();
        var uri = new UriBuilder(host.Address)
        {
            Scheme = "ws",
            Path = "/",
            Query = $"{ProtocolVersion.QueryKey}={ProtocolVersion.Hash}{extraQuery}"
        }.Uri;

        await socket.ConnectAsync(uri, CancellationToken.None);
        return socket;
    }

    private static Task Send(ClientWebSocket socket, INetworkContext context)
    {
        IMessageFromClient message = new RequestMessageFromClient
        {
            Context = context,
            RequestId = 1
        };

        var bytes = MemoryPackSerializer.Serialize(message);
        return socket.SendAsync(bytes, WebSocketMessageType.Binary, true, CancellationToken.None);
    }
}
