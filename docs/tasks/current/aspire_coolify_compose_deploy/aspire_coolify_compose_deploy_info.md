## Задача: Aspire Coolify Compose Deploy

### Цель

Перевести прод-деплой кластера в Coolify с текущей DinD/privileged-схемы (один толстый контейнер с `aspire run` внутри) на prod-way модель: каждый сервис — свой Release-контейнер в `docker-compose.yaml`, Coolify Traefik терминирует TLS и роутит per-service домены. Удалить nginx, DinD, `aspire run` из прод-раннера.

Цели пользователя:
- Деплоить Aspire-проект в Coolify просто и легко (git push → auto-deploy).
- Без nginx — Coolify Traefik делает всё.
- Postgres остаётся как external Coolify-managed ресурс.
- Освободить RAM на VPS (сейчас проект жрёт ~2 GB, цель — ~700–900 MB).

### Контекст

**Текущая архитектура** (см. `tools/deploy/COOLIFY.md`):
```
Coolify host dockerd
└── deploy-container (--privileged)
    ├── nested dockerd (Docker-in-Docker)
    │   └── pgbouncer container
    ├── nginx (7000–7003 → 7100–7103)
    ├── aspire run --configuration Release (Debug в реальности — баг Aspire #13659)
    ├── Aspire AppHost + dcp
    ├── 5× dotnet run лаунчеры
    └── 5× сервисов (Silo, Coordinator, Meta, Game, Console)
```

