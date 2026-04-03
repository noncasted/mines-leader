using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Tests.Fixtures;
using Tests.Grains;
using Xunit;

namespace Tests.State;

/// <summary>
/// Tests state version migration: write V0, read V1, verify migration applies.
/// </summary>
[Collection(nameof(OrleansIntegrationCollection))]
public class StateMigrationTests(OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture) {
    [Fact]
    public async Task StateMigration_WriteV0ReadV1_MigratesCorrectly() {
        var key = Guid.NewGuid().ToString();
        const int writtenValue = 42;
        const string expectedLabel = "migrated-42";

        var v0Grain = GetGrain<IMigrationGrainV0>(key);
        await v0Grain.Write(writtenValue);

        var v1Grain = GetGrain<IMigrationGrainV1>(key);
        var (value, label) = await v1Grain.Read();

        value.Should().Be(writtenValue);
        label.Should().Be(expectedLabel);
    }
}
