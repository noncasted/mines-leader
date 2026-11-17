using FluentAssertions;
using Infrastructure;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Xunit;

namespace Framework.Tests.State;

[Collection(nameof(SideEffectIntegrationCollection))]
public class SideEffectDeadLetterTests(SideEffectTestFixture fixture)
    : IntegrationTestBase<SideEffectTestFixture>(fixture)
{
    [Fact]
    public async Task FailProcessing_LastAttempt_MovesToDeadLetter()
    {
        var storage = GetSiloService<ISideEffectsStorage>();

        // Write a side effect and move it to processing
        await storage.Write(new AlwaysFailingSideEffect { TrackingId = Guid.NewGuid() });
        var entries = await storage.Read(1);
        entries.Should().HaveCount(1);

        var entry = entries[0];
        var config = GetSiloService<ISideEffectsConfig>();
        var maxAttempts = config.Value.MaxRetryCount;

        // The entry failed every attempt before: this failure is attempt number maxAttempts.
        await storage.FailProcessing(entry.Id, maxAttempts - 1, maxAttempts, 30f, "test forced failure");

        var stats = await storage.GetStats();
        stats.DeadLetterCount.Should().Be(1);
    }
}
