# Deploy troubleshooting

Concrete failure modes hit during the Coolify Compose migration and how to recognise / fix each one. Read after `DEPLOY.md` — that file explains the architecture, this one lists the landmines.

## Coolify shared network not joined

**Symptom.** Migrator (or any service) crashes at startup with Npgsql `Name or service not known` / `Failed to resolve host name` for the managed Postgres host (e.g. `qxeff0tbfccmhj4i8qn404o6`). Local `docker compose up` works fine, only Coolify deploys break.

**Cause.** Coolify-managed Postgres lives on the shared bridge network named `coolify`. Our compose, by default, gets its own per-project bridge (`zufqewcez1k024uw3hnzspzp_default`). DNS for the managed resource is only resolvable inside `coolify`, so containers on the project bridge cannot reach it.

**Fix.** Two pieces both required in `docker-compose.yaml`:

```yaml
networks:
  coolify:
    external: true            # tell compose this network already exists

services:
  silo:
    networks: [default, coolify]   # MUST be set per-service
```

Setting it only at the top level (`networks: default: external: name: coolify`) does **not** stick — Coolify's compose pre-processor overrides the top-level definition. Per-service `networks: [default, coolify]` is the only form that survives.

Verify: `sudo docker network inspect coolify --format '{{range .Containers}}{{.Name}} {{end}}'` should list every service plus the managed Postgres container.

## Orleans clustering schema missing

**Symptom.** Silo loops forever logging `relation "orleansquery" does not exist` or `function membership_read_all(...) does not exist`. Healthcheck `/ready` never goes green, deploy times out.

**Cause.** Orleans `UseAdoNetClustering` expects the schema (`OrleansQuery` table + a set of stored queries) to already exist. Locally Aspire's dev provider auto-bootstraps it; in prod nothing does. Worse, Orleans 10.1.0's bundled `PostgreSQL-Clustering.sql` is missing the `CleanupDefunctSiloEntriesKey` row, so even after running upstream SQL the cluster fails on cleanup paths.

**Fix.** `OrleansClusteringSetup` in `Aspire/Startup/Migrations/` runs three SQL files in order on every migrator boot:
1. `Sql/PostgreSQL-Main.sql` — creates `OrleansQuery` if absent.
2. `Sql/PostgreSQL-Clustering.sql` — membership tables + 8 stored queries from upstream.
3. `Sql/PostgreSQL-Supplemental.sql` — our patch with `CleanupDefunctSiloEntriesKey`, applied via `ON CONFLICT DO NOTHING` so it is safe to re-run.

The setup is gated on `OrleansQuery` existence, so first deploy bootstraps everything and subsequent deploys are essentially no-ops. If you upgrade Orleans, re-check upstream `PostgreSQL-Clustering.sql` — the supplemental row may have been merged and the patch can be removed.

## Gateway service intermittently returns Gateway Timeout after redeploy (THE BIG ONE)

**Symptom.** After a Coolify Redeploy, one of the public gateways (`console.minesleader.xyz`, `game.minesleader.xyz`, `meta.minesleader.xyz`, or `aspire.minesleader.xyz`) starts returning `504 Gateway Timeout`. The rest work fine. Which one breaks is **different every time** — sometimes console, sometimes aspire. Force-Redeploy without cache does not fix it. The backend container is `Up (healthy)`, internal `curl http://localhost:8080/...` returns 200 in <5ms, direct `curl` from host to any container IP returns 200, but HTTPS through Traefik hangs.

This entry is long because figuring it out was long. Read before changing anything in `networks:` or `labels:`.

### Root cause

Traefik's Docker provider, in the absence of a `traefik.docker.network` label, picks **any** Docker network the backend container is a member of and uses that network's IP as the upstream. If the chosen network does not also contain `coolify-proxy`, every SYN the proxy sends to that IP disappears into the bridge with no return path, and the HTTP handler sits waiting forever.

In our initial deploy each gateway container ended up in **three** networks simultaneously:

| Network | Who created it | Contains coolify-proxy? |
|---|---|---|
| `coolify` | Coolify (shared) | yes |
| `<application-UUID>` | Coolify auto-injects one per compose app | yes |
| `<project>_default` | Docker Compose auto-created from top-level | **no** |

For game/meta the Docker provider happened to pick a "good" network. For the unlucky gateway it picked `_default` → request enters Traefik, never leaves. That is the flaky Gateway Timeout.

### How we proved it

The decisive evidence is a `tcpdump` inside the `coolify-proxy` network namespace while curl'ing the broken HTTPS endpoint:

```bash
PID=$(sudo docker inspect coolify-proxy --format '{{.State.Pid}}')
sudo nsenter -t $PID -n tcpdump -nni any 'port 8080' -c 20
```

