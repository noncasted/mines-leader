# Telemetry & Logging

All telemetry data is stored in `backend/.telemetry/` at the project root (gitignored).

## Directory Structure

```
backend/.telemetry/
  metrics/          — service metrics snapshots (JSON)
  logs/             — backend file logs (all levels including Trace)
  logs-games/       — game session logs (per-session files)
```

## Metrics (`backend/.telemetry/metrics/`)

**Writer:** `backend/Infrastructure/Metrics/MetricsSnapshotService.cs`

- Hosted service, writes every 10 seconds
- **Development only:** in other environments `StartAsync` does nothing (metrics go through OpenTelemetry); in
  Development no repository root (`.git`) fails the service start
- Listens to `MeterListener` for "Backend" meter
- Output: `metrics_{serviceName}.json` (e.g. `metrics_silo.json`, `metrics_metagateway.json`)
- Contains counters, histograms with min/max/avg/sum

## Backend File Logs (`backend/.telemetry/logs/`)

**Writer:** `backend/Infrastructure/Logging/FileLoggerProvider.cs`

- Registered by `ServiceDefaultsExtensions.AddFileLogging` **only in the Development environment** (Aspire);
  production logs go through OpenTelemetry only
- Options `FileLoggerOptions`, configuration section `FileLogging`: `Enabled` (default true), `Directory` (default
  `backend/.telemetry/logs` of the repository found via `.git`; no repository and no `Directory` -> the service fails
  at startup), `MinLevel` (default Trace, applied on top of the `Logging` filters)
- Log calls only enqueue a line; one background writer appends and flushes when its queue is drained, on host
  shutdown and on process exit / unhandled exception
- Output: `{serviceName}.log` (e.g. `silo.log`, `metagateway.log`)
- Format: `[timestamp] [TRC/DBG/INF/WRN/ERR/CRT] [category] message`
- Logs go to both Aspire (OpenTelemetry) and file simultaneously

## Game Session Logs (`backend/.telemetry/logs-games/`)

**Writer:** `backend/Game/Session/Logging/SessionFileLogger.cs`

- Created per game session, disposed on session end
- Output: `{date}/{sessionId}.log` (e.g. `2026-04-13/{guid}.log`)
- Logs game events: cell opens, flags, card usage, health/mana changes, bot actions, round start/end, game over

## Shared Utility

`backend/Infrastructure/TelemetryPaths.cs` — shared helper:
- `FindProjectRoot()` — walks up from `AppContext.BaseDirectory` looking for `.git` (a directory, or a
  `gitdir:` file in a git worktree / submodule)
- `GetTelemetryDir(subfolder)` — returns `backend/.telemetry/{subfolder}`, creates directory if needed; `null`
  without a repository root (session logs then fall back to `logs/sessions` next to the binary)
- `RequireTelemetryDir(subfolder)` — same path, but throws without a repository root (metrics snapshots)
