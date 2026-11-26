using Microsoft.Extensions.Configuration;
using OpenTelemetry.Resources;

namespace Infrastructure.Telemetry;

// What the telemetry of one host (or of the monolith process) is named and exports, read from its configuration,
// never from Environment: the hosts of a monolith share the variables of the process, the runner gives each host its
// own values on the command line, which win over the variables.
public record TelemetrySettings
{
    // service.name of the resource (the role of a deploy: silo, meta, ...). Absent: the resource keeps the name the
    // SDK finds itself (OTEL_SERVICE_NAME of the Aspire AppHost).
    public const string ServiceNameKey = "SERVICE_NAME";

    public const string ServiceInstanceKey = "SERVICE_INSTANCE_ID";
    public const string HostNameKey = "HOSTNAME";

    // Set by the monolith runner on every host: the tracer and meter providers belong to the process (the sources
    // and meters are static, a provider per host would export every span once per host), the host exports its logs.
    public const string ProcessProvidersKey = "Telemetry:ProcessProviders";

    // False: no meter provider at all (a stack that takes no metrics). The dev snapshots of MetricsSnapshotService
    // listen to the meter themselves and stay.
    public const string ExportMetricsKey = "Telemetry:ExportMetrics";

    public string? ServiceName { get; init; }
    public required string InstanceId { get; init; }

    // The name of the dev log file: the service name or the application name.
    public required string LogName { get; init; }

    public required bool UsesProcessProviders { get; init; }
    public required bool ExportsMetrics { get; init; }

    public static TelemetrySettings Read(IConfiguration configuration, string applicationName)
    {
        var serviceName = configuration[ServiceNameKey];
        var logName = serviceName ?? applicationName.ToLowerInvariant();

        return new TelemetrySettings
        {
            ServiceName = serviceName,
            InstanceId = configuration[ServiceInstanceKey] ?? configuration[HostNameKey] ?? logName,
            LogName = logName,
            UsesProcessProviders = configuration.GetValue(ProcessProvidersKey, false),
            ExportsMetrics = configuration.GetValue(ExportMetricsKey, true)
        };
    }

    public void ApplyTo(ResourceBuilder resource)
    {
        if (ServiceName == null)
            return;

        resource.AddService(ServiceName, autoGenerateServiceInstanceId: false, serviceInstanceId: InstanceId);
    }
}
