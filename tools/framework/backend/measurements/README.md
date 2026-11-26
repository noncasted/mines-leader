# Backend memory measurements

How much memory the backends take: on the Coolify hosts, before and after a change of the deploy (the monolith of the
cluster, the observability stack instead of the Aspire dashboard, GC settings), and in a local Release run of the same
deploy compose, where every variant runs under the same conditions. The numbers go into [RESULTS.md](RESULTS.md) and into
the release notes of the change.

## Repeat

Production, read-only over ssh (the hosts and their aliases: `deploy/registry.json`, `servers`):

```bash
backend/measurements/tools/snapshot.sh backend/measurements/results/<date>-<label> firstvds ovh
backend/measurements/tools/prod_report.py backend/measurements/results/<date>-<label>            # tables per host
backend/measurements/tools/prod_report.py --summary backend/measurements/results/<date>-<label>  # one row per project
```

Coolify itself on both hosts: `SNAPSHOT_FILTER=name=coolify backend/measurements/tools/snapshot.sh <out> ovh firstvds`.

Locally, every variant of a project at the head of its deploy branch (~7 minutes per variant, the first build longer):

```bash
backend/measurements/tools/local.sh backend/measurements/results/<date>-local pixel-swarm
backend/measurements/tools/local.sh <out> pixel-swarm monolith/workstation services/server         # chosen variants
LOCAL_REF=<full commit hash> backend/measurements/tools/local.sh <out> pixel-swarm monolith/workstation      # another commit
```

`local.sh` checks out the deploy branch (`registry.json`: `repository`, `branch`) into `.build/measurements/<project>`,
turns the deploy compose into a run of its own (`local_compose.py`: own Postgres, own networks and volumes, no published
ports, `cpus` of the VPS, the GC variant), builds it in Release on the host network, waits for every healthcheck, leaves it
idle for `LOCAL_IDLE` seconds (300), snapshots its containers with `snapshot.sh local:<variant>`, removes its containers
and volumes and prints the summary. The images stay as `backend-measurements/<service>:local` for the next run
(`docker image rm $(docker images -q 'backend-measurements/*')` removes them). Variants:

| Part | Values |
|---|---|
| layout | `services`, `monolith`: the compose file of each is `compose` of the project in `deploy/registry.json` (pixel-swarm: `backend/Tools/deploy/docker-compose.yaml`, `docker-compose.monolith.yaml`) |
| gc | `server` (the default of the Web SDK, DATAS), `workstation` (`DOTNET_gcServer=0`), `conserve` (`DOTNET_gcServer=0`, `DOTNET_GCConserveMemory=5`) |
| telemetry | from the source: `aspire` when its compose runs `aspire-dashboard`, `victoria` when it exports into the stack of the host (network `observability`); the run then starts `deploy/observability` of this tree next to it |

The load scenario is not automated: pixel-swarm has no headless bots or match runner, and its console benchmarks run only
in Development against a local database (`BenchmarksAvailability`), so they cannot load the Release compose.

## What the columns mean (MiB)

| Column | Source | Meaning |
|---|---|---|
| stats | `memory.current - inactive_file` of the cgroup | what `docker stats` and Coolify show |
| anon | `anon` of `memory.stat` | private memory in RAM: GC heaps, JIT code, native allocations |
| swap | `memory.swap.current` | private pages pushed to swap; `docker stats` does not show them |
| cost | anon + swap | what the container costs the host besides the shared page cache; compare this one |
| pss | `Pss` of `/proc/<pid>/smaps_rollup`, summed over the processes | resident pages, the shared ones split between the processes that map them (RSS counts them twice) |

A project is grouped by the Coolify label `coolify.projectName` (a local run sets it); `dotnet` is the sum of the roles
(silo, coordinator, meta, game, console, or the one `backend` of the monolith), `observability` the stack (otlp,
victorialogs, victoriatraces). Compare snapshots taken under the same conditions: a fresh deploy has not grown yet, a
container that ran for weeks has its idle pages in swap.

## Layout

| Path | What |
|---|---|
| `tools/snapshot.sh` | the read-only snapshot of the hosts or of this machine (`local:<name>`, `SNAPSHOT_FILTER`) |
| `tools/local.sh` | the local run of a project: checkout, compose, build, idle, snapshot, cleanup |
| `tools/local_compose.py` | the env file and the compose of a local run, from the deploy compose of the game |
| `tools/prod_report.py` | the tables of a snapshot folder, `--summary` of several |
| `tools/tests/` | the tests of the python tools (`tools/scripts/test-all.sh`, stage `measurements`) |
| `results/<date>-<label>/` | raw snapshots: `<host or variant>.txt` (free, stats, labels, smaps), `<...>.cgroup.txt` (cgroup memory); a local run adds `conditions.info` (source commit, idle, cpus, machine) |
