using System.Diagnostics;
using System.Net.WebSockets;
using Cluster.State;
using Common.Network;
using Internal;
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
        await world.EntryPoint.DidNotReceive().OnConnected(Arg.Any<IUserSession>());
    }

    // A client before S3 put the user id into the query: it is ignored, an empty first frame is a new guest.
    [Fact]
    public async Task QueryUserId_IsIgnored_EmptyFrameIsNewGuest()
    {
        var world = new World();
        var queryUserId = Guid.NewGuid();
        var created = new ResolvedUser { Id = Guid.NewGuid(), IssuedSecret = "issued" };
        world.Users.CreateGuest().Returns(created);

        var connected = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);

        world.EntryPoint.OnConnected(Arg.Any<IUserSession>())
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

    // The root span "Meta connect" is a trace of its own with a child per phase; its trace id is the one of the session.
    [Fact]
    public async Task NewGuest_TracesMetaConnect_SessionKeepsItsTraceId()
    {
        var stopped = new List<Activity>();

        // Read before the listener: the source created inside the type initializer would call ShouldListenTo with the
        // field still null.
        var sourceName = MetaConnectionTelemetry.Source.Name;

        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == sourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => {
                lock (stopped)
                    stopped.Add(activity);
            }
        };

        ActivitySource.AddActivityListener(listener);

        var world = new World();
        var created = new ResolvedUser { Id = Guid.NewGuid(), IssuedSecret = "issued" };
        world.Users.CreateGuest().Returns(created);

        var session = new TaskCompletionSource<IUserSession>(TaskCreationOptions.RunContinuationsAsynchronously);

        world.EntryPoint.OnConnected(Arg.Any<IUserSession>())
             .Returns(call => {
                 session.TrySetResult(call.Arg<IUserSession>());
                 return Task.CompletedTask;
             });

        await using var host = await world.Start();
        using var socket = await Connect(host, string.Empty);

        await Send(socket, new SharedBackendSocketAuth.Request());

        using var cancellation = new CancellationTokenSource(Wait);
        (await ReadResponse(socket, new WebSocketMessageReader(), cancellation.Token)).Status.Should().Be(SharedBackendSocketAuth.AuthStatus.Ok);
        var traceId = (await session.Task.WaitAsync(Wait, TestContext.Current.CancellationToken)).TraceId;

        Activity root;

        while (true)
        {
            lock (stopped)
            {
                var found = stopped.FirstOrDefault(activity => activity.DisplayName == "Meta connect");

                if (found != null)
                {
                    root = found;
                    break;
                }
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        root.Kind.Should().Be(ActivityKind.Server);
        root.ParentSpanId.Should().Be(default(ActivitySpanId));
        root.Status.Should().Be(ActivityStatusCode.Unset);
        traceId.Should().Be(root.TraceId);

        List<Activity> children;

        lock (stopped)
            children = stopped.Where(activity => activity.ParentSpanId == root.SpanId).ToList();

        children.Select(activity => activity.DisplayName).Should().Equal(
            "Meta read request", "Meta auth", "Meta send response", "Meta projections");

        var auth = children.Single(activity => activity.DisplayName == "Meta auth");
        auth.GetTagItem("meta.auth.guest").Should().Be("new");
        auth.GetTagItem("meta.auth.status").Should().Be(nameof(SharedBackendSocketAuth.AuthStatus.Ok));

        socket.Abort();
    }

    private sealed class World
    {
        public IUserFactory Users { get; } = Substitute.For<IUserFactory>();
        public IUserConnectionEntryPoint EntryPoint { get; } = Substitute.For<IUserConnectionEntryPoint>();

        public async Task<TestWebHost> Start()
        {
            var participant = Substitute.For<IClusterParticipantContext>();
            participant.IsInitialized.Returns(new SharedViewableProperty<bool>(true));

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
            Query = $"{ProtocolVersion.QueryKey}={ProtocolHash.Value}{extraQuery}"
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
