# Memory baseline — замеры до/после миграции на docker-compose

Собрано 2026-04-21 в процессе миграции с `aspire run` на compose (см. `aspire_coolify_compose_deploy_*.md`).

## Контекст VPS

- **Провайдер:** VPS 12 GB RAM (реально 11 GiB)
- **Swap:** 0 B
- **OS:** Ubuntu
- **Uptime на момент замера:** 39 дней
- **Coolify + Laravel (PHP-FPM + Horizon)** — постоянно работают параллельно с игровым кластером
- **Postgres 17 (ml-pg)** — отдельный контейнер на порту 9432

`free -h`:
```
              total  used  free  shared  buff/cache  available
Mem:           11Gi  3.4Gi 3.0Gi   778Mi       6.1Gi       8.0Gi
Swap:           0B    0B    0B
```

Пользовательский мониторинг Coolify/VPS-панели показывает «память занята» как `used + buff/cache` — отсюда цифры вида 20–27 %. Реально процессами занято 3.4 GiB (~31 %), остальное — дисковый кеш и зарезервированная память ядра.

## Baseline `htop` (старая AppHost-модель — DinD, `aspire run --configuration Release`)

Сделано после **свежего запуска** проекта через Aspire-оркестратор (TIME+ у игровых сервисов 0:02–0:14). Build-серверы ещё висят после `dotnet build`.

### Игровой кластер (.NET сервисы)

| PID | Service | RES | SHR | CPU% | MEM% | TIME+ |
|-----|---------|-----|-----|------|------|-------|
| 600696 | ConsoleGateway | 191M | 135M | 1.3 | 1.6 | 0:09 |
| 600634 | Silo | 187M | 118M | 1.3 | 1.6 | 0:14 |
| 600616 | MetaGateway | 167M | 114M | 0.7 | 1.4 | 0:10 |
| 600576 | GameGateway | 165M | 114M | 0.0 | 1.4 | 0:09 |
| 600577 | Coordinator | 160M | 110M | 1.3 | 1.4 | 0:09 |

**Сумма 5 сервисов: ~870 MB.**

### Aspire dev-обвязка (нужна только для `aspire run`)

| PID | Process | RES |
|-----|---------|-----|
| 599933 | `dotnet exec aspire.dashboard` | 212M |
| 599562 | `Aspire.AppHost` | 188M |
| 597897 | `aspire run --configuration Release` | 169M |
| 599537 | `dotnet run --no-build --project Aspire.csproj` | 140M |
| 600377 | `dotnet run --project ConsoleGateway.csproj` | 133M |
| 600390 | `dotnet run --project MetaGateway.csproj` | 131M |
| 600374 | `dotnet run --project GameGateway.csproj` | 131M |
| 600344 | `dotnet run --project Silo.csproj` | 131M |
| 600358 | `dotnet run --project Coordinator.csproj` | 130M |
| 599662 | `aspire dcp start-apiserver` | 88M |
| 599742 | `aspire dcp run-controllers` | 85M |
| 600394 | `aspire dcp monitor-process --child` | 66M |

**Сумма Aspire-обвязки: ~1.6 GB.** Это цена работы через `aspire run`, а не прямой запуск сервисов.

### Build-серверы (оставлены после `dotnet build`, уходят через 15 мин)

| PID | Process | RES |
|-----|---------|-----|
| 598506 | `VBCSCompiler` (Roslyn build server) | 429M |
| 597997 | `MSBuild.dll /nodemode:8` | 315M |
| 597737 | `MSBuild.dll /noautoresponse` | 188M |
| 597739 | `MSBuild.dll /noautoresponse` | 184M |
| 597738 | `MSBuild.dll /noautoresponse` | 184M |
| 597812 | `MSBuild.dll /noautoresponse` | 178M |
| 597827 | `MSBuild.dll /noautoresponse` | 175M |

**Сумма: ~1.65 GB временно**, уйдут сами или через `dotnet build-server shutdown`.

### Постороннее (НЕ игровой кластер, работает постоянно)

| Процесс | Сумма RES | Примечание |
|---------|-----------|-----------|
| Coolify docker + containerd + traefik | ~530M | оркестратор контейнеров |
| 10+ php-fpm workers + horizon + scheduler | ~800–900M | Laravel-стек, TIME+ 50+ часов |
| Sentinel (`/app/sentinel`) | 95M | 10ч CPU |
| systemd-journald | 74M | 8ч45 CPU за 39 дней |
| postgres (checkpointer + bg writer) | ~187M | отдельный контейнер |

## Baseline heap-отчётов (мониторинг Silo, Coordinator, Meta, Console, Game из кластера)

Из `.telemetry/heap-reports/remote-report-04-20-17-44.txt` (ClrMD deep walk):

