using Microsoft.Extensions.Configuration;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Infrastructure.Telemetry;

// The OTLP exporter of one signal, configured by OtlpSignalExport. AddOtlpExporter per signal, not UseOtlpExporter:
// UseOtlpExporter turns on all three signals of the container, and a monolith host must export its logs only.
public static class OtlpExport
{
    // The named options of the exporters: the signals do not share one OtlpExporterOptions.
    public static string OptionsName(string signal) => $"otlp-{signal}";

    public static LoggerProviderBuilder AddOtlpExport(this LoggerProviderBuilder logs, IConfiguration configuration)
    {
        var export = OtlpSignalExport.Resolve(configuration, OtlpSignalExport.Logs);

        if (export != null)
            logs.AddOtlpExporter(OptionsName(OtlpSignalExport.Logs), export.ApplyTo);

        return logs;
    }

    public static TracerProviderBuilder AddOtlpExport(this TracerProviderBuilder traces, IConfiguration configuration)
    {
        var export = OtlpSignalExport.Resolve(configuration, OtlpSignalExport.Traces);

        if (export != null)
            traces.AddOtlpExporter(OptionsName(OtlpSignalExport.Traces), export.ApplyTo);

        return traces;
    }

    public static MeterProviderBuilder AddOtlpExport(this MeterProviderBuilder metrics, IConfiguration configuration)
    {
        var export = OtlpSignalExport.Resolve(configuration, OtlpSignalExport.Metrics);

        if (export != null)
            metrics.AddOtlpExporter(OptionsName(OtlpSignalExport.Metrics), export.ApplyTo);

        return metrics;
    }
}
