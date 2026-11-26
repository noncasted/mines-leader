using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;

namespace Common.Extensions;

// The meter every feature of the backend creates its instruments on; the features declare the instruments next to
// themselves and call AddBackendMeter when they are registered, so this class lists none of them.
public static class BackendMeter
{
    public const string Name = "Backend";

    public static readonly Meter Instance = new(Name);

    extension(IHostApplicationBuilder builder)
    {
        // Applied by the meter provider of AddOpenTelemetry whenever it is called; repeated calls are harmless. The
        // registration is kept for the meter provider of a monolith process too (MeterRegistration).
        public IHostApplicationBuilder AddBackendMeter()
        {
            builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(Name));
            builder.Services.AddSingleton(new MeterRegistration(Name));
            return builder;
        }
    }
}
