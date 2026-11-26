using Framework.Tests.Fixtures;
using Framework.Tests.Session;
using Game.ClientTelemetry;
using Infrastructure;
using Infrastructure.State;
using Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Framework.Tests.ClientTelemetry;

// The silo as AddClientTelemetrySessions makes it, on a manual clock, with the calls of the storage counted. The timer of a session
// checks often, the clock decides when the client is lost.
public class ClientTelemetryClusterFixture : SideEffectTestFixture
{
    private readonly Lifetime _lifetime = new();

    public ManualTimeProvider Clock { get; } = new();
    public CapturingLogs Logs { get; } = new();
    public virtual ClientTelemetryStorageOptions Options { get; } = new() { LostCheckInterval = TimeSpan.FromMilliseconds(10), MaxSamples = 4000, MaxErrors = 5, MaxMarks = 5 };
    public CountingStateStorage Storage { get; private set; } = null!;
    public ITelemetrySessionCollection Collection { get; private set; } = null!;
    public ClientTelemetrySweep Sweep { get; private set; } = null!;
    public ITelemetryStartupCollection Startups { get; private set; } = null!;
    public IClientTelemetryReader Reader { get; private set; } = null!;
    public IOrleans Orleans { get; private set; } = null!;
    public IMessaging Messaging => Silo.GetRequiredService<IMessaging>();

    protected virtual TimeProvider SessionTime => Clock;

    private IServiceProvider Silo => Cluster.Silos[0].ServiceProvider;

    protected override void ConfigureSiloServices(IServiceCollection services)
    {
        services.AddSingleton<StateStorage>();
        services.AddSingleton<CountingStateStorage>(provider => new CountingStateStorage(provider.GetRequiredService<StateStorage>()));
        services.AddSingleton<IStateStorage>(provider => provider.GetRequiredService<CountingStateStorage>());
        services.AddSingleton(new TelemetrySessionClock(SessionTime));
        services.AddSingleton(Options);
        services.AddSingleton<ITelemetrySessionCollection, TelemetrySessionCollection>();
        services.AddSingleton<ITelemetryStartupCollection, TelemetryStartupCollection>();
        services.AddSingleton<ClientTelemetrySweep>();
        services.AddLogging(logging => logging.AddProvider(Logs));
    }

    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();

        Storage = Silo.GetRequiredService<CountingStateStorage>();
        Collection = Silo.GetRequiredService<ITelemetrySessionCollection>();
        Startups = Silo.GetRequiredService<ITelemetryStartupCollection>();
        Sweep = Silo.GetRequiredService<ClientTelemetrySweep>();
        Orleans = Silo.GetRequiredService<IOrleans>();

        await ((TelemetrySessionCollection)Collection).OnLocalSetupCompleted(_lifetime);

        await ((TelemetryStartupCollection)Startups).OnLocalSetupCompleted(_lifetime);

        var store = new TelemetrySessionStore(Collection, Storage);
        Reader = new ClientTelemetryReader(Collection, store, new TelemetryStartupStore(Startups, Storage), Orleans);
    }

    public override async ValueTask DisposeAsync()
    {
        _lifetime.Terminate();
        await base.DisposeAsync();
    }

    // What a host gets from the cluster in a game, for a web host of a test next to the silo.
    public void AddClusterServicesTo(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton(Orleans);
        builder.Services.AddSingleton<IStateStorage>(Storage);
        builder.Services.AddSingleton(Silo.GetRequiredService<IMessaging>());
        builder.Services.AddSingleton(Silo.GetRequiredService<IGrainStatesRegistry>());
    }

    // Every notification reaches the observers of the queue.
    public async Task PumpSideEffects()
    {
        while (!(await Pipeline.PumpOnce()).IsEmpty)
        {
        }
    }

}
