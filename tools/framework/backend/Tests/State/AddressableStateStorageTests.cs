using Cluster.Configs;
using Common;
using FluentAssertions;
using Framework.Tests.Fixtures;
using Infrastructure;
using Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Shared;
using Xunit;

namespace Framework.Tests.State;

// The real AddressableState over the database: the stored value survives a restart of the service.
// Regression: a value loaded from the database was reported as not initialized, so the coordinator overwrote the
// stored configs with the seed (ConfigsInitializer.Init) on every start.
[Collection(nameof(OrleansIntegrationCollection))]
public class AddressableStateStorageTests(OrleansTestClusterFixture fixture)
    : IntegrationTestBase<OrleansTestClusterFixture>(fixture)
{
    [Fact]
    public async Task LoadedFromDatabase_ReportsInitialized()
    {
        var writer = CreateState();
        await writer.SetValue(new AddressableStorageTestOptions { Label = "stored", Limit = 7 });

        var reader = CreateState();
        var lifetime = new Lifetime();

        await reader.OnOrleansStarted(lifetime);

        reader.IsInitialized.Should().BeTrue();
        reader.UpdateDate.Should().Be(writer.UpdateDate);
        reader.Value.Label.Should().Be("stored");
        reader.Value.Limit.Should().Be(7);

        lifetime.Terminate();
    }

    [Fact]
    public async Task NeverWritten_ReportsNotInitialized()
    {
        var reader = CreateState();
        var lifetime = new Lifetime();

        await reader.OnOrleansStarted(lifetime);

        reader.IsInitialized.Should().BeFalse();
        reader.UpdateDate.Should().Be(default);
        reader.Value.Label.Should().BeEmpty();

        lifetime.Terminate();
    }

    [Fact]
    public async Task ConfigsInitializer_AfterRestart_KeepsStoredValue()
    {
        var writer = CreateState();
        await writer.SetValue(new AddressableStorageTestOptions { Label = "edited", Limit = 3 });

        var restarted = CreateState();
        var restartedLifetime = new Lifetime();
        await restarted.OnOrleansStarted(restartedLifetime);

        await ConfigsInitializer.Init("config.addressableStorageTest", restarted, NullLogger.Instance);

        restarted.Value.Label.Should().Be("edited");

        // The database still holds the edited value, not the seed.
        var next = CreateState();
        var nextLifetime = new Lifetime();
        await next.OnOrleansStarted(nextLifetime);

        next.Value.Label.Should().Be("edited");
        next.Value.Limit.Should().Be(3);

        restartedLifetime.Terminate();
        nextLifetime.Terminate();
    }

    private AddressableStorageTestState CreateState()
    {
        var utils = new AddressableStateUtils(
            GetSiloService<IOrleans>(),
            GetSiloService<IMessaging>(),
            GetSiloService<ILoggerFactory>());

        return new AddressableStorageTestState(utils);
    }
}
