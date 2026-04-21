## Aspire Coolify Compose Deploy — Рабочие заметки

### Статус: В работе (сессия 3 — продолжение)

### Сессия 3: что сделано

- [x] **Console gateway timeout — разрешилось.** Все три домена (console/game/meta) отвечают 200 OK, time ~0.4s. Гипотеза о залипшем Traefik router подтвердилась косвенно (после очередного Redeploy в Coolify проблема ушла без ручного вмешательства). Диагностические шаги задокументированы в `DEPLOY_TROUBLESHOOTING.md` для будущего self.
- [x] **`.claude/docs/DEPLOY_TROUBLESHOOTING.md` создан** — 149 строк, охватывает: Coolify shared network, Orleans schema bootstrap, console 502/timeout (с командами для Traefik API + label-collision scan), `host.docker.internal` Linux quirk, plain `postgres` vs `ConnectionStrings__postgres`, NuGet restore caching, resource-service пустой Resources tab, console healthcheck 302, OTLP nag, quick reference table.
- [x] **`.claude/docs/DEPLOY.md` финализирован** (был ~70% в конце сессии 2 — теперь полный, ссылка на TROUBLESHOOTING валидна).

### Статус: Закрытые vs незакрытые

**Закрытые:**
- Console timeout — resolved.
- DEPLOY.md + DEPLOY_TROUBLESHOOTING.md — написаны, закоммичены.

**Незакрытые (низкий приоритет):**
- [ ] Cutover старого DinD-приложения — не проверено существует ли оно ещё на VPS.
- [ ] PR в upstream `kiapanahi/Aspire.ResourceServer.Standalone` (label filter + state mapping + display-name/exit-code) — опционально.

### Сессия 2: что было сделано

- [x] **Prod-деплой в Coolify взлетел.** Все 9 сервисов (pgbouncer, migrator, silo, coordinator, meta, game, console, aspire-dashboard, resource-service) поднялись на https://<svc>.minesleader.xyz. TLS — Let's Encrypt через Coolify Traefik. ACME прошло после фикса DNS/сети.
- [x] **Сеть coolify** — наш compose не резолвил managed Postgres (`qxeff0tbfccmhj4i8qn404o6`) потому что Coolify Postgres живёт в shared сети `coolify`, а compose создавал свою. Фикс: `networks: coolify: external: true` + `networks: [default, coolify]` per-service.
- [x] **Orleans schema bootstrap.** В прод-деплое AdoNet clustering не работал без таблиц. Добавил `OrleansClusteringSetup` в DeploySetup: embedded SQL `PostgreSQL-Main.sql` + `PostgreSQL-Clustering.sql` + наш `PostgreSQL-Supplemental.sql` (patch для missing `CleanupDefunctSiloEntriesKey` в Orleans 10.1.0). Gate на существование `OrleansQuery`; supplemental идёт через `ON CONFLICT DO NOTHING` каждый раз.
- [x] **Resource-service fork.** `kiapanahi/Aspire.ResourceServer.Standalone` → `noncasted/Aspire.ResourceServer.Standalone` (ветка `mines-leader-filter`, commit `3a215aa`). Три патча: `COMPOSE_PROJECT_FILTER` env-based label filter, маппинг docker state → KnownResourceStates, display name из `com.docker.compose.service` + exit-code-based Finished/Failed. Подключён через `build.context: git-url#sha` в compose.
- [x] **Profiles убраны.** Было `with-dashboard`, теперь dashboard + resource-service запускаются всегда.
- [x] **Dockerfile radically upgraded.**
  - `1.7-labs` syntax + `COPY --parents` для узкого restore layer (только `.csproj` + `Directory.*.props` + `.slnx`).
  - Единый `publish-all` stage компилирует все 6 сервисов в одном RUN, msbuild incremental переиспользует shared-output между сервисами.
  - 6 тонких runtime-stage, выбор через `ASSEMBLY_NAME` build-arg.
  - `PROJECT_PATH` build-arg удалён из compose — больше не нужен.
  - `.dockerignore` исключает Tests (они не в scope прод).
