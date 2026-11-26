using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Game.Global;
using Game.Session;
using GameGateway;
using MetaGateway;
using MetaGateway.Matchmaking;
using MetaGateway.UserFlow;
using MetaGateway.UserFlow.Connection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Orchestration;
using Shared;
using Xunit;

namespace Tests.Security;

/// <summary>
/// Meta /api/monitor/* and game /api/sessions/* expose user and session ids, so they need the console token
/// (port of peace-data-reborn F1.3).
/// </summary>
public class InternalEndpointsTests
{
    private const string Token = "secret-token";

    public static TheoryData<string> Paths => new()
    {
        "/api/monitor/connected-users",
        "/api/monitor/matchmaking",
        "/api/sessions",
        "/api/sessions/{session}/players"
    };

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Production_WithoutToken_Unauthorized(string path)
    {
        await using var host = await Start("Production", Token);

        var response = await host.Get(path, null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await response.Content.ReadAsStringAsync()).Should().NotContain(host.SessionId.ToString());
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Production_WrongToken_Unauthorized(string path)
    {
        await using var host = await Start("Production", Token);

        var response = await host.Get(path, "secret-tokem");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Production_ConsoleToken_Ok(string path)
    {
        await using var host = await Start("Production", Token);

        var response = await host.Get(path, Token);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Production_ConsoleToken_ListsTheSession()
    {
        await using var host = await Start("Production", Token);

        var response = await host.Get("/api/sessions", Token);

        (await response.Content.ReadAsStringAsync()).Should().Contain(host.SessionId.ToString());
    }

    // The local cluster runs with the development token: the endpoints stay open for curl.
    [Theory]
    [MemberData(nameof(Paths))]
    public async Task Development_DevelopmentToken_OpenWithoutHeader(string path)
    {
        await using var host = await Start("Development", ConsoleAccess.DevelopmentToken);

        var response = await host.Get(path, null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<Host> Start(string environment, string token)
    {
        var sessionId = Guid.NewGuid();

        var session = Substitute.For<ISession>();
        session.Id.Returns(sessionId);
        session.Type.Returns(SessionType.Match);
        session.Users.Returns(new SessionUsers());

        var sessions = Substitute.For<ISessionsCollection>();
        sessions.Entries.Returns(new Dictionary<Guid, ISession> { [sessionId] = session });

        var users = Substitute.For<IConnectedUsers>();
        users.Entries.Returns(new Dictionary<Guid, IUserSession>());

        var matchmaking = Substitute.For<IMatchmaking>();
        matchmaking.GetStats().Returns(new MatchmakingStatsDto());

        var builder = TestWebHost.CreateBuilder(environment, (ConsoleAccess.TokenKey, token));
        builder.AddConsoleAccess();
        builder.Services.AddSingleton(users);
        builder.Services.AddSingleton(sessions);
        builder.Services.AddSingleton(matchmaking);

        var app = builder.Build();
        app.AddMonitorEndpoints();
        app.AddSessionEndpoints();

        return new Host(await TestWebHost.Start(app), sessionId);
    }

    private sealed class Host(TestWebHost web, Guid sessionId) : IAsyncDisposable
    {
        public Guid SessionId { get; } = sessionId;

        public Task<HttpResponseMessage> Get(string path, string? token)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path.Replace("{session}", SessionId.ToString()));

            if (token != null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            return web.Http.SendAsync(request);
        }

        public ValueTask DisposeAsync()
        {
            return web.DisposeAsync();
        }
    }
}
