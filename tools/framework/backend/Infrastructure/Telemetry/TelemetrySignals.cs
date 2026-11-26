using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Infrastructure.Telemetry;

// The shape of the three signals, the same for a separate host (TelemetryDefaultsExtensions) and for the process of a
// monolith (MonolithTelemetry of the cluster): the record options of the logs, the resource named by the settings,
// the default instrumentation and the OTLP export.
public static class TelemetrySignals
{
    public static ILoggingBuilder AddTelemetryLogs(this ILoggingBuilder logging)
    {
        return logging.AddOpenTelemetry(options => {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });
    }

    public static OpenTelemetryBuilder WithTelemetryLogs(
        this OpenTelemetryBuilder telemetry,
        TelemetrySettings settings,
        IConfiguration configuration)
    {
        return telemetry.WithLogging(logs => logs.ConfigureResource(settings.ApplyTo)
                                                 .AddOtlpExport(configuration));
    }

    // The sources of the features are added by the caller (configure), next to the default instrumentation.
    public static OpenTelemetryBuilder WithTelemetryTracing(
        this OpenTelemetryBuilder telemetry,
        TelemetrySettings settings,
        IConfiguration configuration,
        Action<TracerProviderBuilder> configure)
    {
        return telemetry.WithTracing(tracing => {
            configure(tracing.ConfigureResource(settings.ApplyTo)
                             .AddDefaultInstrumentation()
                             .AddOtlpExport(configuration));
        });
    }

    // The meters of the features are added by the caller (configure), next to the default instrumentation.
    public static OpenTelemetryBuilder WithTelemetryMetrics(
        this OpenTelemetryBuilder telemetry,
        TelemetrySettings settings,
        IConfiguration configuration,
        Action<MeterProviderBuilder> configure)
    {
        return telemetry.WithMetrics(metrics => {
            configure(metrics.ConfigureResource(settings.ApplyTo)
                             .AddDefaultInstrumentation()
                             .AddOtlpExport(configuration));
        });
    }
}
