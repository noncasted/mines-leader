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

namespace Framework.Tests.Security;

// Browser origins of meta / game come from configuration (CORS and the WebSocket handshake);
// Development adds loopback, production needs an explicit list or the explicit "no browser origins" value.
public class AllowedOriginsTests
{
    private const string ProductionOrigins = "https://game.example, https://web.telegram.org";

    [Theory]
    [InlineData("https://game.example", true)]
    [InlineData("https://GAME.example", true)]
    [InlineData("https://game.example:443", true)]
    [InlineData("https://web.telegram.org", true)]
    [InlineData("http://game.example", false)]
    [InlineData("https://evil.io", false)]
    [InlineData("https://game.example.evil.io", false)]
    [InlineData("https://game.example:8443", false)]
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
        var origins = Read("Development", "https://game.example");

        origins.IsAllowed("https://game.example").Should().BeTrue();
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
    [InlineData("https://game.example/tg/")]
    [InlineData("https://game.example?x=1")]
    [InlineData("https://game.example#x")]
    [InlineData("https://user@game.example")]
    [InlineData("https://user:secret@game.example")]
    [InlineData("game.example")]
    [InlineData("*.itch.zone")]
    [InlineData("wss://game.example")]
    [InlineData("None")]
    public void NotAnOrigin_FailsStartup(string origin)
    {
        var read = () => Read("Production", origin);

        read.Should().Throw<InvalidOperationException>().WithMessage($"*{origin}*");
    }

    [Theory]
    [InlineData("Production", "https://evil.io")]
    [InlineData("Production", "http://localhost:5000")]
    [InlineData("Production", "https://localhost")]
    [InlineData("Production", "null")]
    [InlineData("Staging", "http://127.0.0.1:8080")]
    public void NoBrowserOrigins_OutsideDevelopment_NothingAllowed(string environment, string origin)
    {
        var origins = Read(environment, $" {AllowedOrigins.NoBrowserOrigins} ");

        origins.Origins.Should().BeEmpty();
        origins.IsAllowed(origin).Should().BeFalse();
    }

    [Theory]
    [InlineData("http://localhost:5000", true)]
    [InlineData("http://127.0.0.1:8080", true)]
    [InlineData("https://evil.io", false)]
    public void NoBrowserOrigins_Development_LoopbackOnly(string origin, bool allowed)
    {
        var origins = Read("Development", AllowedOrigins.NoBrowserOrigins);

        origins.IsAllowed(origin).Should().Be(allowed);
    }

    [Theory]
    [InlineData("none, https://game.example")]
    [InlineData("https://game.example,none")]
    public void NoBrowserOrigins_WithOrigins_FailsStartup(string list)
    {
        var read = () => Read("Production", list);

        read.Should().Throw<InvalidOperationException>().WithMessage($"*{AllowedOrigins.NoBrowserOrigins}*");
    }

    [Fact]
    public async Task Cors_AllowedOrigin_GetsTheHeader()
    {
        await using var host = await Start();

        var response = await Send(host, HttpMethod.Get, "https://game.example");

        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("https://game.example");
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

        var allowed = await Send(host, HttpMethod.Options, "https://web.telegram.org", preflight: true);
        var foreign = await Send(host, HttpMethod.Options, "https://evil.io", preflight: true);

        allowed.Headers.GetValues("Access-Control-Allow-Origin").Should().Equal("https://web.telegram.org");
        foreign.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Theory]
    [InlineData("https://game.example")]
    [InlineData("https://web.telegram.org")]
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

    [Fact]
    public async Task NoBrowserOrigins_NativeClientPasses_BrowserIsRejected()
    {
        await using var host = await Start(AllowedOrigins.NoBrowserOrigins);

        var native = await host.Http.GetAsync("/ping", TestContext.Current.CancellationToken);
        var browser = await Send(host, HttpMethod.Get, "https://game.example");
        var preflight = await Send(host, HttpMethod.Options, "https://game.example", preflight: true);

        native.IsSuccessStatusCode.Should().BeTrue();
        (await native.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Be("pong");
        browser.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
        preflight.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();

        using var socket = await Connect(host, null);
        socket.State.Should().Be(WebSocketState.Open);

        var browserSocket = () => Connect(host, "https://game.example");
        (await browserSocket.Should().ThrowAsync<WebSocketException>()).Which.Message.Should().Contain("403");
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
    private static async Task<TestWebHost> Start(string origins = ProductionOrigins)
    {
        var builder = TestWebHost.CreateBuilder("Production", (AllowedOrigins.ConfigurationKey, origins));
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
