using System.Net;
using Cluster.Deploy;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Game.ClientTelemetry;
using Infrastructure;
using Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Shared;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The switch over the real database and channel. A new switch is a new start of a host: it has nothing but the row. Two switches
// on one fixture are two hosts: the only link between them is the channel and the row.
[Collection(nameof(ClientTelemetryCollection))]
public class ClientTelemetrySwitchTests : ClientTelemetryEndpointTest
{
    public ClientTelemetrySwitchTests(ClientTelemetryClusterFixture cluster) : base(cluster)
    {
    }

    [Fact]
    public async Task NeverWritten_IsOffOnceLoaded()
    {
        await DeleteTheRow();

        var telemetry = Create();
        telemetry.IsLoaded.Should().BeFalse();

        await telemetry.OnOrleansStarted(TestLifetime);

        telemetry.IsLoaded.Should().BeTrue();
        telemetry.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Enabled_StaysOnAfterARestartAndANewDeploy()
    {
        await DeleteTheRow();

        var before = await Start();
        await before.Set(true);

        await RunTheCleanupOfANewDeploy();

        var after = await Start();
        after.IsEnabled.Should().BeTrue("the row has a fixed key and no deploy id");
    }

    [Fact]
    public void TheState_IsNotDeployScoped()
    {
        DeployCleanup.DeployScopedStates(Cluster.Storage.Registry).Select(state => state.Type)
                     .Should().NotContain(typeof(ClientTelemetrySettingsState));
    }

    [Fact]
    public async Task Set_ReachesAnotherHostBothWays()
    {
        await DeleteTheRow();

        var console = await Start();
        var gateway = await Start();

        await console.Set(true);
        await TestWait.Until(() => gateway.IsEnabled);

        await console.Set(false);
        await TestWait.Until(() => !gateway.IsEnabled);
    }

    [Fact]
    public async Task Set_BeforeTheLoad_Throws()
    {
        var telemetry = Create();

        var set = () => telemetry.Set(true);

        await set.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task FailedWrite_Throws_AndTheValueStays()
    {
        await DeleteTheRow();

        var telemetry = await Start();
        Cluster.Storage.FailWrites(true);

        var set = () => telemetry.Set(true);

        await set.Should().ThrowAsync<Exception>();
        telemetry.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task View_FiresAtOnce_OnTheLoad_AndOnAChange()
    {
        await DeleteTheRow();

        var telemetry = Create();
        var seen = new List<(bool IsLoaded, bool IsEnabled)>();
        telemetry.View(TestLifetime, () => seen.Add((telemetry.IsLoaded, telemetry.IsEnabled)));

        seen.Should().Equal((false, false));

        await telemetry.OnOrleansStarted(TestLifetime);
        seen.Should().Equal((false, false), (true, false));

        await telemetry.Set(true);
        seen.Last().Should().Be((true, true));
    }

    [Fact]
    public async Task AStart_BeforeTheLoad_IsOff_ThenFollowsTheSwitch()
    {
        await DeleteTheRow();

        var gateway = Create();
        var connection = new ClientTelemetryConnection(gateway, Cluster.Orleans, NullLogger<ClientTelemetryConnection>.Instance);

        (await connection.Start(new SharedClientTelemetry.SessionStart())).Enabled.Should().BeFalse();

        await gateway.OnOrleansStarted(TestLifetime);
        (await connection.Start(new SharedClientTelemetry.SessionStart())).Enabled.Should().BeFalse();

        var console = await Start();
        await console.Set(true);
        await TestWait.Until(() => gateway.IsEnabled);

        var started = await connection.Start(new SharedClientTelemetry.SessionStart());
        started.Enabled.Should().BeTrue();
        await connection.OnDisconnected();
    }

    [Fact]
    public async Task AStart_ReadsNoDatabaseForTheFlag()
    {
        await DeleteTheRow();

        var gateway = await Start();
        var connection = new ClientTelemetryConnection(gateway, Cluster.Orleans, NullLogger<ClientTelemetryConnection>.Instance);
        var reads = Cluster.Storage.ReadsOfTheSwitch;
        reads.Should().BeGreaterThan(0, "the start of the switch reads its row through this storage");

        await connection.Start(new SharedClientTelemetry.SessionStart());
        await connection.Start(new SharedClientTelemetry.SessionStart());

        Cluster.Storage.ReadsOfTheSwitch.Should().Be(reads);
    }

    private ClientTelemetrySwitch Create()
    {
        return new ClientTelemetrySwitch(new AddressableStateUtils(Cluster.Orleans, Cluster.Messaging, NullLoggerFactory.Instance));
    }

    private async Task<ClientTelemetrySwitch> Start()
    {
        var telemetry = Create();
        await telemetry.OnOrleansStarted(TestLifetime);
        return telemetry;
    }

    // The cleanup a coordinator of a new deploy runs: every record of an old deploy goes.
    private async Task RunTheCleanupOfANewDeploy()
    {
        var deploy = Guid.NewGuid();
        var context = Substitute.For<IDeployContext>();
        context.DeployId.Returns(deploy);

        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimePipe.Send<DeployIdResponse>(DeployIdPipe.Id, Arg.Any<object>())
                 .Returns(new DeployIdResponse { DeployId = deploy });

        await new DeployCleanup(context, Cluster.Storage, messaging, NullLogger<DeployCleanup>.Instance).Run();
    }

    // The fixture is shared and the key is fixed: a test that starts from "never written" clears what the previous one left.
    private async Task DeleteTheRow()
    {
        await using var connection = await Cluster.Database.DataSource.OpenConnectionAsync();
        await using var command = new NpgsqlCommand($"DELETE FROM configs WHERE type = '{ClientTelemetrySettingsState.StateName}'", connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
