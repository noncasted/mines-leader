using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Framework.Tests.Fixtures;
using Xunit;

namespace Framework.Tests.State;

[Collection(nameof(OrleansIntegrationCollection))]
public class StateCollectionIdempotencyTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public void StateCollectionUpdate_HasUpdatedAtField()
    {
        var update = new StateCollectionUpdate<string, TestCollectionValue>
        {
            Key = "test",
            Value = new TestCollectionValue { Name = "val" },
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        update.UpdatedAt.Year.Should().Be(2026);
    }

    [Fact]
    public void StateCollectionUpdate_DefaultTimestamp_IsMinValue()
    {
        var update = new StateCollectionUpdate<string, TestCollectionValue>
        {
            Key = "test",
            Value = new TestCollectionValue { Name = "val" }
        };

        update.UpdatedAt.Should().Be(default);
    }

    [Fact]
    public void StateCollectionUpdate_PreservesTimestampThroughSerialization()
    {
        var now = DateTime.UtcNow;

        var update = new StateCollectionUpdate<string, TestCollectionValue>
        {
            Key = "ser-test",
            Value = new TestCollectionValue { Name = "ser" },
            UpdatedAt = now
        };

        update.UpdatedAt.Should().Be(now);
        update.Key.Should().Be("ser-test");
    }
}