On the proxy side you see repeated `[S]` (SYN) packets to `10.0.4.8:8080` (the backend's address in `<project>_default`) with no SYN-ACK ever coming back. On the backend's namespace you see zero packets from the proxy IP — the SYNs never arrive there, because the proxy has no route into `_default`.

The full investigation (all three networks enumerated via `docker network inspect`, proxy-reachable networks via `docker inspect coolify-proxy --format '{{range $k,$v := .NetworkSettings.Networks}}{{$k}} {{end}}'`, proof that internal `curl localhost:8080` works) is documented in progress notes.

### Why the obvious fixes did not work

- **"Just restart coolify-proxy."** Symptom returned on the next Redeploy because the root cause (non-deterministic network pick) persists.
- **Top-level `networks.default: {external: true, name: coolify}`** (alias the compose default to the shared network). Coolify's compose pre-processor **rewrites** this — it silently substitutes its own per-application UUID network as the default and drops the `coolify` alias. Symptom moved: pgbouncer lost access to managed Postgres and `DNS lookup failed: qxeff0...` flooded its logs.
- **Custom `mines-internal` bridge + `traefik.docker.network=${APP_UUID}` label.** The intent was to pin Traefik to the right network via a user-set Coolify env var. It does not work: Coolify does **not** perform compose variable substitution in the `labels:` section (only in `environment:`). The placeholder reaches Docker as a literal string `${APP_UUID}`, Traefik ignores the bogus label, and we are back to arbitrary network picking.
  - **Confirmed bug**: [coollabsio/coolify#5351](https://github.com/coollabsio/coolify/issues/5351) — open since 2025-03, no fix as of 2026-04.
  - Additional quirk: variable names with the `COOLIFY_` prefix are **reserved** — even if you add `COOLIFY_RESOURCE_UUID` via the UI, Coolify strips it and does not export it to compose. Use a neutral name like `APP_UUID` — but that still does not help because of #5351.

### The fix (what is in place now)

Assign the application to its own **Coolify Destination** (Servers → Destinations → Add, called `mines-leader`, backing Docker network named `mines-leader-production`). Move the application (and optionally Postgres) to that Destination from the UI.

Move the managed Postgres to the same Destination as well (or create a new Postgres resource on it) so pgbouncer can reach the DB directly through the Destination network — no second network leg needed. The compose then simplifies to:

```yaml
networks:
  mines-leader-production:
    external: true            # single network for the whole stack

services:
  pgbouncer / silo / coordinator / meta / game / console / aspire-dashboard / migrator:
    networks: [mines-leader-production]

  meta / game / console / aspire-dashboard:
    labels:
      - "traefik.docker.network=mines-leader-production"   # hardcoded name; no UUID, no substitution needed
```

Why this survives:
- `mines-leader-production` is a dedicated Destination — **only** our containers, managed Postgres, and `coolify-proxy` are members. No neighbours, no ambiguity.
- Every gateway is a member of exactly one network. Traefik's Docker provider has only one choice of backend IP.
- The `traefik.docker.network` label is stable text — no `${VAR}` involved, so Coolify's broken label substitution cannot affect it.
- pgbouncer resolves Postgres via the Destination DNS directly. Nobody touches the shared `coolify` bridge.

### Coolify limitations to remember

1. **No variable substitution in `labels:`.** This is Coolify's pre-processor being incomplete. `${…}` in `environment:` works; `${…}` in `labels:` is passed through literally. If you need a variable in a label, either (a) use a Coolify magic var (`SERVICE_FQDN_…`, which Coolify itself expands into labels), or (b) hardcode the value.
2. **`COOLIFY_*` variable names are reserved.** User-supplied env vars with that prefix are not propagated.
3. **Top-level `networks:` aliases and `external` flags are partially overridden** by Coolify's compose pre-processor. Do not rely on aliasing `default` to a shared network — use explicit per-service `networks: [name]` lists instead.
4. **Coolify injects a per-application `<UUID>` network** regardless of what your compose declares. Factor that in when reasoning about network membership.
5. **Docker Compose will auto-create `<project>_default`** whenever a service has no explicit `networks:` list. Always list networks explicitly — this was the proximate trigger of the whole saga.

### If the symptom returns

First, rule out whether it is the same root cause:

```bash
# 1. container actually up?
sudo docker ps --format '{{.Names}}\t{{.Status}}' | grep <svc>

# 2. backend reachable from host by IP?
IP=$(sudo docker inspect <container> --format '{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}')
for ip in $IP; do curl -sS -o /dev/null -w "$ip %{http_code}\n" --max-time 3 http://$ip:8080/; done

# 3. reachable from coolify-proxy network namespace?
sudo docker exec coolify-proxy curl -sS -o /dev/null -w "%{http_code} %{time_total}\n" --max-time 3 http://<svc>:8080/

# 4. how many networks is the container in, and is proxy in the same set?
sudo docker inspect <container> --format '{{range $k,$v := .NetworkSettings.Networks}}{{$k}} {{end}}'
sudo docker inspect coolify-proxy --format '{{range $k,$v := .NetworkSettings.Networks}}{{$k}} {{end}}'
```

If step 4 shows the gateway in an extra network that the proxy is not in, that is the same class of bug again — audit `networks:` in compose.

If step 3 works (proxy reaches backend by DNS name) but external HTTPS still hangs, the label check:

```bash
sudo docker inspect <container> --format '{{index .Config.Labels "traefik.docker.network"}}'
# expect: mines-leader-production  (literal, not ${...})
```

If the literal string is `${...}`, Coolify did not substitute — hardcode the value.

**Nuclear option**: move the entire application to a fresh Destination by creating a new Coolify application pointing at the same Git repo. We did this during the saga and it reset all Coolify-injected state cleanly.

## Setting up a new Coolify Compose app correctly (from scratch)

This is the short version of everything the saga above taught us. Follow it whenever you add a new compose-based app to Coolify — deviating from any single step resurrects one of the failure modes above.

1. **Create a dedicated Destination first.**
   - `Servers → <your server> → Destinations → Add`
   - Name: something human-readable, e.g. `<projectname>-production`. This becomes the Docker network name — no UUID in your compose.
   - Do **not** reuse the default `coolify` Destination. Neighbour apps live there and their short service aliases (`silo`, `pgbouncer`, ...) will collide with yours in DNS.

2. **Create every managed resource on that Destination.**
   - Postgres / Redis / etc. → Destination = `<projectname>-production`.
   - If the resource already exists on the default Destination, the UI cannot migrate it — drop it and recreate it on the new Destination (dump/restore data first). You cannot move it later.

3. **Create the Application on the same Destination.**
   - Build Pack: Docker Compose.
   - Destination: `<projectname>-production`.
   - Env vars: use any names **except** `COOLIFY_*`. That prefix is reserved — user-set values with it are silently stripped.

4. **In compose, declare the Destination network as the only network.**
   ```yaml
   networks:
     <projectname>-production:
       external: true

   services:
     <every service>:
       networks: [<projectname>-production]
   ```
   - Do **not** leave Compose to create a default network — always list networks explicitly on every service.
   - Do **not** attach services to both the Destination and `coolify` unless a specific container genuinely needs to reach the shared bridge (nothing in our stack does).

5. **For public-facing services, pin Traefik to the Destination.**
   ```yaml
   labels:
     - "traefik.docker.network=<projectname>-production"
   ```
   Hardcode the name — compose `${VAR}` substitution does not work inside `labels:` (Coolify bug [#5351](https://github.com/coollabsio/coolify/issues/5351)). Paste the literal string.

6. **Do not try to parameterise anything Coolify-specific.**
   - No `${COOLIFY_RESOURCE_UUID}` (reserved prefix, stripped).
   - No `${APP_UUID}` in `labels:` (never substituted).
   - Hardcode Destination name and UUID-adjacent values, keep them in one clearly-commented block so future-you knows what to update.

7. **Verify after first deploy.**
   ```bash
   # network membership sanity
   sudo docker network inspect <projectname>-production \
     --format '{{range .Containers}}{{.Name}}{{"\n"}}{{end}}'
   # expect: coolify-proxy + every app container + managed DB + (possibly) UUID-injected container

   # label sanity on every public service
   for svc in meta game console aspire-dashboard; do
     C=$(sudo docker ps --format '{{.Names}}' | grep -m1 "^$svc-")
     echo "$svc: $(sudo docker inspect $C --format '{{index .Config.Labels "traefik.docker.network"}}')"
   done
   # expect: literal <projectname>-production on every line, never ${...}

   # external HTTPS
   for h in <public-hosts>; do
     curl -sS -o /dev/null -w "$h %{http_code} %{time_total}\n" --max-time 6 https://$h/
   done
   # expect: 200/302/404/405, all under ~1s
   ```

If all three checks pass, the app is configured correctly and future Redeploys are deterministic.

## `host.docker.internal` does not resolve in pgbouncer (local only)

**Symptom.** Local smoke test: `pgbouncer` exits with `getaddrinfo failed for host.docker.internal`. Production is unaffected.

**Cause.** Linux Docker does not auto-inject `host.docker.internal` into containers (macOS / Windows do).

**Fix.** Already in compose:
```yaml
pgbouncer:
  extra_hosts:
    - "host.docker.internal:host-gateway"
```
If you copy the pgbouncer block to a new compose file, do not drop this line.

## Migrator runs but `state_*` tables are empty

**Symptom.** Migrator container exits 0, but Silo logs `relation "state_player" does not exist`.

**Cause.** `DbExtensions.GetConnection` reads the **plain** key `postgres` from configuration, **not** `ConnectionStrings:postgres`. If only the latter is set, the migrator opens a connection to a wrong / empty database (or to localhost), creates tables there, and exits successfully.

**Fix.** Set both env vars on the `migrator` service:
```yaml
environment:
  postgres: "Host=pgbouncer;Port=6432;Database=...;Username=...;Password=..."
  ConnectionStrings__postgres: "${postgres}"
```

Double-check with `psql` against the actual managed Postgres after a deploy: `\dt state_*` should list all GeneratedStatesRegistration entries.

## Deploy build hits NuGet restore on every code edit

**Symptom.** Build time stays at ~3+ minutes per deploy even after warm cache. Restore step never shows `CACHED`.

**Cause.** Either (a) the `restore` stage `COPY` is too wide and pulls in source files (any source edit invalidates restore), or (b) `Directory.Build.props` / `Directory.Packages.props` were not copied into the restore stage, so the implicit project graph keeps changing.

**Fix.** Two requirements:
1. The Dockerfile must use `# syntax=docker/dockerfile:1.7-labs` and `COPY --parents <patterns>` to copy **only** `*.csproj`, `Directory.*.props`, `*.slnx`. Anything else in the restore stage breaks layer caching.
2. `.dockerignore` must not exclude `Directory.*.props` (it doesn't — but if you ever rewrite it, double-check).

Verify: edit a `.cs` file, rerun `docker build` locally, confirm the `restore` step shows `CACHED`. If not, the `COPY --parents` glob is wrong.

## Aspire Dashboard Resources tab is empty (and Console Logs tab is dead)

**Symptom.** Telemetry tabs (Structured logs, Traces, Metrics) work; Resources and Console Logs show nothing.

**Cause.** Expected — this is by design since 2026-09-03. The standalone `mcr.microsoft.com/dotnet/aspire-dashboard` image only renders those tabs when `DASHBOARD__RESOURCESERVICECLIENT__URL` points at a gRPC server implementing the Aspire resource contract, and we no longer run one.

**Why it was removed.** The `resource-service` container was built from a remote git context (`context: https://github.com/noncasted/Aspire.ResourceServer.Standalone.git#<sha>`), so every deploy needed GitHub to serve an anonymous fetch from the VPS IP. When GitHub answered 401 the whole compose build died before touching app code:
```
#5 [resource-service internal] load git source https://github.com/noncasted/Aspire.ResourceServer.Standalone.git#6d9cba46...
fatal: could not read Username for 'https://github.com': terminal prompts disabled
target resource-service: failed to solve: failed to read dockerfile
```
A cosmetic tab was taking prod deploys down, so the service, its `depends_on`, and the dashboard's `DASHBOARD__RESOURCESERVICECLIENT__*` env were dropped.

**Fix.** Use `docker ps` / `docker logs <container>` on the VPS or the Coolify UI for container state and logs. If the tabs are ever wanted back, build the fork once and push the image to a registry — never reintroduce a remote git build context in compose.

## Console healthcheck fails with 302 redirects

**Symptom.** `console` container marked unhealthy. `docker exec console curl -v http://localhost:8080/ready` returns `302 → /login`.

**Cause.** Console gateway has an auth middleware that redirects every unauthenticated request to `/login`. Health endpoints were not on its allowlist.

**Fix.** `ConsoleGateway/Program.cs` registers an explicit allowlist for `/health`, `/alive`, `/ready` before the auth middleware. If you add a new health route, add it to the allowlist too.

## Aspire Dashboard browser nags about insecure OTLP

**Symptom.** Dashboard console / browser warning about OTLP API key over plain HTTP.

**Cause.** OTLP traffic between services and the dashboard is intra-compose plain HTTP — by design, never leaves the host network. Dashboard cannot tell.

**Fix.** Not a bug, do not switch to TLS for intra-cluster OTLP. The browser warning is about the dashboard UI itself, which already has TLS via Coolify Traefik. Ignore.

## Coolify build fails with `lstat /backend: no such file or directory`

**Symptom.** Build container logs `Error: resolve : lstat /backend: no such file or directory` immediately after pulling the repo. `docker compose config` locally works fine. Coolify also prints earlier warnings like `Dockerfile not found for service migrator at ../../.././backend/Orchestration/Dockerfile, skipping ARG injection`.

**Cause.** Coolify invokes `docker compose` with an explicit `--project-directory`, which overrides the default (the compose file's directory). `build.context: ../../..` in our compose was written relative to the compose file location; relative to Coolify's project directory it resolves to the filesystem root, so `./backend/...` becomes `/backend/...` which does not exist.

**Fix.** Coolify UI → App → General:
- **Base Directory:** `/backend/Tools/deploy`
- **Docker Compose Location:** `/docker-compose.yaml`

That puts project-directory at `/backend/Tools/deploy/`, so `context: ../../..` resolves to the repo root. Any time you move the compose file, update both fields together.

## Dockerfile parse error `unknown flag: parents`

**Symptom.** Build fails on the first `COPY --parents ...` line with `dockerfile parse error on line N: unknown flag: parents`.

**Cause.** `--parents` is an experimental BuildKit flag only available on the **labs** variant of the Dockerfile frontend. Plain `# syntax=docker/dockerfile:1.7` rejects it.

**Fix.** First line of Dockerfile must be:
```dockerfile
# syntax=docker/dockerfile:1.7-labs
```
If you ever bump to a newer syntax version (e.g. `1.8`), check that it is a `-labs` tag or switch to the upstream-master-labs channel.

## Restore fails with `NETSDK1013: TargetFramework value ''`

**Symptom.** `dotnet restore <path.csproj>` during build fails with `NETSDK1013: The TargetFramework value '' was not recognized` — but only in Docker builds, never locally.

**Cause.** Two combined mistakes:
1. Our `.csproj` files rely on `Directory.Build.props` to set `TargetFramework`.
2. If the restore stage uses `dotnet restore backend/backend.slnx` and the solution includes `Tests.csproj` while `.dockerignore` excludes `backend/Tools/Tests/`, the solution file references a project that is not in the build context → restore fails on the missing csproj.

**Fix.** Two guardrails:
1. Restore stage must copy all `Directory.*.props` files (we use `COPY --parents backend/backend.slnx **/Directory.*.props **/*.csproj`).
2. Do not `dotnet restore` a solution that references excluded projects. List the six production `.csproj` files explicitly in the Dockerfile restore step. When you add a new production service, add a `dotnet restore` line for it alongside the existing five.

## BuildKit cache-mount race across parallel publishes

**Symptom.** Concurrent `dotnet publish` on 6 services intermittently fails with `Could not find a part of the path '/root/.nuget/packages/<pkg>/<ver>/lib/.../X.dll'`. Different services fail on different packages between runs — a classic race signature.

**Cause.** We used `RUN --mount=type=cache,id=nuget,...` to share the NuGet cache across build stages, then ran six parallel `publish` stages that all wrote to it. BuildKit does not lock shared cache mounts by default — two writers racing on the same file yield a half-written file for a third reader.

**Fix (historical, superseded).** The current Dockerfile avoids the problem entirely by publishing all six services **sequentially in a single RUN** inside one `publish-all` stage. No cache-mount needed. If you ever fork the Dockerfile back to per-service publish stages, add `sharing=locked` to the cache mount: `--mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked`.

## Coolify routes to wrong port on multi-port container

**Symptom.** Service with multiple exposed ports returns 502 / Gateway Timeout even though the container is healthy and traffik labels exist. Rotating Redeploys may intermittently fix it.

**Cause.** Coolify generates Traefik labels from the **Domains** UI field. When the Domain is just `https://x.example.com` (no port) and the service exposes more than one port, Traefik has no deterministic way to pick a backend. It may pick the OTLP port (18889) instead of the frontend (18888) and hang.

**Fix.** In Coolify UI → Domains, always include the port for multi-port services:
```
https://aspire.minesleader.xyz:18888
```
Single-port services (meta/game/console on 8080) can omit the port — Traefik picks the only option. Affects `aspire-dashboard` (18888 / 18889 / 18890). If you expose a second port on any other service, add `:port` to its Domain entry.

## Dev: Aspire persistent container stuck on wrong network

**Symptom.** `aspire run` locally boots OK, but pgbouncer logs `DNS lookup failed: postgres: result=-2` forever. Services cannot reach Postgres. Happens after a config or Aspire version change.

**Cause.** `DbUpstreamFactory` uses `WithLifetime(ContainerLifetime.Persistent)` on the Postgres container so dcp reuses it between `aspire run` invocations. When the internal dcp network model changes (e.g. an Aspire upgrade added the `aspire-persistent-network` bridge), persistent containers keep their old `NetworkMode=bridge` and never join the new network. Other services (pgbouncer) spawn fresh on the new network and cannot see the stranded Postgres.

**Fix.** Kill the persistent containers so the next `aspire run` recreates them on the current network:
```bash
docker rm -f postgres-<dcp-id> pgbouncer-<dcp-id>
```
`docker ps | grep -E "postgres|pgbouncer"` to find the IDs. This has to be re-done after Aspire upgrades that change internal networking.

## Dev: port collision with standalone local Postgres

**Symptom.** `aspire run` fails to start Postgres: `Ports=map[] ExposedPorts=...` — container is alive but no host-port binding. All downstream services time out.

**Cause.** Someone left `ml-pg` (a standalone `docker run postgres -p 9432:5432`) running on the host. Aspire's `DbUpstreamFactory` also tries to bind host port 9432 (from `ConnectionStrings:db` in `appsettings.json`). The second bind silently fails with `isProxied: false`, leaving the container unrouted, and dcp then also does not attach it to the aspire bridge.

**Fix.** Before `aspire run`:
```bash
docker stop ml-pg
```
Symmetric hygiene before the reverse: when switching from local compose testing to `aspire run`, stop whatever container was bound to 9432.

Long-term option: change `ConnectionStrings:db` port to something unlikely to collide (e.g. 15432) in `appsettings.json`, so dev never fights other Postgres-like containers.

## Aspire Dashboard: every page throws `CryptographicException: key ... was not found in the key ring`

**Symptom.** Opening any tab in the Aspire Dashboard (StructuredLogs, Traces, Metrics, …) immediately fails with a Blazor unhandled-exception circuit error. Server logs show `The key {<guid>} was not found in the key ring` from `Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector.UnprotectCore`.

**Cause.** The `mcr.microsoft.com/dotnet/aspire-dashboard` image stores ASP.NET Core DataProtection keys under `/root/.aspnet/DataProtection-Keys` by default — an in-container path that does not survive a container restart. Every redeploy generates a fresh keyring. The browser still sends the previous instance's `ProtectedBrowserStorage` cookie, the new container cannot decrypt it, every page that calls `BrowserStorageBase.GetAsync` throws on first parameter set.

**Fix.** Persist the keyring on a named volume:

```yaml
services:
  aspire-dashboard:
    volumes:
      - aspire-dashboard-keys:/root/.aspnet/DataProtection-Keys

volumes:
  aspire-dashboard-keys:
```

After the next deploy the keyring is stable across restarts. Existing browser cookies stay invalid forever (their key is gone) — clear cookies for the dashboard host once and the loop ends.

## Console: `Failed to load resource: 404` for `_framework/blazor.web.js` (Blazor admin dead)

**Symptom.** `https://console.minesleader.xyz/login` loads, but every page is dead — no buttons clickable, top-bar links return 404, browser console shows `blazor.web.js:1 Failed to load resource: the server responded with a status of 404`. `/_content/...` and `/css/...` work fine. `/_framework/blazor.web.js` returns 404 from prod, even though `MapStaticAssets()` is wired and the manifest (`<svc>.staticwebassets.endpoints.json`) lists the route.

**Cause (after a long detour).** The `mcr.microsoft.com/dotnet/sdk` image — both `:10.0`, `:10.0.201`, `:10.0.202` — does **not** auto-restore the private `Microsoft.AspNetCore.App.Internal.Assets` package, even for Web SDK projects that need it. That package is the actual carrier of `blazor.web.js`, `dotnet.js`, etc; without it, `dotnet publish` writes the manifest pointing at files that do not exist on disk (because they were never copied — never even fetched). Local hosts have the package because earlier projects pulled it; the SDK image starts clean and the implicit framework reference for Web SDK is not enough to drag it in.

The investigation that led here, in case the symptom returns and the fix is not obvious:
1. Cookies cleared, persistent keyring added — still 404.
2. `app.UseStaticFiles()` removed (it duplicates `MapStaticAssets`) — still 404.
3. `dotnet publish` repro on host: ConsoleGateway gets `wwwroot/_framework/` with 6 files. The same loop in Docker: empty.
4. SDK pinned 10.0.201 (matches host) — still 404.
5. Per-service `BaseIntermediateOutputPath` isolation tried — broke `ProjectReference` resolution (CS0246 everywhere) and reverted.
6. SWA tracking files (`staticwebassets*.json`, `*.StaticWebAssets.Up2Date`) deleted between iterations — still 404.
7. Repro built locally with our exact Dockerfile, `find / -name 'blazor.web.js'` → only `/src/publish/...` (a stale dev artifact from `tools/scripts/publish-local.sh` that slipped past `.dockerignore`). Nothing under `/usr/share/dotnet/`, nothing under `/root/.nuget/packages/`. The package that should ship it was never restored.
8. On host, the same `find / -name 'blazor.web.js'` finds it under `/home/<user>/.nuget/packages/microsoft.aspnetcore.app.internal.assets/10.0.5/_framework/blazor.web.js`. That package was nowhere in the Docker image's restored set.

**Fix.** Add an explicit reference in `ConsoleGateway.csproj` (or any other Web SDK project that interactively renders Blazor) plus a Central Package Management entry:

```xml
<!-- ConsoleGateway.csproj -->
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.App.Internal.Assets" />
  ...
</ItemGroup>
```

```xml
<!-- backend/Directory.Packages.props -->
<PackageVersion Include="Microsoft.AspNetCore.App.Internal.Assets" Version="10.0.5" />
```

The version must match (or be ≤) the `Microsoft.AspNetCore.App.Ref` pack version installed in the SDK image (`/usr/share/dotnet/packs/Microsoft.AspNetCore.App.Ref/<ver>/`) — currently `10.0.5`. When you bump the .NET SDK image, also bump this package version, otherwise restore will silently downgrade the framework reference.

**Verification.**

```bash
docker build -f backend/Orchestration/Dockerfile --target publish-all -t debug:latest .
docker run --rm debug:latest ls /app/out/ConsoleGateway/wwwroot/_framework/
# expect: blazor.server.js  blazor.web.js  blazor.server.js.{br,gz}  blazor.web.js.{br,gz}
```

If the output is empty after this fix, search the SDK image for the package — `docker run --rm debug:latest ls /root/.nuget/packages/ | grep aspnetcore.app.internal` — and bump the version in Directory.Packages.props if missing.

## Console (Blazor admin): `AntiforgeryValidationException: token could not be decrypted` after every redeploy

**Symptom.** Identical mechanism to the Aspire dashboard DataProtection bug, but for our own admin console. Server logs show `Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException` wrapping `CryptographicException: The key {<guid>} was not found in the key ring`. Every form POST (every button, every save) fails silently after a redeploy.

**Cause.** Same as the dashboard: ASP.NET Core DataProtection auto-generates an in-container keyring at `/root/.aspnet/DataProtection-Keys`. Container restart → fresh keyring → cookies set by the previous instance (auth cookie, antiforgery cookie) become undecryptable. Auth cookie failures redirect users to `/login`; antiforgery failures look like silent button no-ops.

**Fix.** Persist the keyring on a named volume, same shape as the dashboard:

```yaml
services:
  console:
    user: root        # the runtime image drops privileges; without root the volume is RO
    volumes:
      - console-keys:/root/.aspnet/DataProtection-Keys

volumes:
  console-keys:
```

Apply this to **every** Blazor Server-rendered service that uses cookies / antiforgery. After the next redeploy, ask users to clear cookies for the console host once — old cookies are encrypted with the now-discarded ephemeral key and will never decrypt again.

**Why `user: root`.** The aspnet base image runs the entrypoint as a non-root user by default in some variants. The `/root/...` directory is owned by root, so the writable volume mount needs root. Same one-line fix on `aspire-dashboard` for the same reason.

## Compose `git+sha` references must use the FULL commit SHA

**Symptom.** (Historical — compose no longer has any git build context. Applies only if one is reintroduced.) Coolify build aborts immediately on the stage using a git context:
```
ERROR: failed to read dockerfile: failed to load cache key:
       repository does not contain ref b2f751c, output: ""
```
Even though the SHA exists on `origin` and `git rev-parse b2f751c` resolves locally.

**Cause.** BuildKit's `git://...#<ref>` source resolver does **not** accept abbreviated SHAs. Branch names and tags work; full 40-char SHAs work. Anything between (8/12-char shorthand) fails, because BuildKit calls `git fetch --depth=1 <ref>` and the git protocol only accepts full SHAs there.

**Fix.** Use the full SHA in compose, keep the short-SHA only for the local `image:` tag (cosmetic):
```yaml
build:
  context: https://github.com/<owner>/<repo>.git#ba56b364bfb7d56262e984c476071a811af07c5a
image: mines-leader/<some-service>:ba56b36
```

Copy the full SHA into `context:` (`git rev-parse HEAD` in the source worktree), then truncate it for the image tag.

## `BaseIntermediateOutputPath` per service breaks ProjectReference resolution

**Symptom.** During the publish-all loop, the second iteration crashes with hundreds of `CS0246: type or namespace 'Grain' / 'IGrainFactory' / 'GenerateSerializerAttribute' could not be found` errors in transitive Infrastructure projects.

**Cause.** A previous attempt at fixing the missing-`_framework` issue passed `/p:BaseIntermediateOutputPath=/tmp/obj/<svc>/` and `/p:BaseOutputPath=/tmp/bin/<svc>/` to isolate per-service intermediate dirs. That property only applies to the **entry** project — every transitive `<ProjectReference>` keeps using the default `obj/` in its own source folder. Restore goes to `/tmp/obj/<svc>/`, transitives have no `project.assets.json` in their default `obj/`, and the C# compiler resolves none of the Orleans types.

**Fix.** Do **not** use `BaseIntermediateOutputPath` to isolate per-service publishes. Either (a) accept that obj/ is shared across the loop and clean SWA tracking between iterations, or (b) publish each service in its own copy of the source tree. The current Dockerfile uses (a) — sequential publish, shared obj/, and the `Microsoft.AspNetCore.App.Internal.Assets` package reference makes the static-asset issue moot.

## Console: `app.UseStaticFiles()` together with `app.MapStaticAssets()`

**Symptom.** Some Blazor static-asset routes serve correctly, others 404 — inconsistent depending on which middleware reaches the request first.

**Cause.** In ASP.NET Core 9+/Blazor Web App, `MapStaticAssets()` replaces `UseStaticFiles()` for wwwroot files **and** virtual framework assets. Calling both causes `UseStaticFiles` to short-circuit some paths (own-wwwroot files) before they hit the endpoint dispatch where `MapStaticAssets` would have served them. The two systems are mutually exclusive.

**Fix.** Remove `app.UseStaticFiles()`; keep only `app.MapStaticAssets()`:
```csharp
// before
app.UseHttpsRedirection();
app.UseStaticFiles();        // ❌ delete
...
app.MapStaticAssets();

// after
app.UseHttpsRedirection();
...
app.MapStaticAssets();
```
Done in `ConsoleGateway/Program.cs`. Apply the same change to any other Web SDK service that mixes the two.

## "Login to the dashboard at http://localhost:18888" startup log line

**Symptom.** Aspire dashboard logs `Login to the dashboard at http://localhost:1...` (truncated). Looks alarming.

**Cause.** Cosmetic. The dashboard prints its bind address (always `0.0.0.0:18888` inside the container) and assumes the user opens it as `localhost`. Coolify Traefik terminates TLS externally and proxies to that bind address.

**Fix.** Ignore. The real entry point is `https://aspire.<your-domain>/` and the browser-token URL is shown inside the dashboard UI after first login — not in this log line.

## Quick reference: where to look when a deploy goes sideways

| Symptom | First check | Second |
|---|---|---|
| Build never finishes | BuildKit log on Coolify — is `restore` `CACHED`? | `.dockerignore` not over-excluding |
| `lstat /backend` at build start | Coolify Base Directory / Compose Location fields | `context:` relative path in compose |
| `unknown flag: parents` | first line of Dockerfile uses `1.7-labs` | — |
| `NETSDK1013` during restore | `Directory.Build.props` copied into restore stage | no excluded `.csproj` in the solution being restored |
| Random missing NuGet file | publish still parallel on shared cache? | switch to sequential `publish-all` or add `sharing=locked` |
| 502 on multi-port service | Coolify Domain field has explicit `:port`? | other containers also claim the host |
| Migrator fails | `docker logs <migrator>` for Npgsql error | both `postgres` and `ConnectionStrings__postgres` set |
| Silo never goes healthy | `docker logs <silo>` for `OrleansQuery` errors | `OrleansClusteringSetup` ran (migrator logs) |
| Service unreachable via HTTPS | `coolify-proxy` access logs for the host | Traefik routers API for stale entries |
| Dashboard Resources / Console Logs tab empty | expected — no resource service is deployed | use `docker ps` / `docker logs` on the VPS |
| Dev: pgbouncer `DNS lookup failed: postgres` | `docker ps` persistent postgres/pgbouncer still around? | `docker rm -f` them, rerun `aspire run` |
| Dev: Aspire postgres `Ports=map[]` | other Postgres on 9432? | `docker stop ml-pg` before `aspire run` |
| RAM blew up after deploy | `docker stats --no-stream` filtered by project prefix | a service stuck in restart loop |
| Blazor `_framework/blazor.web.js` 404 | `Microsoft.AspNetCore.App.Internal.Assets` in csproj? | version matches SDK pack `Microsoft.AspNetCore.App.Ref/<ver>` |
| Blazor buttons silently dead | server logs for `AntiforgeryValidationException` | persistent DataProtection volume + `user: root` |
| `repository does not contain ref <sha>` | `context:` uses **full** 40-char SHA, not abbreviated | `git rev-parse <ref>` to get full SHA |
| `CS0246` flood after Dockerfile tweak | per-service `BaseIntermediateOutputPath` was added? | revert — it breaks transitive ProjectReferences |
| `_blazor/negotiate` 502/503/504 then container is `Up healthy` | Traefik pool holding the previous container ID | `docker restart coolify-proxy` (kicks all sites for ~10 s) |