- [x] **Healthcheck ускорен** — `interval: 2s retries: 60 start_period: 10s` (было 5s/30/30s). Pgbouncer тоже.
- [x] **OrleansSetupExtensions вернули в правильный вид** — dev uses localhost clustering, prod uses AdoNet (после короткого эксперимента всегда-localhost, который ломал клиентов).
- [x] **Aspire Program.cs упрощён** — убраны `AddDockerComposeEnvironment`, `AddPublicUrls` (COOLIFY_URL), `SetDashboardToken` (ASPIRE_TOKEN env), внешняя ветка `DB_CONNECTION_STRING` в `DbUpstreamFactory`, DNS-резолв в `PgBouncerFactory`. AppHost теперь dev-only, чисто.
- [x] **Деплой-файлы переехали** — `/docker-compose.yaml`, `/docker-compose.local.yaml`, `/.env.example`, `/.env.local`, `/tools/deploy/COOLIFY.md` → `/backend/Tools/deploy/`. В Coolify: Base Directory `/backend/Tools/deploy`, Compose Location `/docker-compose.yaml`.
- [x] **tools/publish-local.sh → tools/scripts/publish-local.sh** (оутлайер в tools/, переехал к остальным скриптам).
- [x] **.telemetry/deploy-warnings.txt** — 67 уникальных варнингов из последнего деплоя (66 CS8632 в `shared/Game/Snapshots/CardActionSnapshotRecord.cs` + 1 CS0162). Папка .telemetry/ в gitignore.
- [x] **Коммиты в main.** Последний: `6bc1c0c1 [Infra] Tighten healthcheck cadence to cut deploy wait`. Полный список по теме от `9db7dba9` до `6bc1c0c1` (14 штук).

### Результат по памяти

| Компонент | AppHost/DinD | Coolify Compose | Δ |
|---|---|---|---|
| 5 игровых сервисов | 870 MB (Debug) | 576 MiB (Release) | −34% |
| dashboard + resource-service | — | 156 MiB | +156 |
| Aspire host/dcp/6×dotnet run | ~1600 MB | — | −1600 |
| pgbouncer | 3 MB | 1.7 MiB | ≈ |
| **Всего** | **~2470 MB** | **~734 MiB** | **−70%** |

VPS 11 GiB: used 2.5 GiB (проект + Coolify + Laravel + dockerd), available 8.9 GiB, место под ~6-8 параллельных таких проектов.

### Таймер деплоя

3m40s (первая версия) → 2m30s (single publish-all stage) → 1m46s (warm cache) → следующий с ускоренным healthcheck ещё меньше.

### Текущий момент остановки (сессия 2)

**Проблема которая открыта:** после последнего Redeploy на прод (`6bc1c0c1` с ускоренным healthcheck) https://console.minesleader.xyz возвращает **Gateway Timeout**. Другие сервисы (meta, game, aspire) работают.

**Что знаем:**
- Контейнер console `Up 6 minutes (healthy)`, логи чистые, `/login` внутри контейнера отвечает 405 на HEAD / 200 на GET.
- Traefik labels **идентичные** другим сервисам (`Host(console.minesleader.xyz) && PathPrefix(/)`, gzip middleware, tls certresolver=letsencrypt).
- Сети: console в `coolify`, `zufqewcez1k024uw3hnzspzp`, `zufqewcez1k024uw3hnzspzp_default`. Proxy в `coolify`, `zufqewcez1k024uw3hnzspzp`. Общая есть.
- `coolify-proxy --since 2m` — пусто по слову `console.minesleader`. То ли access log off, то ли запросы не доходят.
- `curl https://console.minesleader.xyz/_blazor` WebSocket — timeout. Plain GET `/login` тоже timeout. Ровно как было с `server.minesleader.xyz` до того как юзер переключился на `game.minesleader.xyz` — там помогала смена домена.
- Kestrel логирует `Failed to determine the https port for redirect` (warning, не критично, всегда было).

**Гипотеза** (не подтверждена): залип старый router в Traefik-store на `console.minesleader.xyz`, как было раньше с `server.minesleader.xyz`. После смены на `game.minesleader.xyz` проблема ушла — а тут console нельзя так переименовать, надо чистить Traefik-state либо найти настоящую причину.

**Следующие шаги для новой сессии:**

1. Проверить API Traefik (в `coolify-proxy`) — какие routers фактически известны для `console.minesleader.xyz`:
   ```bash
   sudo docker exec coolify-proxy wget -qO- http://127.0.0.1:8080/api/http/routers | python3 -m json.tool | grep -iC2 console
   ```
   Если API 404 — Traefik dashboard отключён, включить через label или env на `coolify-proxy`.

2. Проверить есть ли **другие** контейнеры (кроме нашего console) с rule на тот же host:
   ```bash
   sudo docker ps --format "{{.Names}}" | while read c; do
     sudo docker inspect "$c" --format '{{range $k,$v := .Config.Labels}}{{$k}}={{$v}} {{end}}' 2>/dev/null | grep -o "console.minesleader.xyz" | head -1 | grep -q . && echo "=== $c ==="
   done
   ```
   Если выдаст больше одного `===` — коллизия. Удалить старый.

