using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using MemoryPack;
using System.Text.Json;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// End to end on a real Kestrel and a real websocket: MemoryPack, the union of the protocol, the connection classes, the reading.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetrySocketTests : ClientTelemetryEndpointTest
{
    public ClientTelemetrySocketTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    [Fact]
    public async Task ASessionOverTheSocket_IsReadWhileLive_AndAfterTheCloseOfTheSocket()
    {
        EnableFlag(true);
        var host = await StartHost();
        var client = await TelemetryMetaClient.Connect(host.Address);

        var started = await client.Request<SharedClientTelemetry.SessionStarted>(new SharedClientTelemetry.SessionStart
        {
            Device = new SharedClientTelemetry.Device { Platform = "web", Cores = 8 },
            Tags = ["menu"]
        });
        started.Enabled.Should().BeTrue();

        var result = await client.Request<SharedClientTelemetry.BatchResult>(TelemetryWire.Batch(started.SessionId, ["frame.dt"], [(1000, [16.5], false)], marks: [(1000, "stage.start")]));
        result.Enabled.Should().BeTrue();

        var live = await Get(host, $"/sessions/{started.SessionId}/samples");
        live.GetProperty("status").GetString().Should().Be("live");
        live.GetProperty("items")[0].GetProperty("values").GetProperty("frame.dt").GetDouble().Should().Be(16.5);

        await client.DisposeAsync();

        await TestWait.Until(async () => (await Reader.GetSession(started.SessionId))?.Status == TelemetrySessionStatus.Lost);

        var closed = await Get(host, $"/sessions/{started.SessionId}");
        closed.GetProperty("device").GetProperty("cores").GetInt32().Should().Be(8);
        closed.GetProperty("tags")[0].GetString().Should().Be("menu");
        closed.GetProperty("digest").GetProperty("summary").GetProperty("samples").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task FlagOff_OverTheSocket_AnswersOffAndStoresNothing()
    {
        EnableFlag(false);
        var host = await StartHost();
        await using var client = await TelemetryMetaClient.Connect(host.Address);

        var started = await client.Request<SharedClientTelemetry.SessionStarted>(new SharedClientTelemetry.SessionStart());

        started.Enabled.Should().BeFalse();
        (await client.Request<SharedClientTelemetry.BatchResult>(TelemetryWire.Batch(Guid.NewGuid()))).Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task AStartupTraceOverTheSocket_IsReadWithItsSessions_AfterTheCloseOfTheSocket()
    {
        EnableFlag(true);
        var host = await StartHost();
        var client = await TelemetryMetaClient.Connect(host.Address);

        var snapshot = await client.Request<SharedClientTelemetry.StartupTraceResult>(StartupTrace(Guid.Empty, final: false, durationMs: 50));
        snapshot.Enabled.Should().BeTrue();

        var answer = await Get(host, $"/startups/{snapshot.StartupId}");
        answer.GetProperty("startup").GetProperty("final").GetBoolean().Should().BeFalse();

        var final = await client.Request<SharedClientTelemetry.StartupTraceResult>(StartupTrace(snapshot.StartupId, final: true, durationMs: 200));
        final.StartupId.Should().Be(snapshot.StartupId);

        var session = await client.Request<SharedClientTelemetry.SessionStarted>(new SharedClientTelemetry.SessionStart { StartupId = snapshot.StartupId, Tags = ["menu"] });

        await client.DisposeAsync();
        await TestWait.Until(async () => (await Reader.GetSession(session.SessionId))?.Status == TelemetrySessionStatus.Lost);

        var page = await Get(host, $"/startups/{snapshot.StartupId}");
        var startup = page.GetProperty("startup");
        startup.GetProperty("final").GetBoolean().Should().BeTrue();
        startup.GetProperty("durationMs").GetDouble().Should().Be(200);
        startup.GetProperty("frames").GetInt32().Should().Be(7);
        startup.GetProperty("spanCount").GetInt32().Should().Be(2);
        startup.GetProperty("depth").GetInt32().Should().Be(2);
        startup.GetProperty("device").GetProperty("platform").GetString().Should().Be("web");
        startup.GetProperty("receivedAt").GetString().Should().NotBeNullOrEmpty();

        var spans = page.GetProperty("spans");
        spans[0].GetProperty("name").GetString().Should().Be("Scope: Boot");
        spans[0].GetProperty("ownMs").GetDouble().Should().Be(150);
        spans[0].GetProperty("file").GetString().Should().Be("Build.wasm");
        spans[0].GetProperty("transferBytes").GetInt64().Should().Be(4096);
        spans[0].GetProperty("cached").GetBoolean().Should().BeTrue();
        spans[1].GetProperty("file").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        spans[1].GetProperty("name").GetString().Should().Be("GET <url>");
        spans[1].GetProperty("parentIndex").GetInt32().Should().Be(0);
        spans[1].GetProperty("depth").GetInt32().Should().Be(1);
        spans[1].GetProperty("unfinished").GetBoolean().Should().BeTrue();

        page.GetProperty("sessions")[0].GetProperty("id").GetGuid().Should().Be(session.SessionId);

        var card = await Get(host, $"/sessions/{session.SessionId}");
        card.GetProperty("startupId").GetGuid().Should().Be(snapshot.StartupId);

        var listed = await Get(host, "/startups?limit=1000");
        listed.EnumerateArray().Select(row => row.GetProperty("id").GetGuid()).Should().Contain(snapshot.StartupId);

        var bySession = await Get(host, $"/sessions?startupId={snapshot.StartupId}");
        bySession.GetArrayLength().Should().Be(1);
        bySession[0].GetProperty("startupId").GetGuid().Should().Be(snapshot.StartupId);
    }

    [Fact]
    public async Task ASessionWithoutAStartup_ReadsAnExplicitNull()
    {
        EnableFlag(true);
        var host = await StartHost();
        await using var client = await TelemetryMetaClient.Connect(host.Address);

        var session = await client.Request<SharedClientTelemetry.SessionStarted>(new SharedClientTelemetry.SessionStart { Tags = ["menu"] });

        var card = await Get(host, $"/sessions/{session.SessionId}");
        card.GetProperty("startupId").ValueKind.Should().Be(JsonValueKind.Null);

        var row = await Get(host, "/sessions?tag=menu&status=live&limit=1");
        row[0].GetProperty("startupId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task AStartupTrace_UnderTheFlagOff_AnswersOff()
    {
        EnableFlag(false);
        var host = await StartHost();
        await using var client = await TelemetryMetaClient.Connect(host.Address);

        var answer = await client.Request<SharedClientTelemetry.StartupTraceResult>(StartupTrace(Guid.Empty, final: true, durationMs: 1));

        answer.Enabled.Should().BeFalse();
        answer.StartupId.Should().BeEmpty();
    }

    [Fact]
    public void TheStartupTrace_GoesThroughMemoryPack_AndATypicalOneFitsTheLimit()
    {
        var spans = Enumerable.Range(0, 365)
                              .Select(index => new SharedClientTelemetry.StartupSpan { ParentIndex = index - 1, Name = "Sprites: Game_UIIconsSprites", StartMs = index * 1.5, DurationMs = 12.25, Frames = 1, AllocBytes = 1024 })
                              .ToArray();
        var trace = new SharedClientTelemetry.StartupTrace { Name = "Boot", Final = true, DurationMs = 54337.09, Frames = 3033, Spans = spans, Device = new SharedClientTelemetry.Device { Platform = "web" } };

        var bytes = MemoryPackSerializer.Serialize<INetworkContext>(trace);
        var read = (SharedClientTelemetry.StartupTrace)MemoryPackSerializer.Deserialize<INetworkContext>(bytes)!;

        bytes[0].Should().Be((byte)NetworkContextTags.TelemetryStartupTrace);
        read.Spans[364].Name.Should().Be(spans[364].Name);
        read.Spans[364].ParentIndex.Should().Be(363);
        read.Spans[364].StartMs.Should().Be(spans[364].StartMs);
        read.Frames.Should().Be(3033);
        read.Final.Should().BeTrue();

        // Measured over the real sizes: 365 spans take about 30 KB of the limit.
        bytes.Length.Should().BeLessThan(ClientTelemetryLimits.MaxStartupBytes / 4);
    }

    private static SharedClientTelemetry.StartupTrace StartupTrace(Guid id, bool final, double durationMs)
    {
        return new SharedClientTelemetry.StartupTrace
        {
            StartupId = id,
            Device = new SharedClientTelemetry.Device { Platform = "web" },
            Name = "Boot",
            Final = final,
            DurationMs = durationMs,
            Frames = 7,
            Spans =
            [
                new SharedClientTelemetry.StartupSpan { ParentIndex = -1, Name = "Scope: Boot", StartMs = 0, DurationMs = durationMs, File = "Build.wasm", TransferBytes = 4096, Cached = true },
                new SharedClientTelemetry.StartupSpan { ParentIndex = 0, Name = "GET https://api.example.com/users/1?token=abc", StartMs = 0, DurationMs = durationMs - 150 > 0 ? 50 : durationMs, Unfinished = true }
            ]
        };
    }

    [Fact]
    public void TheMessages_GoThroughMemoryPack_AndABatchIsCompact()
    {
        var names = Enumerable.Range(0, 30).Select(index => $"metric.m{index}").ToArray();
        var sample = Enumerable.Repeat<double?>(12.5, 30).ToArray();
        var first = TelemetryWire.Batch(Guid.NewGuid(), names, [(1000, sample, false), (2000, sample, false)]);
        var next = TelemetryWire.Batch(Guid.NewGuid(), samples: [(3000, sample, false), (4000, sample, false)]);

        var bytes = MemoryPackSerializer.Serialize<INetworkContext>(next);
        var read = (SharedClientTelemetry.Batch)MemoryPackSerializer.Deserialize<INetworkContext>(bytes)!;

        read.SessionId.Should().Be(next.SessionId);
        read.Samples![1].Values.Should().Equal(next.Samples![1].Values);
        bytes[0].Should().Be((byte)NetworkContextTags.TelemetryBatch, "a tag of the framework is one byte on the wire");

        // Measured: 30 metrics and 2 samples, every 2 seconds. The names go once, in the first batch of a session.
        bytes.Length.Should().BeLessThan(600);
        MemoryPackSerializer.Serialize<INetworkContext>(first).Length.Should().BeLessThan(1500);
    }
}
