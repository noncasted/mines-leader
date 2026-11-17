using Cluster.Deploy;
using Cluster.Discovery;
using Cluster.Lobbies;
using Cluster.Monitoring;
using Cluster.Monitoring.Online;
using Cluster.State;
using Common.Extensions;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Infrastructure.Execution;
using Infrastructure.State;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Orleans.Serialization;
using Xunit;

namespace Framework.Tests.State;

// The format of the rows already in the database. The json below was written by the types when they still had public
// setters; a setter the serializer can't reach brings a value back as the default, without an error. Every value here
// differs from the default of its member, the zeros included. Do not regenerate the literals: a changed literal means
// that the rows of a live deploy no longer load.
[Collection(nameof(OrleansIntegrationCollection))]
public class StoredStatesFormatTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    private static readonly Guid IdA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid IdB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid IdC = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly DateTime Time = new(2026, 11, 2, 10, 20, 30, 123, DateTimeKind.Utc);

    private const string DeployStateJson =
        """
        {"$type":"Cluster.Deploy.DeployState, Cluster","DeployId":"11111111-1111-1111-1111-111111111111","LastHeartbeat":"2026-11-02T10:20:30.123Z","CoordinatorReady":true,"CreatedAt":"2026-11-02T10:20:30.123Z","Version":0}
        """;

    private const string ServiceDiscoveryStorageStateJson =
        """
        {"$type":"Cluster.Discovery.ServiceDiscoveryStorageState, Cluster","Members":{"$type":"System.Collections.Generic.Dictionary`2[[System.Guid, System.Private.CoreLib],[Cluster.Discovery.IServiceOverview, Cluster]], System.Private.CoreLib","11111111-1111-1111-1111-111111111111":{"$type":"Cluster.Discovery.ServiceOverview, Cluster","Id":"11111111-1111-1111-1111-111111111111","Tag":"Meta","UpdateTime":"2026-11-02T10:20:30.123Z"},"22222222-2222-2222-2222-222222222222":{"$type":"Cluster.Discovery.GameServerOverview, Cluster","Id":"22222222-2222-2222-2222-222222222222","Tag":"Silo","UpdateTime":"2026-11-02T10:20:30.123Z","Url":"http://s"}},"Version":0}
        """;

    private const string LobbyRegistryStateJson =
        """
        {"$type":"Cluster.Lobbies.LobbyRegistryState, Cluster","Entries":{"$type":"System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[Cluster.Lobbies.LobbyRegistryEntry, Cluster]], System.Private.CoreLib","ABCD":{"$type":"Cluster.Lobbies.LobbyRegistryEntry, Cluster","ServerId":"11111111-1111-1111-1111-111111111111","SessionId":"22222222-2222-2222-2222-222222222222","MatchSessionId":"33333333-3333-3333-3333-333333333333","Listing":{"$type":"Cluster.Lobbies.LobbyListing, Cluster","IsOpen":true,"LeaderUserId":"11111111-1111-1111-1111-111111111111","PlayersCount":2,"IsRunning":true,"MaxPlayers":4,"IsListed":true}}},"HiddenServers":{"$type":"System.Collections.Generic.HashSet`1[[System.Guid, System.Private.CoreLib]], System.Private.CoreLib","$values":["33333333-3333-3333-3333-333333333333"]},"ListingVersion":7,"Version":0}
        """;

    private const string ConnectedUsersLiveDataJson =
        """
        {"$type":"Cluster.Monitoring.ConnectedUsersLiveData, Cluster","Slices":{"$type":"System.Collections.Generic.Dictionary`2[[System.Guid, System.Private.CoreLib],[System.Collections.Generic.List`1[[Cluster.Monitoring.ConnectedUserEntry, Cluster]], System.Private.CoreLib]], System.Private.CoreLib","11111111-1111-1111-1111-111111111111":{"$type":"System.Collections.Generic.List`1[[Cluster.Monitoring.ConnectedUserEntry, Cluster]], System.Private.CoreLib","$values":[{"$type":"Cluster.Monitoring.ConnectedUserEntry, Cluster","UserId":"22222222-2222-2222-2222-222222222222"}]}},"Version":0}
        """;

    private const string LiveSessionsDataJson =
        """
        {"$type":"Cluster.Monitoring.LiveSessionsData, Cluster","Slices":{"$type":"System.Collections.Generic.Dictionary`2[[System.Guid, System.Private.CoreLib],[System.Collections.Generic.List`1[[Cluster.Monitoring.LiveSessionEntry, Cluster]], System.Private.CoreLib]], System.Private.CoreLib","11111111-1111-1111-1111-111111111111":{"$type":"System.Collections.Generic.List`1[[Cluster.Monitoring.LiveSessionEntry, Cluster]], System.Private.CoreLib","$values":[{"$type":"Cluster.Monitoring.LiveSessionEntry, Cluster","Id":"22222222-2222-2222-2222-222222222222","UserIds":{"$type":"System.Collections.Generic.List`1[[System.Guid, System.Private.CoreLib]], System.Private.CoreLib","$values":["33333333-3333-3333-3333-333333333333"]},"CreatedAt":"2026-11-02T10:20:30.123Z"}]}},"Version":0}
        """;

    private const string ClusterFeaturesStateJson =
        """
        {"$type":"Cluster.State.ClusterFeaturesState, Cluster","AcceptingConnections":true,"SideEffectsEnabled":true,"CoordinatorUnhealthy":true,"GameFlags":{"$type":"System.Collections.Generic.Dictionary`2[[System.String, System.Private.CoreLib],[System.Boolean, System.Private.CoreLib]], System.Private.CoreLib","f":true,"g":false},"Version":0}
        """;

    private const string AddressableStateValueJson =
        """
        {"$type":"Infrastructure.AddressableStateValue, Infrastructure","Value":"payload","IsInitialized":true,"UpdateDate":"2026-11-02T10:20:30.123Z","Version":0}
        """;

    private const string TaskBalancerOptionsJson =
        """
        {"$type":"Infrastructure.Execution.TaskBalancerOptions, Infrastructure","EmptyDelayMs":0,"NextDelayMs":11,"IterationScore":12,"ExceptionPenalty":13,"ConcurrentTasks":14}
        """;

    private const string RuntimeChannelOptionsJson =
        """
        {"$type":"Infrastructure.RuntimeChannelOptions, Infrastructure","ObserverKeepAliveMinutes":0,"CatchUpBufferSize":21,"DeliveryTimeoutSeconds":22,"ResubscribeMinIntervalMs":23,"ResubscribeMaxIntervalMs":24,"ResubscribeTimeoutMs":25}
        """;

    private const string RuntimePipeOptionsJson =
        """
        {"$type":"Infrastructure.RuntimePipeOptions, Infrastructure","ObserverKeepAliveMinutes":0,"SendTimeoutSeconds":31,"SendRetryCount":32,"SendRetryBaseDelayMs":33}
        """;

    private const string DurableQueueOptionsJson =
        """
        {"$type":"Infrastructure.DurableQueueOptions, Infrastructure","ObserverKeepAliveMinutes":41}
        """;

    private const string SideEffectsOptionsJson =
        """
        {"$type":"Infrastructure.SideEffectsOptions, Infrastructure","ScanDelay":0,"EmptyScanDelay":51,"ConcurrentExecutions":52,"MaxRetryCount":53,"IncrementalRetryDelay":54.5,"StuckCheckIntervalSeconds":55,"StuckThresholdMinutes":56.5}
        """;

    private const string TransactionOptionsJson =
        """
        {"$type":"Infrastructure.TransactionOptions, Infrastructure","LockWaitSeconds":0.0,"StuckGraceSeconds":61.5}
        """;
    [Fact]
    public void DeployState_KeepsValuesAndKeys()
    {
        CheckState<DeployState>(DeployStateJson, state => {
            state.DeployId.Should().Be(IdA);
            state.LastHeartbeat.Should().Be(Time);
            state.LastHeartbeat.Kind.Should().Be(DateTimeKind.Utc);
            state.CoordinatorReady.Should().BeTrue();
            state.CreatedAt.Should().Be(Time);
        });
    }

    [Fact]
    public void ServiceDiscoveryStorageState_KeepsValuesAndKeys()
    {
        CheckState<ServiceDiscoveryStorageState>(ServiceDiscoveryStorageStateJson, state => {
            state.Members.Should().HaveCount(2);
            state.Members[IdA].Should().BeOfType<ServiceOverview>();
            state.Members[IdA].Id.Should().Be(IdA);
            state.Members[IdA].Tag.Should().Be(TestRoles.Meta);
            state.Members[IdA].UpdateTime.Should().Be(Time);
            state.Members[IdB].Should().BeOfType<GameServerOverview>().Which.Url.Should().Be("http://s");
        });
    }

    [Fact]
    public void LobbyRegistryState_KeepsValuesAndKeys()
    {
        CheckState<LobbyRegistryState>(LobbyRegistryStateJson, state => {
            state.ListingVersion.Should().Be(7);
            state.HiddenServers.Should().Equal(IdC);
            state.Entries.Should().ContainKey("ABCD");

            var entry = state.Entries["ABCD"];
            entry.ServerId.Should().Be(IdA);
            entry.SessionId.Should().Be(IdB);
            entry.MatchSessionId.Should().Be(IdC);

            var listing = entry.Listing;
            listing.IsOpen.Should().BeTrue();
            listing.LeaderUserId.Should().Be(IdA);
            listing.PlayersCount.Should().Be(2);
            listing.IsRunning.Should().BeTrue();
            listing.MaxPlayers.Should().Be(4);
        });
    }

    // A row written while the listing had IsJoinable (id 5) still loads; the field is dropped on the next write.
    [Fact]
    public void LobbyRegistryState_RowWithRemovedIsJoinable_Loads()
    {
        var serializer = GetSiloService<IStateSerializer>();
        var json = LobbyRegistryStateJson.Replace("\"IsRunning\":true,", "\"IsRunning\":true,\"IsJoinable\":true,");

        var state = serializer.Deserialize<LobbyRegistryState>(json);

        state.Entries["ABCD"].Listing.MaxPlayers.Should().Be(4);
        serializer.Serialize((object)state).Should().Be(LobbyRegistryStateJson);
    }

    [Fact]
    public void ConnectedUsersLiveData_KeepsValuesAndKeys()
    {
        CheckState<ConnectedUsersLiveData>(ConnectedUsersLiveDataJson, state => {
            state.Slices.Should().ContainKey(IdA);
            state.Slices[IdA].Single().UserId.Should().Be(IdB);
            state.AllUsers().Single().UserId.Should().Be(IdB);
        });
    }

    [Fact]
    public void LiveSessionsData_KeepsValuesAndKeys()
    {
        CheckState<LiveSessionsData>(LiveSessionsDataJson, state => {
            var entry = state.Slices[IdA].Single();
            entry.Id.Should().Be(IdB);
            entry.UserIds.Should().Equal(IdC);
            entry.CreatedAt.Should().Be(Time);
        });
    }

    [Fact]
    public void ClusterFeaturesState_KeepsValuesAndKeys()
    {
        CheckState<ClusterFeaturesState>(ClusterFeaturesStateJson, state => {
            state.AcceptingConnections.Should().BeTrue();
            state.SideEffectsEnabled.Should().BeTrue();
            state.CoordinatorUnhealthy.Should().BeTrue();
            state.GameFlags.Should().HaveCount(2);
            state.GameFlags["f"].Should().BeTrue();
            state.GameFlags["g"].Should().BeFalse();
        });
    }

    [Fact]
    public void AddressableStateValue_KeepsValuesAndKeys()
    {
        CheckState<AddressableStateValue>(AddressableStateValueJson, state => {
            state.Value.Should().Be("payload");
            state.IsInitialized.Should().BeTrue();
            state.UpdateDate.Should().Be(Time);
        });
    }

    // The path of ClusterFeaturesWriter: the stored row, the patch, the row written back and read again.
    [Fact]
    public void ClusterFeaturesPatch_OnStoredRow_ChangesOnlyWhatItNames()
    {
        var serializer = GetSiloService<IStateSerializer>();
        var state = serializer.Deserialize<ClusterFeaturesState>(ClusterFeaturesStateJson);

        new ClusterFeaturesPatch
        {
            SideEffectsEnabled = false,
            GameFlags = new Dictionary<string, bool> { ["g"] = true, ["h"] = true }
        }.ApplyTo(state);

        var reloaded = serializer.Deserialize<ClusterFeaturesState>(serializer.Serialize(state));

        reloaded.AcceptingConnections.Should().BeTrue();
        reloaded.SideEffectsEnabled.Should().BeFalse();
        reloaded.CoordinatorUnhealthy.Should().BeTrue();
        reloaded.GameFlags.Should().BeEquivalentTo(new Dictionary<string, bool>
        {
            ["f"] = true,
            ["g"] = true,
            ["h"] = true
        });
    }

    // The path of ClusterFeaturesWriter when the deploy has no row yet.
    [Fact]
    public void ClusterFeaturesPatch_OnNewState_SurvivesWriteAndRead()
    {
        var serializer = GetSiloService<IStateSerializer>();
        var state = new ClusterFeaturesState();

        new ClusterFeaturesPatch
        {
            AcceptingConnections = true,
            CoordinatorUnhealthy = true,
            GameFlags = new Dictionary<string, bool> { ["f"] = true }
        }.ApplyTo(state);

        var reloaded = serializer.Deserialize<ClusterFeaturesState>(serializer.Serialize(state));

        reloaded.AcceptingConnections.Should().BeTrue();
        reloaded.SideEffectsEnabled.Should().BeFalse();
        reloaded.CoordinatorUnhealthy.Should().BeTrue();
        reloaded.GameFlags["f"].Should().BeTrue();
    }

    [Fact]
    public void TaskBalancerOptions_KeepValuesAndKeys()
    {
        CheckOptions<TaskBalancerOptions>(TaskBalancerOptionsJson, options => {
            options.EmptyDelayMs.Should().Be(0);
            options.NextDelayMs.Should().Be(11);
            options.IterationScore.Should().Be(12);
            options.ExceptionPenalty.Should().Be(13);
            options.ConcurrentTasks.Should().Be(14);
        });
    }

    [Fact]
    public void RuntimeChannelOptions_KeepValuesAndKeys()
    {
        CheckOptions<RuntimeChannelOptions>(RuntimeChannelOptionsJson, options => {
            options.ObserverKeepAliveMinutes.Should().Be(0);
            options.CatchUpBufferSize.Should().Be(21);
            options.DeliveryTimeoutSeconds.Should().Be(22);
            options.ResubscribeMinIntervalMs.Should().Be(23);
            options.ResubscribeMaxIntervalMs.Should().Be(24);
            options.ResubscribeTimeoutMs.Should().Be(25);
        });
    }

    [Fact]
    public void RuntimeChannelOptions_StoredBeforeTheIntervals_KeepProductionIntervals()
    {
        var options = JsonUtils.Deserialize<RuntimeChannelOptions>("""
            {"$type":"Infrastructure.RuntimeChannelOptions, Infrastructure","ObserverKeepAliveMinutes":0,"CatchUpBufferSize":21,"DeliveryTimeoutSeconds":22}
            """);

        options.DeliveryTimeoutSeconds.Should().Be(22);
        options.ResubscribeMinIntervalMs.Should().Be(10_000);
        options.ResubscribeMaxIntervalMs.Should().Be(60_000);
        options.ResubscribeTimeoutMs.Should().Be(30_000);
    }

    [Fact]
    public void RuntimePipeOptions_KeepValuesAndKeys()
    {
        CheckOptions<RuntimePipeOptions>(RuntimePipeOptionsJson, options => {
            options.ObserverKeepAliveMinutes.Should().Be(0);
            options.SendTimeoutSeconds.Should().Be(31);
            options.SendRetryCount.Should().Be(32);
            options.SendRetryBaseDelayMs.Should().Be(33);
        });
    }

    [Fact]
    public void DurableQueueOptions_KeepValuesAndKeys()
    {
        CheckOptions<DurableQueueOptions>(DurableQueueOptionsJson, options => {
            options.ObserverKeepAliveMinutes.Should().Be(41);
        });
    }

    [Fact]
    public void SideEffectsOptions_KeepValuesAndKeys()
    {
        CheckOptions<SideEffectsOptions>(SideEffectsOptionsJson, options => {
            options.ScanDelay.Should().Be(0);
            options.EmptyScanDelay.Should().Be(51);
            options.ConcurrentExecutions.Should().Be(52);
            options.MaxRetryCount.Should().Be(53);
            options.IncrementalRetryDelay.Should().Be(54.5f);
            options.StuckCheckIntervalSeconds.Should().Be(55);
            options.StuckThresholdMinutes.Should().Be(56.5f);
        });
    }

    [Fact]
    public void TransactionOptions_KeepValuesAndKeys()
    {
        CheckOptions<TransactionOptions>(TransactionOptionsJson, options => {
            options.LockWaitSeconds.Should().Be(0f);
            options.StuckGraceSeconds.Should().Be(61.5f);
        });
    }

    // The first read of a state without a row is new T(): it writes and reads back like a stored one.
    [Fact]
    public void NewStates_SurviveWriteAndRead()
    {
        CheckNew<DeployState>();
        CheckNew<ServiceDiscoveryStorageState>();
        CheckNew<LobbyRegistryState>();
        CheckNew<ConnectedUsersLiveData>();
        CheckNew<LiveSessionsData>();
        CheckNew<OnlineDayState>();
        CheckNew<OnlineStatsLiveData>();
        CheckNew<ClusterFeaturesState>();
        CheckNew<AddressableStateValue>();
        CheckNew<TaskBalancerOptions>();
        CheckNew<RuntimeChannelOptions>();
        CheckNew<RuntimePipeOptions>();
        CheckNew<DurableQueueOptions>();
        CheckNew<SideEffectsOptions>();
        CheckNew<TransactionOptions>();
    }

    private void CheckNew<T>() where T : class, new()
    {
        var serializer = GetSiloService<IStateSerializer>();
        var json = serializer.Serialize((object)new T());

        serializer.Serialize((object)serializer.Deserialize<T>(json)).Should().Be(json);
    }

    // A grain state: json of the storage, the rewrite under the same keys, the serializer of a remote call and the
    // copier of a local one.
    private void CheckState<T>(string json, Action<T> check) where T : class, IStateValue
    {
        CheckStorage(json, check);

        var value = GetSiloService<IStateSerializer>().Deserialize<T>(json);
        var serializer = GetSiloService<Serializer>();
        var copier = GetSiloService<DeepCopier>();

        check(serializer.Deserialize<T>(serializer.SerializeToArray(value)));
        check(copier.Copy(value));

        GetSiloService<IStateSerializer>().Serialize<IStateValue>(value).Should().Be(json);
    }

    // The options of a config: the row of AddressableState, the config files and the console (JsonUtils).
    private void CheckOptions<T>(string json, Action<T> check) where T : class
    {
        CheckStorage(json, check);

        check(JsonUtils.Deserialize<T>(json));

        var value = JsonUtils.Deserialize<T>(json);
        check(JsonUtils.Deserialize<T>(JsonUtils.Serialize(value)));

        JToken.Parse(JsonUtils.Serialize(value)).ToString(Formatting.None).Should().Be(json);
    }

    private void CheckStorage<T>(string json, Action<T> check) where T : class
    {
        var serializer = GetSiloService<IStateSerializer>();

        check(serializer.Deserialize<T>(json));
        check(serializer.TryDeserialize<T>(json)!);
        check((T)serializer.Deserialize(json, typeof(T))!);

        // A type moved to another assembly is found by its full name (MovedTypesBinder).
        check(serializer.Deserialize<T>(MoveToGoneAssembly(json)));

        serializer.Serialize<T>(serializer.Deserialize<T>(json)).Should().Be(json);
        serializer.Serialize((object)serializer.Deserialize<T>(json)).Should().Be(json);
        serializer.Serialize((object)serializer.Deserialize<T>(MoveToGoneAssembly(json))).Should().Be(json);
    }

    private static string MoveToGoneAssembly(string json)
    {
        return json.Replace(", Cluster", ", GoneAssembly").Replace(", Infrastructure", ", GoneAssembly");
    }
}