3. Если коллизии нет — перезапустить proxy чтобы Traefik перечитал labels:
   ```bash
   sudo docker restart coolify-proxy
   ```
   (осторожно — обрубит ВСЕ приложения на 5-10 сек).

4. Если и это не помогло — временно поменять домен на `admin.minesleader.xyz` и посмотреть, залипает ли Traefik именно на имени или проблема в конкретном контейнере.

**Незакрытые todos (переехали из сессии 1):**
- [ ] `CONSOLE_TOKEN`/`GAME_SERVER_URL` в Coolify env прописаны (подтверждено работой game).
- [ ] Описать деплой в `.claude/docs/DEPLOY.md` — файл создан, начато писаться; user прервал на `DEPLOY_TROUBLESHOOTING.md` (ещё не существует). **Dedicated docs для будущего self** — запланировано в следующую сессию.
- [ ] Cutover старого DinD-приложения — если оно ещё существует, снести.
- [ ] PR в upstream `kiapanahi/Aspire.ResourceServer.Standalone` с тремя патчами из нашего форка (label filter + state mapping + display-name/exit-code) — опционально.

### Рабочая копия на момент разделения

- `.claude/docs/DEPLOY.md` — **untracked**, написана ~70% (разделы Overview, Why split, File layout, Dockerfile layering, Caches, compose structure, Networks, Service graph, Ports, Env vars, Profiles, local overlay, Coolify config, Health, Resource service, Memory, Timing). Нужно ли дописывать — решать в следующей сессии.
- `client/Assets/Tools/SceneBuilder/Runtime/Scenes.cs` — modified, **не по теме задачи**.

### Коммиты сессии 2 (все запушены в main)

```
6bc1c0c1 [Infra] Tighten healthcheck cadence to cut deploy wait
0c0d18bd [Infra] Copy Directory.Build.props into restore stage
b720bb0e [Infra] Switch Dockerfile syntax to 1.7-labs for COPY --parents
b477a9f3 [Infra] Publish all services in a single build stage
5dd51e4c [Infra] Always run aspire-dashboard + resource-service
56a26ca9 [Infra] Patch missing Orleans 10.1 CleanupDefunctSiloEntries query
d69fa653 [Infra] Attach services to Coolify shared network explicitly
15d4980c [Infra] Bootstrap Orleans clustering schema in DeploySetup
bfb9a185 [Infra] Join Coolify shared network for managed-resource reachability
ba3f2561 [Infra] Restore only production projects instead of full solution
c9d22513 [Infra] Drop custom bridge network from deploy compose
d2cb206b [Infra] Restore whole solution once, publish per-service in parallel
4e5898dd [Infra] Serialize NuGet cache mount to avoid parallel build race
9db7dba9 [Infra] [Deploy] Move production deploy to Coolify Docker Compose
```

---

## Предыдущая сессия

### Выполнено

