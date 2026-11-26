using System.Diagnostics;
using Infrastructure;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Framework.Tests.Security;
using Game.ClientTelemetry;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetryReadEndpointTests : ClientTelemetryEndpointTest
{
    public ClientTelemetryReadEndpointTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    [Theory]
    [InlineData("/sessions")]
    [InlineData("/sessions/3f2b8c1e-7a4d-4e6f-9b10-2c5d8e7f6a01")]
    [InlineData("/sessions/3f2b8c1e-7a4d-4e6f-9b10-2c5d8e7f6a01/summary")]
    [InlineData("/sessions/3f2b8c1e-7a4d-4e6f-9b10-2c5d8e7f6a01/samples")]
    [InlineData("/sessions/3f2b8c1e-7a4d-4e6f-9b10-2c5d8e7f6a01/marks")]
    [InlineData("/sessions/3f2b8c1e-7a4d-4e6f-9b10-2c5d8e7f6a01/errors")]
    [InlineData("/errors")]
    public async Task WithoutTheToken_EveryRouteAnswers401(string path)
    {
        var host = await StartHost();

        var response = await host.Http.GetAsync($"{ClientTelemetryRoutes.Prefix}{path}", Cancel);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("/summary")]
    [InlineData("/samples")]
    [InlineData("/marks")]
    [InlineData("/errors")]
    public async Task UnknownSession_Answers404(string path)
    {
        var host = await StartHost();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClientTelemetryRoutes.Prefix}/sessions/{Guid.NewGuid()}{path}");
        request.Headers.Authorization = new("Bearer", TestToken);

        (await host.Http.SendAsync(request, Cancel)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/sessions?status=broken")]
    [InlineData("/sessions?limit=0")]
    [InlineData("/sessions?limit=100000")]
    [InlineData("/sessions?offset=-1")]
    [InlineData("/sessions?offset=abc")]
    [InlineData("/sessions?averageBelow=10")]
    [InlineData("/sessions?averageAbove=10")]
    [InlineData("/errors?limit=0")]
    [InlineData("/errors?limit=100000")]
    public async Task BadQueryOfTheList_Answers400(string path)
    {
        var host = await StartHost();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClientTelemetryRoutes.Prefix}{path}");
        request.Headers.Authorization = new("Bearer", TestToken);

        (await host.Http.SendAsync(request, Cancel)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("wait=31")]
    [InlineData("wait=-1")]
    [InlineData("limit=0")]
    [InlineData("after=-1")]
    public async Task BadQueryOfSamples_Answers400(string query)
    {
        var (host, session) = await StartWithSession();

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClientTelemetryRoutes.Prefix}/sessions/{session}/samples?{query}");
        request.Headers.Authorization = new("Bearer", TestToken);

        (await host.Http.SendAsync(request, Cancel)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sessions_AreFoundByTheSummary()
    {
        var (host, session) = await StartWithSession(ended: true);

        await TestWait.Until(() => Cluster.Collection.TryGetValue(session, out var known) && known.Closed);

        (await Ids(host, "/sessions?metric=frame.dt&averageBelow=100")).Should().Contain(session);
        (await Ids(host, "/sessions?metric=frame.dt&averageAbove=100")).Should().NotContain(session);
        (await Ids(host, "/sessions?metric=no.such")).Should().NotContain(session);
        (await Ids(host, "/sessions?minErrors=1")).Should().NotContain(session);
    }

    [Fact]
    public async Task Sessions_AreFilteredAndNamedByStatusWord()
    {
        var (host, session) = await StartWithSession(ended: true);

        var ended = await Get(host, "/sessions?status=ended");
        var live = await Get(host, "/sessions?status=live");

        ended.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().Contain(session);
        ended.EnumerateArray().Should().OnlyContain(item => item.GetProperty("status").GetString() == "ended");
        live.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).Should().NotContain(session);

        var one = await Get(host, $"/sessions/{session}");
        one.GetProperty("id").GetGuid().Should().Be(session);
        one.GetProperty("names").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Samples_GoWithTheNamesOfMetrics_AndPagesByTheSeq()
    {
        var (host, session) = await StartWithSession();

        var first = await Get(host, $"/sessions/{session}/samples");

        first.GetProperty("status").GetString().Should().Be("live");
        first.GetProperty("lastSeq").GetInt64().Should().Be(1);

        var items = first.GetProperty("items");
        items.GetArrayLength().Should().Be(3);
        items[0].GetProperty("time").GetInt64().Should().Be(1000);
        items[0].GetProperty("hidden").GetBoolean().Should().BeFalse();
        items[0].GetProperty("values").GetProperty("frame.dt").GetDouble().Should().Be(10);
        items[1].GetProperty("hidden").GetBoolean().Should().BeTrue();

        // A null value stays out, not a name with a hole.
        items[2].GetProperty("values").TryGetProperty("memory.heap", out _).Should().BeFalse();

        var next = await Get(host, $"/sessions/{session}/samples?after=1");
        next.GetProperty("items").GetArrayLength().Should().Be(0);
        next.GetProperty("lastSeq").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task LongPoll_ReturnsWhenDataAppears()
    {
        var (host, session) = await StartWithSession();
        var clock = Stopwatch.StartNew();

        var poll = Get(host, $"/sessions/{session}/samples?after=1&wait=30");
        await Task.Delay(300, Cancel);
        poll.IsCompleted.Should().BeFalse();

        await Store(Hidden(session, 2, samples: [(4000, [40, 400], false)]));

        var page = await poll;

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
        page.GetProperty("lastSeq").GetInt64().Should().Be(2);
        page.GetProperty("items")[0].GetProperty("values").GetProperty("frame.dt").GetDouble().Should().Be(40);
    }

    [Fact]
    public async Task LongPoll_EndsWithTheCloseOfTheSession()
    {
        var (host, session) = await StartWithSession();
        var clock = Stopwatch.StartNew();

        var poll = Get(host, $"/sessions/{session}/samples?after=1&wait=30");
        await Task.Delay(300, Cancel);

        await Store(Hidden(session, 2, ended: true));

        var page = await poll;

        clock.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(15));
        page.GetProperty("items").GetArrayLength().Should().Be(0);
        page.GetProperty("status").GetString().Should().Be("ended");
    }

    [Fact]
    public async Task LongPoll_DoesNotAskTheDatabase()
    {
        var (host, session) = await StartWithSession();

        await Cluster.PumpSideEffects();
        await TestWait.Until(() => Cluster.Collection.ContainsKey(session));

        var reads = Cluster.Storage.ReadsOf(session);

        (await Get(host, $"/sessions/{session}/samples?after=1&wait=2")).GetProperty("items").GetArrayLength().Should().Be(0);

        Cluster.Storage.ReadsOf(session).Should().Be(reads);
    }

    [Fact]
    public async Task LongPoll_ByTheTimeoutIsEmpty()
    {
        var (host, session) = await StartWithSession();
        var clock = Stopwatch.StartNew();

        var page = await Get(host, $"/sessions/{session}/samples?after=1&wait=1");

        clock.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900));
        page.GetProperty("items").GetArrayLength().Should().Be(0);
        page.GetProperty("lastSeq").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task LongPoll_StopsAskingTheReaderWhenTheRequestEnds()
    {
        var polls = 0;
        var reader = Substitute.For<IClientTelemetryReader>();
        var session = Guid.NewGuid();

        reader.GetSession(session).Returns(new TelemetrySessionInfo
        {
            Id = session,
            Status = TelemetrySessionStatus.Live,
            Device = new TelemetryDevice(),
            StartedAt = DateTime.UnixEpoch,
            LastSampleAt = null,
            LastSeq = 0,
            Names = [],
            NameSlots = 0
        });

        reader.GetSamples(session, Arg.Any<long>(), Arg.Any<int>()).Returns(_ => {
            Interlocked.Increment(ref polls);
            return Task.FromResult<IReadOnlyList<TelemetryStoredSample>>([]);
        });

        var host = await StartHost(configure: builder => builder.Services.AddSingleton(reader));

        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ClientTelemetryRoutes.Prefix}/sessions/{session}/samples?wait=30");
        request.Headers.Authorization = new("Bearer", TestToken);

        var send = host.Http.SendAsync(request, cancel.Token);
        await TestWait.Until(() => Volatile.Read(ref polls) >= 2);

        await cancel.CancelAsync();
        var aborted = () => send;
        await aborted.Should().ThrowAsync<OperationCanceledException>();

        // A poll already on its way may still finish, then only a loop that kept going asks again: a few of its steps of 500 ms.
        await Task.Delay(300, Cancel);
        var after = Volatile.Read(ref polls);
        await Task.Delay(1500, Cancel);

        Volatile.Read(ref polls).Should().Be(after);
    }

    [Fact]
    public async Task LongPoll_SeesARowOfAnotherGateway()
    {
        var (_, session) = await StartWithSession();

        // Another gateway with collections of its own: only the queue of their updates tells it what the grain stored.
        var other = await StartHost(ownCollections: true);
        var collection = other.Services.GetRequiredService<ITelemetrySessionCollection>();
        collection.Should().NotBeSameAs(Cluster.Collection);
        await TestWait.Until(async () => {
            await Cluster.PumpSideEffects();
            return collection.ContainsKey(session);
        });

        var poll = Get(other, $"/sessions/{session}/samples?after=1&wait=30");
        await Store(Hidden(session, 2, samples: [(4000, [40, 400], false)]));
        await Cluster.PumpSideEffects();

        (await poll).GetProperty("lastSeq").GetInt64().Should().Be(2);
    }

    [Fact]
    public async Task Summary_LeavesTheHiddenOut_AndGoesOverTheWindow()
    {
        var (host, session) = await StartWithSession();

        var whole = await Get(host, $"/sessions/{session}/summary");
        whole.GetProperty("samples").GetInt32().Should().Be(2);
        whole.GetProperty("hiddenSamples").GetInt32().Should().Be(1);

        var frame = Metric(whole, "frame.dt");
        frame.GetProperty("count").GetInt32().Should().Be(2);
        frame.GetProperty("average").GetDouble().Should().Be(20);
        frame.GetProperty("max").GetDouble().Should().Be(30);
        frame.GetProperty("growth").ValueKind.Should().Be(JsonValueKind.Null);

        var memory = Metric(whole, "memory.heap");
        memory.GetProperty("count").GetInt32().Should().Be(1);

        var window = await Get(host, $"/sessions/{session}/summary?from=2500&to=3500");
        Metric(window, "frame.dt").GetProperty("max").GetDouble().Should().Be(30);
        Metric(window, "frame.dt").GetProperty("count").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task MarksAndErrors_AreRead()
    {
        EnableFlag(true);
        var host = await StartHost();
        var session = Guid.NewGuid();

        await Store(Request(session, 1, marks: [(6, "stage.start")], errors: [TelemetryWire.Error(5, SharedClientTelemetry.ErrorLevel.Exception, "Boom", "A:B ()", 2)]));
        await TestWait.Until(async () => await Reader.GetSession(session) != null);

        var errors = await Get(host, $"/sessions/{session}/errors");
        var error = errors.GetProperty("items")[0];
        error.GetProperty("message").GetString().Should().Be("Boom");
        error.GetProperty("level").GetString().Should().Be("exception");
        error.GetProperty("repeat").GetInt32().Should().Be(2);

        var marks = await Get(host, $"/sessions/{session}/marks");
        marks.GetProperty("items")[0].GetProperty("name").GetString().Should().Be("stage.start");
        marks.GetProperty("lastSeq").GetInt64().Should().Be(1);
    }

    [Fact]
    public async Task TheDetailAndTheDurationOfAMark_AreRead_NullWithoutThem()
    {
        EnableFlag(true);
        var host = await StartHost();
        var session = Guid.NewGuid();
        var batch = TelemetryWire.Batch(session);
        batch.Marks =
        [
            new SharedClientTelemetry.Mark { Time = 6, Name = "load.end", Detail = "Audio: MusicRunAudio", DurationMs = 1200 },
            new SharedClientTelemetry.Mark { Time = 7, Name = "stage.start" }
        ];

        await Store(Step(session, 1, false, batch, "web", null));
        await TestWait.Until(async () => await Reader.GetSession(session) != null);

        var items = (await Get(host, $"/sessions/{session}/marks")).GetProperty("items");
        items[0].GetProperty("detail").GetString().Should().Be("Audio: MusicRunAudio");
        items[0].GetProperty("durationMs").GetInt64().Should().Be(1200);
        items[1].GetProperty("detail").ValueKind.Should().Be(JsonValueKind.Null);
        items[1].GetProperty("durationMs").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task TheTags_AreInTheAnswers_AndTagFiltersByAllOfThem()
    {
        EnableFlag(true);
        var host = await StartHost();
        var word = $"t{Guid.NewGuid():N}";
        var both = Guid.NewGuid();
        var one = Guid.NewGuid();

        await Store(Request(both, 1, tags: ["run", word]), Request(one, 1, tags: [word], ended: true));
        await TestWait.Until(async () => (await Ids(host, $"/sessions?tag={word}")).Length == 2);

        (await Ids(host, $"/sessions?tag={word}&tag=run")).Should().Equal(both);
        (await Ids(host, $"/sessions?tag={word}x")).Should().BeEmpty();

        var live = await Get(host, $"/sessions/{both}");
        live.GetProperty("status").GetString().Should().Be("live");
        live.GetProperty("tags").EnumerateArray().Select(tag => tag.GetString()).Should().Equal("run", word);
        live.GetProperty("ackSeq").GetInt64().Should().Be(1, "the skill starts a watch from this field");

        var listed = (await Get(host, $"/sessions?tag={word}")).EnumerateArray().Single(item => item.GetProperty("id").GetGuid() == one);
        listed.GetProperty("tags").EnumerateArray().Single().GetString().Should().Be(word);
    }

    // The skill reads the previous session of a client in both ways: from the new one, and from the lost one by previousId.
    [Fact]
    public async Task ThePreviousSession_IsInTheAnswers_AndPreviousIdFindsTheNextOne()
    {
        EnableFlag(true);
        var host = await StartHost();
        var lost = Guid.NewGuid();
        var next = Guid.NewGuid();
        var samples = new[] { new TelemetryPreviousSample(30_000, true, new Dictionary<string, double> { ["frame.dt.max"] = 41.5 }) };
        var previous = new TelemetryPreviousSession(lost, ["menu"], 31_000, null, [new TelemetryMark(30_500, "page.pagehide")]) { Samples = samples };

        await Cluster.Orleans.GetGrain<IClientTelemetrySession>(next).Start(new TelemetryDevice(), new TelemetrySessionTags([], 0), null, previous);
        await Store(Request(lost, 1, errors: [TelemetryWire.Error(5, SharedClientTelemetry.ErrorLevel.Warning, "Slow", null, 3)], ended: true));
        await TestWait.Until(async () => (await Ids(host, $"/sessions?previousId={lost}")).Length == 1);

        (await Ids(host, $"/sessions?previousId={lost}")).Should().Equal(next);

        var card = (await Get(host, $"/sessions/{next}")).GetProperty("previous");
        card.GetProperty("sessionId").GetGuid().Should().Be(lost);
        card.GetProperty("lastSampleTime").GetInt64().Should().Be(31_000);
        card.GetProperty("sinceMs").ValueKind.Should().Be(JsonValueKind.Null);
        card.GetProperty("marks")[0].GetProperty("name").GetString().Should().Be("page.pagehide");
        var sample = card.GetProperty("samples").EnumerateArray().Single();
        sample.GetProperty("time").GetInt64().Should().Be(30_000);
        sample.GetProperty("hidden").GetBoolean().Should().BeTrue();
        sample.GetProperty("values").EnumerateObject().Select(value => (value.Name, value.Value.GetDouble())).Should().Equal(("frame.dt.max", 41.5));
        (await Get(host, $"/sessions/{lost}")).GetProperty("previous").ValueKind.Should().Be(JsonValueKind.Null);

        var without = Guid.NewGuid();
        await Cluster.Orleans.GetGrain<IClientTelemetrySession>(without).Start(new TelemetryDevice(), new TelemetrySessionTags([], 0), null,
            new TelemetryPreviousSession(lost, [], null, null, []));
        (await Get(host, $"/sessions/{without}")).GetProperty("previous").GetProperty("samples").GetArrayLength().Should().Be(0);

        var digest = (await Get(host, $"/sessions/{lost}")).GetProperty("digest");
        digest.GetProperty("errorCount").GetInt64().Should().Be(0);
        digest.GetProperty("warningCount").GetInt64().Should().Be(3);
        (await Get(host, $"/sessions/{lost}/errors")).GetProperty("items")[0].GetProperty("level").GetString().Should().Be("warning");
    }

    private async Task<(TestWebHost Host, Guid Session)> StartWithSession(bool ended = false)
    {
        EnableFlag(true);
        var host = await StartHost();
        var session = Guid.NewGuid();

        await Store(Hidden(session, 1, ["frame.dt", "memory.heap"],
            [(1000, [10, 100], false), (2000, [500, 900], true), (3000, [30, null], false)],
            ended));
        await TestWait.Until(async () => await Reader.GetSession(session) != null);

        return (host, session);
    }

    private static async Task<Guid[]> Ids(TestWebHost host, string path)
    {
        return (await Get(host, path)).EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToArray();
    }

    private static JsonElement Metric(JsonElement summary, string name)
    {
        return summary.GetProperty("metrics").EnumerateArray().Single(metric => metric.GetProperty("name").GetString() == name);
    }
}
