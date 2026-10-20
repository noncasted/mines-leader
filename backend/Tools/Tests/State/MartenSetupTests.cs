using Cluster.State;
using DeploySetup;
using FluentAssertions;
using Infrastructure.State;
using Marten;
using Meta.Users;
using Orchestration;
using Shared;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// F2.40: the inline snapshot projections come from the composition's generated state registry, not from the
/// assemblies that happen to be loaded into the process.
/// </summary>
public class MartenSetupTests
{
    [Fact]
    public void EventStateTypes_AreTheEventStatesOfTheRegistry()
    {
        var registry = new GrainStatesRegistry(new List<GrainStateInfo>
        {
            Info<ClusterFeaturesState>("cluster"),
            Info<EventTestAggregate>("mt_doc_eventtestaggregate")
        });

        MartenSetupExtensions.EventStateTypes(registry).Should().Equal(typeof(EventTestAggregate));
    }

    [Fact]
    public void EventStateTypes_ProductionComposition_HasUserEventStates()
    {
        // The test assembly has its own generated registration (with test states): take the hosts' states from the
        // StatesLookup the migrator sees.
        var states = DeployStates.All
                                 .Select(info => new GrainStateInfo
                                 {
                                     TableName = info.TableName,
                                     KeyType = info.KeyType,
                                     Type = info.Type,
                                     Name = info.StateName
                                 })
                                 .ToList();

        var types = MartenSetupExtensions.EventStateTypes(new GrainStatesRegistry(states));

        types.Should().Contain([typeof(UserState), typeof(UserAuthState)]);
        types.Should().NotContain(typeof(EventTestAggregate), "test states are not in the production registry");
        types.Should().OnlyContain(type => typeof(IEventStateValue).IsAssignableFrom(type));
    }

    [Fact]
    public void Configure_RegistersInlineSnapshotPerEventState()
    {
        var registry = new GrainStatesRegistry(new List<GrainStateInfo>
        {
            Info<ClusterFeaturesState>("cluster"),
            Info<EventTestAggregate>("mt_doc_eventtestaggregate")
        });

        var options = new StoreOptions();
        MartenSetupExtensions.Configure(options, registry);

        options.Projections.All.Should().ContainSingle()
               .Which.Name.Should().Contain(nameof(EventTestAggregate));
    }

    private static GrainStateInfo Info<T>(string table)
    {
        return new GrainStateInfo
        {
            TableName = table,
            KeyType = GrainKeyType.Guid,
            Type = typeof(T),
            Name = typeof(T).Name
        };
    }
}