- [x] **1. Scaffold через `aspire publish`** — `Aspire.Hosting.Docker 13.2.2` добавлен в `Directory.Packages.props` + `Aspire.csproj`, `builder.AddDockerComposeEnvironment("compose")` в `Program.cs`. Сгенерированный compose изучен, используется как starting point.
- [x] **2. Multi-stage Dockerfile** — `backend/Orchestration/Dockerfile` (build-args `PROJECT_PATH` + `ASSEMBLY_NAME`), устанавливает curl, BuildKit cache-mount для NuGet.
- [x] **3. docker-compose.yaml** — корневой compose: 5 сервисов через build.args, pgbouncer (edoburu), опциональный aspire-dashboard (profile `with-dashboard`), `SERVICE_FQDN_*` для Coolify Traefik.
- [x] **4. PgBouncer вне Aspire** — sibling-контейнер edoburu/pgbouncer с env-based config. Добавлен `extra_hosts: host.docker.internal:host-gateway` для Linux.
- [x] **5. Env vars для сервисов** — `postgres` + `ConnectionStrings__postgres`, `CONSOLE_TOKEN`, `GAME_SERVER_URL`, OTel endpoint, `ASPNETCORE_ENVIRONMENT=Production`.
- [x] **6. Aspire Dashboard (опционально)** — profile `with-dashboard`, `SERVICE_FQDN_ASPIREDASHBOARD_18888`.
- [x] **7. Удалить/упростить tools/deploy** — удалены `Dockerfile`, `entrypoint.sh`, `nginx-server.template`, `Steps/*`, `Options.cs`, `Command.cs`, `Program.cs`, `deploy.csproj`, `deploy.slnx`, `bin/`, `obj/`, `.idea/`, `*.txt`. Оставлен только переписанный `COOLIFY.md`.
- [x] **8. AppHost compatibility** — `aspire run` в dev продолжает работать (ProjectReference на DeploySetup, `using DeploySetup;` в AppHost's Program.cs).
- [x] **9. Coolify конфигурация** — переписан `tools/deploy/COOLIFY.md` под compose-модель, без `--privileged`, без DinD volume.
- [x] **10. Локальная валидация** — `docker compose build` + `up -d` → все сервисы healthy, `/ready=200 /alive=200` у всех 5 сервисов. RAM: ~473 MiB (vs ~2 GB AppHost).
- [x] **DeploySetup init-container (сверх плана)** — новый проект `backend/Tools/DeploySetup/`, 7 миграционных файлов вынесены из `Aspire/Startup/`, namespace → `DeploySetup`. Migrator как сервис compose с `service_completed_successfully` зависимостью.
- [x] **Healthchecks (сверх плана)** — `OrleansReadyHealthCheck` + `CoordinatorReadyHealthCheck` (тег `ready`), `MapDefaultEndpoints` без `if IsDevelopment`, добавлено в `Coordinator/Program.cs` и `ConsoleGateway/Program.cs`, allowlist health-путей в Console auth middleware.
- [x] **Dockerfile.prebuilt + docker-compose.local.yaml (для локал dev)** — чтобы не ждать docker-build 5 минут. `tools/publish-local.sh` публикует на хост, Docker копирует артефакты.
- [x] **memory_baseline.md** — сохранены baseline htop-данные и замеры до/после.
- [ ] **11. Прод-деплой в Coolify** — настройка Coolify UI, создание нового приложения, env vars, домены.
- [ ] **Cutover** — остановка старого DinD-приложения, переключение DNS/доменов.
- [ ] **Commit** — ~16 новых/изменённых файлов по этой задаче. Зафиксировать.

### Текущий момент остановки

Локальный smoke-test полностью прошёл: `docker compose -f docker-compose.yaml -f docker-compose.local.yaml up -d` поднимает весь стек, все healthcheck'и зелёные, migrator успешно накатывает миграции в Postgres (`host.docker.internal:9432`, user `postgres`).

**Ни одного коммита ещё не сделано** — все изменения в рабочей копии. Тестовый стек запущен на локалке, контейнеры `mines-leader-*` ещё крутятся (можно поднять/погасить через `docker compose --env-file .env.local -f docker-compose.yaml -f docker-compose.local.yaml up/down`).

Следующая сессия: **продолжить деплой в Coolify**. Локально всё работает, теперь надо:
1. Закоммитить все изменения отдельным коммитом (есть ~16 файлов новых/изменённых в scope задачи, остальные diff'ы в рабочей копии — не связаны с этой задачей, существуют с начала сессии).
2. Запушить на GitHub.
3. Создать в Coolify новое приложение с Build Pack = Docker Compose, настроить env vars и домены (инструкции в `tools/deploy/COOLIFY.md`).
4. Сделать cutover со старого DinD-приложения на новое.

### Важные находки

**Aspire publish limitations:**
- Генерит compose только как скаффолд, Dockerfiles не производит.
- `aspire run --configuration Release` не пробрасывает Release в дочерние `dotnet run` (bug #13659), сервисы запускаются в Debug.
- `aspire publish` + `aspire deploy` — официальный prod-путь от Microsoft, `aspire run` только для dev.

**DeploySetup**:
- `DbExtensions.GetConnection` читает plain ключ `postgres` из конфига, НЕ `ConnectionStrings:postgres`. В compose migrator надо set и `postgres`, и `ConnectionStrings__postgres`.
- Миграции идемпотентны (CreateIfNotExists) — безопасно re-run.

**Healthchecks:**
- `BackgroundService.ExecuteAsync` НЕ блокирует startup хоста (fire-and-forget после первого await), миф развеян.
- `IServiceLoopObserver.IsOrleansStarted` — через `ILifecycleParticipant<ISiloLifecycle>/IClusterClientLifecycle`, идеальный сигнал готовности Orleans.
- `IDeployContext.DeployId != Guid.Empty` — сигнал что `DeployIdentity` BackgroundService инициализировал кластер.
- Console auth middleware по умолчанию редиректит ВСЁ кроме /login и /_/css на /login → возвращает 302 на /alive /ready. Пришлось добавить allowlist.
- `MapDefaultEndpoints` раньше мапил health только в Development → в Production эндпоинты возвращали 404. Убрал условие.

**Linux docker quirks:**
- `host.docker.internal` не резолвится без `extra_hosts: "host.docker.internal:host-gateway"`. Уже в compose.
- docker build в контейнере ~5+ мин на NuGet restore. BuildKit cache mount помогает со 2-го раза. Для локал dev — prebuilt overlay быстрее.
- Build-серверы `.NET` (VBCSCompiler 429M + 5× MSBuild ~180M каждый) висят после `dotnet build`, `dotnet build-server shutdown` чистит.

**docker-compose deps:**
- `condition: service_healthy` + правильные healthchecks эквивалентны `AppHost.WaitFor(...)`.
- `restart: "no"` + `service_completed_successfully` — init-container паттерн для migrator.

### Измененные файлы (на момент разделения)

Все в рабочей копии, ничего не закоммичено. Файлы В SCOPE задачи:

**Новые:**
| Файл | Статус | Что это |
|------|--------|---------|
| `docker-compose.yaml` | новый, uncommitted | корневой prod compose для Coolify |
| `docker-compose.local.yaml` | новый, uncommitted | overlay с Dockerfile.prebuilt для локал dev |
| `.env.example` | новый, uncommitted | шаблон переменных |
| `.env.local` | новый, uncommitted | локальные креды (DB_HOST=host.docker.internal, etc.) |
| `.dockerignore` | новый, uncommitted | исключает bin/obj/client/tests |
| `tools/publish-local.sh` | новый, uncommitted | публикует 6 проектов на хосте |
| `backend/Orchestration/Dockerfile` | новый, uncommitted | shared multi-stage per-service image |
| `backend/Orchestration/Dockerfile.prebuilt` | новый, uncommitted | runtime-only для prebuilt workflow |
| `backend/Orchestration/Extensions/OrleansReadyHealthCheck.cs` | новый, uncommitted | healthcheck `IServiceLoopObserver.IsOrleansStarted` |
| `backend/Orchestration/Extensions/CoordinatorReadyHealthCheck.cs` | новый, uncommitted | healthcheck `IDeployContext.DeployId != Guid.Empty` |
| `backend/Tools/DeploySetup/DeploySetup.csproj` | новый, uncommitted | init-container проект |
| `backend/Tools/DeploySetup/Program.cs` | новый, uncommitted | entry point для migrator |
| `backend/Tools/DeploySetup/PostResourcesSetup.cs` | перемещён из Aspire/Startup/ | с изменением namespace |
| `backend/Tools/DeploySetup/StatesSetup.cs` | перемещён | namespace |
| `backend/Tools/DeploySetup/StatesDrop.cs` | перемещён | namespace |
| `backend/Tools/DeploySetup/StatesCleanup.cs` | перемещён | namespace |
| `backend/Tools/DeploySetup/SideEffectsSetup.cs` | перемещён | namespace |
| `backend/Tools/DeploySetup/AuditLogSetup.cs` | перемещён | namespace |
| `backend/Tools/DeploySetup/BenchmarkSetup.cs` | перемещён | namespace |

**Изменённые:**
| Файл | Что изменено |
|------|-------------|
| `backend/Directory.Packages.props` | +`Aspire.Hosting.Docker 13.2.2`, deduped `BlazorBlueprint.Components`, удалён `WebAssembly.Server 9.0.1` |
| `backend/Orchestration/Aspire/Aspire.csproj` | +`PackageReference Aspire.Hosting.Docker`, +`ProjectReference ../../Tools/DeploySetup/` |
| `backend/Orchestration/Aspire/Program.cs` | +`builder.AddDockerComposeEnvironment("compose")`, +`using DeploySetup;` |
| `backend/Orchestration/Extensions/ServiceDefaultsExtensions.cs` | `MapDefaultEndpoints` мапит /health /alive /ready без `if IsDevelopment`, регистрация `OrleansReadyHealthCheck` |
| `backend/Orchestration/Extensions/ProjectsSetupExtensions.cs` | `SetupCoordinator` регистрирует `CoordinatorReadyHealthCheck` |
| `backend/Orchestration/Coordinator/Program.cs` | +`app.MapDefaultEndpoints()` |
| `backend/Orchestration/ConsoleGateway/Program.cs` | +`app.MapDefaultEndpoints()`, allowlist /health /alive /ready в auth middleware |
| `tools/deploy/COOLIFY.md` | полностью переписан под compose-модель |

**Удалённые** (DinD-инфраструктура):
- `tools/deploy/Dockerfile`
- `tools/deploy/entrypoint.sh`
- `tools/deploy/nginx-server.template`
- `tools/deploy/Steps/{Nginx,Application,Validation}.cs`
- `tools/deploy/{Options,Command,Program}.cs`
- `tools/deploy/deploy.csproj`, `deploy.slnx`, `deploy.csproj.DotSettings`
- `tools/deploy/{bin,obj,.idea,Steps}/`
- `tools/deploy/{db-setup,cleanup}.txt`
- `backend/Orchestration/Aspire/Startup/{Post*,States*,SideEffects*,AuditLog*,Benchmark*}Setup.cs` (перемещены в DeploySetup/)

**Также создан:**
- `docs/tasks/current/aspire_coolify_compose_deploy/memory_baseline.md` — замеры до/после

### Важно: рабочая копия грязная

В `git diff` много других изменений (в Infrastructure/Messaging, Game/*, Orchestration/ClusterParticipantStartup.cs, BotContext.cs и т.д.), которые **существовали до начала этой задачи** и НЕ относятся к ней. При коммите нужно выбирать файлы прицельно, не делать `git add -A`.

Список файлов в scope задачи — выше. Всё остальное — не трогать.

### Заметки<!--- Сюда записываются находки, решения и полезная информация по ходу реализации --->

### Архитектурные решения принятые до старта

- **Вариант 1** — без nginx, каждый сервис в своём Release-контейнере, Coolify Traefik роутит per-service домены.
- **Postgres** — external Coolify-managed, не в compose-стеке.
- **PgBouncer** — отдельный sibling container в compose (не через Aspire AddContainer).
- **Aspire AppHost** остаётся для dev (`aspire run` локально). Прод-compose пишется **отдельно** от AppHost и не использует его в runtime.
- **`aspire publish`** используется один раз для scaffold'а (посмотреть, что генерирует), дальше compose ведётся руками.
- **Dev-режим не ломаем** — текущий `aspire run` должен продолжать работать на локалке.

### Шаг 1: Scaffold через `aspire publish` — сделано

Добавил `Aspire.Hosting.Docker 13.2.2` в Directory.Packages.props + Aspire.csproj. В Program.cs — `builder.AddDockerComposeEnvironment("compose")`. Build проходит 0 ошибок.

Запустил `aspire publish --non-interactive --no-build --output-path ./aspire-output`. Сгенерился `docker-compose.yaml` (5 KB) + `.env`.

### Анализ сгенерированного compose

**Что хорошего:**
- Compose DNS работает — сервисы видят друг друга по имени (`http://silo`, `http://coordinator`).
- OTel endpoint уже прописан (`OTEL_EXPORTER_OTLP_ENDPOINT=http://compose-dashboard:18889`).
- `depends_on` выстроены корректно по порядку старта.
- `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` — нужно для работы за прокси.
- `HTTP_PORTS` пробрасывается через env per-service.
- `OTEL_SERVICE_NAME` прописан.

**Что требует правки для Coolify:**
1. **Dockerfiles не сгенерированы** — сервисы ссылаются на `${SILO_IMAGE}`, пустой в `.env`. Нужно создать per-service Dockerfile'ы и переписать на `build: context/dockerfile` вместо `image`.
2. **Connection strings хардкод `Host=127.0.0.1;Port=5433`** — это остаток DinD-логики из AppHost. В compose должно быть `Host=pgbouncer;Port=6432`.
3. **Postgres встроенный** — надо убрать, у нас external Coolify.
4. **PgBouncer через bind-mounts** `${PGBOUNCER_BINDMOUNT_0..2}` пустые — нужно либо entrypoint генерирующий конфиг из env, либо зашить конфиг в свой образ.
5. **`CONSOLE_TOKEN=local-dev-token`** и **`GAME_SERVER_URL=http://localhost:5268`** — хардкод из local config, надо через Coolify env.
6. **Нет Traefik labels / SERVICE_FQDN** — Coolify не поймёт какой сервис куда роутить. Надо добавить вручную.
7. **Dashboard image — `nightly/aspire-dashboard:latest`** — поменять на stable `mcr.microsoft.com/dotnet/aspire-dashboard:latest`.
8. **depends_on pgbouncer** есть только у silo; у coordinator/meta/game/console — нужно добавить (они тоже ходят в БД).
9. **Нет expose для Orleans clustering портов** — Silo в Orleans использует порты 11111 (silo-to-silo) и 30000 (gateway). Нужно `expose` эти внутри compose-сети (но НЕ `ports:` наружу).

### Решение

Использую сгенерированный compose как **отправную точку**, но прод-compose.yaml пишу вручную в корне репо. Выводы:

- Aspire publish — годится только для scaffold'а, реальный прод-compose всё равно руками.
- Структура (naming, depends_on, OTel, networks) — хорошая, оставлю.
- Всё остальное перепишу.

### Шаг 2-3: Dockerfile + docker-compose.yaml — сделано

Создано:
- `backend/Orchestration/Dockerfile` — **общий** multi-stage (sdk → publish Release → aspnet runtime) с build-args `PROJECT_PATH` + `ASSEMBLY_NAME`. Устанавливает `curl` для healthcheck.
- `.dockerignore` — исключает bin/obj/client/tests/docs. `Tools/Benchmarks` оставлен (референсится Silo/Console/Extensions).
- `docker-compose.yaml` в корне репо — 5 сервисов через build.args + pgbouncer (edoburu/pgbouncer) + опциональный aspire-dashboard (profile `with-dashboard`).
- `.env.example` — переменные для локального тестирования / настройки Coolify.

### Healthchecks — сделано

Поднята проблема: `service_started` ≠ AppHost `WaitFor`. AppHost ждёт healthy, compose — нет. А `MapDefaultEndpoints` мапил health эндпоинты только в Development. Исправлено:

- `backend/Orchestration/Extensions/OrleansReadyHealthCheck.cs` — новый, проверяет `IServiceLoopObserver.IsOrleansStarted` (поднимается через Orleans `ILifecycleParticipant<ISiloLifecycle>` / `IClusterClientLifecycle`). Регистрируется в `AddDefaultHealthChecks()` с тегом `ready` → есть во всех 5 сервисах.
- `backend/Orchestration/Extensions/CoordinatorReadyHealthCheck.cs` — новый, проверяет `IDeployContext.DeployId != Guid.Empty`. `DeployIdentity` BackgroundService ставит DeployId после инициализации Orleans grain'а и attach pipe-handler. Регистрируется только в `SetupCoordinator` с тегом `ready`.
- `ServiceDefaultsExtensions.MapDefaultEndpoints` переделан: теперь мапит `/health`, `/alive` (тег `live`), `/ready` (тег `ready`) **в любом окружении**. Старый `if (IsDevelopment())` убран.
- В compose — `service-healthcheck` anchor: `curl -fsS http://localhost:8080/ready`, interval=5s, retries=30, start_period=30s.
- `depends_on.condition` везде `service_healthy` вместо `service_started`.

### Итог цепочки готовности

```
pgbouncer (pg_isready healthcheck)
   ↓ service_healthy
silo (orleans-ready → /ready OK)
   ↓ service_healthy
coordinator (orleans-ready AND coordinator-deploy → /ready OK)
   ↓ service_healthy
meta / game / console (orleans-ready → /ready OK)
```

Идентично `WaitFor(...)` в AppHost, плюс дополнительный сигнал coordinator-deploy идентично поведению AppHost (тот ждёт DeployHealthChecker через стандартный healthcheck).

### Сборка

- `dotnet build Aspire.csproj` — 0 ошибок после всех изменений.
- `dotnet publish Silo.csproj -c Release /p:UseAppHost=false` — успешно, ~5 сек.
- `docker compose config --quiet` — прошёл валидацию (с заполненными env vars).

### DeploySetup init-container — сделано

Проблема: `Aspire/Startup/` содержал идемпотентные DB-миграции (StatesSetup, SideEffectsSetup, AuditLogSetup), которые AppHost дёргал через `PostResourcesSetup.Run`. В compose-деплое AppHost отсутствует → миграции нигде не выполнятся.

Решение:
- Создан `backend/Tools/DeploySetup/DeploySetup.csproj` (console exe, ссылки на shared + Common + Extensions).
- Перенесены 7 файлов из `Aspire/Startup/`: `PostResourcesSetup`, `StatesSetup`, `StatesDrop`, `StatesCleanup`, `SideEffectsSetup`, `AuditLogSetup`, `BenchmarkSetup`. Namespace → `DeploySetup`.
- `Program.cs` у Migrator читает env vars + `--connection=`, дёргает `PostResourcesSetup.Run`.
- Aspire AppHost обновлён: ProjectReference + `using DeploySetup;` — продолжает работать в dev.
- Остались в `Aspire/Startup/`: `DbUpstreamFactory`, `PgBouncerFactory`, `ProcessCleanup` — dev-only штуки.
- В compose добавлен сервис `migrator` (restart: no), остальные зависят через `condition: service_completed_successfully`.

### Healthchecks — сделано

Проблема: `depends_on.service_started` слабее AppHost `WaitFor` (тот ждёт healthy). И `MapDefaultEndpoints` не мапил эндпоинты в Production.

Решение:
- `OrleansReadyHealthCheck` (`IServiceLoopObserver.IsOrleansStarted`) — регистрируется во всех сервисах через `AddDefaultHealthChecks`, тег `ready`.
- `CoordinatorReadyHealthCheck` (`IDeployContext.DeployId != Guid.Empty`) — регистрируется только в `SetupCoordinator`, тег `ready`.
- `MapDefaultEndpoints` теперь мапит `/health`, `/alive`, `/ready` в любом окружении (убран `if IsDevelopment`).
- Добавлены вызовы `app.MapDefaultEndpoints()` в `Coordinator/Program.cs` и `ConsoleGateway/Program.cs` (в Silo/Meta/Game уже были).
- Console auth middleware дополнен allowlist для `/health`, `/alive`, `/ready` (возвращали 302 из-за редиректа на /login).
- В compose: `healthcheck: curl /ready` (interval 5s, retries 30, start_period 30s), `depends_on.condition: service_healthy`.

### pgbouncer + host.docker.internal — сделано

Проблема: `host.docker.internal` не резолвится на Linux без `extra_hosts`.

Решение: добавлен `extra_hosts: host.docker.internal:host-gateway` к сервису pgbouncer. Работает без изменений на macOS/Windows.

Также фикс: migrator env — добавлена переменная `postgres` (plain), потому что `DbExtensions.GetConnection` читает её напрямую (не только `ConnectionStrings:postgres`).

### Directory.Packages.props — починка

Убраны дубли `BlazorBlueprint.Components 3.9.6` и конфликт `Microsoft.AspNetCore.Components.WebAssembly.Server` 9.0.1 vs 10.0.5 (оставлена 10.0.5, актуальная для net10.0).

### Dockerfile.prebuilt + docker-compose.local.yaml — локальный dev

Для быстрого локального smoke-теста (docker build в контейнере ~5 мин на восстановление NuGet кеша vs секунды через prebuilt):
- `tools/publish-local.sh` — публикует все 6 проектов на хосте.
- `backend/Orchestration/Dockerfile.prebuilt` — runtime-only, принимает `PUBLISH_DIR` через build-arg.
- `docker-compose.local.yaml` — overlay, заменяет build.dockerfile на `Dockerfile.prebuilt`.
- `.dockerignore` — исключил `**/publish/` чтобы корневой `./publish/` не игнорировался.

### Локальный smoke test — ✅ успех

Baseline и замеры сохранены в `memory_baseline.md`.

```
pgbouncer     healthy   ~3 MiB
migrator      exited 0  (миграции накатились: state_*, se_queue, se_processing, se_retry, audit_log)
silo          healthy   113 MiB    /ready=200 /alive=200
coordinator   healthy    87 MiB    /ready=200 /alive=200
meta          healthy    82 MiB    /ready=200 /alive=200
game          healthy    84 MiB    /ready=200 /alive=200
console       healthy   103 MiB    /ready=200 /alive=200
```

**Итого ~473 MiB vs ~2 GB в AppHost-режиме. Экономия ~1.5 GB.**

### Cleanup — сделано

Удалено из `tools/deploy/` (старая DinD-инфраструктура):
- `Dockerfile` + `entrypoint.sh` (docker-in-docker)
- `nginx-server.template` + `Steps/Nginx.cs` (внутренний nginx)
- `Steps/Application.cs` (запускал `aspire run` внутри контейнера)
- `Steps/Validation.cs`, `Options.cs`, `Command.cs`, `Program.cs`, `deploy.csproj`, `deploy.slnx`
- `bin/`, `obj/`, `.idea/`, вспомогательные `*.txt`

Остался только `tools/deploy/COOLIFY.md`, переписанный под compose-модель.

### Следующие шаги (неблокирующие)

1. **Commit.** ~16 файлов изменено/создано, старая DinD-инфра удалена.
2. **Coolify deploy** — создать новое приложение:
   - Build Pack: Docker Compose
   - Compose Location: `/docker-compose.yaml`
   - Custom Options: пусто (без `--privileged`, без `/var/lib/docker` volume)
   - Env vars: DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD, CONSOLE_TOKEN, GAME_SERVER_URL, ASPIRE_TOKEN
3. **Cutover** — остановить старое DinD-приложение, переключить DNS/домены на новое compose-приложение.
