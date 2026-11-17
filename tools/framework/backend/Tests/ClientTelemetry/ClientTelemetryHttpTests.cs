using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Security;
using Game.ClientTelemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// End to end on a real Kestrel: the JSON of AddHttpClientTelemetry (JsonUtility: no null, NaN as empty slots), the links as sockets,
// the group of the game with its own filter.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetryHttpTests : ClientTelemetryEndpointTest
{
    public ClientTelemetryHttpTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    private const string GameHeader = "X-Game";

    private Task<TestWebHost> StartHttpHost(TimeSpan? idleAfter = null)
    {
        return StartHost(
            builder =>
            {
                if (idleAfter is { } idle)
                    builder.Services.AddSingleton(new ClientTelemetryHttpOptions { IdleAfter = idle });
            },
            app => app.MapGroup("/api/telemetry").AddEndpointFilter(RequireGame).MapClientTelemetryHttpIntake());
    }

    // The authorization of the game on its group: the intake knows none.
    private static async ValueTask<object?> RequireGame(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.Request.Headers[GameHeader] != "signed")
            return Results.Unauthorized();

        return await next(context);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Post(TestWebHost host, string path, string json, bool signed = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/telemetry{path}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        if (signed)
            request.Headers.Add(GameHeader, "signed");

        using var response = await host.Http.SendAsync(request, Cancel);
        var text = await response.Content.ReadAsStringAsync(Cancel);
        var body = text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();

        return (response.StatusCode, body);
    }

    private static async Task<Guid> Start(TestWebHost host, Guid link, Guid startupId = default)
    {
        var json = $$"""
            {"linkId":"{{link}}","device":{"platform":"web","buildVersion":"","browser":"","browserMajor":0,"os":"","osMajor":0,"gpu":"",
             "cores":8,"memoryClass":0,"screenWidth":0,"screenHeight":0,"dpr":2},"tags":["menu"],"startupId":"{{startupId}}",
             "hasPrevious":false,"previous":{"sessionId":"{{Guid.Empty}}","tags":[],"lastSampleTime":0,"sinceMs":0,"marks":[],"sampleNames":[],"samples":[]} }
            """;
        var (status, body) = await Post(host, "/start", json);

        status.Should().Be(HttpStatusCode.OK);
        body.GetProperty("enabled").GetBoolean().Should().BeTrue();
        return body.GetProperty("sessionId").GetGuid();
    }

    private static string Batch(Guid link, Guid session, bool withNames = true)
    {
        var names = withNames ? """[{"name":"frame.dt","unit":"ms"},{"name":"gc.allocated","unit":""}]""" : "[]";

        return $$"""
            {"linkId":"{{link}}","sessionId":"{{session}}","names":{{names}},
             "samples":[{"time":1000,"values":[16.5,0],"empty":[1],"hidden":false}],
             "errors":[{"time":1000,"level":"Warning","message":"w","stack":"","repeat":1}],
             "marks":[{"time":1000,"name":"load.end","detail":"","hasDuration":true,"durationMs":250}]}
            """;
    }

    [Fact]
    public async Task ASessionOverHttp_NaNAsEmptySlots_IsReadAndEnds()
    {
        EnableFlag(true);
        var host = await StartHttpHost();
        var link = Guid.NewGuid();

        var session = await Start(host, link);
        var (status, body) = await Post(host, "/batch", Batch(link, session));
        status.Should().Be(HttpStatusCode.OK);
        body.GetProperty("enabled").GetBoolean().Should().BeTrue();

        var live = await Get(host, $"/sessions/{session}/samples");
        var values = live.GetProperty("items")[0].GetProperty("values");
        values.GetProperty("frame.dt").GetDouble().Should().Be(16.5);
        values.TryGetProperty("gc.allocated", out _).Should().BeFalse();

        var marks = await Get(host, $"/sessions/{session}/marks");
        marks.GetProperty("items")[0].GetProperty("durationMs").GetInt64().Should().Be(250);
        marks.GetProperty("items")[0].GetProperty("detail").ValueKind.Should().Be(JsonValueKind.Null);

        (await Post(host, "/end", $$"""{"linkId":"{{link}}","sessionId":"{{session}}"}""")).Status.Should().Be(HttpStatusCode.OK);

        await TestWait.Until(async () => (await Reader.GetSession(session))?.Status == TelemetrySessionStatus.Ended);
        var card = await Get(host, $"/sessions/{session}");
        card.GetProperty("device").GetProperty("cores").GetInt32().Should().Be(8);
        card.GetProperty("digest").GetProperty("warningCount").GetInt32().Should().Be(1);
    }

    // The silence of a link is the close of a socket.
    [Fact]
    public async Task ALinkSilentForIdleAfter_ClosesItsSessionAsLost()
    {
        EnableFlag(true);
        var host = await StartHttpHost(TimeSpan.FromMilliseconds(300));
        var link = Guid.NewGuid();

        var session = await Start(host, link);
        (await Post(host, "/batch", Batch(link, session))).Body.GetProperty("enabled").GetBoolean().Should().BeTrue();

        await TestWait.Until(async () => (await Reader.GetSession(session))?.Status == TelemetrySessionStatus.Lost);

        // The same id again is a new link: the session is not there.
        (await Post(host, "/batch", Batch(link, session, false))).Body.GetProperty("enabled").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ABatchOnAnotherLink_IsDropped_TheSessionGoesOn()
    {
        EnableFlag(true);
        var host = await StartHttpHost();
        var link = Guid.NewGuid();
        var session = await Start(host, link);

        (await Post(host, "/batch", Batch(Guid.NewGuid(), session))).Body.GetProperty("enabled").GetBoolean().Should().BeFalse();
        (await Post(host, "/batch", Batch(link, session))).Body.GetProperty("enabled").GetBoolean().Should().BeTrue();
        (await Reader.GetSession(session))!.Status.Should().Be(TelemetrySessionStatus.Live);
    }

    [Fact]
    public async Task AStartupTrace_CountsOnItsLinkOnly()
    {
        EnableFlag(true);
        var host = await StartHttpHost();
        var link = Guid.NewGuid();

        static string Trace(Guid link, Guid id, bool final) => $$"""
            {"linkId":"{{link}}","startupId":"{{id}}","device":{"platform":"web"},"name":"Boot","final":{{(final ? "true" : "false")}},
             "durationMs":200.5,"frames":7,"spans":[{"parentIndex":-1,"name":"Page","startMs":0,"durationMs":200.5,"frames":0,
             "unfinished":false,"allocBytes":0,"file":"","transferBytes":0,"cached":false}]}
            """;

        var snapshot = await Post(host, "/startup", Trace(link, Guid.Empty, false));
        snapshot.Body.GetProperty("enabled").GetBoolean().Should().BeTrue();
        var id = snapshot.Body.GetProperty("startupId").GetGuid();

        (await Post(host, "/startup", Trace(Guid.NewGuid(), id, true))).Body.GetProperty("enabled").GetBoolean().Should().BeFalse();
        (await Post(host, "/startup", Trace(link, id, true))).Body.GetProperty("enabled").GetBoolean().Should().BeTrue();

        var session = await Start(host, link, id);
        (await Get(host, $"/sessions/{session}")).GetProperty("startupId").GetGuid().Should().Be(id);

        var page = await Get(host, $"/startups/{id}");
        page.GetProperty("startup").GetProperty("final").GetBoolean().Should().BeTrue();
        page.GetProperty("spans")[0].GetProperty("file").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task TheFlagOff_AnswersOff()
    {
        EnableFlag(false);
        var host = await StartHttpHost();

        var (status, body) = await Post(host, "/start", $$"""{"linkId":"{{Guid.NewGuid()}}","tags":[]}""");

        status.Should().Be(HttpStatusCode.OK);
        body.GetProperty("enabled").GetBoolean().Should().BeFalse();
    }

    // JSON has no NaN: a client that writes it breaks the contract, nothing of the request is taken.
    [Fact]
    public async Task NaNInTheJson_IsBadRequest()
    {
        EnableFlag(true);
        var host = await StartHttpHost();
        var link = Guid.NewGuid();
        var session = await Start(host, link);

        var json = $$"""{"linkId":"{{link}}","sessionId":"{{session}}","samples":[{"time":1,"values":[NaN]}]}""";

        (await Post(host, "/batch", json)).Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task NoLink_IsBadRequest_AndTheGroupKeepsTheFilterOfTheGame()
    {
        EnableFlag(true);
        var host = await StartHttpHost();

        (await Post(host, "/start", """{"tags":["menu"]}""")).Status.Should().Be(HttpStatusCode.BadRequest);
        (await Post(host, "/start", $$"""{"linkId":"{{Guid.NewGuid()}}"}""", signed: false)).Status.Should().Be(HttpStatusCode.Unauthorized);
    }
}
