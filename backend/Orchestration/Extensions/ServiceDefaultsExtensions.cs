using Infrastructure;
using Infrastructure.Telemetry;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Shared;

namespace Orchestration;

public static class ServiceDefaultsExtensions
{
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        // The INetworkContext union of the game: registered before any socket reads a message.
        NetworkProtocol.Register();

        builder.AddTelemetryDefaults();
        builder.AddDefaultHealthChecks();

        var services = builder.Services;

        services.AddServiceDiscovery();

        services.ConfigureHttpClientDefaults(http => {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    extension(IHostApplicationBuilder builder)
    {
        private void AddDefaultHealthChecks()
        {
            builder.Services.AddHealthChecks()
                   .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"])
                   .AddCheck<OrleansReadyHealthCheck>("orleans", tags: ["ready"]);
        }
    }

    public static void MapDefaultEndpoints(this WebApplication app)
    {
        app.MapHealthChecks(TelemetryInstrumentation.HealthEndpointPath);

        app.MapHealthChecks(TelemetryInstrumentation.AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("live")
        });

        app.MapHealthChecks(TelemetryInstrumentation.ReadyEndpointPath, new HealthCheckOptions
        {
            Predicate = r => r.Tags.Contains("ready")
        });
    }
}