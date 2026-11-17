using Infrastructure;
using Internal;
using Microsoft.Extensions.DependencyInjection;
using Orleans.TestingHost;
using Xunit;

namespace Framework.Tests.Fixtures;

// Base of the tests against a shared silo: a clean database per test, and a lifetime of the test ended in DisposeAsync,
// so no subscription of a test stays in the silo after it.
public abstract class IntegrationTestBase<TFixture> : IAsyncLifetime
    where TFixture : OrleansTestClusterFixture
{
    protected IntegrationTestBase(TFixture fixture)
    {
        Fixture = fixture;
    }

    private readonly Lifetime _lifetime = new();

    protected TFixture Fixture { get; }
    protected ILifetime TestLifetime => _lifetime;

    // --- Convenience proxies ---

    protected InProcessTestCluster Cluster => Fixture.Cluster;
    protected IGrainFactory GrainFactory => Fixture.GrainFactory;
    protected DatabaseFixture Database => Fixture.Database;

    protected SideEffectTestPipeline? Pipeline =>
        Fixture is SideEffectTestFixture seFixture ? seFixture.Pipeline : null;

    // --- Per-test lifecycle ---

    public virtual async ValueTask InitializeAsync()
    {
        await Database.ResetDatabase();
    }

    public virtual ValueTask DisposeAsync()
    {
        _lifetime.Terminate();
        return ValueTask.CompletedTask;
    }

    // --- Transaction helpers ---

    protected async Task RunTransaction(Func<Task> action)
    {
        var transactions = GetSiloService<ITransactions>();
        var result = await transactions.Run(action);

        if (!result.IsSuccess)
            throw new Exception($"Transaction failed: {result}");
    }

    protected T GetGrain<T>(Guid key) where T : IGrainWithGuidKey
    {
        return GrainFactory.GetGrain<T>(key);
    }

    protected T GetGrain<T>(string key) where T : IGrainWithStringKey
    {
        return GrainFactory.GetGrain<T>(key);
    }

    protected T GetSiloService<T>() where T : notnull
    {
        return Cluster.Silos[0].ServiceProvider.GetRequiredService<T>();
    }

    // Other options of a config of the first silo until the result is disposed.
    protected ConfigOverride<TOptions> OverrideConfig<TConfig, TOptions>(TOptions options)
        where TConfig : IAddressableState<TOptions>
        where TOptions : class, new()
    {
        return new ConfigOverride<TOptions>(GetSiloService<TConfig>(), options);
    }

    // Settles and drains the side effects and throws if any of them failed.
    protected async Task<DrainResult> DrainSideEffects()
    {
        var result = await DrainSideEffectsWithFailures();
        result.AssertDrainedSuccessfully();
        return result;
    }

    // For a test that expects a side effect to fail: the result is checked by the test.
    protected async Task<DrainResult> DrainSideEffectsWithFailures()
    {
        if (Pipeline == null)
            throw new InvalidOperationException("Pipeline not available. Use SideEffectTestFixture.");

        return await Pipeline.SettleAndDrain();
    }
}