# Coolify deployment

Production deploy is driven by `backend/Tools/deploy/docker-compose.yaml`. Coolify uses its own Docker host to build and run the stack — **no nested dockerd, no privileged mode, no `aspire run`**. Each service is a separate Release-built container; Coolify Traefik terminates TLS and routes per-service domains.

## Runtime topology

```
Coolify host Docker + Traefik (Let's Encrypt TLS)
├── migrator        (one-shot init — creates tables, then exits)
├── pgbouncer       (edoburu/pgbouncer, DB_HOST=<external Postgres>)
├── silo            (Orleans silo, Postgres clustering)
├── coordinator     (DeployIdentity + cluster tests)
├── meta            (public: meta.<domain>)
├── game            (public: game.<domain>)
├── console         (public: console.<domain>)
└── aspire-dashboard (optional, --profile with-dashboard; public: aspire.<domain>)
```

All services share a single compose-network. Internal DNS names (`silo`, `coordinator`, `pgbouncer`, …) resolve automatically.

External Postgres — Coolify-managed resource, reached by its Coolify-internal hostname via `pgbouncer` service.

## Build model

Build context is repo root (`.`). One shared `backend/Orchestration/Dockerfile` publishes each service from source:

```
docker build --build-arg PROJECT_PATH=backend/Orchestration/Silo/Silo.csproj \
             --build-arg ASSEMBLY_NAME=Silo \
             -f backend/Orchestration/Dockerfile .
```

`dotnet publish -c Release /p:UseAppHost=false`, runtime image `mcr.microsoft.com/dotnet/aspnet:10.0`. Cache-mount for `~/.nuget/packages` keeps subsequent builds fast.

A second Dockerfile `backend/Orchestration/Dockerfile.prebuilt` + `docker-compose.local.yaml` are for local smoke-tests only (publish on host via `tools/scripts/publish-local.sh`, Docker only copies). Coolify uses the full self-contained `Dockerfile`.

## Coolify application settings

### General
- **Build Pack:** `Docker Compose`
- **Base Directory:** `/`
- **Docker Compose Location:** `/backend/Tools/deploy/docker-compose.yaml`
- **Custom Docker Options:** *(empty — no `--privileged`)*

### Domains (adjust to your hostnames)
Either via Coolify UI per-service, or via env vars (see Magic FQDN section):
- `https://meta.<domain>` → service `meta` port `8080`
- `https://game.<domain>` → service `game` port `8080`
- `https://console.<domain>` → service `console` port `8080`
- `https://aspire.<domain>` → service `aspire-dashboard` port `18888` (optional)

Coolify Traefik handles TLS + WebSocket upgrade + X-Forwarded-* headers automatically. No nginx layer, no certificates to mount.

### Advanced
- **Auto Deploy:** on
- **Force Https:** on
- **Enable Gzip Compression:** on
- **Git → Submodules / LFS / Shallow Clone:** on (as needed)

### Network
No manual `Ports Exposes` — containers use `expose:` (internal-only), Traefik picks them up via compose labels / FQDN env.

### Persistent Storage
No volumes required for the compose stack itself. Postgres data lives on the Coolify-managed Postgres resource.

### Environment Variables

Set these in the Coolify UI ("Environment Variables" tab, all marked Build-time + Runtime):

| Name | Purpose |
|------|---------|
| `DB_HOST` | Coolify-internal DNS name of the managed Postgres |
| `DB_PORT` | Postgres port (usually `5432`) |
| `DB_NAME` | Database name |
| `DB_USER` | Database user |
| `DB_PASSWORD` | Database password (secret) |
| `CONSOLE_TOKEN` | Admin token for `/login?token=…` (secret) |
| `GAME_SERVER_URL` | Public URL of the game gateway advertised to clients |
| `ASPIRE_TOKEN` | Dashboard browser token (only if dashboard enabled) |

`SERVICE_FQDN_<NAME>_<PORT>` env vars are already baked into the compose. Coolify auto-generates URLs, or you can override per-service via Coolify UI.

## Readiness ordering

`depends_on.condition: service_healthy` enforces startup order identical to the old `AppHost.WaitFor`:

```
pgbouncer  (pg_isready healthcheck)
   │
migrator  (one-shot, exits 0 — dependents use service_completed_successfully)
   │
silo         (/ready: Orleans silo lifecycle participant signalled started)
   │
coordinator  (/ready: Orleans started AND DeployId assigned by DeployIdentity)
   │
meta / game / console  (/ready: Orleans client started)
```

Healthcheck implementation:
- `/alive` (tag `live`): Kestrel is listening (self-check always healthy).
- `/ready` (tag `ready`):
  - all services: `OrleansReadyHealthCheck` reads `IServiceLoopObserver.IsOrleansStarted`.
  - coordinator also has `CoordinatorReadyHealthCheck` reading `IDeployContext.DeployId != Guid.Empty`.

## Local smoke test

Run from the repo root:

```bash
./tools/scripts/publish-local.sh
cp backend/Tools/deploy/.env.example backend/Tools/deploy/.env.local  # fill DB_HOST etc.
docker compose \
  --env-file backend/Tools/deploy/.env.local \
  -f backend/Tools/deploy/docker-compose.yaml \
  -f backend/Tools/deploy/docker-compose.local.yaml \
  up --build -d
docker compose \
  --env-file backend/Tools/deploy/.env.local \
  -f backend/Tools/deploy/docker-compose.yaml \
  -f backend/Tools/deploy/docker-compose.local.yaml \
  ps
```

Expected RAM (measured): ~470 MB total across all 5 services + pgbouncer (vs ~2 GB under the old `aspire run` model).

## Troubleshooting

- **`host.docker.internal` not resolving on Linux** — only matters for local testing with Postgres on the host. `pgbouncer` already has `extra_hosts: host.docker.internal:host-gateway` baked in.
- **`ConnectionString property has not been initialized`** — migrator/services expect **both** `ConnectionStrings__postgres` and `postgres` env vars (`DbExtensions.GetConnection` reads plain `postgres`). The compose sets both; don't strip one.
- **Coordinator stuck at `/ready` = unhealthy** — check coordinator logs for `DeployIdentity` errors (Orleans grain initialization failing). Usually upstream Postgres connectivity.
- **Service 302 on `/alive` or `/ready`** — console's auth middleware; must have `/health /alive /ready` in the allowlist (already patched).
- **Traefik routing not working** — verify `SERVICE_FQDN_*` env vars present, or add explicit Traefik labels in compose.

## Migration from old DinD deploy

Old `tools/deploy/` contained a single-container DinD image (`Dockerfile`, `entrypoint.sh`, `Steps/*.cs`, `nginx-server.template`) that ran `aspire run --configuration Release` internally. All of that has been removed.

Replaced by:
- `backend/Tools/deploy/docker-compose.yaml` — Coolify's build+deploy target.
- `backend/Tools/deploy/docker-compose.local.yaml` + `backend/Orchestration/Dockerfile.prebuilt` — local dev overlay.
- `backend/Tools/deploy/.env.example` / `.env.local` — env templates.
- `backend/Orchestration/Dockerfile` — shared multi-stage per-service image.
- `backend/Tools/DeploySetup/` — init-container executable with migration code (was `Aspire/Startup/Setup*.cs`).

Aspire AppHost (`backend/Orchestration/Aspire/`) remains unchanged — still used for `aspire run` in local dev.
