---
name: benchmark-analyse
description: Run benchmarks via API and analyse results — trends, regressions, anomalies. Use when user asks to run benchmarks, check performance, compare benchmark results, or analyse benchmark history. Also trigger on "run benchmarks", "check performance", "benchmark results", "performance regression".
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /benchmark-analyse, benchmark-analyse, анализ бенчмарков, запусти бенчмарки, проверь производительность, benchmark results, performance regression, сравни бенчмарки, run benchmarks, check performance
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Benchmark Analyse

Run benchmarks through the ConsoleGateway API and interpret results.

## Prerequisites

The Aspire cluster must be running. Use `/start-cluster` skill to start it if needed, or follow the procedure from `skills/start-cluster/SKILL.md`.

## API Base URL

`http://localhost:7103`

## Available Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/benchmarks` | List all benchmarks with last result |
| GET | `/api/benchmarks/group/{group}` | List benchmarks in group (State, Messaging, Infrastructure) |
| POST | `/api/benchmarks/{title}/run` | Run single benchmark, returns result |
| POST | `/api/benchmarks/group/{group}/run` | Run all benchmarks in group sequentially |
| GET | `/api/benchmarks/{title}/history` | Get historical results for benchmark |
| GET | `/api/benchmarks/group/{group}/history` | Get history for all benchmarks in group |

## Groups
- `State` — state read/write, transactions (13 benchmarks)
- `Messaging` — queues, channels, pipes (6 benchmarks)
- `Infrastructure` — task queues, balancers (6 benchmarks)

## Execution Steps

### Step 1 — Determine what to do

Parse the user's request:
- **"run all"** or **"run benchmarks"** → run all groups one by one
- **"run {group}"** → run specific group
- **"run {title}"** → run specific benchmark
- **"analyse"** or **"check"** → fetch history only, no new run
- **"compare"** → fetch history, look for trends

### Step 2 — Check cluster availability and start if needed

Use Bash to check if the API is reachable:
```bash
curl -s -o /dev/null -w "%{http_code}" http://localhost:7103/api/benchmarks
```

If not 200 or 503, start the cluster yourself:
1. Launch in background: `dotnet run --project backend/Orchestration/Aspire/Aspire.csproj --launch-profile http` (run_in_background=true)
2. Poll for readiness: loop `curl` every 5s until 200 (up to 2 min)
3. If still not ready after 2 min, check the background task output for errors and report to user

If 503 (cluster initializing), just poll until 200.

### Step 3 — Run benchmarks (if requested)

Use Bash with curl. Benchmarks can take 5-60 seconds each, so set appropriate timeout.

```bash
# Run single benchmark
curl -s -X POST http://localhost:7103/api/benchmarks/{title}/run --connect-timeout 5 --max-time 120

# Run group
curl -s -X POST http://localhost:7103/api/benchmarks/group/{group}/run --connect-timeout 5 --max-time 600
```

Report each result as it completes. Show a summary table after all benchmarks finish.

### Step 4 — Fetch history

```bash
# Single benchmark history
curl -s http://localhost:7103/api/benchmarks/{title}/history

# Group history
curl -s http://localhost:7103/api/benchmarks/group/{group}/history
```

### Step 5 — Analyse and report

Present results in a readable table format:

```
## Результаты бенчмарков — {Group}

| Benchmark | Result | Metric | Duration | Status |
|-----------|--------|--------|----------|--------|
| state     | 12345  | ops/s  | 3200ms   | OK     |

## Анализ трендов (последние N запусков)

| Benchmark | Current | Previous | Delta | Trend |
|-----------|---------|----------|-------|-------|
| state     | 12345   | 11800    | +4.6% | stable |
```

### Analysis criteria:

- **Regression**: current value < previous by more than 10% → flag as regression
- **Improvement**: current value > previous by more than 10% → flag as improvement
- **Stable**: within +/-10% → stable
- **Anomaly**: single run deviates from last 5 runs average by >25%
- **Trend**: if 3+ consecutive runs show monotonic decrease → "degrading trend"

### Step 6 — Recommendations

Based on analysis, provide:
- Which benchmarks regressed and by how much
- Possible causes (if user made recent code changes — check git log)
- Whether the regression is statistically significant (compare against variance in history)
- Suggested next steps (re-run to confirm, investigate specific area)

## Output Language

All prose output in Russian. Benchmark names and metrics in English.
