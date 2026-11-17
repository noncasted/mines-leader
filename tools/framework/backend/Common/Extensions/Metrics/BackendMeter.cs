using System.Diagnostics.Metrics;
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
        // Applied by the meter provider of AddOpenTelemetry whenever it is called; repeated calls are harmless.
        public IHostApplicationBuilder AddBackendMeter()
        {
            builder.Services.ConfigureOpenTelemetryMeterProvider(metrics => metrics.AddMeter(Name));
            return builder;
        }
    }
}