| Service | WorkingSet | GC Total | Gen2 | LOH |
|---------|-----------|----------|------|-----|
| Coordinator | 172.74 MB | 31.22 MB | 10.17 MB | 17.79 MB |
| Game | 184.25 MB | 31.49 MB | 10.02 MB | 17.92 MB |
| Console | 262.92 MB | 44.61 MB | 23.04 MB | 20.54 MB |
| Meta | 184.92 MB | 35.97 MB | 11.23 MB | 17.92 MB |
| Silo | 198.11 MB | 65.48 MB | 18.93 MB | 43.77 MB |

`WorkingSet` заметно больше `GC Total` — разница это nativeheap, stack, pinned pools, assembly loader, reflection cache, JIT cache.

## Утечки, найденные в managed heap

### 1. SideEffectsSnapshotRequest pipe observer на non-Silo сервисах (уже починено)

Commit `dabfa0fa` до начала задачи. В Silo раньше:
- `SideEffectsThroughputEntry`: **582,401 штук / 23.3 MB**
- `SideEffectsThroughputEntry[]`: **5,005 / 4.9 MB**

После фикса (на момент текущего отчёта):
- Silo: 16,068 / 642 KB
- Meta: 14,475 / 579 KB
- Console: 39,822 / 1.6 MB (наибольшее — aggregator)

### 2. Matchmaking `Task.Delay(100, lifetime.Token)` (не починено — отложено)

В `backend/Orchestration/MetaGateway/Matchmaking/Matchmaking.cs:179-283` поллинг 10 раз/сек без игроков:
- `TimerQueueTimer`: 1,158 → 4,664 за 44ч (+3,506)
- `DelayPromiseWithCancellation`: ≈0 → 3,047
- `List<int>` / `List<Guid>` / `OrderedIterator<int,int>`: ≈0 → 8–9k каждого

Root cause: `Task.Delay(ms, longLivedToken)` регистрирует callback на долгоживущем `CancellationTokenSource`. Ленивое удаление callback-узлов при штатном истечении delay + накопление `TimerQueueTimer` в глобальном TimerQueue. Meta WorkingSet вырос с 157.74 → 184.92 MB за 44 ч — мелкая, но стабильная утечка.

**План фикса (на будущее):** event-driven loop вместо поллинга, либо `Task.Delay(ms)` без токена + `if (lifetime.IsTerminated) break;`.

## Замеры НОВОЙ docker-compose модели

`docker compose --env-file .env.local -f docker-compose.yaml -f docker-compose.local.yaml up -d`

Сразу после поднятия всего стека (без нагрузки):

| Service | RAM | vs AppHost-модель | % |
|---------|-----|-------------------|---|
| silo | 113 MiB | 187 MB | −40 % |
| console | 103 MiB | 191 MB | −46 % |
| coordinator | 87 MiB | 160 MB | −46 % |
| game | 84 MiB | 165 MB | −49 % |
| meta | 82 MiB | 167 MB | −51 % |
| pgbouncer | 3 MiB | — | — |
| **Итого 5 сервисов** | **470 MiB** | **870 MB** | **−46 %** |

Плюс убрана вся Aspire-обвязка (~1.6 GB):
- `aspire.dashboard` 212M
- `Aspire.AppHost` 188M
- `aspire run` 169M
- 6× `dotnet run` лаунчеры ~780M
- 3× dcp контроллеры ~240M

**Итого освобождено ≈ 1.7 GB RAM.**

## Сравнительная таблица «один проект»

| Компонент | AppHost (`aspire run`) | compose (prod) |
|-----------|------------------------|----------------|
| 5 игровых сервисов | 870 MB (Debug сборка!) | **470 MB** (Release) |
| Aspire host + dashboard + dcp | 569 MB | — |
| 6× `dotnet run` лаунчеры | 780 MB | — |
| Nginx + DinD + privileged | ~50 MB + runtime overhead | — |
| pgbouncer (sibling container в compose) | — | 3 MB |
| **Итого** | **~2.25 GB** | **~473 MB** |

## Вывод

Миграция на prod-way `docker publish` + docker-compose освобождает **~1.77 GB RAM на один проект**. Для 12 GB VPS это значит: место под +2–3 таких проекта параллельно, либо комфортный headroom под нагрузку и swap-fallback.

Дополнительно:
- Убрана dev-обвязка из прод-раннера (нет `dotnet run`, нет dcp, нет DinD, нет privileged).
- Сервисы в Release-сборке (JIT-оптимизации, меньше JIT-кеша, меньше aggressive pre-compilation).
- Можно скейлить сервисы независимо (`docker compose up --scale game=2`).
- pgbouncer нормальный sibling-контейнер, без внутреннего dockerd.
- TLS-терминация Coolify Traefik, нет ручных сертификатов/nginx.
