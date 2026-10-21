using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Monitoring;
using Cluster.State;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using NSubstitute;
using Tests.Fixtures;
using Xunit;

namespace Tests.Cluster;

// IDeployCleanup removes the records of old deploys from every state marked with IDeployScopedState.
[Collection(nameof(OrleansIntegrationCollection))]
public class DeployCleanupTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    // The states the cleanup handled before the registry (a hardcoded list) plus the DeploymentState rows.
    private static readonly Type[] _clusterDeployScopedStates =
    [
        typeof(DeployState),
        typeof(ServiceDiscoveryStorageState),
        typeof(ClusterFeaturesState),
        typeof(MatchmakingLiveData),
        typeof(LiveMatchesData),
        typeof(ConnectedUsersLiveData),
        typeof(LiveSessionsData),
    ];

    // DeploymentState writes these rows (an AddressableStateValue under the default identity): the registered state
    // name must be the one it writes, or the cleanup would look for rows nobody writes.
    [Fact]
    public void DeploymentStates_RegisteredName_IsTheDeploymentStateIdentity()
    {
        var storage = GetSiloService<IStateStorage>();
        var deployId = Guid.NewGuid();

        Identity<MatchmakingLiveData>(storage, deployId).Should().BeEquivalentTo(
            DeploymentState<MatchmakingLiveData>.CreateDefaultStateIdentity(deployId));
        Identity<LiveMatchesData>(storage, deployId).Should().BeEquivalentTo(
            DeploymentState<LiveMatchesData>.CreateDefaultStateIdentity(deployId));
        Identity<ConnectedUsersLiveData>(storage, deployId).Should().BeEquivalentTo(
            DeploymentState<ConnectedUsersLiveData>.CreateDefaultStateIdentity(deployId));
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
        result.RemovedDeployIds.Should().Contain(stale);
        result.RemovedDeployIds.Should().NotContain(current);

        (await Keys<DeployState>(storage)).Should().Equal(current);
        (await Keys<ServiceDiscoveryStorageState>(storage)).Should().Equal(current);
        (await Keys<ClusterFeaturesState>(storage)).Should().Equal(current);
        (await Keys<MatchmakingLiveData>(storage)).Should().Equal(current);
        (await Keys<LiveMatchesData>(storage)).Should().Equal(current);
        (await Keys<ConnectedUsersLiveData>(storage)).Should().Equal(current);
    }

    [Fact]
    public async Task Run_OnlyCurrentDeploy_RemovesNothing()
    {
        var storage = GetSiloService<IStateStorage>();
        var current = Guid.NewGuid();
        await WriteAllScopedStates(storage, current);

        // Rows of other tests in the shared database are removed by the first run.
        await CreateCleanup(storage, current).Run();
        var result = await CreateCleanup(storage, current).Run();

        result.Removed.Should().Be(0);
        result.RemovedDeployIds.Should().BeEmpty();
        (await Keys<DeployState>(storage)).Should().Equal(current);
    }

    private static DeployCleanup CreateCleanup(IStateStorage storage, Guid currentDeployId)
    {
        var deployContext = Substitute.For<IDeployContext>();
        deployContext.DeployId.Returns(currentDeployId);
        return new DeployCleanup(deployContext, storage, NullLogger<DeployCleanup>.Instance);
    }

    private static async Task WriteAllScopedStates(IStateStorage storage, Guid deployId)
    {
        var memberId = Guid.NewGuid();

        await Write(storage, deployId, new DeployState { DeployId = deployId, LastHeartbeat = DateTime.UtcNow });
        await Write(storage, deployId, new ServiceDiscoveryStorageState
        {
            Members = new Dictionary<Guid, IServiceOverview>
            {
                [memberId] = new ServiceOverview { Id = memberId, Tag = GameRoles.Silo, UpdateTime = DateTime.UtcNow }
            }
        });

        // The production format of the DeploymentState rows: the value is wrapped into an AddressableStateValue
        // (ClusterFeaturesWriter, DeploymentState<T>).
        await WriteAddressable(storage, deployId, new ClusterFeaturesState { AcceptingConnections = true });
        await WriteAddressable(storage, deployId, new MatchmakingLiveData { TotalInQueue = 1 });
        await WriteAddressable(storage, deployId, new LiveMatchesData
        {
            Matches = [new LiveMatchEntry { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow }]
        });
        await WriteAddressable(storage, deployId, new ConnectedUsersLiveData
        {
            Slices = { [Guid.NewGuid()] = [new ConnectedUserEntry { UserId = Guid.NewGuid() }] }
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