**Проблемы текущей схемы:**
- `aspire run` → `dotnet run` не пробрасывает `--configuration Release` (Aspire bug #13659), сервисы запускаются в Debug.
- DinD требует `--privileged`, усложняет безопасность.
- AppHost + dcp + dashboard + 6× `dotnet run` лаунчеры = 1.5 GB паразитной RAM.
- Nginx дублирует функционал Coolify Traefik (WebSocket upgrade, X-Forwarded-*).
- Нельзя масштабировать сервисы независимо.

**Целевая архитектура** (Вариант 1 — без nginx, per-service домены):
```
Coolify Traefik (Let's Encrypt TLS)
├── meta.domain     → meta container :8080
├── game.domain     → game container :8080
├── console.domain  → console container :8080
└── aspire.domain   → aspire-dashboard container :18888 (опционально)

Внутри compose network (не выставлено наружу):
├── silo            (Orleans membership на 30000)
├── coordinator     (cluster-internal)
├── pgbouncer       (sibling container, pool до external Postgres)
└── (остальные сервисы видят друг друга по DNS: http://silo:8080 и т.д.)
```

External Postgres — Coolify-managed, подключение через env `DB_CONNECTION_STRING`.

**Ключевое решение:**
- Dev-режим (`aspire run` локально) остаётся как есть — AppHost/Program.cs не трогаем для dev.
- Prod-деплой через отдельный `docker-compose.yaml` + per-service Dockerfiles, **независимый** от Aspire AppHost. `aspire publish` используем один раз для scaffold'а, дальше compose ведём руками.

### Шаги реализации

**1. Исследование и scaffold**
  1.1. Добавить пакет `Aspire.Hosting.Docker` в `backend/Orchestration/Aspire/Aspire.csproj`
  1.2. Прописать `builder.AddDockerComposePublisher("docker-compose")` в AppHost Program.cs (только для publish-режима, не для run)
  1.3. Запустить локально: `aspire publish --publisher docker-compose --output-path ./deploy-artifacts`
  1.4. Изучить сгенерированный `docker-compose.yaml` и Dockerfiles, решить: использовать as-is или переписать руками

**2. Per-service Dockerfiles**
  2.1. Создать `backend/Orchestration/Silo/Dockerfile` (multi-stage: sdk → publish Release → runtime) [новый файл]
  2.2. Создать `backend/Orchestration/Coordinator/Dockerfile` [новый файл]
  2.3. Создать `backend/Orchestration/MetaGateway/Dockerfile` [новый файл]
  2.4. Создать `backend/Orchestration/GameGateway/Dockerfile` [новый файл]
  2.5. Создать `backend/Orchestration/ConsoleGateway/Dockerfile` [новый файл]
  2.6. Общий base Dockerfile шаблон: `mcr.microsoft.com/dotnet/sdk:10.0` для build-stage, `mcr.microsoft.com/dotnet/aspnet:10.0` для runtime; `dotnet publish -c Release /p:UseAppHost=false`

**3. Root docker-compose.yaml**
  3.1. Создать `docker-compose.yaml` в корне репо [новый файл]
  3.2. Определить 5 сервисов (silo, coordinator, meta, game, console) + pgbouncer + опционально aspire-dashboard
  3.3. Указать `build.context` и `build.dockerfile` для каждого сервиса
  3.4. Одна общая compose-сеть (`default`), сервисы видят друг друга по имени
  3.5. Для внешних сервисов (meta/game/console): `expose:` без `ports:` (Coolify Traefik сам подключится)
  3.6. Для publishing доменов — env `SERVICE_FQDN_<NAME>_8080=/` (Coolify auto-assign) или Traefik labels

**4. PgBouncer вне Aspire**
  4.1. Вынести pgbouncer из `PgBouncerFactory.cs` в compose-сервис (использовать `edoburu/pgbouncer` образ)
  4.2. Сгенерировать static `databases.ini` / `userlist.txt` из env vars через entrypoint-скрипт внутри pgbouncer контейнера, или использовать `DATABASES_HOST`/`DATABASES_PORT` env-схему образа
  4.3. Сервисы подключаются к `pgbouncer:5433` (не `127.0.0.1:5433`)
  4.4. `DB_CONNECTION_STRING` разбирается compose'ом в компоненты для pgbouncer, и собирается в Npgsql-строку для сервисов

**5. Env vars для сервисов**
  5.1. Каждый сервис получает `ConnectionStrings__postgres=Host=pgbouncer;Port=5433;Database=...;Username=...;Password=...`
  5.2. Console получает `CONSOLE_TOKEN`
  5.3. Game получает `GAME_SERVER_URL`
  5.4. Orleans clustering: все сервисы получают одинаковый `ORLEANS_CLUSTER_ID` и Postgres clustering provider
  5.5. Service discovery: встроенный Aspire service discovery работает через env vars (`services__<name>__http__0`) — прописать руками, или использовать compose DNS (`http://silo:8080`)

**6. Aspire Dashboard (опционально)**
  6.1. Добавить контейнер `mcr.microsoft.com/dotnet/aspire-dashboard:latest` в compose
  6.2. Сервисы отправляют OTel на `aspire-dashboard:18889` (env `OTEL_EXPORTER_OTLP_ENDPOINT`)
  6.3. Защита — `DASHBOARD__FRONTEND__AUTHMODE=BrowserToken` + `DASHBOARD__FRONTEND__BROWSERTOKEN=${ASPIRE_TOKEN}`

**7. Удалить/упростить tools/deploy**
  7.1. Удалить `tools/deploy/Dockerfile` (DinD не нужен) или пометить как legacy
  7.2. Удалить `tools/deploy/entrypoint.sh`, `Steps/Nginx.cs`, `Steps/Application.cs`, `nginx-server.template`
  7.3. Оставить `tools/deploy/Validation.cs` если валидация env vars ещё нужна — но встроить в compose через healthchecks
  7.4. Обновить `tools/deploy/COOLIFY.md` → переписать под новую модель

**8. AppHost compatibility для dev**
  8.1. Проверить что `aspire run` локально работает после добавления `Aspire.Hosting.Docker` пакета
  8.2. Убедиться что dev-режим не пытается использовать docker-compose publisher при `aspire run`
  8.3. Оставить `PgBouncerFactory` / `DbUpstreamFactory` для dev-режима

**9. Coolify конфигурация**
  9.1. Обновить `tools/deploy/COOLIFY.md`: Build Pack = **Docker Compose**, Compose Location = `/docker-compose.yaml`
  9.2. Убрать `--privileged`, убрать volume `/var/lib/docker`
  9.3. Настроить env vars в Coolify UI: `DB_CONNECTION_STRING`, `CONSOLE_TOKEN`, `GAME_SERVER_URL`, `ASPIRE_TOKEN`
  9.4. Настроить домены в Coolify UI per-service или через `SERVICE_FQDN_*` env

**10. Локальная валидация**
  10.1. `docker compose build` в корне репо
  10.2. `docker compose up -d` с локальным Postgres в env
  10.3. Проверить: сервисы стартуют, видят друг друга, Orleans кластер собирается
  10.4. Проверить WebSocket на Console (Blazor Server) через reverse proxy (например, Caddy для имитации Coolify Traefik)
  10.5. Замерить RAM — должно быть ~700–900 MB для всего стека vs ~2 GB сейчас

**11. Прод-деплой и cutover**
  11.1. Подготовить staging-домены в Coolify
  11.2. Задеплоить новый compose-стек параллельно со старым
  11.3. Проверить с реальными клиентами
  11.4. Переключить DNS / домены → новый стек
  11.5. Снести старый DinD деплой

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Orchestration/Aspire/Program.cs` | AppHost — trigger публикации, минимальные изменения для dev-compat |
| `backend/Orchestration/Aspire/Aspire.csproj` | Добавить `Aspire.Hosting.Docker` |
| `backend/Orchestration/Aspire/Startup/PgBouncerFactory.cs` | Логика pgbouncer для dev, в проде — отдельный compose-контейнер |
| `backend/Orchestration/*/Program.cs` × 5 | Сервисы — возможно нужны правки для service discovery через compose DNS |
| `docker-compose.yaml` | **Новый** — корневой compose для Coolify |
| `backend/Orchestration/*/Dockerfile` × 5 | **Новые** — per-service multi-stage |
| `tools/deploy/Dockerfile` | Удалить или пометить legacy |
| `tools/deploy/entrypoint.sh` | Удалить |
| `tools/deploy/Steps/Nginx.cs` | Удалить |
| `tools/deploy/Steps/Application.cs` | Удалить |
| `tools/deploy/nginx-server.template` | Удалить |
| `tools/deploy/COOLIFY.md` | Переписать под новую модель |

### Документация к прочтению

- `backend/Orchestration/Aspire/Program.cs` — чтобы понять текущий AppHost и что он настраивает для каждого сервиса
- `tools/deploy/COOLIFY.md` — текущая документация прод-деплоя (источник истины для текущей схемы)
- [Aspire publishing and deployment overview](https://aspire.dev/deployment/overview/) — официальная модель
- [Coolify Docker Compose docs](https://coolify.io/docs/knowledge-base/docker/compose) — build pack и SERVICE_FQDN
- Найти: как Aspire service discovery работает в compose — возможно нужны `services__<name>__http__0` env vars

### Риски

1. **Orleans clustering в compose.** Все сервисы должны быть в одной compose-сети, с общим cluster ID, clustering provider'ом (Postgres). Если AppHost текущий полагается на Aspire service discovery — нужно реплицировать через compose DNS + env vars.

2. **Service discovery между сервисами.** Сейчас Aspire прокидывает `services__silo__http__0=http://silo:<port>` каждому клиенту. В compose должно работать через compose DNS, но возможно нужны явные env vars.

3. **PgBouncer dynamic DNS resolution.** Текущий `PgBouncerFactory` резолвит DNS внешнего Postgres в IP и пишет в `databases.ini` — потому что pgbouncer в инном dockerd не видит Coolify DNS. В новой схеме pgbouncer — sibling в compose, Coolify DNS доступен, можно напрямую использовать hostname без резолва.

4. **WebSocket для Blazor Console через Traefik.** Traefik должен работать, но надо протестировать. Если не работает из коробки — добавить Traefik middleware.

5. **Aspire Dashboard в проде.** Можно оставить, но 200 MB RAM. Альтернатива — внешний OTel backend или вообще без дашборда.

6. **Secrets/credentials.** В compose нужно аккуратно передавать пароли БД, токены. Через Coolify env-инжекцию, не в репо.

7. **Миграция без даунтайма.** Кластер Orleans с Postgres clustering — если поднять два стека одновременно с тем же cluster ID, будут конфликты. Лучше cutover через отдельный cluster ID или остановку старого.

8. **Aspire publish output quality.** Сгенерированный compose может быть слишком примитивным или некорректным для нетривиальных случаев (pgbouncer, Orleans). Скорее всего придётся писать руками, используя publish только как inspiration.
