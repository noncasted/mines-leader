# B10 + B11: handoff

Статус: B10 сделана (review), B11 не подтверждена (без правок).

## Шаг 0: подтверждение

Файлы B побайтно совпадали с A на старте этапа (`0a67a0b08^`): `FileLoggerProvider.cs`, `TelemetryPaths.cs`,
`MetricsSnapshotService.cs`, `MartenSetupExtensions.cs`, `PgBouncerFactory.cs`, `pgbouncer.ini`. Баги подтверждены чтением кода:

- FileLogger: синхронная запись под lock с `AutoFlush`, пустой `catch`, `_writer == null` молча глотает логи; включён во всех окружениях, в том числе в проде.
- pgbouncer: `edoburu/pgbouncer:latest` в compose и в `PgBouncerFactory`; в `pgbouncer.ini` стоит `suspend_timeout`, который 1.26 отвергает.
- TelemetryPaths: ищет только каталог `.git`, в git worktree (`.git` — файл) корень не находится. `MetricsSnapshotService` без корня молча не пишет (`if (dir == null) return;`).
- Marten: типы event-state ищутся сканом `AppDomain` с `catch { continue; }` и пишут `/tmp/marten_setup.log` при каждом старте (и в проде). Тестовая фикстура дублирует этот скан.
- Benchmark-таблицы: `StatesSetup` создаёт таблицы для всех `StatesLookup.All`, включая `state_benchmark` и прочие состояния сборки Benchmarks. Кроме того, бенчмарки в B зарегистрированы всегда, в том числе в проде (runner, nodes, `TestCleanup`, удаляющий state-строки, API `/api/benchmarks` и страница консоли).

B11: `SessionFileLogger` в B **не мёртвый**. `ISessionLogger` внедряется в `GameFlow`, раунды, `BotRunner`, профили и действия ботов, `GameCommandUtils`, регистрируется в `SessionExtensions`, кормит `IObservationEventBuffer` (наблюдения агента). Не удалял. Хунк A `bdf86c516` (`RequireTelemetryDir` в `SessionFileLogger`) тоже не переносил: в проде нет `.git`, логгер упал бы при создании каждой сессии.

## Что сделано

- `d6d9ae33c` (патч): `FileLoggerOptions` (секция `FileLogging`), фоновый писатель через `Channel`, регистрация только в Development (`AddFileLogging`), fail-fast без корня репозитория; `pgbouncer.ini` без `suspend_timeout`; `PgBouncerFactory` на `v1.26.0-p0`. Тест `Telemetry/FileLoggerProviderTests.cs`.
- compose: `edoburu/pgbouncer:v1.26.0-p0` (правка поверх S1/B7).
- `bdf86c516` (патч, только TelemetryPaths и MetricsSnapshot): `RequireTelemetryDir`, `MetricsSnapshotService` dev-only, в Development без `.git` падает на старте. Тест `Telemetry/TelemetryPathsTests.cs`.
- `a1203e5d8`: `.git`-файл `gitdir:` (worktree) считается корнем; Marten берёт типы из `IGrainStatesRegistry` (`MartenSetupExtensions.Configure`/`EventStateTypes`), фикстура использует ту же настройку; `StatesLookup.Info.Type` в генераторе; `DeployStates.Select` + `StatesSetup` создают benchmark-таблицы только при `Benchmarks:Enabled`. Тесты `State/MartenSetupTests.cs`, `Deploy/DeployStatesTests.cs`, хунки `Deploy/StatesSchemaTests.cs`.
- `42a971126` (только бенчмарк-часть, без composition root): `BenchmarksAvailability` (флаг `Benchmarks:Enabled`, только Development и локальная БД), при выключенном флаге бенчмарк-сервисы не регистрируются, классы грейнов Benchmarks убираются из `GrainTypeOptions`, API не маппится, страница консоли показывает "Benchmarks are off". Aspire включает флаг для всех проектов и для in-process мигратора.
- Документация: `.claude/docs/TELEMETRY.md`, `.claude/docs/DEPLOY.md`.

## Отличия от A

- Нет `BenchmarksModule`/`IGameModule` (это B12): `BenchmarksAvailability` лежит в `Tools/Benchmarks/Common/BenchmarksAvailability.cs` со статическим `Assembly`, гейт сделан в `ProjectsSetupExtensions.AddTests`/`SetupConsole`.
- В B тестовая сборка генерирует свои `StatesLookup`/`GeneratedStatesRegistration` (с тестовыми состояниями). Добавил `DeployStates.All` (список мигратора); тесты A, которые брали `global::Shared.StatesLookup.All` и `GeneratedStatesRegistration`, читают его через `DeployStates.All`.
- Тест генератора `Codegen/StatesLookupGeneratorTests.cs` в B отсутствует, хунк не переносился.
- Хунки `.env.example`, Orleans ClusterId из `d6d9ae33c` не трогал (B7).
- `SessionFileLogger` без изменений (см. B11).

## Проверка

- `dotnet build backend/backend.slnx`: зелёная (новых предупреждений нет).
- Новые тесты (FileLoggerProvider, TelemetryPaths, MartenSetup, DeployStates, StatesSchema): 25/25.
- Полный прогон: 1196/1198, падают только 2 `Tests.Meta.UserDeckTests` (известное, B13). Пропущенных нет (Docker доступен).
- Aspire не запускал: в worktree рядом работают другие сессии, а `Local:KillPrevious` убивает процессы. Нужна живая проверка в основном дереве.

## Деплой

- pgbouncer в compose переходит с `latest` на `v1.26.0-p0`. Если в проде `latest` уже подтянул 1.26, это no-op; если стоит старый образ, Coolify скачает новый тег. Compose-pgbouncer настраивается env-переменными (`pgbouncer.ini` правится только в Aspire), `suspend_timeout` там не задаётся.
- Прод теперь без файловых логов (`FileLogger` только в Development) и без метрик-снапшотов: логи и метрики идут только через OTEL.
- Бенчмарки в проде выключены: нет `/api/benchmarks`, страница консоли показывает "off". Новые БД не получают benchmark-таблицы; в существующей прод-БД старые `state_benchmark` и прочие таблицы остаются (мигратор их не удаляет).
- Если `Benchmarks__Enabled=true` случайно задать в проде, хосты упадут на старте (не Development).
- Замечание вне задачи: `SessionFileLogger` в проде пишет файл на каждую сессию в `logs/sessions` внутри контейнера (без ротации, теряется при пересоздании контейнера, ошибки записи глотаются).
