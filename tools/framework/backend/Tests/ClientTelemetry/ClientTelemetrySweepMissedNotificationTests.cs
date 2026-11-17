using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// A host that missed a notification (its push failed after the row was written or deleted) learns the truth from the sweep it runs.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetrySweepMissedNotificationTests : ClientTelemetryStorageTest
{
    public ClientTelemetrySweepMissedNotificationTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    [Fact]
    public async Task AClosedSession_TheOtherHostMissed_IsClosedThereByItsSweep()
    {
        var (other, utils) = await OtherSessions();
        var id = Guid.NewGuid();
        await Store(Request(id, 1, ["a"], [(1, [1])]));
        await Cluster.PumpSideEffects();
        await TestWait.Until(() => other.ContainsKey(id));

        utils.Deafen();
        await Store(Request(id, 2, ended: true));
        await Cluster.PumpSideEffects();
        utils.Hear();
        other[id].Closed.Should().BeFalse("the other host missed the close");

        await Cluster.Orleans.GetGrain<IClientTelemetrySession>(id).Sweep();
        await Cluster.PumpSideEffects();

        await TestWait.Until(() => other[id].Closed);
    }

    [Fact]
    public async Task ADeletedSession_TheOtherHostMissed_LeavesItThroughItsSweep()
    {
        var (other, utils) = await OtherSessions();
        var id = Guid.NewGuid();
        await Store(Request(id, 1, ["a"], [(1, [1])], ended: true));
        await Cluster.PumpSideEffects();
        await TestWait.Until(() => other.ContainsKey(id));
        Cluster.Clock.Advance(TimeSpan.FromDays(Cluster.Options.RetentionDays + 1));
        var grain = Cluster.Orleans.GetGrain<IClientTelemetrySession>(id);

        utils.Deafen();
        await grain.Sweep();
        await Cluster.PumpSideEffects();
        utils.Hear();
        other.ContainsKey(id).Should().BeTrue("the other host missed the delete");

        await grain.Sweep();
        await Cluster.PumpSideEffects();

        await TestWait.Until(() => !other.ContainsKey(id));
    }

    [Fact]
    public async Task ADeletedStartup_TheOtherHostMissed_LeavesItThroughItsSweep()
    {
        var (other, utils) = await OtherStartups();
        var id = await StoreStartup();
        await Cluster.PumpSideEffects();
        await TestWait.Until(() => other.ContainsKey(id));
        Cluster.Clock.Advance(TimeSpan.FromDays(Cluster.Options.RetentionDays + 1));
        var grain = Cluster.Orleans.GetGrain<IClientTelemetryStartup>(id);

        utils.Deafen();
        await grain.Sweep();
        await Cluster.PumpSideEffects();
        utils.Hear();
        other.ContainsKey(id).Should().BeTrue("the other host missed the delete");

        await grain.Sweep();
        await Cluster.PumpSideEffects();

        await TestWait.Until(() => !other.ContainsKey(id));
    }

    [Fact]
    public async Task AStartupWrittenAgain_TheOtherHostMissed_IsRefreshedThereByItsSweep()
    {
        var (other, utils) = await OtherStartups();
        var connection = OpenSocket();
        var id = (await connection.AcceptStartup(Trace(final: false, startupId: Guid.Empty), default)).StartupId;
        await Cluster.PumpSideEffects();
        await TestWait.Until(() => other.ContainsKey(id));
        Cluster.Clock.Advance(TimeSpan.FromDays(Cluster.Options.RetentionDays + 1));

        utils.Deafen();
        await connection.AcceptStartup(Trace(final: true, startupId: id), default);
        await Cluster.PumpSideEffects();
        utils.Hear();
        other[id].Final.Should().BeFalse("the other host missed the write");

        await Cluster.Orleans.GetGrain<IClientTelemetryStartup>(id).Sweep();
        await Cluster.PumpSideEffects();

        await TestWait.Until(() => other[id].Final);
    }

    private async Task<(TelemetrySessionCollection, DeafStateCollectionUtils<Guid, TelemetrySessionState>)> OtherSessions()
    {
        var utils = new DeafStateCollectionUtils<Guid, TelemetrySessionState>(Cluster.Storage, Cluster.Messaging);
        var collection = new TelemetrySessionCollection(utils, NullLogger<TelemetrySessionCollection>.Instance);
        await collection.OnLocalSetupCompleted(TestLifetime);
        return (collection, utils);
    }

    private async Task<(TelemetryStartupCollection, DeafStateCollectionUtils<Guid, TelemetryStartupState>)> OtherStartups()
    {
        var utils = new DeafStateCollectionUtils<Guid, TelemetryStartupState>(Cluster.Storage, Cluster.Messaging);
        var collection = new TelemetryStartupCollection(utils, NullLogger<TelemetryStartupCollection>.Instance);
        await collection.OnLocalSetupCompleted(TestLifetime);
        return (collection, utils);
    }

    private async Task<Guid> StoreStartup()
    {
        return (await OpenSocket().AcceptStartup(Trace(final: true, startupId: Guid.Empty), default)).StartupId;
    }

    private ClientTelemetryConnection OpenSocket()
    {
        var flag = Substitute.For<IClientTelemetrySwitch>();
        flag.IsEnabled.Returns(true);

        return new ClientTelemetryConnections(flag, Cluster.Orleans, NullLogger<ClientTelemetryConnection>.Instance).Of(TestLifetime);
    }

    private static SharedClientTelemetry.StartupTrace Trace(bool final, Guid startupId)
    {
        return new SharedClientTelemetry.StartupTrace
        {
            StartupId = startupId,
            Device = new SharedClientTelemetry.Device { Platform = "web", BuildVersion = "1.0" },
            Name = "Boot",
            Final = final,
            DurationMs = 100,
            Frames = 3,
            Spans = [new SharedClientTelemetry.StartupSpan { ParentIndex = -1, Name = "Stage", DurationMs = 10 }]
        };
    }
}
