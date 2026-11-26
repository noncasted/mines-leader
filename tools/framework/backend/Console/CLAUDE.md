# Console

A Razor class library: the project keeps its home page and game pages in its own assembly (add both
assemblies to the router), sets role icons (`ServiceRoleIcons`), registers game flags (`AddConsoleGameFlag`) and
extra infrastructure cards (`AddConsoleInfrastructureCard`, shown on `/infrastructure`), puts `HomeClusterSection` (Features,
Client Telemetry) and `OnlineWidget` (online of the coordinator's `AddOnlineTracker`, page `/online`) on its home, and adds `tools/framework/backend/Console` to the
Tailwind content paths. The `app.css` of a game is committed and built by hand, so no build of the framework refreshes it: a page
of the framework uses only the classes that are in the `app.css` of every game. The infrastructure pages need
`AddConsoleInfrastructure(ConsoleInfrastructureOptions)` (`CoordinatorConfigFolder`: the folder of the coordinator configs, no
Write JSON button without it) and the table of the audit log, created by the deploy setup with `AuditLogTable.Create(connection)`.

## Backend traces

The startup page of a client draws the backend trace of the authorization of its socket (`BackendTraceId`, the lowercase hex
of `ActivityTraceId`) through one `IBackendTraceSource`. A game picks the source by the extension its console calls, never by a
setting inside the framework; without a call the page shows the id alone, a second call throws.

| Variant | Call | Section of the game | Store |
|---|---|---|---|
| `BackendTraces/AspireDashboard` | `AddConsoleAspireDashboardTraces(AspireDashboardTraceOptions \| IConfigurationSection)` | `AspireDashboard`: `Url`, `ApiKey` | the HTTP API of the Aspire dashboard (local cluster): `GET api/telemetry/traces/{id}`, `x-api-key`; telemetry in memory only |
| `BackendTraces/VictoriaTraces` | `AddConsoleVictoriaTraces(VictoriaTracesOptions \| IConfigurationSection)` | `BackendTraces`: `Url` (`http://victoriatraces:10428` in the network `observability`) | the Jaeger query API of VictoriaTraces v0.13 (deploy): `GET select/jaeger/api/traces/{id}`, no key; a trace is there 20-30 s after its last span |

Both options take `Timeout` (10 s); a missing `Url` or `ApiKey` of a section throws at the start. A URL under a path keeps the
path. 200 is parsed (another shape throws: the contract of the store has changed), 404 is `NotFound` with the reason of the store,
another status, a network failure or a timeout is `Failed` with its message. A new store is a new folder with its reader and
parser into `BackendTraceWireSpan`; the tree (`BackendTraceAssembler`), the HTTP boundary (`BackendTraceHttp`) and the
registration (`BackendTraceRegistration`) are shared.
