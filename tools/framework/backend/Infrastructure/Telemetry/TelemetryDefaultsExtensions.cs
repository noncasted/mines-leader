using Common.Extensions;
using Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Telemetry;

public static class TelemetryDefaultsExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        // Logs, traces and metrics of a host over OTLP (OtlpSignalExport), named by its configuration
        // (TelemetrySettings). A separate service owns its three providers. A host of a monolith
        // (Telemetry:ProcessProviders) owns only its logger provider: its sources and meters go to the providers of
        // the process (MonolithTelemetry of the cluster), which read the AddTraceSource / AddBackendMeter
        // registrations of the host; a source added to a tracer provider any other way is not exported there.
        public IHostApplicationBuilder AddTelemetryDefaults()
        {
            var configuration = builder.Configuration;
            var settings = TelemetrySettings.Read(configuration, builder.Environment.ApplicationName);

            builder.Logging.SetMinimumLevel(LogLevel.Trace);
            builder.AddFileLogging(settings.LogName);

            builder.Logging.AddTelemetryLogs();

            // The resource is set per signal, as the providers of a monolith host and of its process differ.
            var telemetry = builder.Services.AddOpenTelemetry()
                                   .WithTelemetryLogs(settings, configuration);

            // The source of the host itself, registered like the sources of the features.
            builder.AddTraceSource(builder.Environment.ApplicationName);

            if (settings.UsesProcessProviders)
                return builder;

            telemetry.WithTelemetryTracing(settings, configuration, _ => { });

            if (settings.ExportsMetrics)
                telemetry.WithTelemetryMetrics(settings, configuration, _ => { });

            return builder;
        }
    }
}
