using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Lobbies;
using Cluster.Monitoring;
using Cluster.Monitoring.Online;
using Cluster.State;
using Internal;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.Cluster;

// IDeployCleanup removes the records of old deploys from every state marked with IDeployScopedState.
[Collection(nameof(OrleansIntegrationCollection))]
public class DeployCleanupTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    // The deploy scoped states of the framework cluster: the registry must find exactly these.
    private static readonly Type[] _clusterDeployScopedStates =
    [
        typeof(DeployState),
        typeof(ServiceDiscoveryStorageState),
        typeof(LobbyRegistryState),
        typeof(ClusterFeaturesState),
        typeof(LiveSessionsData),
        typeof(ConnectedUsersLiveData),
        typeof(OnlineStatsLiveData),
    ];

    // DeploymentState writes these rows (an AddressableStateValue under the default identity): the registered state
    // name must be the one it writes, or the cleanup would look for rows nobody writes.
    [Fact]
    public void ServiceSlicedStates_RegisteredName_IsTheDeploymentStateIdentity()
    {
        var storage = GetSiloService<IStateStorage>();
        var deployId = Guid.NewGuid();

        Identity<LiveSessionsData>(storage, deployId).Should().BeEquivalentTo(
            DeploymentState<LiveSessionsData>.CreateDefaultStateIdentity(deployId));
        Identity<ConnectedUsersLiveData>(storage, deployId).Should().BeEquivalentTo(
            DeploymentState<ConnectedUsersLiveData>.CreateDefaultStateIdentity(deployId));
        Identity<OnlineStatsLiveData>(storage, deployId).Should().BeEquivalentTo(
            DeploymentState<OnlineStatsLiveData>.CreateDefaultStateIdentity(deployId));
    }

    [Fact]
    public void DeployScopedStates_FromGeneratedRegistry_AreTheClusterDeployStates()
    {
        var storage = GetSiloService<IStateStorage>();

        var states = DeployCleanup.DeployScopedStates(storage.Registry);

        states.Select(s => s.Type).Should().BeEquivalentTo(_clusterDeployScopedStates);
    }

    [Fact]
    public void DeployScopedStates_NotGuidKeyed_Throws()
    {
        var registry = new GrainStatesRegistry(new List<GrainStateInfo>
        {
            new()
            {
                TableName = "cluster",
                KeyType = Shared.GrainKeyType.String,
                Type = typeof(DeployState),
                Name = "deploy"
            }
        });

        var act = () => DeployCleanup.DeployScopedStates(registry);

        act.Should().Throw<InvalidOperationException>().WithMessage("*DeployState*Guid*");
    }

    [Fact]
    public async Task Run_RemovesOldDeploysOfEveryScopedState_KeepsCurrent()
    {
        var storage = GetSiloService<IStateStorage>();
        var current = Guid.NewGuid();
        var stale = new[] { Guid.NewGuid(), Guid.NewGuid() };

        foreach (var deployId in stale.Append(current))
            await WriteAllScopedStates(storage, deployId);

        var result = await CreateCleanup(storage, current).Run();

        result.KeptDeployId.Should().Be(current);
        result.Removed.Should().Be(stale.Length * _clusterDeployScopedStates.Length);
        result.RemovedDeployIds.Should().BeEquivalentTo(stale);

        (await Keys<DeployState>(storage)).Should().Equal(current);
        (await Keys<ServiceDiscoveryStorageState>(storage)).Should().Equal(current);
        (await Keys<ClusterFeaturesState>(storage)).Should().Equal(current);
        (await Keys<LobbyRegistryState>(storage)).Should().Equal(current);
        (await Keys<LiveSessionsData>(storage)).Should().Equal(current);
        (await Keys<ConnectedUsersLiveData>(storage)).Should().Equal(current);
        (await Keys<OnlineStatsLiveData>(storage)).Should().Equal(current);
    }

    [Fact]
    public async Task Run_OnlyCurrentDeploy_RemovesNothing()
    {
        var storage = GetSiloService<IStateStorage>();
        var current = Guid.NewGuid();
        await WriteAllScopedStates(storage, current);

        var result = await CreateCleanup(storage, current).Run();

        result.Removed.Should().Be(0);
        result.RemovedDeployIds.Should().BeEmpty();
        (await Keys<DeployState>(storage)).Should().Equal(current);
    }

    // A console started before the current deploy still has the old deploy id: the cleanup must not take the rows of
    // the deploy the cluster runs (the one the deploy id pipe answers) for stale ones.
    [Fact]
    public async Task Run_LocalDeployIdBehindCluster_RemovesNothing()
    {
        var storage = GetSiloService<IStateStorage>();
        var local = Guid.NewGuid();
        var cluster = Guid.NewGuid();

        await WriteAllScopedStates(storage, local);
        await WriteAllScopedStates(storage, cluster);

        var run = () => CreateCleanup(storage, local, cluster).Run();
        await run.Should().ThrowAsync<InvalidOperationException>();

        (await Keys<DeployState>(storage)).Should().BeEquivalentTo(new[] { local, cluster });
    }

    // Service discovery rows written before the tags became strings hold the numeric enum value.
    [Fact]
    public async Task Run_LegacyNumericServiceTagRow_IsReadAndRemoved()
    {
        var storage = GetSiloService<IStateStorage>();
        var current = Guid.NewGuid();
        var stale = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        await WriteAllScopedStates(storage, current);
        var discovery = new ServiceDiscoveryStorageState();
        discovery.Set(new ServiceOverview { Id = memberId, Tag = TestRoles.Meta, UpdateTime = DateTime.UtcNow });
        await Write(storage, stale, discovery);

        await using (var connection = new NpgsqlConnection(Database.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);

            await using var command = new NpgsqlCommand(
                "UPDATE cluster SET value = jsonb_set(value, ARRAY['Members', @member, 'Tag'], '200'::jsonb) " +
                "WHERE type = 'service_discovery' AND key::text = @key", connection);

            command.Parameters.AddWithValue("member", memberId.ToString());
            command.Parameters.AddWithValue("key", stale.ToString());

            (await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken)).Should().Be(1);
        }

        var legacy = await storage.Read<ServiceDiscoveryStorageState>(Identity<ServiceDiscoveryStorageState>(storage, stale));
        legacy.Members[memberId].Tag.Should().Be(TestRoles.Meta);

        var result = await CreateCleanup(storage, current).Run();

        result.RemovedDeployIds.Should().Equal(stale);
        (await Keys<ServiceDiscoveryStorageState>(storage)).Should().Equal(current);
    }

    private static DeployCleanup CreateCleanup(IStateStorage storage, Guid currentDeployId, Guid? clusterDeployId = null)
    {
        var deployContext = Substitute.For<IDeployContext>();
        deployContext.DeployId.Returns(currentDeployId);

        // The deploy id pipe answers the deploy the cluster runs.
        var messaging = Substitute.For<IMessaging>();
        messaging.RuntimePipe.Send<DeployIdResponse>(DeployIdPipe.Id, Arg.Any<object>())
                 .Returns(new DeployIdResponse { DeployId = clusterDeployId ?? currentDeployId });

        return new DeployCleanup(deployContext, storage, messaging, NullLogger<DeployCleanup>.Instance);
    }

    private static async Task WriteAllScopedStates(IStateStorage storage, Guid deployId)
    {
        var memberId = Guid.NewGuid();

        var deployState = new DeployState();
        deployState.Init(deployId, DateTime.UtcNow);
        await Write(storage, deployId, deployState);
        var discovery = new ServiceDiscoveryStorageState();
        discovery.Set(new ServiceOverview { Id = memberId, Tag = TestRoles.Silo, UpdateTime = DateTime.UtcNow });
        await Write(storage, deployId, discovery);
        var lobbies = new LobbyRegistryState();
        lobbies.AdvanceListingVersion();
        await Write(storage, deployId, lobbies);

        // The production format of the DeploymentState rows: the value is wrapped into an AddressableStateValue
        // (ClusterFeaturesWriter, ServiceSlicesWriter).
        var features = new ClusterFeaturesState();
        features.SetAcceptingConnections(true);
        await WriteAddressable(storage, deployId, features);
        var sessions = new LiveSessionsData();
        sessions.SetSlice(Guid.NewGuid(), [new LiveSessionEntry { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }]);
        await WriteAddressable(storage, deployId, sessions);

        var users = new ConnectedUsersLiveData();
        users.SetSlice(Guid.NewGuid(), [new ConnectedUserEntry { UserId = Guid.NewGuid() }]);
        await WriteAddressable(storage, deployId, users);
        await WriteAddressable(storage, deployId, new OnlineStatsLiveData
        {
            Current = 1,
            Hours = [new OnlinePeriod { Start = DateTime.UtcNow, Peak = 1, UniqueUsers = 1 }]
        });
    }

    private static Task WriteAddressable<T>(IStateStorage storage, Guid deployId, T value) where T : IStateValue
    {
        return storage.Write(Identity<T>(storage, deployId), new AddressableStateValue
        {
            IsInitialized = true,
            UpdateDate = DateTime.UtcNow,
            Value = Newtonsoft.Json.JsonConvert.SerializeObject(value, JsonStateSettings.CreateBase())
        });
    }

    private static Task Write<T>(IStateStorage storage, Guid deployId, T value) where T : IStateValue
    {
        return storage.Write(Identity<T>(storage, deployId), value);
    }

    private static StateIdentity Identity<T>(IStateStorage storage, Guid deployId)
    {
        var info = storage.Registry.Get<T>();

        return new StateIdentity
        {
            Key = deployId,
            Type = info.Name,
            TableName = info.TableName,
            Extension = null
        };
    }

    // Keys by SQL: the rows of DeploymentState hold an AddressableStateValue, not T.
    private async Task<List<Guid>> Keys<T>(IStateStorage storage) where T : class, IStateValue, new()
    {
        var info = storage.Registry.Get<T>();
        var keys = new List<Guid>();

        await using var connection = new NpgsqlConnection(Database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = new NpgsqlCommand($"SELECT key FROM {info.TableName} WHERE type = @type", connection);
        command.Parameters.AddWithValue("type", info.Name);

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            keys.Add(reader.GetGuid(0));

        return keys;
    }
}
