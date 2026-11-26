using System.Diagnostics;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Infrastructure.State;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The startup trace of one socket over the real grain and database, without a socket: its lifetime is enough.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetryStartupTests : ClientTelemetryStorageTest
{
    public ClientTelemetryStartupTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    private readonly IClientTelemetrySwitch _switch = Substitute.For<IClientTelemetrySwitch>();

    private ClientTelemetryConnection Open(bool flag = true)
    {
        _switch.IsEnabled.Returns(flag);

        return new ClientTelemetryConnections(_switch, Cluster.Orleans, NullLogger<ClientTelemetryConnection>.Instance).Of(TestLifetime.Child());
    }

    private static SharedClientTelemetry.StartupTrace Trace(bool final = false, Guid startupId = default, int spans = 1)
    {
        return new SharedClientTelemetry.StartupTrace
        {
            StartupId = startupId,
            Device = new SharedClientTelemetry.Device { Platform = "web", BuildVersion = "1.0" },
            Name = "Boot",
            Final = final,
            DurationMs = 100,
            Frames = 3,
            Spans = Enumerable.Range(0, spans)
                              .Select(index => new SharedClientTelemetry.StartupSpan { ParentIndex = index - 1, Name = $"Stage {index}", DurationMs = 10 })
                              .ToArray()
        };
    }

    private async Task<TelemetryStartupInfo> Info(Guid id)
    {
        return await Reader.GetStartup(id) ?? throw new InvalidOperationException($"No startup {id}");
    }

    [Fact]
    public async Task ASnapshot_IsStored_WithTheTimeOfTheServer_AndWithoutASession()
    {
        var connection = Open();

        var answer = await connection.AcceptStartup(Trace(spans: 3), default);

        answer.Enabled.Should().BeTrue();
        answer.StartupId.Should().NotBeEmpty();

        var info = await Info(answer.StartupId);
        info.Final.Should().BeFalse();
        info.Name.Should().Be("Boot");
        info.Device.Platform.Should().Be("web");
        info.ReceivedAt.Should().Be(Cluster.Clock.GetUtcNow().UtcDateTime);
        info.DurationMs.Should().Be(100);
        info.Frames.Should().Be(3);
        info.SpanCount.Should().Be(3);
        info.Depth.Should().Be(3);
        (await Reader.GetStartupSpans(answer.StartupId)).Should().HaveCount(3);
    }

    [Fact]
    public async Task TheFinalTrace_ReplacesTheSnapshotOfTheSameRecord()
    {
        var connection = Open();
        var first = (await connection.AcceptStartup(Trace(spans: 2), default)).StartupId;
        var receivedAt = (await Info(first)).ReceivedAt;

        Cluster.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await connection.AcceptStartup(Trace(final: true, startupId: first, spans: 5), default);

        second.StartupId.Should().Be(first);

        var info = await Info(first);
        info.Final.Should().BeTrue();
        info.SpanCount.Should().Be(5);
        info.ReceivedAt.Should().Be(receivedAt);
        info.UpdatedAt.Should().BeAfter(receivedAt);
        (await Reader.GetStartupSpans(first)).Should().HaveCount(5);
    }

    [Fact]
    public async Task TheFinalTrace_StaysAgainstALateSnapshot()
    {
        var connection = Open();
        var id = (await connection.AcceptStartup(Trace(final: true, spans: 5), default)).StartupId;

        (await connection.AcceptStartup(Trace(startupId: id, spans: 2), default)).Enabled.Should().BeTrue();

        (await Info(id)).SpanCount.Should().Be(5);
    }

    [Fact]
    public async Task TheBackendTrace_IsStored_AndAFinalAfterTheSnapshotKeepsIt()
    {
        var connection = Open();
        var backendTrace = ActivityTraceId.CreateRandom();
        var id = (await connection.AcceptStartup(Trace(spans: 2), backendTrace)).StartupId;

        (await Info(id)).BackendTraceId.Should().Be(backendTrace.ToHexString());

        await connection.AcceptStartup(Trace(final: true, startupId: id, spans: 5), backendTrace);

        var info = await Info(id);
        info.Final.Should().BeTrue();
        info.BackendTraceId.Should().Be(backendTrace.ToHexString());
    }

    [Fact]
    public async Task NoBackendTrace_IsStoredAsNull()
    {
        var id = (await Open().AcceptStartup(Trace(), default)).StartupId;

        (await Info(id)).BackendTraceId.Should().BeNull();
    }

    [Fact]
    public async Task AnIdOfAnotherSocket_IsNotAccepted_AndItsRecordStaysAsItIs()
    {
        var owner = Open();
        var id = (await owner.AcceptStartup(Trace(spans: 2), default)).StartupId;
        var stranger = Open();

        var answer = await stranger.AcceptStartup(Trace(final: true, startupId: id, spans: 5), default);

        answer.Enabled.Should().BeFalse();
        answer.StartupId.Should().BeEmpty();

        var info = await Info(id);
        info.Final.Should().BeFalse();
        info.SpanCount.Should().Be(2);
    }

    [Fact]
    public async Task AnIdThatNobodyGave_IsNotAccepted_AndCreatesNothing()
    {
        var id = Guid.NewGuid();

        (await Open().AcceptStartup(Trace(startupId: id), default)).Enabled.Should().BeFalse();

        (await Reader.GetStartup(id)).Should().BeNull();
    }

    [Fact]
    public async Task TheFlagOff_AnswersOff_AndWritesNothing()
    {
        var before = Cluster.Startups.Count;

        var answer = await Open(flag: false).AcceptStartup(Trace(), default);

        answer.Enabled.Should().BeFalse();
        answer.StartupId.Should().BeEmpty();
        Cluster.Startups.Count.Should().Be(before);
    }

    [Fact]
    public async Task OverTheRecordsOfASocket_NothingNewIsCreated()
    {
        var connection = Open();

        for (var i = 0; i < ClientTelemetryLimits.MaxStartupsPerSocket; i++)
            (await connection.AcceptStartup(Trace(), default)).Enabled.Should().BeTrue();

        (await connection.AcceptStartup(Trace(), default)).Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task OverTheWritesOfASocket_TheRecordStopsChanging()
    {
        var connection = Open();
        var id = (await connection.AcceptStartup(Trace(), default)).StartupId;

        for (var i = 1; i < ClientTelemetryLimits.MaxStartupWritesPerSocket; i++)
            (await connection.AcceptStartup(Trace(startupId: id), default)).Enabled.Should().BeTrue();

        (await connection.AcceptStartup(Trace(final: true, startupId: id), default)).Enabled.Should().BeFalse();
        (await Info(id)).Final.Should().BeFalse();
    }

    [Fact]
    public async Task ASession_RefersToTheRecordOfItsSocket_AndIsFoundByIt()
    {
        var connection = Open();
        var startup = (await connection.AcceptStartup(Trace(), default)).StartupId;

        var session = (await connection.Start(new SharedClientTelemetry.SessionStart { StartupId = startup })).SessionId;
        var without = (await Open().Start(new SharedClientTelemetry.SessionStart())).SessionId;

        (await Session(session)).StartupId.Should().Be(startup);
        (await Session(without)).StartupId.Should().BeNull();

        var found = await Reader.GetSessions(new TelemetrySessionFilter { StartupId = startup });
        found.Select(item => item.Id).Should().Equal(session);
    }

    [Fact]
    public async Task ASession_WithAnIdOfAnotherSocket_DoesNotKeepIt_AndCountsIt()
    {
        var foreign = (await Open().AcceptStartup(Trace(), default)).StartupId;
        var connection = Open();

        var id = (await connection.Start(new SharedClientTelemetry.SessionStart { StartupId = foreign })).SessionId;

        (await Session(id)).StartupId.Should().BeNull();

        await connection.End(new SharedClientTelemetry.SessionEnd { SessionId = id });

        (await Session(id)).Digest!.RejectedRequests.Should().Be(1);
    }

    [Fact]
    public async Task TheEndOfTheSocket_ChangesNothing_AndTheRecordIsRead()
    {
        var lifetime = new Lifetime(TestLifetime);
        _switch.IsEnabled.Returns(true);
        var connection = new ClientTelemetryConnections(_switch, Cluster.Orleans, NullLogger<ClientTelemetryConnection>.Instance).Of(lifetime);
        var id = (await connection.AcceptStartup(Trace(spans: 2), default)).StartupId;

        lifetime.Terminate();

        var info = await Info(id);
        info.Final.Should().BeFalse();
        (await Reader.GetStartupSpans(id)).Should().HaveCount(2);
    }

    [Fact]
    public async Task AWriteFailure_AnswersOff_AndThrowsNothing()
    {
        var connection = Open();
        Cluster.Storage.FailWrites(true);

        (await connection.AcceptStartup(Trace(), default)).Enabled.Should().BeFalse();
    }

    [Fact]
    public async Task TheSweep_DeletesAnExpiredRecordOnly()
    {
        var connection = Open();
        var expired = (await connection.AcceptStartup(Trace(), default)).StartupId;

        Cluster.Clock.Advance(TimeSpan.FromDays(Cluster.Options.RetentionDays + 1));

        var fresh = (await connection.AcceptStartup(Trace(), default)).StartupId;

        await Cluster.Sweep.Run();

        await Cluster.PumpSideEffects();
        await TestWait.Until(() => !Cluster.Startups.ContainsKey(expired));

        (await Reader.GetStartup(expired)).Should().BeNull();
        (await Reader.GetStartupSpans(expired)).Should().BeEmpty();
        (await Reader.GetStartup(fresh)).Should().NotBeNull();
        (await Scalar($"SELECT count(*) FROM state_client_telemetry_startup_traces WHERE key = '{expired}'")).Should().Be(0);
    }
}
