# Client telemetry (backend)

`backend/ClientTelemetry`, off until the switch on the Features page of the console. The game calls
`AddClientTelemetrySessions` on the silo, `AddClientTelemetry` on the meta gateway and `AddClientTelemetryReader` on the console (the
panel appears from it, no other registration), maps `MapClientTelemetryRead().RequireConsoleToken()` and keeps the commands of its
meta socket over `IClientTelemetryConnections.Of(session.Lifetime)`: `Start`, `Accept`, `End`, `AcceptStartup`; the id of the user
stays in the game. A client without the meta socket posts JSON: the game maps `MapClientTelemetryHttpIntake()` on its own group with its
authorization (`/start`, `/batch`, `/end`, `/startup`; a link id of the client is its socket, ended by `ClientTelemetryHttpOptions.IdleAfter`,
60 s, below `LostAfter`). A session holds the previous one its client did not close (`previous`, found back by `sessions?previousId=`);
warnings are counted apart from the errors (`warningCount`). The test host runs without a game: `--token <value>` (required), `--seed`, `--console <folder>` (`theme.css` and
`app.css` of a console of a game: its pages on this host), `--no-telemetry` (the console of a game without the telemetry).
`AddConsoleAspireDashboardTraces` or `AddConsoleVictoriaTraces` on the console (optional, `backend/Console/CLAUDE.md` § Backend traces)
reads the `BackendTraceId` of a startup and draws it on the startup page; without it the page shows the id.

What the client sends: `client/modules/telemetry/CLAUDE.md`. The host and the end to end tests of the transport:
`backend/ClientTelemetry.TestHost/CLAUDE.md`.
