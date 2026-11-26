using Common.Extensions;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Exporter;

namespace Infrastructure.Telemetry;

// Where the OTLP exporter of one signal (logs, traces, metrics) sends, from the configuration of the host. The
// exporter of every signal is added on its own (AddOtlpExporter), which reads only the OTEL_EXPORTER_OTLP_* variables
// of all signals; the variables of one signal are applied here, so the logs and the traces may go to different
// receivers without a router in front of them.
public record OtlpSignalExport
{
    public const string Logs = "logs";
    public const string Traces = "traces";
    public const string Metrics = "metrics";

    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";

    // Null: the exporter takes OTEL_EXPORTER_OTLP_ENDPOINT, _PROTOCOL and _HEADERS itself and appends the path of
    // the signal for http/protobuf.
    public Uri? Endpoint { get; init; }
    public OtlpExportProtocol? Protocol { get; init; }

    // Null: the signal is not exported (no endpoint configured, as a host without a collector).
    public static OtlpSignalExport? Resolve(IConfiguration configuration, string signal)
    {
        // The connection string of the AppHost is a base url of http/protobuf, as UseOtlpExporter(protocol, uri) took it.
        var connection = configuration.GetConnectionString(ConnectionNames.OpenTelemetry);

        if (connection != null)
        {
            return new OtlpSignalExport
            {
                Endpoint = new Uri($"{connection.TrimEnd('/')}/v1/{signal}"),
                Protocol = OtlpExportProtocol.HttpProtobuf
            };
        }

        var prefix = $"OTEL_EXPORTER_OTLP_{signal.ToUpperInvariant()}";
        var signalEndpoint = configuration[$"{prefix}_ENDPOINT"];

        // The endpoint of a signal is the full url, used as it is (the OTLP specification).
        if (string.IsNullOrWhiteSpace(signalEndpoint) == false)
        {
            var signalProtocol = configuration[$"{prefix}_PROTOCOL"];

            return new OtlpSignalExport
            {
                Endpoint = new Uri(signalEndpoint),
                Protocol = signalProtocol == null ? null : ParseProtocol(signalProtocol)
            };
        }

        if (string.IsNullOrWhiteSpace(configuration[EndpointKey]) == false)
            return new OtlpSignalExport();

        return null;
    }

    public void ApplyTo(OtlpExporterOptions options)
    {
        if (Endpoint != null)
            options.Endpoint = Endpoint;

        if (Protocol != null)
            options.Protocol = Protocol.Value;
    }

    private static OtlpExportProtocol ParseProtocol(string value)
    {
        return value switch
        {
            "grpc" => OtlpExportProtocol.Grpc,
            "http/protobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => throw new InvalidOperationException($"Unknown OTLP protocol {value}: grpc or http/protobuf")
        };
    }
}
