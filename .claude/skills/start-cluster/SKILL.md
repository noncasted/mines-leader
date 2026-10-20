---
name: start-cluster
description: Start the backend Aspire cluster. Use when any task requires a running cluster — benchmarks, tests, API calls, telemetry analysis. Also trigger on "start cluster", "start backend", "start server", "run cluster".
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /start-cluster, start-cluster, запусти кластер, start cluster, start backend, start server, run cluster, запусти бэкенд, запусти сервер
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Start Cluster

Start the Aspire backend cluster and wait for it to be ready.

## Launch Command

```bash
dotnet run --project backend/Orchestration/Aspire/Aspire.csproj --launch-profile http
```

IMPORTANT: `aspire run` does NOT support `--launch-profile` (known issue). Always use `dotnet run` instead.

The `http` profile is the first entry in `launchSettings.json` and is what both local runs and the Coolify deploy use. All service-to-service transport is plain HTTP — no pfx/dev-cert is needed anywhere.

## Procedure

1. Check if cluster is already running:
```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:7103/api/benchmarks 2>/dev/null
```
If returns `200` — cluster is already running, skip startup.

2. Start in background:
```bash
dotnet run --project backend/Orchestration/Aspire/Aspire.csproj --launch-profile http 2>&1 &
```
Use `run_in_background=true` for the Bash tool.

3. Poll until API is ready (up to 2 minutes):
```bash
for i in $(seq 1 24); do
  code=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:7103/api/benchmarks 2>/dev/null)
  if [ "$code" = "200" ]; then echo "READY"; break; fi
  sleep 5
done
```

4. If cluster fails to start, check the background process output and report the error to the user.

## Stopping the Cluster

```bash
pkill -f "Aspire.dll"
```

## Key Ports

| Service | Port |
|---------|------|
| ConsoleGateway (API + UI) | 7103 |
| GameGateway | 7102 |
| MetaGateway | 7101 |
| Coordinator | 6001 |
| Silo (Orleans gateway HTTP) | 6002 |
| Aspire Dashboard | 7100 |
| PostgreSQL | 9432 |

## Requirements

- PostgreSQL must be running on port 9432 (Aspire creates it via Docker container)
- Docker must be available for the PostgreSQL container
