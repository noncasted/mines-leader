using FluentAssertions;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests basic grain state read/write operations via the real Orleans TestCluster.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class StateReadWriteTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture) {
    [Fact]
    public async Task SimpleGrain_WriteAndRead_ReturnsSameValue() {
        var id = Guid.NewGuid();
        var grain = GetGrain<ISimpleTestGrain>(id);

        await grain.SetCounter(42);
        var result = await grain.GetCounter();

        result.Should().Be(42);
    }

    [Fact]
    public async Task SimpleGrain_WriteLabel_ReturnsSameLabel() {
        var id = Guid.NewGuid();
        var grain = GetGrain<ISimpleTestGrain>(id);

        await grain.SetLabel("hello-world");
        var label = await grain.GetLabel();

        label.Should().Be("hello-world");
    }

    [Fact]
    public async Task SimpleGrain_MultipleWrites_KeepsLatest() {
        var id = Guid.NewGuid();
        var grain = GetGrain<ISimpleTestGrain>(id);

        await grain.SetCounter(1);
        await grain.SetCounter(2);
        await grain.SetCounter(3);
        var result = await grain.GetCounter();

        result.Should().Be(3);
    }

    [Fact]
    public async Task SimpleGrain_DifferentGrains_IndependentState() {
        var grain1 = GetGrain<ISimpleTestGrain>(Guid.NewGuid());
        var grain2 = GetGrain<ISimpleTestGrain>(Guid.NewGuid());

        await grain1.SetCounter(100);
        await grain2.SetCounter(200);

        var result1 = await grain1.GetCounter();
        var result2 = await grain2.GetCounter();

        result1.Should().Be(100);
        result2.Should().Be(200);
    }
}
