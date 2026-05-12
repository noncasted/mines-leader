using FluentAssertions;
using Meta.Users;
using Tests.Fixtures;
using Xunit;

namespace Tests.Meta;

[Collection(nameof(OrleansIntegrationCollection))]
public class UserGrainTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Initialize_PersistsId()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUser>(id);
        await RunTransaction(() => grain.Initialize());
        UserState? state = null;

        await RunTransaction(async () => {
            state = await grain.GetState();
        });
        state!.Id.Should().Be($"user_entity:{id}");
    }

    [Fact]
    public async Task Initialize_DefaultNameIsEmpty()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUser>(id);
        await RunTransaction(() => grain.Initialize());
        UserState? state = null;

        await RunTransaction(async () => {
            state = await grain.GetState();
        });
        state!.Name.Should().BeEmpty();
    }

    [Fact]
    public async Task SetName_PersistsName()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUser>(id);
        await RunTransaction(() => grain.Initialize());
        await RunTransaction(() => grain.SetName("TestPlayer"));
        UserState? state = null;

        await RunTransaction(async () => {
            state = await grain.GetState();
        });
        state!.Name.Should().Be("TestPlayer");
    }

    [Fact]
    public async Task SetName_OverwritesPreviousName()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUser>(id);
        await RunTransaction(() => grain.Initialize());
        await RunTransaction(() => grain.SetName("First"));
        await RunTransaction(() => grain.SetName("Second"));
        UserState? state = null;

        await RunTransaction(async () => {
            state = await grain.GetState();
        });
        state!.Name.Should().Be("Second");
    }

    [Fact]
    public async Task GetState_CalledTwice_ReturnsConsistentState()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUser>(id);
        await RunTransaction(() => grain.Initialize());
        await RunTransaction(() => grain.SetName("ConsistencyTest"));
        var grain2 = GetGrain<IUser>(id);
        UserState? state1 = null;
        UserState? state2 = null;

        await RunTransaction(async () => {
            state1 = await grain.GetState();
        });

        await RunTransaction(async () => {
            state2 = await grain2.GetState();
        });
        state1!.Id.Should().Be($"user_entity:{id}");
        state2!.Id.Should().Be($"user_entity:{id}");
        state1.Name.Should().Be("ConsistencyTest");
        state2.Name.Should().Be("ConsistencyTest");
    }

    [Fact]
    public async Task GetState_FreshGrain_ReturnsDefaultState()
    {
        var id = Guid.NewGuid();
        var grain = GetGrain<IUser>(id);
        UserState? state = null;

        await RunTransaction(async () => {
            state = await grain.GetState();
        });
        state!.Id.Should().BeEmpty();
        state.Name.Should().BeEmpty();
    }
}
