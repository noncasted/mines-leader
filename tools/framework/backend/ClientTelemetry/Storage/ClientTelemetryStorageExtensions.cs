using Common.Extensions;
using Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Game.ClientTelemetry;

// The state tables come from the deploy setup of the game (StatesLookup), like any state.
public static class ClientTelemetryStorageExtensions
{
    // The silo that activates the grain of a session.
    public static IHostApplicationBuilder AddClientTelemetrySessions(this IHostApplicationBuilder builder, ClientTelemetryStorageOptions? options = null)
    {
        builder.AddSessionList(options);
        builder.Add<ClientTelemetrySweep>().As<ILocalSetupCompleted>();

        return builder;
    }

    // A host that only reads and holds the switch: the console.
    public static IHostApplicationBuilder AddClientTelemetryReader(this IHostApplicationBuilder builder, ClientTelemetryStorageOptions? options = null)
    {
        builder.AddSessionList(options);
        builder.Services.AddSingleton<TelemetrySessionStore>();
        builder.Services.AddSingleton<TelemetryStartupStore>();
        builder.Services.AddSingleton<IClientTelemetryReader, ClientTelemetryReader>();
        builder.AddAddressableState<ClientTelemetrySwitch>().As<IClientTelemetrySwitch>();

        return builder;
    }

    // The gateway of the clients: the telemetry of its sockets, the links of MapClientTelemetryHttpIntake and the reading. A game
    // with other ClientTelemetryHttpOptions registers them before this call.
    public static IHostApplicationBuilder AddClientTelemetry(this IHostApplicationBuilder builder, ClientTelemetryStorageOptions? options = null)
    {
        builder.AddClientTelemetryReader(options);
        builder.Services.AddSingleton<IClientTelemetryConnections, ClientTelemetryConnections>();
        builder.Services.TryAddSingleton(new ClientTelemetryHttpOptions());
        builder.Services.AddSingleton<ClientTelemetryHttpLinks>();

        return builder;
    }

    private static void AddSessionList(this IHostApplicationBuilder builder, ClientTelemetryStorageOptions? options)
    {
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<TelemetrySessionClock>();
        builder.Services.AddSingleton(options ?? new ClientTelemetryStorageOptions());
        builder.AddStateCollection<TelemetrySessionCollection, Guid, TelemetrySessionState>().As<ITelemetrySessionCollection>();
        builder.AddStateCollection<TelemetryStartupCollection, Guid, TelemetryStartupState>().As<ITelemetryStartupCollection>();
    }
}
