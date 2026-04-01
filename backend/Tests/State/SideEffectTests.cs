using FluentAssertions;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests side effect registration, execution, and pipeline drain.
/// </summary>
[Collection(nameof(SideEffectIntegrationCollection))]
public class SideEffectTests(SideEffectTestFixture fixture) : IntegrationTestBase<SideEffectTestFixture>(fixture) {
    [Fact]
    public async Task SideEffect_RegisterAndDrain_ExecutesTargetGrain() {
        var sourceId = Guid.NewGuid();
        var targetId = Guid.NewGuid();

        // Register a side effect that increments the target grain
        var sourceGrain = GetGrain<ISideEffectTestGrain>(sourceId);
        await sourceGrain.RegisterSideEffect(targetId);

        // Drain side effects
        var result = await DrainSideEffectsAsync();
        result.AssertDrainedWithWork();

        // Verify target grain was incremented
        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(1);
    }

    [Fact]
    public async Task SideEffect_MultipleDrain_AllExecuted() {
        var targetId = Guid.NewGuid();

        // Register 3 side effects targeting the same grain
        for (var i = 0; i < 3; i++) {
            var sourceGrain = GetGrain<ISideEffectTestGrain>(Guid.NewGuid());
            await sourceGrain.RegisterSideEffect(targetId);
        }

        // Drain all
        var result = await DrainSideEffectsAsync();
        result.AssertDrainedSuccessfully();

        // Verify target was incremented 3 times
        var targetGrain = GetGrain<ITxTestGrain>(targetId);
        var value = await targetGrain.Get();
        value.Should().Be(3);
    }
}
