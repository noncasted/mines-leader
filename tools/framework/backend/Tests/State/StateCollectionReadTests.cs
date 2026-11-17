using System.Collections.Concurrent;
using Internal;
using FluentAssertions;
using Infrastructure;
using Infrastructure.State;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Framework.Tests.Fixtures;
using Framework.Tests.Grains;
using Xunit;

namespace Framework.Tests.State;

// StateCollectionUtils.Read against the real database - the re-read used for every notification.
[Collection(nameof(OrleansIntegrationCollection))]
public class StateCollectionReadTests
    (OrleansTestClusterFixture fixture) : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task Read_ReturnsStoredRecords_MissingKeysAbsent()
    {
        var stored = Guid.NewGuid();
        var missing = Guid.NewGuid();

        await GetGrain<ICollectionTestGrain>(stored).SetName("stored");

        var utils = GetSiloService<StateCollectionUtils<Guid, CollectionTestState>>();
        var result = await utils.Read([stored, missing]);

        result.Should().ContainKey(stored);
        result[stored].Name.Should().Be("stored");
        result.Should().NotContainKey(missing);
    }
}
