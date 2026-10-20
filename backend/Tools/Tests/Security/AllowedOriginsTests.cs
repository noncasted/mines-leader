using System.Net;
using System.Net.WebSockets;
using Common.Extensions;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Tests.Security;

/// <summary>
/// F1.5: browser origins of meta / game come from configuration (CORS and the WebSocket handshake);
/// Development adds loopback, production needs an explicit list.
/// </summary>
public class AllowedOriginsTests
{
    private const string ProductionOrigins = "https://minesleader.xyz, https://play.minesleader.xyz, https://html-classic.itch.zone";

    [Theory]
    [InlineData("https://minesleader.xyz", true)]
    [InlineData("https://MINESLEADER.xyz", true)]
    [InlineData("https://minesleader.xyz:443", true)]
    [InlineData("https://play.minesleader.xyz", true)]
    [InlineData("https://html-classic.itch.zone", true)]
    [InlineData("http://minesleader.xyz", false)]
    [InlineData("https://evil.minesleader.xyz", false)]
    [InlineData("https://evil.itch.zone", false)]
    [InlineData("https://evil.io", false)]
    [InlineData("https://minesleader.xyz.evil.io", false)]
    [InlineData("https://minesleader.xyz:8443", false)]
    [InlineData("http://localhost:5000", false)]
    [InlineData("http://127.0.0.1:8080", false)]
    [InlineData("null", false)]
    public void Production_ConfiguredOriginsOnly(string origin, bool allowed)
    {
        var origins = Read("Production", ProductionOrigins);

        origins.IsAllowed(origin).Should().Be(allowed);
    }

    [Theory]
    [InlineData("http://localhost:5000", true)]
    [InlineData("http://127.0.0.1:8080", true)]
    [InlineData("https://localhost", true)]
    [InlineData("https://evil.io", false)]
    [InlineData("null", false)]
    public void Development_NoList_LoopbackOnly(string origin, bool allowed)
    {
        var origins = Read("Development", "");

        origins.IsAllowed(origin).Should().Be(allowed);
    }

    [Fact]
    public void Development_List_IsAddedToLoopback()
    {
        var origins = Read("Development", "https://minesleader.xyz");

        origins.IsAllowed("https://minesleader.xyz").Should().BeTrue();
        origins.IsAllowed("http://localhost:5000").Should().BeTrue();
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void OutsideDevelopment_EmptyList_FailsStartup(string environment)
    {
        var read = () => Read(environment, " , ");

        read.Should().Throw<InvalidOperationException>().WithMessage($"*{AllowedOrigins.ConfigurationKey}*");
    }

    [Theory]
    [InlineData("https://minesleader.xyz/play/")]
    [InlineData("minesleader.xyz")]
    [InlineData("wss://minesleader.xyz")]
    public void NotAnOrigin_FailsStartup(string origin)
    {
        var read = () => Read("Production", origin);

        read.Should().Throw<InvalidOperationException>().WithMessage($"*{origin}*");
    }

    [Fact]
    public async Task Cors_AllowedOrigin_GetsTheHeader()
    {
        await using var host = await Start();

        var response = await Send(host, HttpMethod.Get, "https://minesleader.xyz");

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("https://minesleader.xyz");
        response.Headers.Contains("Access-Control-Allow-Credentials").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_ForeignOrigin_GetsNoHeader()
    {
        await using var host = await Start();

        var response = await Send(host, HttpMethod.Get, "https://evil.io");

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Cors_Preflight_ForeignOrigin_GetsNoHeader()
    {
        await using var host = await Start();

        var allowed = await Send(host, HttpMethod.Options, "https://html-classic.itch.zone", preflight: true);
        var foreign = await Send(host, HttpMethod.Options, "https://evil.io", preflight: true);

        allowed.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("https://html-classic.itch.zone");
        foreign.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Theory]
    [InlineData("https://minesleader.xyz")]
    [InlineData("https://html-classic.itch.zone")]
    [InlineData(null)]
    public async Task WebSocket_AllowedOrNoOrigin_Connects(string? origin)
    {
        await using var host = await Start();

        using var socket = await Connect(host, origin);

        socket.State.Should().Be(WebSocketState.Open);
    }

    [Theory]
    [InlineData("https://evil.io")]
    [InlineData("http://localhost:5000")]
    public async Task WebSocket_ForeignOrigin_Forbidden(string origin)
    {
        await using var host = await Start();

        var connect = () => Connect(host, origin);

        (await connect.Should().ThrowAsync<WebSocketException>()).Which.Message.Should().Contain("403");
    }

    private static AllowedOrigins Read(string environment, string list)
    {
        var configuration = new ConfigurationBuilder()
                            .AddInMemoryCollection([new KeyValuePair<string, string?>(AllowedOrigins.ConfigurationKey, list)])
                            .Build();

        var hostEnvironment = Substitute.For<IHostEnvironment>();
        hostEnvironment.EnvironmentName.Returns(environment);

        return AllowedOrigins.Read(configuration, hostEnvironment);
    }

    // The same pipeline head as GameGateway.AddMiddleware / BackendMiddlewareExtensions.AddBackendMiddleware.
    private static async Task<TestWebHost> Start()
    {
        var builder = TestWebHost.CreateBuilder("Production", (AllowedOrigins.ConfigurationKey, ProductionOrigins));
        builder.ConfigureCors();

        var app = builder.Build();
        app.UseAllowedOrigins();

        app.Use(async (HttpContext context, RequestDelegate next) => {
            if (context.WebSockets.IsWebSocketRequest == false)
            {
                await next(context);
                return;
            }

            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await socket.ReceiveAsync(new byte[16], CancellationToken.None);
        });

        app.UseRouting();
        app.MapGet("/ping", () => "pong");

        return await TestWebHost.Start(app);
    }

    private static Task<HttpResponseMessage> Send(TestWebHost host, HttpMethod method, string origin, bool preflight = false)
    {
        var request = new HttpRequestMessage(method, "/ping");
        request.Headers.Add("Origin", origin);

        if (preflight == true)
            request.Headers.Add("Access-Control-Request-Method", "GET");

        return host.Http.SendAsync(request);
    }

    private static async Task<ClientWebSocket> Connect(TestWebHost host, string? origin)
    {
        var socket = new ClientWebSocket();

        if (origin != null)
            socket.Options.SetRequestHeader("Origin", origin);

        var uri = new UriBuilder(host.Address) { Scheme = "ws", Path = "/" }.Uri;
        await socket.ConnectAsync(uri, CancellationToken.None);

        return socket;
    }
}
