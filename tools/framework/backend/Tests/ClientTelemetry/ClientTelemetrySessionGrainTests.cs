using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Infrastructure;
using Orleans.Core.Internal;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetrySessionGrainTests : ClientTelemetryStorageTest
{
    private const string _sessions = "state_client_telemetry_sessions";
    private const string _logs = "state_client_telemetry_logs";

    public ClientTelemetrySessionGrainTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    [Fact]
    public async Task ACommonSession_WritesExactlyTwice_AtTheStartAndAtTheClose()
    {
        var id = Guid.NewGuid();

        await Store(Request(id, 1, ["frame.dt"], [(1000, [10])]));
        Cluster.Storage.WritesOf(id).Should().Be(1);
        (await RawState(_sessions, id)).Should().NotBeNull();
        (await RawState(_logs, id)).Should().BeNull();

        for (var seq = 2; seq < 20; seq++)
            await Store(Request(id, seq, samples: [(seq * 1000, [seq])]));

        Cluster.Storage.WritesOf(id).Should().Be(1);

        await Store(Request(id, 20, samples: [(20000, [20])], ended: true));

        Cluster.Storage.WritesOf(id).Should().Be(2);
        (await RawState(_logs, id)).Should().NotBeNull();

        var session = await Session(id);
        session.Status.Should().Be(TelemetrySessionStatus.Ended);
        session.Digest!.DurationMs.Should().Be(20000);
        session.Digest.Summary.Samples.Should().Be(20);
        (await Reader.GetSamples(id, 0, 1000)).Should().HaveCount(20);
    }

    [Fact]
    public async Task LiveReading_DoesNotTouchTheDatabase()
    {
        var id = Guid.NewGuid();
        await Store(Request(id, 1, ["frame.dt"], [(1000, [10])], [(1000, "stage.start")]));

        var readsBefore = Cluster.Storage.ReadsOf(id);
        var writesBefore = Cluster.Storage.WritesOf(id);

        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Live);
        (await Session(id)).Names.Single().Name.Should().Be("frame.dt");
        (await Reader.GetSamples(id, 0, 100)).Single().Values.Should().Equal(10);
        (await Reader.GetMarks(id, 0, 100)).Single().Name.Should().Be("stage.start");
        (await Reader.GetErrors(id, 0, 100)).Should().BeEmpty();

        Cluster.Storage.ReadsOf(id).Should().Be(readsBefore);
        Cluster.Storage.WritesOf(id).Should().Be(writesBefore);
    }

    [Fact]
    public async Task ABrokenClient_IsClosedByTheTimerAsLost_ThroughTheDeactivationToo()
    {
        var id = Guid.NewGuid();
        await Store(Request(id, 1, ["frame.dt"], [(1000, [10])]));

        Cluster.Clock.Advance(Cluster.Options.LostAfter - TimeSpan.FromSeconds(1));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        (await RawState(_logs, id)).Should().BeNull("a request came in time");

        Cluster.Clock.Advance(TimeSpan.FromSeconds(2));

        await TestWait.Until(async () => await RawState(_logs, id) != null);
        await TestWait.Until(() => Cluster.Logs.Lines.Any(line => line.Contains($"Session {id} deactivated")));

        var session = await Session(id);
        session.Status.Should().Be(TelemetrySessionStatus.Lost);
        session.ClosedAt.Should().NotBeNull();
        session.Digest!.Summary.Metrics.Single().Average.Should().Be(10);
        (await Reader.GetSamples(id, 0, 100)).Should().HaveCount(1);
        Cluster.Storage.WritesOf(id).Should().Be(2);
    }

    [Fact]
    public async Task TheTimer_RepeatsAFailedClose()
    {
        var id = Guid.NewGuid();
        await Store(Request(id, 1, ["a"], [(1000, [1])]));

        Cluster.Storage.FailWrites(true);
        Cluster.Clock.Advance(Cluster.Options.LostAfter + TimeSpan.FromSeconds(1));
        await TestWait.Until(() => Cluster.Storage.Failures >= 2);

        Cluster.Storage.FailWrites(false);
        await TestWait.Until(async () => await RawState(_logs, id) != null);
    }

    [Fact]
    public async Task AFailedClose_Throws_AndTheRetryCloses()
    {
        var id = Guid.NewGuid();
        var grain = Cluster.Orleans.GetGrain<IClientTelemetrySession>(id);
        await Store(Request(id, 1, ["a"], [(1000, [1])]), Request(id, 2, samples: [(2000, [2])]));

        Cluster.Storage.FailWrites(true);
        var failed = () => grain.Close(TelemetrySessionStatus.Ended);
        await failed.Should().ThrowAsync<Exception>();
        Cluster.Storage.FailWrites(false);

        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Live, "nothing is stored, the memory is the truth");
        await grain.Close(TelemetrySessionStatus.Ended);

        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Ended);
        (await Reader.GetSamples(id, 0, 10)).Should().HaveCount(2);
    }

    [Fact]
    public async Task ADeactivation_ClosesALiveSessionAsLost()
    {
        var id = Guid.NewGuid();
        await Store(Request(id, 1, ["a"], [(1000, [1])]));

        await Cluster.Orleans.GetGrain<IClientTelemetrySession>(id).Cast<IGrainManagementExtension>().DeactivateOnIdle();

        await TestWait.Until(async () => await RawState(_logs, id) != null);
        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Lost);
    }

    [Fact]
    public async Task AStopOfTheSilo_ClosesALiveSession()
    {
        var cluster = new ClientTelemetryClusterFixture();
        await cluster.InitializeAsync();

        var id = Guid.NewGuid();
        await cluster.Orleans.GetGrain<IClientTelemetrySession>(id).Start(new TelemetryDevice(), new TelemetrySessionTags([], 0), null, null);

        await cluster.Cluster.StopSiloAsync(cluster.Cluster.Silos[0]);

        await using var connection = await cluster.Database.DataSource.OpenConnectionAsync();
        await using var command = new Npgsql.NpgsqlCommand($"SELECT count(*) FROM {_logs} WHERE key = '{id}'", connection);
        (await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)).Should().Be(1L);

        await cluster.Database.DisposeAsync();
    }

    [Fact]
    public async Task NewNamesAndSlots_KeepTheirPlaces()
    {
        var id = Guid.NewGuid();

        await Store(Request(id, 1, ["a", "Bad Name", "b"], [(1, [1, 2, 3])]));
        await Store(Request(id, 2, ["a", "c"], [(2, [1, 2, 3, 4, 5])]));

        var session = await Session(id);
        session.NameSlots.Should().Be(5);
        session.LastSeq.Should().Be(2);
        session.Names.Select(name => (name.Slot, name.Name)).Should().Equal((0, "a"), (2, "b"), (4, "c"));
    }

    [Fact]
    public async Task ABatchOverTheSessionLimits_IsCounted_AndKeepsItsSlots()
    {
        var id = Guid.NewGuid();

        await Store(Request(id, 1, ["a"]));
        await Store(Request(id, 2, ["b"], [(1, [1, 2, 3])]));
        await Store(Request(id, 3, samples: [(2, [1, 2])], ended: true));

        var session = await Session(id);
        session.Digest!.RejectedRequests.Should().Be(1);
        session.Names.Select(name => name.Name).Should().Equal("a", "b");
        (await Reader.GetSamples(id, 0, 10)).Single().Time.Should().Be(2);
    }

    [Fact]
    public async Task AnOverflowOfTheBuffer_DropsTheOldest_AndCountsThem()
    {
        var id = Guid.NewGuid();
        var limit = Cluster.Options.MaxSamples;
        var perRequest = ClientTelemetryLimits.MaxSamplesPerBatch;
        var requests = limit / perRequest + 1;
        var over = requests * perRequest - limit;

        await Store(Request(id, 1, ["a"],
            Enumerable.Range(0, perRequest).Select(index => ((long)index, new double?[] { 1 })).ToArray(),
            Enumerable.Range(0, Cluster.Options.MaxMarks + 1).Select(index => ((long)index, $"m{index}")).ToArray(),
            errors: Enumerable.Range(0, Cluster.Options.MaxErrors + 1).Select(index => TelemetryWire.Error(index, SharedClientTelemetry.ErrorLevel.Error, $"e{index}")).ToArray()));

        for (var seq = 2; seq <= requests; seq++)
        {
            var from = (seq - 1) * perRequest;
            await Store(Request(id, seq, samples: Enumerable.Range(from, perRequest).Select(time => ((long)time, new double?[] { 1 })).ToArray()));
        }

        var live = await Reader.GetSamples(id, 0, limit + 1);
        live.Should().HaveCount(limit);
        live[0].Time.Should().Be(over);
        (await Reader.GetErrors(id, 0, 100)).First().Message.Should().Be("e1");
        (await Reader.GetMarks(id, 0, 100)).First().Name.Should().Be("m1");

        await Store(Request(id, requests + 1, ended: true));

        var digest = (await Session(id)).Digest!;
        digest.DroppedSamples.Should().Be(over);
        digest.DroppedErrors.Should().Be(1);
        digest.DroppedMarks.Should().Be(1);
        digest.Summary.Samples.Should().Be(limit);
        (await Reader.GetSamples(id, 0, limit + 1)).Should().HaveCount(limit);
        Cluster.Storage.WritesOf(id).Should().Be(2);
    }

    [Fact]
    public async Task ASessionLeftOpenByACrash_IsClosedByTheNextActivationWithAnEmptyLog_AndItsClientStops()
    {
        var id = Guid.NewGuid();
        await WriteOpenRow(id);

        (await Cluster.Orleans.GetGrain<IClientTelemetrySession>(id).Accept(new TelemetryBatch())).Should().BeFalse();

        var session = await Session(id);
        session.Status.Should().Be(TelemetrySessionStatus.Lost);
        session.Digest.Should().BeNull();
        (await RawState(_logs, id)).Should().BeNull();
        (await Reader.GetSamples(id, 0, 10)).Should().BeEmpty();
    }

    [Fact]
    public async Task TheSweep_ClosesWhatACrashLeftOpen()
    {
        var id = Guid.NewGuid();
        await WriteOpenRow(id);
        await Cluster.Collection.OnUpdated(id, await Cluster.Storage.Read<TelemetrySessionState>(Identity(id)));

        await Cluster.Sweep.Run();

        (await RawState(_sessions, id)).Should().Contain("\"Closed\": true");
        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Lost);
    }

    [Fact]
    public async Task TheSweep_DeletesBothStatesOfAnExpiredSession_AndOnlyThem()
    {
        var old = Guid.NewGuid();
        var live = Guid.NewGuid();

        await Store(Request(old, 1, ["a"], [(1, [1])], ended: true));
        Cluster.Clock.Advance(TimeSpan.FromDays(Cluster.Options.RetentionDays) - TimeSpan.FromHours(1));
        await Store(Request(live, 1, ["a"], [(1, [1])]));

        await Cluster.Sweep.Run();
        (await RawState(_logs, old)).Should().NotBeNull("a day of the term is left");

        Cluster.Clock.Advance(TimeSpan.FromHours(2));
        await Store(Request(live, 2, samples: [(2, [2])]));
        await Cluster.Sweep.Run();

        (await RawState(_sessions, old)).Should().BeNull();
        (await RawState(_logs, old)).Should().BeNull();
        (await RawState(_sessions, live)).Should().NotBeNull();

        await Cluster.PumpSideEffects();
        await TestWait.Until(() => !Cluster.Collection.ContainsKey(old));
        (await Reader.GetSession(old)).Should().BeNull();
    }

    [Fact]
    public async Task TheLoopOfTheSweep_Runs()
    {
        var id = Guid.NewGuid();
        await WriteOpenRow(id);
        await Cluster.Collection.OnUpdated(id, await Cluster.Storage.Read<TelemetrySessionState>(Identity(id)));

        var sweep = new ClientTelemetrySweep(
            Cluster.Collection,
            Cluster.Startups,
            Cluster.Orleans,
            new ClientTelemetryStorageOptions { SweepFirstDelay = TimeSpan.Zero, SweepInterval = TimeSpan.FromHours(1) },
            new TelemetrySessionClock(Cluster.Clock),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<ClientTelemetrySweep>.Instance);

        var lifetime = new Internal.Lifetime();
        await sweep.OnLocalSetupCompleted(lifetime);

        await TestWait.Until(async () => (await RawState(_sessions, id))!.Contains("\"Closed\": true"));
        lifetime.Terminate();
    }

    [Fact]
    public async Task TheCollectionOfAnotherHost_HearsAboutTheSession()
    {
        var other = new TelemetrySessionCollection(
            new StateCollectionUtils<Guid, TelemetrySessionState>(
                Cluster.Storage.Registry,
                Cluster.Storage,
                Cluster.Messaging,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<StateCollectionUtils<Guid, TelemetrySessionState>>.Instance),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TelemetrySessionCollection>.Instance);

        var lifetime = new Internal.Lifetime();
        await other.OnLocalSetupCompleted(lifetime);

        var id = Guid.NewGuid();
        await Store(Request(id, 1, ["a"], [(1, [1])], platform: "itch"));
        await Cluster.PumpSideEffects();
        await TestWait.Until(() => other.ContainsKey(id));
        other[id].Closed.Should().BeFalse();

        await Store(Request(id, 2, ended: true));
        await Cluster.PumpSideEffects();
        await TestWait.Until(() => other[id].Closed);

        lifetime.Terminate();
    }

    [Fact]
    public async Task TheSearch_GoesOverTheSummary()
    {
        var slow = Guid.NewGuid();
        var fast = Guid.NewGuid();
        var live = Guid.NewGuid();
        var platform = $"search{Random.Shared.Next(100000)}";

        await Store(Request(slow, 1, ["frame.fps"], [(1, [20])], ended: true, platform: platform));
        await Store(Request(fast, 1, ["frame.fps"], [(1, [60])], ended: true, platform: platform));
        await Store(Request(live, 1, ["frame.fps"], [(1, [20])], platform: platform));

        var all = new TelemetrySessionFilter { Platform = platform };

        (await Reader.GetSessions(all)).Should().HaveCount(3);
        (await Reader.GetSessions(all with { Status = TelemetrySessionStatus.Live })).Single().Id.Should().Be(live);
        (await Reader.GetSessions(all with { Metric = "frame.fps", AverageBelow = 30 })).Single().Id.Should().Be(slow);
        (await Reader.GetSessions(all with { Metric = "frame.fps", AverageAbove = 30 })).Single().Id.Should().Be(fast);
        (await Reader.GetSessions(all with { Metric = "no.such" })).Should().BeEmpty();
        (await Reader.GetSessions(all with { MinErrors = 1 })).Should().BeEmpty();
    }

    [Fact]
    public async Task TheStates_KeepAnyValueOfAClient()
    {
        var id = Guid.NewGuid();

        var device = TelemetryDeviceParser.Parse(new SharedClientTelemetry.Device { Platform = "web", Gpu = "Adreno (TM) 640" });
        var batch = TelemetryWire.Batch(id, ["a"], [(1, [1e300], false), (2, [5e-324], false), (3, [null], false)], [TelemetryWire.Error(1, SharedClientTelemetry.ErrorLevel.Error, "bad\0text", "at\0A.B", 3)]);
        await Store(Step(id, 1, true, batch) with { Device = device });

        (await Session(id)).Status.Should().Be(TelemetrySessionStatus.Ended);
        (await Session(id)).Device.Gpu.Should().Be("Adreno (TM) 640");
        (await Session(id)).Digest!.ErrorCount.Should().Be(3);

        var samples = await Reader.GetSamples(id, 0, 10);
        samples.Select(sample => sample.Values[0]).Should().Equal(1e300, 5e-324, null);
        (await Reader.GetErrors(id, 0, 10)).Single().Message.Should().Be("bad<text>text", "a jsonb string cannot hold a zero character: the scrubber takes it out");
    }

    [Fact]
    public async Task TheLogOfAnHourWith30Metrics_HasAKnownSize()
    {
        var id = Guid.NewGuid();
        var random = new Random(1);
        var names = Enumerable.Range(0, 30).Select(index => index % 5 == 0 ? $"memory.m{index}" : $"metric.m{index}").ToArray();

        for (var seq = 1; seq <= 1800; seq++)
        {
            var samples = Enumerable.Range(0, 2)
                .Select(index => ((long)(seq * 2000 + index * 1000), names.Select(name => Metric(random, name)).ToArray()))
                .ToArray();

            await Store(Request(id, seq, seq == 1 ? names : null, samples, ended: seq == 1800));
        }

        var bytes = await Scalar($"SELECT octet_length(value::text) FROM {_logs} WHERE key = '{id}'");
        var stored = await Scalar($"SELECT pg_column_size(value) FROM {_logs} WHERE key = '{id}'");
        var row = await Scalar($"SELECT octet_length(value::text) FROM {_sessions} WHERE key = '{id}'");

        // Measured: 1 110 KB of text, 664 KB in the table (TOAST compression), 12 KB of the metadata with the summary.
        bytes.Should().BeLessThan(1_300_000);
        stored.Should().BeLessThan(800_000);
        row.Should().BeLessThan(16_000);
    }

    [Fact]
    public void ARowWithoutTags_ReadsAsAnEmptyList()
    {
        var settings = Infrastructure.State.JsonStateSettings.CreateBase();
        var row = Newtonsoft.Json.JsonConvert.DeserializeObject<TelemetrySessionState>("""{ "IsStarted": true }""", settings)!;

        row.Tags.Should().BeEmpty();
    }

    private static double? Metric(Random random, string name)
    {
        return name.StartsWith("memory.") ? random.NextInt64(1L << 20, 1L << 31) : Math.Round(random.NextDouble() * 100, 3);
    }

    private async Task WriteOpenRow(Guid id)
    {
        var state = new TelemetrySessionState();
        state.Open(id, new TelemetryDevice { Platform = "web" }, [], null, null, Cluster.Clock.GetUtcNow().UtcDateTime);
        await Cluster.Storage.Write(new Infrastructure.State.StateWriteRequest
        {
            Records = new Dictionary<Infrastructure.State.StateIdentity, Infrastructure.State.IStateValue> { [Identity(id)] = state }
        });
    }

    private Infrastructure.State.StateIdentity Identity(Guid id)
    {
        var info = Cluster.Storage.Registry.Get<TelemetrySessionState>();
        return new Infrastructure.State.StateIdentity { Key = id, Type = info.Name, TableName = info.TableName, Extension = null };
    }
}
