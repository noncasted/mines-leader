using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Security;
using Game.MatchMetrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Framework.Tests.MatchMetrics;

// A real Kestrel with the endpoints over the real tables; the token filter stands for RequireConsoleToken of the game.
public class MatchMetricsReadEndpointTests : IClassFixture<MatchMetricsDatabase>, IAsyncLifetime
{
    public MatchMetricsReadEndpointTests(MatchMetricsDatabase database)
    {
        _database = database;
    }

    private const string _token = "secret";

    private readonly MatchMetricsDatabase _database;
    private readonly MatchMetricsReadSeed _seed = new();
    private TestWebHost _host = null!;

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _seed.Write(_database);

        var builder = TestWebHost.CreateBuilder("Production");
        builder.Services.AddSingleton<IDbSource>(_database);
        builder.AddMatchMetricsReader();

        var app = builder.Build();
        app.UseRouting();
        app.MapMatchMetricsRead().AddEndpointFilter(RequireToken);

        _host = await TestWebHost.Start(app);
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    private async Task<HttpResponseMessage> Send(string path, bool withToken = true)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{MatchMetricsRoutes.Prefix}{path}");

        if (withToken)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);

        return await _host.Http.SendAsync(request, Cancel);
    }

    private async Task<JsonElement> Get(string path)
    {
        using var response = await Send(path);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Cancel));

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(Cancel)).RootElement.Clone();
    }

    [Theory]
    [InlineData("/series")]
    [InlineData("/totals")]
    [InlineData("/matches/3f2b8c1e-7a4d-4e6f-9b10-2c5d8e7f6a01/streams")]
    [InlineData("/matches/3f2b8c1e-7a4d-4e6f-9b10-2c5d8e7f6a01/series")]
    [InlineData("/matches/3f2b8c1e-7a4d-4e6f-9b10-2c5d8e7f6a01/totals")]
    [InlineData("/matches/3f2b8c1e-7a4d-4e6f-9b10-2c5d8e7f6a01/events")]
    public async Task WithoutTheToken_EveryRouteAnswers401(string path)
    {
        (await Send(path, false)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("series?step=5")]
    [InlineData("series?axis=wall")]
    [InlineData("series?name=Bad%20Name")]
    [InlineData("series?groupBy=source,source")]
    [InlineData("series?tag.source=Shot")]
    [InlineData("events?user=nobody")]
    public async Task BadQuery_Answers400(string path)
    {
        (await Send($"/matches/{_seed.Match}/{path}")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Streams_have_the_user_kind_and_last_seq()
    {
        var streams = await Get($"/matches/{_seed.Match}/streams");

        streams.GetArrayLength().Should().Be(2);
        streams[0].GetProperty("kind").GetString().Should().Be("client");
        streams.EnumerateArray().Select(stream => stream.GetProperty("lastSeq").GetInt32()).Should().BeEquivalentTo([1, 2]);
    }

    [Fact]
    public async Task Series_of_a_match_group_by_tags_with_a_step()
    {
        var groups = await Get($"/matches/{_seed.Match}/series?name=damage.dealt&tag.source=shot&groupBy=user,source&step=10");

        groups.GetArrayLength().Should().Be(2);
        var b = groups.EnumerateArray().Single(group => group.GetProperty("group").GetProperty("user").GetString() == _seed.B.ToString());
        b.GetProperty("name").GetString().Should().Be("damage.dealt");
        b.GetProperty("kind").GetString().Should().Be("counter");
        b.GetProperty("group").GetProperty("source").GetString().Should().Be("shot");
        b.GetProperty("points")[0].GetProperty("time").GetInt64().Should().Be(0);
        b.GetProperty("points")[0].GetProperty("value").GetDouble().Should().Be(2);
    }

    [Fact]
    public async Task Totals_of_a_match_and_across_matches()
    {
        var match = await Get($"/matches/{_seed.Match}/totals?name=damage.dealt&groupBy=card");
        match[1].GetProperty("group").GetProperty("card").GetString().Should().Be("lightning");
        match[1].GetProperty("sum").GetDouble().Should().Be(10);

        var since = Uri.EscapeDataString(_database.Time.GetUtcNow().UtcDateTime.AddHours(-1).ToString("O"));
        var across = await Get($"/totals?name=damage.dealt&user={_seed.A}&since={since}");
        across.EnumerateArray().Single().GetProperty("sum").GetDouble().Should().Be(77);
        across.EnumerateArray().Single().GetProperty("streams").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Events_and_the_dictionary()
    {
        var events = await Get($"/matches/{_seed.Match}/events?user={_seed.A}");
        events.EnumerateArray().Single().GetProperty("name").GetString().Should().Be("upgrade.pick");
        events.EnumerateArray().Single().GetProperty("fields").GetProperty("card").GetDouble().Should().Be(12);
        events.EnumerateArray().Single().GetProperty("time").GetSingle().Should().Be(2.5f);

        var series = await Get("/series?name=hearts");
        series.EnumerateArray().Single().GetProperty("kind").GetString().Should().Be("gauge");
    }

    private static async ValueTask<object?> RequireToken(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        if (context.HttpContext.Request.Headers.Authorization.ToString() != $"Bearer {_token}")
            return Results.Unauthorized();

        return await next(context);
    }
}
