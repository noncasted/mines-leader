using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Framework.Tests.Fixtures;

public class SideEffectTestFixture : OrleansTestClusterFixture
{
    public SideEffectTestPipeline Pipeline { get; private set; } = null!;

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        // Build pipeline from silo's DI container
        var siloServiceProvider = Cluster.Silos[0].ServiceProvider;

        var storage = siloServiceProvider.GetRequiredService<ISideEffectsStorage>();
        var transactions = siloServiceProvider.GetRequiredService<ITransactions>();
        var orleans = siloServiceProvider.GetRequiredService<IOrleans>();
        var config = siloServiceProvider.GetRequiredService<ISideEffectsConfig>();
        var logger = siloServiceProvider.GetRequiredService<ILogger<SideEffectExecution>>();

        Pipeline = new SideEffectTestPipeline(storage,
            new SideEffectExecution(storage, transactions, orleans, config, logger), config);
    }
}
