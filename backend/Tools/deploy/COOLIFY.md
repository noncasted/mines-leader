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

## Deploy branch

Coolify clones the repo before every build (`git clone --depth=1 -b <branch>`). The tip tree of `main` carries the Unity client, art and docs, while the backend images need ~11 MB of it — so a deploy spent most of its wall clock downloading files no service reads.

Coolify therefore builds from the orphan branch **`deploy`**, which mirrors only `backend/`, `shared/`, `docs/obsidian/game/cards/icons/` (the card icons `ConsoleGateway.csproj` ships at `/card-icons`), `.dockerignore`, `.editorconfig` and the backend part of the framework submodule (`tools/framework/backend/`, `tools/framework/shared/`, `tools/framework/common/`, `Directory.Build.props`, `Directory.Packages.props`, `global.json` at the commit `main` pins) as plain files. The branch has no submodules, so Coolify needs no access to the private framework repository. A shallow clone of one branch never fetches the objects of the others. Paths are preserved, so Base Directory, Compose Location and `context: ../../..` are the same as for `main`. LFS files inside the mirrored paths (none today) are committed with their content and `.gitattributes` is not mirrored, so the branch needs no LFS.

The branch is refreshed by `.github/workflows/sync-deploy-branch.yml` on every push to `main` that touches `backend/**`, `shared/**`, `docs/obsidian/game/cards/icons/**`, `tools/framework` (the submodule pointer), `.dockerignore` or `.editorconfig` (plus manual *Run workflow* in the Actions tab). The runner checks out blobless + sparse, fetches the framework part with the `FRAMEWORK_TOKEN` secret (a token with read access to `noncasted/framework`, the same as for CI) and force-pushes the subset with the default `GITHUB_TOKEN` (`permissions: contents: write` in the workflow). **That push is what fires the Coolify deploy webhook**, so client-only commits no longer redeploy the backend.

The branch is generated output: never commit to it, every sync overwrites it.

`tools/scripts/sync-deploy-branch.sh` does the same locally — for deploying a commit that is not the tip of `main`, or when Actions is unavailable:

```bash
tools/scripts/sync-deploy-branch.sh             # mirrors origin/main -> origin/deploy
tools/scripts/sync-deploy-branch.sh <ref>       # mirror another commit
DRY_RUN=1 tools/scripts/sync-deploy-branch.sh   # build the mirror commit, do not push
```

A new path the backend build starts reading outside `backend/` and `shared/` must be added to both the workflow (`paths`, sparse checkout, copy loop; framework paths: the sparse checkout and the copy loop of the framework) and the script (`DIRS` / `FILES` / `FRAMEWORK_PATHS`), otherwise the Coolify build fails on a missing file.

## Coolify application settings

### General
- **Branch:** `deploy` (not `main`, see [Deploy branch](#deploy-branch))
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
- **Git → Submodules / LFS:** off (the `deploy` branch has neither); **Shallow Clone:** on

### Network
No manual `Ports Exposes` — containers use `expose:` (internal-only), Traefik picks them up via compose labels / FQDN env.

### Persistent Storage
No volumes required for the compose stack itself. Postgres data lives on the Coolify-managed Postgres resource.

### Environment Variables

Set these in the Coolify UI ("Environment Variables" tab, all marked Build-time + Runtime):

| Name | Required | Purpose |
|------|----------|---------|
| `DB_HOST` | yes | Coolify-internal DNS name of the managed Postgres |
| `DB_PORT` | no (`5432`) | Postgres port |
| `DB_NAME` | yes | Database name |
| `DB_USER` | yes | Database user |
| `DB_PASSWORD` | yes | Database password (secret) |
| `CONSOLE_TOKEN` | yes | Admin token for `/login?token=…` and `Authorization: Bearer` on meta `/api/monitor/*` and game `/api/sessions/*` (secret) |
| `SESSION_TICKET_SECRET` | yes | HMAC key of the game session tickets, base64 of at least 32 bytes (`openssl rand -base64 48`); the same value for meta and game (secret) |
| `CORS_ALLOWED_ORIGINS` | no (`https://minesleader.xyz,https://play.minesleader.xyz,https://html-classic.itch.zone`) | Comma-separated browser origins of the WebGL client; CORS and the WebSocket handshake of meta / game reject other origins |
| `GAME_SERVER_URL` | yes | Public URL of the game gateway advertised to clients |
| `ASPIRE_TOKEN` | yes | Aspire dashboard browser token (secret) |
| `OTEL_API_KEY` | yes | Shared secret for OTLP export from the services to the dashboard (secret) |
| `ASPIRE_API_KEY` | yes | Key of the dashboard telemetry HTTP API (`DASHBOARD__API__PRIMARYAPIKEY`, header `x-api-key`); the console reads the backend traces of the client startup records with it (`AspireDashboard__ApiKey`). `openssl rand -hex 32` (secret) |

Required variables are written as `${NAME:?}` in the compose: if one is missing or empty, the deploy fails at `docker compose` with `required variable NAME is missing a value` instead of starting a half-configured stack. For `CONSOLE_TOKEN` there is a second guard: outside Development console, meta and game refuse to start with an empty token or the dev value `local-dev-token` (`ConsoleAccess`), because that value turns authentication off. The same goes for an empty `Cors__AllowedOrigins` (`AllowedOrigins`) and for `SESSION_TICKET_SECRET` that is not base64, shorter than 32 bytes or the development key (`SessionTicketsSetup`).

A new domain for the client (another host, another itch.io origin) must be added to `CORS_ALLOWED_ORIGINS` before players use it. A handshake without `Origin` (Unity editor, native build) is not affected.

`ASPIRE_API_KEY` turns on the key check of the dashboard telemetry API (`/api/telemetry/*` on the frontend port 18888, the public domain of the dashboard): without it the API would be open. Setting it up the first time or rotating it: generate a key (`openssl rand -hex 32`), set `ASPIRE_API_KEY` in Coolify, redeploy (or restart `aspire-dashboard` and `console`: both read it at start). The console calls `http://aspire-dashboard:18888` over the internal network.

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
|- `backend/Tools/DeploySetup/` — database migrations (migrator container runs `dotnet DeploySetup.dll`). Aspire.AppHost is local-dev-only.
