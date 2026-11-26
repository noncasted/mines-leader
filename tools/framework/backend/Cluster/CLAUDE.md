# Cluster

Service roles are strings: the project declares its `ServiceTag`s and the required set
(`ConfigureRequiredServices`); a game server calls `PublishGameServerUrl`. Game configs: `AddAddressableState` +
an `ICoordinatorSetupCompleted` with `ConfigsInitializer.Init(environment, "config.<name>", state, logger)`: the file
`config.<name>.json` lies in the content root of the host (`IHostEnvironment.ContentRootPath`), a missing one throws.
Game switches: `IClusterFeatures.SetGameFlag` / `IsGameFlagEnabled`. A replaced coordinator leaves its deploy (`IDeployContext.Leave()`; a host follows the deploy id pipe only to a deploy
created after its own, `DeployState.CreatedAt`): what runs on `DeployLifetime`
stops there (`OnlineTracker`, channel listeners); the rows of a deploy are written through `DeploymentStateRow.Change`.
A feature registers its own meter and trace source (`AddBackendMeter`, `AddTraceSource`) in its `Add*`; a source added
to a tracer provider any other way is lost in a monolith. Dev metrics snapshots: `builder.AddMetricsSnapshots()`
(`Monitoring/`, the only way to register `MetricsSnapshotService`) writes `metrics_<role>.json` by `IServiceEnvironment.Tag`
in Development; in a monolith the hosts write none and the process writes one (§ Monolith).

## Monolith (`Monolith/`)

Several hosts of the cluster in one process, for test deploys; the separate services stay the normal mode. Each host
keeps its own container, `IServiceEnvironment`, discovery entry, port and content root, so the discovery and the
coordinator do not change; Orleans clients reach the silo over loopback. The roles are the game's:

```csharp
// Monolith/Program.cs of the game: the same factories the Program.cs of the separate services call.
await MonolithRunner.Create(args, [
    new MonolithHost { Tag = ServiceTag.Coordinator, ApplicationName = typeof(CoordinatorHost).Assembly.GetName().Name!, Build = CoordinatorHost.Build },
    new MonolithHost { Tag = GameRoles.Meta, ApplicationName = typeof(MetaHost).Assembly.GetName().Name!, Build = MetaHost.Build },
    ...
]).Run(CancellationToken.None);
```

- Configuration of the process: `appsettings.json` next to the binaries, the variables, the command line.
  `Monolith:Hosts:<Tag>:Port` (required) and `Monolith:Hosts:<Tag>:ContentRoot` (default `hosts/<Tag>` next to the
  binaries, must exist: the `appsettings*.json` and `config.*.json` of the role). No shared `ASPNETCORE_URLS`.
- The runner builds every host first, then runs all; it gives each host `--urls=http://+:<port>`,
  `--SERVICE_NAME=<tag in lower case>` and `--Telemetry:ProcessProviders=true` on the command line (they win over
  the variables of the process), `ApplicationName`, `ContentRootPath` and `WebRootPath` (`wwwroot` next to the binaries:
  the publish of the executable puts the static web assets of all hosts there) in `WebApplicationOptions`.
- `ApplicationName` must name an assembly of the process: ASP.NET Core loads it as a hosting startup assembly and logs a
  critical `Hosting startup assembly exception` when it is missing (the exe name of the separate service is not one). The
  library of the role, or the executable for a host whose static web assets `MapStaticAssets` reads: it finds
  `<ApplicationName>.staticwebassets.endpoints.json`, which only the executable has.
- The runner owns the stop of the process: SIGTERM / SIGINT / SIGQUIT (`PosixSignalRegistration`, the runtime does not
  end the process) and the token of `Run`; then every host stops gracefully and `Run` returns (exit 0). The hosts
  handle no signal: the runner names its assembly in `--hostingStartupAssemblies`, and `MonolithHostingStartup` replaces
  the `ConsoleLifetime` of each host with `MonolithHostLifetime`.
- The first host that fails or stops without being asked to (`StopApplication` of its own, e.g. a coordinator that
  gave up its deploy) stops the others; `Run` throws the failure of the host that failed or began to stop first (logged
  critical): a non-zero exit, the deploy restarts the container. A host stopped by a failed `BackgroundService`
  (`BackgroundServiceExceptionBehavior.StopHost`, e.g. `ClusterParticipantStartup`) throws the exception of that service,
  one that called `StopApplication` itself `InvalidOperationException("host <tag> stopped by itself")`. "First" is when
  the host began to stop, not when it ended: a host failing on the absence of another (a coordinator without the grains
  of the silo) may end before it. No shared `/ready`: the healthcheck of the deploy checks `/ready` of every port.
- `NoAwaitExceptions.Handler` is set by the runner before the hosts start (to the logger of the process).
- Per process (`MonolithTelemetry`): one tracer provider, one meter provider (none with `Telemetry:ExportMetrics=false`)
  and the logger of the runner; `service.name` = `SERVICE_NAME` of the process (the deploy name). The sources and meters
  are the `AddTraceSource` / `AddBackendMeter` registrations of all hosts plus the default instrumentation: the sources
  are static and heard in the whole process, a provider per host would export every span once per host.
- Dev metrics snapshot: one per process, `metrics_<SERVICE_NAME of the process>.json`, when a host in Development has
  `AddMetricsSnapshots`, written every 10 s and when the process stops; the `MetricsSnapshotService` of a host with
  `Telemetry:ProcessProviders` does nothing (`BackendMeter` is static: a snapshot per host would hold the totals of all roles).
- Per host: the logger provider, `service.name` of the logs = the role. So in a monolith the spans and metrics carry the
  deploy name, the logs the role; in separate services all three carry the role.
- The OTel defaults themselves (`AddTelemetryDefaults`, the OTLP endpoints): `backend/Infrastructure/CLAUDE.md`.
