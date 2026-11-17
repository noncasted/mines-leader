# Result
## Framework (tools/framework, uncommitted)
ClientTelemetry: Connections/ClientTelemetryConnection.cs, Connections/ClientTelemetryStartups.cs, Contract/TelemetryStartupTrace.cs, Contract/CONTRACT.md, Startups/TelemetryStartupState.cs, Startups/TelemetryStartupInfo.cs, Http/ClientTelemetryHttpIntake.cs; Console/Infrastructure/ClientTelemetry/TelemetryStartupPage.razor; Tests/ClientTelemetry (StartupTests, ClosedSocketStartupTests, SweepMissedNotificationTests, TelemetryMetaSocket); .claude/skills/client-telemetry/{report.py,SKILL.md}; releases/unreleased.md.
## Game (uncommitted)
MetaGateway/UserFlow/Connection: MetaConnectionTelemetry.cs (new), MetaConnectTrace.cs (new), BackendConnectionMiddleware.cs, UserSession.cs; UserFlow/UserFlowExtensions.cs; UserFlow/Commands/ClientTelemetryCommands.cs; Tools/Tests: Meta/Telemetry/ClientTelemetryCommandsTests.cs, Meta/Telemetry/MatchMetricsCommandsTests.cs, Network/MetaSocketHandshakeTests.cs; .claude/docs/TELEMETRY.md.
## Evidence
Framework.Tests ClientTelemetry 452/0 failed; backend.slnx build 0 errors; game tests 30/30 + new handshake test; live: startup 4eac7d55-... backendTraceId 1f3d84b522c4413c10965c00928b3fee == Aspire trace "Meta connect".
