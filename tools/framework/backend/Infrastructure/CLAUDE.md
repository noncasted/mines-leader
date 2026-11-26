# Infrastructure

## Telemetry defaults (`Telemetry/`)

`builder.AddTelemetryDefaults()` in the `AddServiceDefaults` of the game, once per host: log level, the dev file log
(`AddFileLogging`, `Logging/`), OpenTelemetry logs, traces and metrics over OTLP, the default instrumentation
(ASP.NET Core without the health paths `/health`, `/alive` and `/ready` of `TelemetryInstrumentation`, which a game maps
from there and checks with `IsHealthCheck`, HttpClient, runtime, the `Microsoft.Orleans` meter) and the trace source
named after `ApplicationName`. The sources and meters of the features come from `AddTraceSource` / `AddBackendMeter`.

Read from the configuration of the host, never from `Environment` (`TelemetrySettings`):

| Key | Meaning |
|-----|---------|
| `SERVICE_NAME` | `service.name` of the resource and the name of the dev log; absent: the SDK keeps `OTEL_SERVICE_NAME` (Aspire) |
| `SERVICE_INSTANCE_ID`, `HOSTNAME` | `service.instance.id`, else the service name |
| `Telemetry:ExportMetrics` | `false`: no meter provider (a stack without metrics); default `true` |
| `Telemetry:ProcessProviders` | set by the monolith runner: the host owns only its logger provider and writes no dev metrics snapshot (`backend/Cluster/CLAUDE.md` § Monolith) |

OTLP endpoint of each signal (`OtlpSignalExport`), first match wins:
1. `ConnectionStrings:otel` (the AppHost): a base url, `http/protobuf`, `/v1/<signal>` appended.
2. `OTEL_EXPORTER_OTLP_<LOGS|TRACES|METRICS>_ENDPOINT`: the full url of that signal, used as it is, with
   `..._<SIGNAL>_PROTOCOL` when set (`grpc` | `http/protobuf`, else the protocol of all signals).
3. `OTEL_EXPORTER_OTLP_ENDPOINT`: the exporter reads it with `_PROTOCOL` and `_HEADERS` itself (the local Aspire dashboard).
4. Nothing: the signal is not exported.

The exporter of each signal is `AddOtlpExporter` with its named options (`otlp-logs`, `otlp-traces`, `otlp-metrics`), not
`UseOtlpExporter`: that one turns on all three signals of the container and cannot be combined with a per-signal
exporter. `AddOtlpExporter` itself ignores the per-signal variables, step 2 applies them, so logs and traces can go
straight to two receivers (VictoriaLogs, VictoriaTraces) without a router. Per-signal headers are not read.
