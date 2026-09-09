# State perf: меньше раунд-трипов на операцию

## Контекст

Прогон группы State 2026-09-09 локально и на проде (`console.minesleader.xyz`) показал:

- **Локально** всё упирается в fsync коммита Postgres (btrfs + dm-crypt, ~8–10 мс на fdatasync). 93% выборок `pg_stat_activity` это `COMMIT` в ожидании `WALWrite`/`WalSync`. С `synchronous_commit=off` `state` даёт 10 159 ops/s вместо 963.
- **На проде** fsync быстрый, silo занят на ~1 ядро из 6, БД в среднем 2.6 активных бэкендов, 22% выборок это `idle in transaction / ClientRead` (Postgres ждёт клиента между `BEGIN`, `INSERT`, `COMMIT`). Система latency-bound: throughput = concurrency / длина цепочки сетевых обращений.

Значит, единственный рычаг для прода это **сократить число обращений туда-обратно на одну операцию**: к Postgres (через pgbouncer) и между console/silo (Orleans RPC).

Референсные цифры (ops/s), от которых считаем прирост:

| Бенчмарк | Локально sync=on | Локально sync=off | Прод |
|---|---|---|---|
| state | 963 | 10 159 | 2 012 |
| transactions-state | 297 | 3 433 | 882 |
| transactions-state-value | 1 042 | 7 632 | 1 556 |
| transactions-large-batch | 249 | – | 130 |
| event-state | 1 621 | 4 774 | 815 |
| event-storage | 929 | – | 846 |
| event-state-transaction | 275 | 1 870 | 440 |

Исходные замеры лежат в истории бенчмарков (локальная БД в контейнере `mines-leader-postgres-data`, прод через `/api/benchmarks/{title}/history`).

## Правила для исполнителя

1. **Один этап = одна ветка правок + тесты + бенчмарки + запись результата в этот файл.** Не переходить к следующему этапу, пока предыдущий не зафиксирован в разделе «Прогресс».
2. **Все тесты обязательны после каждого этапа**, не только State. Транзакции и стейты используются игрой и метой, регрессия может всплыть в `Tests.Game` или `Tests.Meta`.
3. **Бенчмарки гоняем локально с `synchronous_commit=off`**, иначе fsync (8–10 мс) съедает эффект любой правки. После работы вернуть `on`.
4. Бенчмарки сравниваем с baseline через `/api/benchmarks/{title}/compare`. Baseline ставится один раз на этапе 0 через `/api/benchmarks/{id}/set-baseline`.
5. Регрессия любого State-бенчмарка >10% при отсутствии роста целевого это стоп и разбор, а не переход дальше.
6. Никаких изменений на проде. Прод-прогон делает человек после мержа.

## Инструменты

### Тесты

`dotnet test` не работает на SDK 10 (см. `.agents/docs/TESTING.md` и memory `backend-tests-run-exe`). Запуск:

```bash
dotnet build backend/Tools/Tests/Tests.csproj
backend/Tools/Tests/bin/Debug/net10.0/Tests                       # все
backend/Tools/Tests/bin/Debug/net10.0/Tests --filter-namespace Tests.State
backend/Tools/Tests/bin/Debug/net10.0/Tests --filter-class "*TransactionTests*"
```

Логи UTF-16LE, читать через `tools/scripts/get-test-log.sh`. Тесты поднимают свой Postgres через Testcontainers, локальный Aspire для них не нужен.

Ключевые классы: `backend/Tools/Tests/State/` (StateReadWriteTests, StateStorageBatchTests, TransactionTests, EventStateTests, EventStorageTests, StateStorageEventTests, StateMigrationTests, StateCollection*Tests, AddressableStateTests), плюс `Tests.Grains`, `Tests.Game`, `Tests.Meta`, `Tests.Messaging`.

### Кластер и бенчмарки

```bash
# старт (из корня репо), консоль слушает http://localhost:7103
cd backend/Orchestration/Aspire && ASPIRE_ALLOW_UNSECURED_TRANSPORT=true dotnet run --no-launch-profile
# готовность
until curl -s -o /dev/null -w '%{http_code}' http://localhost:7103/api/benchmarks | grep -q 200; do sleep 5; done
```

`tools/scripts/benchmark-*.sh` и `aspire-poll.sh` переведены на 7103 (переопределяется переменной `CONSOLE_URL`).

```bash
B=http://localhost:7103/api/benchmarks
curl -s -X POST $B/group/State/run                    # вся группа, идёт последовательно
curl -s -X POST "$B/transactions-state/run"           # один
curl -s $B/group/State | python3 -c "import sys,json; print([x['title'] for x in json.load(sys.stdin) if x['isRunning']])"
curl -s "$B/state/history"                            # [{id, metricValue, durationMs, samples...}]
curl -s -X POST "$B/{id}/set-baseline"
curl -s "$B/state/compare"                            # {latestMetricValue, baselineMetricValue, regressionPercent, isRegression}
```

Ожидание окончания группы: опрашивать `group/State` пока есть `isRunning`, интервал 10–15 с. Группа занимает ~6–8 минут.

### synchronous_commit локально

```bash
PG=$(docker ps -qf name=postgres)
docker exec $PG psql -U postgres -d postgres -Atc "ALTER SYSTEM SET synchronous_commit = off"
docker exec $PG psql -U postgres -d postgres -Atc "SELECT pg_reload_conf()"
docker exec $PG psql -U postgres -d postgres -Atc "show synchronous_commit"
# вернуть
docker exec $PG psql -U postgres -d postgres -Atc "ALTER SYSTEM RESET synchronous_commit"
docker exec $PG psql -U postgres -d postgres -Atc "SELECT pg_reload_conf()"
```

`ALTER SYSTEM` нельзя запускать в одной `-c` вместе с другими командами.

### Диагностика раунд-трипов

Сэмплер `pg_stat_activity` показывает, сколько времени БД ждёт клиента (`idle in transaction`) против работы:

```bash
docker exec $PG psql -U postgres -d postgres -Atc "select state, wait_event_type, wait_event, left(query,80) from pg_stat_activity where datname='postgres' and state<>'idle' and pid<>pg_backend_pid()"
```

Гонять в цикле с `sleep 0.3` в файл во время бенчмарка, потом `sort | uniq -c`. Доля `idle in transaction|Client|ClientRead` должна падать от этапа к этапу.

Гистограммы `Backend` метра каждого процесса пишутся в `backend/.telemetry/metrics/metrics_{service}.json` раз в 10 с (`MetricsSnapshotService`). Поля `backend.state.write.duration`, `backend.transactions.duration` дают avg/max.

## Этап 0. Baseline

Цель: зафиксировать точку отсчёта на текущем коде и в тех же условиях, в которых будут меряться правки.

1. Собрать и прогнать все тесты, убедиться что зелёные. Записать количество пройденных.
2. Поднять кластер, поставить `synchronous_commit=off`.
3. Прогнать группу State **два раза** подряд. Первый прогрев, второй baseline.
4. Для каждого бенчмарка группы взять `id` последней записи из `history` и вызвать `set-baseline`.
5. Записать таблицу ops/s всех 17 бенчмарков в «Прогресс».
6. Снять сэмпл `pg_stat_activity` во время прогона `state` и `transactions-state`, записать долю `idle in transaction`.

Критерий: baseline проставлен, `compare` для каждого бенчмарка возвращает `regressionPercent` около 0.

## Этап 1. DirectStorage.Write без явной транзакции

**Проблема.** `backend/Infrastructure/Orleans/State/Direct/DirectStorage.cs:181` в ветке без `request.Transaction`: `OpenConnection` → `BeginTransactionAsync` (BEGIN) → `WriteBatch` (INSERT) → `CommitAsync` (COMMIT). Три обращения к БД на одну запись. Один `INSERT ... ON CONFLICT` атомарен сам по себе, транзакция вокруг него не нужна.

**Правка.**

- В `Write` при `request.Transaction == null` и когда `WriteBatch` сформирует **одну** команду (одна группа `(TableName, HasExtension)`), выполнять её напрямую на соединении без транзакции.
- Если групп больше одной (записи в разные таблицы или с/без extension), оставить транзакцию: атомарность между таблицами нужна.
- Для этого `WriteBatch` разбить на два шага: построение списка `NpgsqlCommand`/`NpgsqlBatchCommand` по группам и выполнение. Выполнение: одна команда → `ExecuteNonQueryAsync` на соединении; несколько → транзакция как сейчас.
- То же самое для `Delete` (`DirectStorage.cs:219`): одна группа → без транзакции.
- Метрики `StateWriteDuration`, `StateWriteTotal`, `StateWriteBatchSize` не трогать.

**Не трогать.** Ветку с внешним `request.Transaction`, её использует `Transactions.Process`.

**Тесты.** Все. Особо: `StateReadWriteTests`, `StateStorageBatchTests`, `AddressableStateTests`, `StateCollection*Tests` (у них массовые записи), `Tests.Meta` (пользовательские стейты).

**Бенчмарки.** Группа State целиком. Целевые: `state`, `state-migration-concurrent`. Ожидание: `state` +20–40% локально с sync=off (было 10 159). На проде эффект больше, там раунд-трип дороже.

**Проверка диагностикой.** Во время `state` в сэмпле `pg_stat_activity` не должно быть `idle in transaction` по запросам к `state_test_default_state`.

## Этап 2. EventStorage: убрать лишний AggregateStreamAsync и батчить ReadBatch

**Проблема.** `backend/Infrastructure/Orleans/State/Events/EventStorage.cs:66-76` `Read`: `LoadAsync` снапшота, при null ещё `AggregateStreamAsync`. Для нового стрима оба возвращают null, второй запрос идёт впустую. Снапшот зарегистрирован как inline-проекция (`MartenSetupExtensions.cs`, `SnapshotLifecycle.Inline`), то есть при каждом `Append` он обновляется в той же транзакции и отставать от событий не может. `ReadBatch` (`EventStorage.cs:160-181`) читает стримы по одному в цикле, N обращений вместо одного.

**Правка.**

- `Read`: при `LoadAsync == null` возвращать `new T()` без `AggregateStreamAsync`. Fallback на агрегацию оставить только в `TryLoadAsync` на `JsonException` (битый снапшот), как сейчас.
- Если нужна страховка на случай отставания снапшота (например, после ручного восстановления БД), добавить `IEventStorage.Rebuild<T>(streamId)` с явной агрегацией, но не звать её из горячего пути.
- `ReadBatch`: `session.LoadManyAsync<TValue>(streamIds)` одним запросом, затем для отсутствующих id ничего не делать (как и `Read`, отсутствие снапшота = отсутствие стрима). `SetIdFromStream` и `ParseStreamKey` сохранить.
- `StateStorage.ReadBatchEventSourced` не трогать, он просто делегирует.

**Тесты.** Все. Особо: `EventStateTests`, `EventStorageTests`, `StateStorageEventTests`, `Tests.Meta` (UserMatchHistoryAggregate и другие event-состояния меты, см. `mt_doc_user*`). Если есть тест, который пишет события мимо снапшота и ждёт агрегации при чтении, он упадёт. Это ожидаемо: такой тест нужно переписать на `Rebuild`, а не возвращать старое поведение.

**Бенчмарки.** Группа State. Целевые: `event-state`, `event-storage`, `event-state-transaction`, `event-state-transaction-chained`. Ожидание: `event-state` и `event-storage` +15–30%.

**Проверка диагностикой.** В сэмпле `pg_stat_activity` во время `event-state` не должно быть запросов `select ... from mt_events ... where stream_id` для новых стримов.

## Этап 3. Transactions.Process: один пакет на коммит

**Проблема.** `backend/Infrastructure/Orleans/Transactions/Transactions.cs:121-138`: `OpenConnection` → `BeginTransaction` → `_stateStorage.Write(transaction, ...)` (по команде на группу таблиц) → `_eventStorage.Write` (Marten-сессия) → `_sideEffectsStorage.Write` → callbacks → `Commit`. Каждый шаг ждёт ответа. Транзакция здесь нужна, убирать нельзя, но ожидать после каждой команды не обязательно.

**Правка.**

- Собрать в `NpgsqlBatch` всё, что идёт через `NpgsqlTransaction` напрямую: команды `DirectStorage.WriteBatch` (после этапа 1 они уже строятся отдельно от выполнения) и вставки `SideEffectsStorage`. Один `ExecuteNonQueryAsync` на батч.
- Marten-часть (`_eventStorage.Write`) оставить как есть: она использует `SessionOptions.ForTransaction` и свой пайплайн, в `NpgsqlBatch` её не положить. Выполнять её до или после батча, порядок неважен внутри одной транзакции.
- Callbacks (`parameters.Callbacks`) выполнять после батча, как сейчас.
- `BEGIN` и `COMMIT` остаются отдельными обращениями. Итог для транзакции с одним direct-стейтом: BEGIN, батч, COMMIT = 3 вместо 3+N.
- Метрики оставить.

Этот этап даёт мало на транзакции с одним участником и заметно на `transactions-large-batch` (20 стейтов) и на реальных игровых транзакциях, где пишутся несколько стейтов и side effects.

**Тесты.** Все. Особо: `TransactionTests` (18 кейсов), `Tests.Grains`, `Tests.Game` (игровые транзакции с side effects), `SideEffect*` в `Tests.Messaging`.

**Бенчмарки.** Группа State. Целевые: `transactions-large-batch`, `transactions-state-chained`, `transactions-state`. Ожидание: `transactions-large-batch` +20%, остальные ±5%.

## Этап 4. Orleans RPC: убрать отдельный Join и вернуть CollectResult вместе с ответом

**Проблема.** На одну транзакцию с одним грейном console делает четыре обращения к silo:

1. `TransactionAttribute.cs:77` `Join` на обработчике грейна (отдельный RPC до вызова метода).
2. Сам метод грейна.
3. `Transactions.cs:202` `CollectResult`.
4. `Transactions.cs:140` `OnSuccess`.

Для `transactions-large-batch` с 20 грейнами это ~80 RPC. На проде каждый стоит 0.3–1 мс, и они последовательные внутри цепочки.

Это самый рискованный этап: меняется протокол транзакций. Делать в два подэтапа с тестами и бенчмарками между ними.

**4a. Join внутри вызова.** `TransactionRequestBase.Invoke` уже выполняется на silo, в контексте целевого грейна. `Join` можно звать не через grain reference, а локально: получить `IGrainContext` цели (через `this.GetTarget()` / `RuntimeContext`) и `GetComponent<IGrainTransactionHandler>()`. Тогда `Join` это вызов метода в памяти, а не сообщение. `participantId` в `Context.Participants` класть как сейчас. Проверить, что семафор и логика takeover в `GrainTransactionHandler.Join` не зависят от того, что вызов приходит через messaging (там `[AlwaysInterleave]`, при локальном вызове мы уже внутри activation, интерливинг не нужен).

**4b. CollectResult в ответе.** `TransactionResponse` уже несёт `Context` обратно. Добавить в него снапшот стейтов и событий участника (`TransactionHandlerResult`) на момент возврата из метода. В `Transactions.Process` при `CollectStates` использовать данные из контекста для тех участников, у которых они есть, и звать `CollectResult` только для остальных (например, при вложенных вызовах, где участник не возвращал ответ напрямую). Внимание: если один грейн вызывается несколько раз в одной транзакции, брать последний снапшот. Проще всего хранить в `TransactionContext` словарь `participantId → TransactionHandlerResult` и перезаписывать.

`OnSuccess`/`OnFailure` оставить как есть. Их можно сделать `[OneWay]`, но тогда теряется гарантия, что грейн освободил семафор до возврата результата вызывающему. Не делать в рамках этой задачи.

**Тесты.** Все, два раза (после 4a и после 4b). Особо: `TransactionTests` целиком, включая chained/fail/overlapping сценарии, `Tests.Grains`, `Tests.Game`. Добавить тест: транзакция, где один грейн вызывается дважды и второй вызов меняет стейт, в БД должно попасть последнее значение.

**Бенчмарки.** Группа State после каждого подэтапа. Целевые: `transactions-single-target`, `transactions-single-chain`, `transactions-large-batch`, `transactions-state`. Ожидание после 4a+4b: `transactions-large-batch` +30–50%, `transactions-state` +15–25%. `transactions-state-chained-fail` и `transactions-state-overlapping` не должны упасть: они проверяют rollback и конкуренцию.

## Этап 5. Бенчмарк на прогретых грейнах

**Проблема.** Все State-бенчмарки создают грейн через `Guid.NewGuid()` на каждую итерацию. В ops/s зашита активация Orleans (directory lookup, размещение, конструктор). Это мешает видеть чистую стоимость стейта и сравнивать этапы.

**Правка.** Добавить два бенчмарка в `backend/Tools/Benchmarks/State/`:

- `state-warm`: payload `GrainCount = 100`, `Iterations`, `Concurrent`. Перед `RunConcurrentIterations` активировать 100 грейнов (`Test()` по разу), затем итерации ходят по кругу по этим id. Метрика ops/s.
- `transactions-state-warm`: то же для `ITransactionTestGrain.Increment`.

Регистрация бенчмарков смотрится по соседним классам (`TestsExtensions.cs`, `ProjectsSetupExtensions.cs`). Cleanup: трекать id через `Cleanup.Track`, чтобы таблицы не росли.

Заодно: `TestCleanup` сейчас не удаляет direct-стейты транзакционных тестов, `state_test_transactional_state` и `state_test_default_state` растут на каждом прогоне (165k и 83k строк на 2026-09-09). Добавить `Cleanup.Track` в `TransactionStateTest` и `StateTest` и остальные, где id создаётся через `Guid.NewGuid()`.

**Тесты.** Все (бенчмарки на тесты не влияют, но cleanup трогает `IStateStorage.Delete`).

**Бенчмарки.** Группа State. Новые бенчмарки прогнать дважды, второй прогон записать как baseline для них.

## Этап 6. Финал

1. Вернуть `synchronous_commit` в `on`, прогнать группу State ещё раз, записать цифры (это то, что увидит человек без специальной настройки).
2. Полный прогон всех тестов.
3. Заполнить сводную таблицу «Прогресс»: baseline → после каждого этапа, ops/s и `regressionPercent` из `compare`.
4. Записать в раздел «Заметки» всё, что отклонилось от плана: тесты, которые пришлось переписать, места, где эффект оказался меньше ожидаемого, и почему.
5. Прод-прогон после мержа делает человек: `POST https://console.minesleader.xyz/api/benchmarks/group/State/run` с cookie от `/login?token=...` (токен в `backend/Orchestration/Aspire/appsettings.local.json`). Сравнить с прод-цифрами из таблицы в начале документа.

## Прогресс

Заполняется исполнителем. Формат для каждого этапа:

```
### Этап N — <дата>

Тесты: <пройдено>/<всего>, упавшие: <список или нет>

Бенчмарки (sync=off), ops/s и % к baseline:

| бенчмарк | baseline | после этапа | % |

pg_stat_activity: idle in transaction <X>% (было <Y>%)

Отклонения от плана: ...
```

### Этап 0 — 2026-09-10

Тесты: 991/991, упавшие: нет

Бенчмарки (sync=off), baseline, ops/s (второй прогон группы, первый прогрев):

| бенчмарк | прогрев | baseline |
|---|---|---|
| event-state | 4839 | 5543 |
| event-state-transaction-chained | 917 | 951 |
| event-state-transaction-concurrent | 250 | 248 |
| event-state-transaction | 1918 | 1954 |
| event-storage | 5528 | 5346 |
| state-migration-concurrent | 8358 | 8171 |
| state | 11864 | 11547 |
| transactions-concurrent-value | 1937 | 1880 |
| transactions-cross-path-read | 3720 | 3712 |
| transactions-large-batch | 563 | 525 |
| transactions-single-target | 854 | 848 |
| transactions-single-chain | 1704 | 1762 |
| transactions-state-chained-fail | 1108 | 1138 |
| transactions-state-chained | 1982 | 1950 |
| transactions-state-overlapping | 1044 | 945 |
| transactions-state | 4021 | 3342 |
| transactions-state-value | 8656 | 8275 |

`compare` после `set-baseline` для всех 17: regressionPercent = 0.0.

pg_stat_activity (sync=off, шаг 0.1 с): `state` idle in transaction 52% (49/94), `transactions-state` 28% (7/25).

Отклонения от плана:

- Локальный том `mines-leader-postgres-data` был инициализирован под `postgres/postgres`, а `appsettings.local.json` (в .gitignore) теперь задаёт `mslead-root`/`mslead`. В контейнере создана роль `mslead-root` (superuser) и БД `mslead`, схему создал DeploySetup. Старые таблицы в БД `postgres` не трогались. Все команды psql в этом документе для БД `mslead`.
- Консоль локально требует логин: `curl -c cookies "http://localhost:7103/login?token=<ConsoleToken из appsettings.local.json>"`, дальше `-b cookies`.
- Группа State локально проходит за ~1 мин, а не 6–8 (sync=off).
- Разброс между двумя прогонами до 17% (`transactions-state` 4021 → 3342), поэтому для каждого этапа группа гоняется дважды и берётся второй прогон.

### Этап 1 — 2026-09-10

Правка: `DirectStorage.Write`/`Delete` строят список `NpgsqlBatchCommand` по группам `(TableName, HasExtension)` (`BuildWriteCommands`/`BuildDeleteCommands`) и выполняют их через `NpgsqlBatch`. Без внешней транзакции: одна команда → напрямую на соединении, несколько → локальная транзакция как раньше. Ветка с `request.Transaction` выполняет весь список одним батчем в переданной транзакции.

Тесты: 991/991, упавшие: нет

Бенчмарки (sync=off), ops/s и % к baseline (два прогона, compare по второму):

| бенчмарк | baseline | прогон 1 | прогон 2 | % |
|---|---|---|---|---|
| state | 11547 | 13944 | 13101 | +22.9 (14192 в диагностическом прогоне) |
| state-migration-concurrent | 8171 | 8875 | 8331 | +2.0 |
| transactions-state | 3342 | 4007 | 3925 | +17.5 |
| transactions-state-overlapping | 945 | 1067 | 1035 | +9.5 |
| transactions-state-value | 8275 | 8870 | 8678 | +4.9 |
| transactions-large-batch | 525 | 570 | 534 | +1.7 |
| event-state | 5543 | 5165 | 5568 | +0.4 |
| event-storage | 5346 | 5527 | 5001 | -6.5 |
| event-state-transaction | 1954 | 1982 | 1825 | -6.6 |
| transactions-cross-path-read | 3712 | 3697 | 3471 | -6.5 |
| остальные | | | | в пределах ±5 |

pg_stat_activity во время `state`: idle in transaction 0% (было 52%), запросов BEGIN/COMMIT к `state_test_default_state` нет.

Отклонения от плана: прирост `state` +13–23% вместо ожидаемых +20–40%: локально с sync=off раунд-трип к Postgres через pgbouncer ~0.1 мс, и доля BEGIN/COMMIT в операции меньше, чем на проде. Отрицательные отклонения (-6.5%) на event-бенчмарках лежат внутри разброса между двумя прогонами (event-storage 5527 → 5001 при одном и том же коде).

### Этап 2 — 2026-09-10

Правка: `EventStorage.Read` при `LoadAsync == null` возвращает `new T()` без `AggregateStreamAsync`; добавлен `IEventStorage.Rebuild<T>(streamId)` с явной агрегацией (из горячего пути не зовётся); `ReadBatch` читает все снапшоты одним `LoadManyAsync`, ключ берётся из `Id` снапшота через `ParseStreamKey`. Fallback на агрегацию при `JsonException` в `TryLoadAsync` оставлен.

Тесты: 991/991, упавшие: нет. Тестов, читающих события мимо снапшота, не оказалось, переписывать на `Rebuild` ничего не пришлось.

Бенчмарки (sync=off), ops/s и % к baseline (compare по второму прогону):

| бенчмарк | baseline | после этапа 1 | прогон 1 | прогон 2 | % |
|---|---|---|---|---|---|
| event-state | 5543 | 5568 | 5281 | 6015 | +0.1 (5549 в диагностическом прогоне) |
| event-storage | 5346 | 5001 | 6141 | 5272 | -1.4 |
| event-state-transaction | 1954 | 1825 | 2199 | 2162 | +10.7 |
| event-state-transaction-chained | 951 | 949 | 1056 | 1103 | +15.9 |
| event-state-transaction-concurrent | 248 | 245 | 254 | 251 | +1.3 |
| state | 11547 | 13101 | 13802 | 14075 | +21.9 |
| transactions-state | 3342 | 3925 | 3964 | 4024 | +20.4 |
| transactions-cross-path-read | 3712 | 3471 | 3721 | 2441 | -2.3 (после 4 повторов 3627–3704) |
| остальные | | | | | +1…+10 |

pg_stat_activity во время `event-state`: запросов `select ... from mt_events ... where stream_id` нет. Остались `mt_quick_append_events`, `insert into mt_doc_eventbenchaggregate` (inline-проекция), `select d.data from mt_doc_... where id = $1` (чтение снапшота), `mt_archive_stream` (cleanup). idle in transaction 24%, это Marten-сессия `Append` (append + upsert снапшота в одной транзакции).

Отклонения от плана:

- `event-state` и `event-storage` не выросли на ожидаемые +15–30%: убран один SELECT на новый стрим, но основная стоимость операции это Marten-пайплайн `Append` (BEGIN, `mt_quick_append_events`, чтение снапшота для inline-проекции, upsert снапшота, COMMIT), а не лишняя агрегация. Заметный эффект дали транзакционные event-бенчмарки (+11…16%), где чтение идёт на каждый вызов грейна.
- Разовая просадка `transactions-cross-path-read` до 2441 во втором прогоне совпала с autovacuum по `state_test_transactional_state` (виден в сэмпле), четыре повтора дали baseline-уровень. Не регрессия.

### Этап 3 — 2026-09-10

Правка: `IStateStorage.BuildWriteCommands` (+ расширение для `GrainStateRecord`), `ISideEffectsStorage.BuildWriteCommand`; `Transactions.Process` кладёт команды direct-стейтов и side effects в один `NpgsqlBatch` и выполняет его одним `ExecuteNonQueryAsync` в транзакции, метрики `StateWrite*` пишутся там же. Marten (`_eventStorage.Write`) и callbacks выполняются после батча, как раньше. `SideEffectsStorage.Write(transaction, …)` переведён на тот же `BuildWriteCommand`.

Тесты: 991/991, упавшие: нет

Бенчмарки (sync=off), ops/s и % к baseline (compare по второму прогону):

| бенчмарк | baseline | после этапа 2 | прогон 1 | прогон 2 | % |
|---|---|---|---|---|---|
| transactions-large-batch | 525 | 549 | 516 | 554 | +4.4 |
| transactions-state-chained | 1950 | 1974 | 1974 | 1975 | +1.3 |
| transactions-state | 3342 | 4024 | 4024 | 3982 | +18.9 |
| transactions-state-overlapping | 945 | 1043 | 1044 | 1058 | +11.9 |
| transactions-single-chain | 1762 | 1855 | 1847 | 1875 | +6.4 |
| state | 11547 | 14075 | 13973 | 14369 | +24.4 |
| event-state-transaction-chained | 951 | 1103 | 1001 | 1103 | +15.9 |
| transactions-state-chained-fail | 1138 | 1190 | 1152 | 1092 | -4.1 |
| event-state | 5543 | 6015 | 5720 | 5409 | -2.4 |
| остальные | | | | | -0…+10 |

pg_stat_activity: `transactions-state` idle in transaction 58% (31/53) при BEGIN, INSERT, COMMIT; `transactions-large-batch` 3% (1/32), там 19 из 32 выборок это `select value, version …` (последовательные чтения 20 грейнов внутри транзакции), INSERT один.

Отклонения от плана:

- `transactions-large-batch` +4% вместо +20%: все 20 стейтов бенчмарка лежат в одной таблице `state_test_transactional_state`, `WriteBatch` и до этапа 3 строил для них одну команду, поэтому цепочка была BEGIN, INSERT, COMMIT, а не 3+N. Оценка «3+N» в постановке относилась к числу групп таблиц, а не стейтов. Этап даёт эффект только для транзакций с несколькими таблицами и/или side effects (реальные игровые транзакции), в группе State таких бенчмарков нет.
- Основная стоимость `transactions-large-batch` это RPC (Join, метод, CollectResult, OnSuccess на каждый из 20 грейнов) и 20 последовательных чтений стейта, это этап 4.
- Идея вне задачи: BEGIN и COMMIT остаются отдельными обращениями (58% `idle in transaction` на `transactions-state`); их можно убрать, только отказавшись от `NpgsqlTransaction` в пользу неявной транзакции батча, но тогда Marten-сессия и callbacks с `NpgsqlTransaction` не встроятся.

### Этап 4a — 2026-09-10

Правка: `TransactionRequestBase.Invoke` берёт `IGrainTransactionHandler` через `GetTarget()` → `IGrainBase.GrainContext.GetGrainExtension<IGrainTransactionHandler>()` и зовёт `Join` в памяти; в `Context.Participants` по-прежнему кладётся grain reference (он нужен console для `CollectResult`/`OnSuccess`). Fallback на reference, если target не грейн.

Тесты: 991/991, упавшие: нет

Бенчмарки (sync=off), ops/s и % к baseline (третий прогон после очистки таблиц бенчмарков, см. отклонения):

| бенчмарк | baseline | после этапа 3 | прогон 1 | прогон 2 | прогон 3 (чистые таблицы) | % |
|---|---|---|---|---|---|---|
| transactions-single-target | 848 | 883 | 936 | 901 | 899 | +6.3 |
| transactions-single-chain | 1762 | 1875 | 1917 | 1886 | 1875 | +7.0 |
| transactions-large-batch | 525 | 554 | 567 | 557 | 561 | +6.9 |
| transactions-state | 3342 | 3982 | 4032 | 3725 | 4054 | +21.3 |
| transactions-state-chained-fail | 1138 | 1092 | 1086 | 1199 | 1199 | +5.4 |
| transactions-state-overlapping | 945 | 1058 | 1071 | 1062 | 1073 | +13.5 |
| state | 11547 | 14369 | 14193 | 14352 | 13310 | +15.3 |
| state-migration-concurrent | 8171 | 8922 | 5839 | 8799 | 9417 | +15.3 |
| event-storage | 5346 | 5425 | 6063 | 4866 | 6290 | +17.7 |
| остальные | | | | | | -2…+12 |

Отклонения от плана:

- Эффект 4a локально мал: `Join` был сообщением silo→silo к той же активации (в одном процессе), а дорогие обращения в цепочке это console→silo (`CollectResult`, `OnSuccess`) через TCP. На проде console и silo тоже разные процессы, так что основной выигрыш ожидается от 4b.
- Таблицы бенчмарков растут: после ~12 прогонов группы `state_test_transactional_state` 1.4M строк, `mt_events` 1.28M (cleanup только архивирует стримы, события и снапшоты остаются). Одиночные повторы `transactions-state` на этом фоне давали 2847–3273 вместо ~4000. Перед прогоном 3 и дальше перед каждым этапом таблицы чистятся (TRUNCATE `state_test_*`, DELETE `event_bench:%` из `mt_events`/`mt_streams`/`mt_doc_eventbenchaggregate`, VACUUM ANALYZE), чтобы условия совпадали с baseline, который снимался на почти пустых таблицах.

### Этап 4b — 2026-09-10

Правка: `TransactionContext.Results` (`participantId → TransactionHandlerResult`, `[Id(3)]`) и `AddResult`, который оставляет снапшот с большим `Sequence`; `TransactionHandlerResult.Sequence` выдаёт `GrainTransactionHandler.CollectResult` (монотонно на активацию), `Events` теперь копия списка. `TransactionRequestBase.Invoke` после успешного `BaseInvoke` зовёт `CollectResult` локально и кладёт снапшот в `Context`; исходящий фильтр сливает `Results` ответа в контекст вызывающего. В запросе теперь уходит только `Id` транзакции (новый `TransactionContext { Id }`), а не накопленные участники и снапшоты. `Transactions.CollectStates` берёт снапшот из `Context.Results` и зовёт RPC `CollectResult` только для участников без него.

Тесты: 993/993, упавшие: нет. Добавлены `Transaction_SameGrainTwice_LastSnapshotPersisted` (грейн дважды в одной транзакции с чужим вызовом между, проверка из БД после деактивации) и `Transaction_ParallelCallsSameGrain_LastSnapshotPersisted` (две параллельные ветки на один грейн).

Бенчмарки (sync=off), ops/s и % к baseline (чистые таблицы, два прогона, compare по второму):

| бенчмарк | baseline | после 4a | прогон 1 | прогон 2 | % |
|---|---|---|---|---|---|
| transactions-single-target | 848 | 899 | 946 | 924 | +9.0 |
| transactions-single-chain | 1762 | 1875 | 1988 | 2014 | +14.3 |
| transactions-large-batch | 525 | 561 | 598 | 593 | +12.9 |
| transactions-state | 3342 | 4054 | 4367 | 4482 | +34.1 |
| transactions-state-chained | 1950 | 2000 | 1990 | 2134 | +9.4 |
| transactions-state-chained-fail | 1138 | 1199 | 1157 | 1164 | +2.3 |
| transactions-state-overlapping | 945 | 1073 | 1090 | 1136 | +20.2 |
| transactions-state-value | 8275 | 8881 | 9456 | 9335 | +12.8 |
| transactions-concurrent-value | 1880 | 2017 | 2052 | 2108 | +12.1 |
| transactions-cross-path-read | 3712 | 3597 | 3984 | 4112 | +10.8 |
| event-state-transaction | 1954 | 2187 | 2302 | 2358 | +20.7 |
| event-state-transaction-chained | 951 | 1100 | 850 | 1144 | +20.2 |
| event-state | 5543 | 5468 | 5787 | 6155 | +11.0 |
| event-storage | 5346 | 6290 | 4919 | 6354 | +18.8 |
| state | 11547 | 13310 | 13854 | 14006 | +21.3 |
| state-migration-concurrent | 8171 | 9417 | 8954 | 8736 | +6.9 |
| event-state-transaction-concurrent | 248 | 245 | 238 | 264 | +6.4 |

Отклонения от плана:

- Первая версия 4b сериализовала весь `Context` (с накопленными снапшотами) в каждый исходящий запрос: `transactions-large-batch` просел до 514–517 (payload рос квадратично по числу грейнов). После перехода на `Context { Id }` в запросе: 593–598.
- `transactions-large-batch` +13% вместо +30–50%: остаток цепочки это 20 последовательных console→silo вызовов (каждый с чтением стейта из БД внутри транзакции) плюс 20 `OnSuccess`. Локально RPC console→silo стоит ~0.2–0.3 мс, на проде дороже, там эффект должен быть заметнее. `transactions-state` (один грейн) +34%: цепочка сократилась с 4 RPC до 2.
- Новый тест `Transaction_ParallelCallsSameGrain_LastSnapshotPersisted` вскрыл существующую гонку в `State<T>.Read` на `[Reentrant]` грейне: второй параллельный вызов в той же транзакции видел выставленный `_currentTransactionId` и возвращался до окончания загрузки, `Value` бросал `ArgumentNullException`. Исправлено: in-flight загрузка хранится в `_transactionLoad`, повторный `Read` её ждёт. Не связано с 4b, но без этого тест нельзя было оставить.
- `transactions-state-chained-fail` и `transactions-state-overlapping` не упали (+2.3 / +20.2), rollback и конкуренция работают.

### Этап 5 — 2026-09-10

Правка: бенчмарки `state-warm` и `transactions-state-warm` (`StateWarmTest.cs`, `TransactionStateWarmTest.cs`; payload `GrainCount = 100`, перед итерациями каждый грейн активируется одним вызовом, итерации идут по кругу через `Interlocked.Increment`). `Cleanup.Track` добавлен в `StateTest`, `TransactionStateTest`, `TransactionStateValueTest`, `TransactionCrossPathReadTest`, `TransactionConcurrentValueTest`, `TransactionSingleTargetTest`, `StateMigrationConcurrentTest` и через новый `TestParticipants.Track<TState>(Cleanup)` во все chained/large-batch бенчмарки. После прогона группы `state_test_default_state` и `state_test_transactional_state` пустые (было +33k/+70k строк за прогон).

Тесты: 993/993, упавшие: нет

Бенчмарки (sync=off), ops/s, три прогона (baseline новых = прогон 3):

| бенчмарк | baseline | прогон 1 | прогон 2 | прогон 3 | % |
|---|---|---|---|---|---|
| state-warm | новый | 23855 | 24518 | 21978 | baseline 21978 |
| transactions-state-warm | новый | 4132 | 4205 | 3986 | baseline 3986 |
| state | 11547 | 13411 | 14178 | 13550 | +17.3 |
| transactions-state | 3342 | 4118 | 4004 | 3944 | +18.0 |
| transactions-large-batch | 525 | 572 | 176 | 570 | +8.6 |
| transactions-single-target | 848 | 860 | 546 | 846 | -0.2 |
| transactions-single-chain | 1762 | 1921 | 1368 | 1908 | +8.3 |
| transactions-state-chained | 1950 | 2048 | 1024 | 2006 | +2.9 |
| transactions-state-chained-fail | 1138 | 1006 | 603 | 1012 | -11.0 |
| transactions-state-overlapping | 945 | 1033 | 1002 | 1064 | +12.5 |
| event-state | 5543 | 5662 | 6007 | 6020 | +8.6 |
| event-storage | 5346 | 6062 | 6412 | 5973 | +11.7 |
| остальные | | | | | -2…+9 |

Отклонения от плана:

- `state-warm` 22–24.5k против `state` 13.5–14k: активация грейна на итерацию стоит ~40% времени `state`. `transactions-state-warm` 4.0–4.2k против `transactions-state` 3.9–4.1k: в транзакции активация тонет в RPC и BEGIN/INSERT/COMMIT.
- Прогон 2 дал разовую просадку всех транзакционных бенчмарков в 2–3 раза (`transactions-large-batch` 176), прогон 3 с сэмплером её не воспроизвёл (в сэмпле 74 из 1496 выборок autovacuum, в основном `mt_events`/`mt_streams`/`mt_doc_eventbenchaggregate`, по `state_test_*` по 2). Причина не установлена; после появления cleanup каждый прогон оставляет ~100k dead tuples в `state_test_*`, вероятно autovacuum по ним попал на транзакционную часть группы. compare для этапа берётся по прогону 3.
- `transactions-state-chained-fail` -11% по compare: значения 1006–1012 против baseline 1138, при этом в этапе 4b тот же бенчмарк давал 1157–1164. Бенчмарк короткий (0.4 с, 450 итераций), разброс до 15% между прогонами одного кода.
- `TestCleanup` для event-стейтов архивирует стримы, но снапшоты `mt_doc_eventbenchaggregate` остаются (175k строк после двух прогонов) и события в `mt_events` тоже. Не трогал: вне задачи, для прод-таблиц это те же правила Marten.

### Этап 6 — 2026-09-10

`synchronous_commit` возвращён в `on` (`ALTER SYSTEM RESET`, проверено `show synchronous_commit`).

Тесты: 993/993, упавшие: нет (финальный прогон).

Бенчмарки при sync=on (то, что видно без настройки), ops/s, рядом исходные локальные цифры из начала документа:

| бенчмарк | было (sync=on, 2026-09-09) | стало (sync=on) |
|---|---|---|
| state | 963 | 1005 |
| state-warm | – | 954 |
| transactions-state | 297 | 321 |
| transactions-state-warm | – | 337 |
| transactions-state-value | 1042 | 969 |
| transactions-large-batch | 249 | 260 |
| event-state | 1621 | 1576 |
| event-storage | 929 | 969 |
| event-state-transaction | 275 | 280 |

При sync=on всё упирается в fsync коммита (8–10 мс), сокращение раунд-трипов там не видно, как и предупреждало правило 3.

#### Сводная таблица (sync=off), ops/s по compare после каждого этапа, % к baseline

| бенчмарк | baseline | этап 1 | этап 2 | этап 3 | этап 4a | этап 4b | этап 5 | итог % |
|---|---|---|---|---|---|---|---|---|
| state | 11547 | 14192 | 14075 | 14369 | 13310 | 14006 | 13550 | +17.3 |
| state-migration-concurrent | 8171 | 8331 | 8850 | 8922 | 9417 | 8736 | 8849 | +8.3 |
| transactions-state | 3342 | 3925 | 4024 | 3972 | 4054 | 4482 | 3944 | +18.0 |
| transactions-state-value | 8275 | 8678 | 8667 | 8515 | 8881 | 9335 | 8753 | +5.8 |
| transactions-large-batch | 525 | 534 | 549 | 548 | 561 | 593 | 570 | +8.6 |
| transactions-single-target | 848 | 807 | 879 | 883 | 899 | 924 | 846 | -0.2 |
| transactions-single-chain | 1762 | 1771 | 1855 | 1875 | 1875 | 2014 | 1908 | +8.3 |
| transactions-state-chained | 1950 | 1963 | 1974 | 1975 | 2000 | 2134 | 2006 | +2.9 |
| transactions-state-chained-fail | 1138 | 1175 | 1190 | 1092 | 1199 | 1164 | 1012 | -11.0 (повторы 1114–1128) |
| transactions-state-overlapping | 945 | 1035 | 1043 | 1058 | 1073 | 1136 | 1064 | +12.5 |
| transactions-concurrent-value | 1880 | 1783 | 1907 | 1960 | 2017 | 2108 | 2085 | +10.9 |
| transactions-cross-path-read | 3712 | 3471 | 3627 | 3741 | 3597 | 4112 | 3799 | +2.3 |
| event-state | 5543 | 5568 | 5549 | 5409 | 5468 | 6155 | 6020 | +8.6 |
| event-storage | 5346 | 5001 | 5272 | 5425 | 6290 | 6354 | 5973 | +11.7 |
| event-state-transaction | 1954 | 1825 | 2162 | 2162 | 2187 | 2358 | 2134 | +9.2 |
| event-state-transaction-chained | 951 | 949 | 1103 | 1103 | 1100 | 1144 | 1059 | +11.3 |
| event-state-transaction-concurrent | 248 | 245 | 251 | 254 | 245 | 264 | 242 | -2.4 |
| state-warm | – | – | – | – | – | – | 21978 | baseline |
| transactions-state-warm | – | – | – | – | – | – | 3986 | baseline |

Разброс между прогонами одного кода локально 5–15% (короткие бенчмарки 1–3 с), поэтому колонки этапов надо читать как тренд, а не как точный вклад этапа. Устойчиво выросли `state` (+17…24%), `transactions-state` (+18…34%), `transactions-state-overlapping` (+12…20%), транзакционные event-бенчмарки (+9…20%).

Прод-прогон после мержа делает человек (п. 5 этапа 6). Ожидание для прода: эффект больше локального, потому что там раунд-трип к БД и RPC console→silo дороже, а fsync быстрый.

## Заметки

- Локально без `synchronous_commit=off` сравнение бессмысленно: fsync 8–10 мс перекрывает выигрыш от любого сокращённого раунд-трипа.
- В transaction-mode pgbouncer на проде каждая транзакция это захват серверного соединения из пула на 20 штук. Сокращение числа обращений внутри транзакции сокращает и время удержания соединения.
- Marten `mt_streams` копит dead tuples после cleanup бенчмарков (архивация 33k стримов за прогон), автовакуум справляется, на результаты не влияет.

- Итоговые правки по коду: `DirectStorage` (команды отдельно от выполнения, одна группа без транзакции), `EventStorage` (без агрегации в `Read`, `LoadManyAsync` в `ReadBatch`, `Rebuild<T>`), `StateStorage`/`StateStorageExtensions` (`BuildWriteCommands`), `SideEffectsStorage` (`BuildWriteCommand`), `Transactions` (один `NpgsqlBatch`, снапшоты из контекста), `TransactionAttribute` (локальные `Join`/`CollectResult`, `Context { Id }` в запросе), `TransactionContext` (`Results`, `AddResult`), `GrainTransactionHandler` (`Sequence`), `State<T>` (ожидание in-flight загрузки), бенчмарки State (два warm, `Cleanup.Track`), `TransactionTests` (+2 теста).
- Изменение протокола транзакций (4b) совместимо только при одновременном деплое console и silo: `TransactionContext` получил поле `[Id(3)] Results`, `TransactionHandlerResult` поле `[Id(2)] Sequence`. Старый console со старым silo продолжает работать через RPC `CollectResult`, смешанные версии не проверялись.
- Локальные особенности окружения см. «Этап 0»: роль `mslead-root`/БД `mslead` в локальном контейнере, логин консоли по токену, очистка таблиц бенчмарков перед прогоном (`TRUNCATE state_test_*`, `DELETE ... LIKE 'event_bench:%'`).
- Не сделано / вне задачи: `OnSuccess` остаётся отдельным RPC на участника (по постановке); BEGIN/COMMIT остаются отдельными обращениями; `TestCleanup` для event-стейтов не удаляет снапшоты и события.

## Итоговое сравнение до/после — 2026-09-10

Все группы бенчмарков (Infrastructure, Messaging, State), локально, `synchronous_commit=off`, по одному прогону каждой группы на коммите до правок (`c577e5b8`) и после (`c84413f1`), таблицы бенчмарков очищены перед каждым прогоном. Разброс между прогонами одного кода 5–15%.

| группа | бенчмарк | метрика | до (c577e5b8) | после (c84413f1) | % |
|---|---|---|---|---|---|
| Infrastructure | side-effect-dead-letter-throughput | ops/s | 1244.6 | 1241.9 | -0.2% |
| Infrastructure | side-effect-throughput | ops/s | 1042.3 | 989.0 | -5.1% |
| Infrastructure | task-balancer-concurrency | ops/s | 1995.6 | 2007.6 | +0.6% |
| Infrastructure | task-balancer-exception-penalty | ops/s | 1002.2 | 1006.6 | +0.4% |
| Infrastructure | task-balancer-priority | ops/s | 1999.6 | 2013.7 | +0.7% |
| Infrastructure | task-queue-collect | ops/s | 423369.1 | 431901.8 | +2.0% |
| Infrastructure | task-queue-deduplication | ops/s | 1771185.5 | 2053006.2 | +15.9% |
| Infrastructure | task-queue-delay | ops/s | 550617.0 | 546878.1 | -0.7% |
| Messaging | Broadcast throughput | msg/s | 22187.2 | 19529.2 | -12.0% |
| Messaging | Catch-up stress (disconnect/reconnect) | msg/s | 2220.5 | 2164.7 | -2.5% |
| Messaging | Delivery throughput | msg/s | 977.6 | 977.5 | -0.0% |
| Messaging | Delivery timeout (slow observers) | msg/s | 1871.6 | 1868.9 | -0.1% |
| Messaging | Direct push throughput | msg/s | 975.0 | 972.1 | -0.3% |
| Messaging | Distributed send throughput | msg/s | 22881.0 | 23252.2 | +1.6% |
| Messaging | Request-response throughput | msg/s | 29711.0 | 32803.4 | +10.4% |
| Messaging | Retry stress (intermittent failures) | req/s | 19756.1 | 18202.4 | -7.9% |
| Messaging | StateCollection update throughput | update/s | 981.4 | 982.2 | +0.1% |
| Messaging | Transactional push throughput | msg/s | 974.3 | 966.8 | -0.8% |
| State | event-state | ops/s | 5288.5 | 6221.2 | +17.6% |
| State | event-state-transaction | ops/s | 1983.3 | 2365.0 | +19.2% |
| State | event-state-transaction-chained | ops/s | 917.3 | 1153.0 | +25.7% |
| State | event-state-transaction-concurrent | ops/s | 248.6 | 242.7 | -2.4% |
| State | event-storage | ops/s | 5585.2 | 6216.9 | +11.3% |
| State | state | ops/s | 11382.1 | 14336.3 | +26.0% |
| State | state-migration-concurrent | ops/s | 8249.3 | 9174.0 | +11.2% |
| State | state-warm | ops/s | – | 24640.9 | – |
| State | transactions-concurrent-value | ops/s | 1735.0 | 2232.7 | +28.7% |
| State | transactions-cross-path-read | ops/s | 3585.7 | 4081.8 | +13.8% |
| State | transactions-large-batch | ops/s | 507.5 | 463.7 | -8.6% |
| State | transactions-single-chain | ops/s | 1551.0 | 2087.6 | +34.6% |
| State | transactions-single-target | ops/s | 703.3 | 851.0 | +21.0% |
| State | transactions-state | ops/s | 3903.4 | 4426.3 | +13.4% |
| State | transactions-state-chained | ops/s | 1939.9 | 2076.6 | +7.0% |
| State | transactions-state-chained-fail | ops/s | 955.2 | 1125.5 | +17.8% |
| State | transactions-state-overlapping | ops/s | 1016.1 | 1125.9 | +10.8% |
| State | transactions-state-value | ops/s | 8429.7 | 7006.1 | -16.9% |
| State | transactions-state-warm | ops/s | – | 4529.5 | – |

Перепроверка выбросов на новом коде (по два повтора, sync=off): `transactions-large-batch` 612 / 588 (в таблице 464, до правок 508), `transactions-state-value` 9435 / 9349 (в таблице 7006, до правок 8430), `Broadcast throughput` 20896 / 22914 (в таблице 19529, до правок 22187, код Messaging не менялся). Все три значения в таблице это разовые просадки внутри группового прогона, а не регрессии.

## Orleans 10.2.2 → 10.3.1 — 2026-09-10

Тот же код (`c84413f1`), только версия Orleans. Все группы, sync=off, чистые таблицы. Колонки: до правок (`c577e5b8`, 10.2.2), после правок (10.2.2), после правок на 10.3.1, последняя колонка это 10.3.1 к 10.2.2 на одном коде.

| группа | бенчмарк | до правок | после (10.2.2) | Orleans 10.3.1 | 10.3.1 к 10.2.2 |
|---|---|---|---|---|---|
| Infrastructure | side-effect-dead-letter-throughput | 1244.6 | 1241.9 | 1238.5 | -0.3% |
| Infrastructure | side-effect-throughput | 1042.3 | 989.0 | 1039.8 | +5.1% |
| Infrastructure | task-balancer-concurrency | 1995.6 | 2007.6 | 2030.7 | +1.1% |
| Infrastructure | task-balancer-exception-penalty | 1002.2 | 1006.6 | 1016.6 | +1.0% |
| Infrastructure | task-balancer-priority | 1999.6 | 2013.7 | 2007.5 | -0.3% |
| Infrastructure | task-queue-collect | 423369.1 | 431901.8 | 424094.0 | -1.8% |
| Infrastructure | task-queue-deduplication | 1771185.5 | 2053006.2 | 1909329.0 | -7.0% |
| Infrastructure | task-queue-delay | 550617.0 | 546878.1 | 536448.7 | -1.9% |
| Messaging | Broadcast throughput | 22187.2 | 19529.2 | 22012.8 | +12.7% |
| Messaging | Catch-up stress (disconnect/reconnect) | 2220.5 | 2164.7 | 2210.4 | +2.1% |
| Messaging | Delivery throughput | 977.6 | 977.5 | 978.6 | +0.1% |
| Messaging | Delivery timeout (slow observers) | 1871.6 | 1868.9 | 1862.3 | -0.4% |
| Messaging | Direct push throughput | 975.0 | 972.1 | 972.7 | +0.1% |
| Messaging | Distributed send throughput | 22881.0 | 23252.2 | 23203.7 | -0.2% |
| Messaging | Request-response throughput | 29711.0 | 32803.4 | 28541.8 | -13.0% |
| Messaging | Retry stress (intermittent failures) | 19756.1 | 18202.4 | 7777.2 | -57.3% |
| Messaging | StateCollection update throughput | 981.4 | 982.2 | 979.0 | -0.3% |
| Messaging | Transactional push throughput | 974.3 | 966.8 | 976.0 | +1.0% |
| State | event-state | 5288.5 | 6221.2 | 5107.0 | -17.9% |
| State | event-state-transaction | 1983.3 | 2365.0 | 2037.3 | -13.9% |
| State | event-state-transaction-chained | 917.3 | 1153.0 | 975.8 | -15.4% |
| State | event-state-transaction-concurrent | 248.6 | 242.7 | 242.6 | -0.0% |
| State | event-storage | 5585.2 | 6216.9 | 5303.4 | -14.7% |
| State | state | 11382.1 | 14336.3 | 10429.6 | -27.3% |
| State | state-migration-concurrent | 8249.3 | 9174.0 | 6629.5 | -27.7% |
| State | state-warm | – | 24640.9 | 21725.6 | -11.8% |
| State | transactions-concurrent-value | 1735.0 | 2232.7 | 2008.6 | -10.0% |
| State | transactions-cross-path-read | 3585.7 | 4081.8 | 3417.5 | -16.3% |
| State | transactions-large-batch | 507.5 | 463.7 | 473.5 | +2.1% |
| State | transactions-single-chain | 1551.0 | 2087.6 | 1742.1 | -16.6% |
| State | transactions-single-target | 703.3 | 851.0 | 811.4 | -4.7% |
| State | transactions-state | 3903.4 | 4426.3 | 4035.8 | -8.8% |
| State | transactions-state-chained | 1939.9 | 2076.6 | 1554.4 | -25.1% |
| State | transactions-state-chained-fail | 955.2 | 1125.5 | 961.4 | -14.6% |
| State | transactions-state-overlapping | 1016.1 | 1125.9 | 1052.3 | -6.5% |
| State | transactions-state-value | 8429.7 | 7006.1 | 8289.2 | +18.3% |
| State | transactions-state-warm | – | 4529.5 | 4219.6 | -6.8% |

Повторы на 10.3.1 (группа State второй раз и одиночные прогоны):

| бенчмарк | 10.2.2 (группа) | 10.3.1 группа 1 | 10.3.1 группа 2 | 10.3.1 одиночные ×3 |
|---|---|---|---|---|
| state | 14336 | 10430 | 11297 | 11585 / 11460 / 11343 |
| state-warm | 24641 | 21726 | 24061 | 24463 / 24922 / 24751 |
| state-migration-concurrent | 9174 | 6630 | 7525 | – |
| event-state | 6221 | 5107 | 5742 | – |
| event-state-transaction | 2365 | 2037 | 1916 | – |
| transactions-state | 4426 | 4036 | 4044 | 2714 / 2746 / 2738 (одиночный прогон этого бенчмарка всегда ниже группового, и на 10.2.2 тоже: 2847–3273) |
| transactions-state-warm | 4530 | 4220 | 4395 | – |
| Retry stress (Messaging) | 18202 | 7777 | – | 17767 / 22727 |

Вывод: на 10.3.1 устойчиво (-18…-21% в трёх и более прогонах) просели бенчмарки, которые активируют новый грейн на каждую операцию (`state`, `state-migration-concurrent`, `event-state`, `event-state-transaction`), а `state-warm`/`transactions-state-warm` на прогретых грейнах не изменились. По разнице `state` и `state-warm` стоимость активации выросла с ~0.29 мс до ~0.47 мс на операцию при Concurrent=10. `Retry stress` -57% в первом прогоне не воспроизвёлся (повторы 17767 / 22727). Ошибок и предупреждений в логах silo/console за время прогонов нет.


## Orleans 10.3.1: причина просадки и фикс — 2026-09-10

**Причина.** Orleans 10.3.0 (PR dotnet/orleans#9819) оборачивает placement каждого нового грейна в Polly-пайплайн (`OrleansRuntimeResiliencePolicies`, internal, выключить нельзя). Телеметрия Polly.Extensions пишет на каждую активацию два лога уровня Information: «Resilience pipeline executed» и «Execution attempt». В silo логи идут в файл с AutoFlush, консоль и OTLP, поэтому каждая активация дорожала на ~0.18 мс. В `.telemetry/logs/silo.log` за ночь набралось 1.5 млн строк `[Polly]`.

Сама активация не подорожала: отдельный микробенчмарк (silo без логов, 10.2.2 / 10.3.0 / 10.3.1) показывает 10–15 мкс на новую активацию на всех версиях. Изменения 10.3 в directory (cancellation tokens в `DistributedGrainDirectory`, lock в `CachedVersionSelectorManager`, сам Polly-пайплайн) стоят единицы микросекунд. Upstream issue про шум Polly-логов на момент проверки нет.

**Фикс.** `builder.Logging.AddFilter("Polly", LogLevel.Warning)` в `OrleansSetupExtensions.ConfigureSilo`. После фикса за прогон всех групп в лог попало 10 строк Polly вместо ~130k.

Прогон всех групп на чистых таблицах, sync=off, код `c84413f1` + фильтр:

| группа | бенчмарк | метрика | 10.2.2 | 10.3.1 | 10.3.1 + фильтр Polly | фильтр vs 10.2.2 |
|---|---|---|---|---|---|---|
| Infrastructure | side-effect-dead-letter-throughput | ops/s | 1242 | 1238 | 1241 | -0% |
| Infrastructure | side-effect-throughput | ops/s | 989 | 1040 | 988 | -0% |
| Infrastructure | task-balancer-concurrency | ops/s | 2008 | 2031 | 1991 | -1% |
| Infrastructure | task-balancer-exception-penalty | ops/s | 1007 | 1017 | 990 | -2% |
| Infrastructure | task-balancer-priority | ops/s | 2014 | 2007 | 1986 | -1% |
| Infrastructure | task-queue-collect | ops/s | 431902 | 424094 | 432175 | +0% |
| Infrastructure | task-queue-deduplication | ops/s | 2053006 | 1909329 | 2033400 | -1% |
| Infrastructure | task-queue-delay | ops/s | 546878 | 536449 | 536648 | -2% |
| Messaging | Delivery throughput | msg/s | 978 | 979 | 978 | +0% |
| Messaging | Direct push throughput | msg/s | 972 | 973 | 978 | +1% |
| Messaging | Transactional push throughput | msg/s | 967 | 976 | 976 | +1% |
| Messaging | Catch-up stress (disconnect/reconnect) | msg/s | 2165 | 2210 | 2230 | +3% |
| Messaging | Delivery timeout (slow observers) | msg/s | 1869 | 1862 | 1874 | +0% |
| Messaging | Distributed send throughput | msg/s | 23252 | 23204 | 22448 | -3% |
| Messaging | Broadcast throughput | msg/s | 19529 | 22013 | 20105 | +3% |
| Messaging | Retry stress (intermittent failures) | req/s | 18202 | 7777 | 11059 | -39% |
| Messaging | Request-response throughput | msg/s | 32803 | 28542 | 31398 | -4% |
| Messaging | StateCollection update throughput | update/s | 982 | 979 | 982 | -0% |
| State | event-state | ops/s | 6221 | 5107 | 6045 | -3% |
| State | event-state-transaction-chained | ops/s | 1153 | 976 | 1080 | -6% |
| State | event-state-transaction-concurrent | ops/s | 243 | 243 | 257 | +6% |
| State | event-state-transaction | ops/s | 2365 | 2037 | 2266 | -4% |
| State | event-storage | ops/s | 6217 | 5303 | 6143 | -1% |
| State | state-migration-concurrent | ops/s | 9174 | 6629 | 9057 | -1% |
| State | state | ops/s | 14336 | 10430 | 13887 | -3% |
| State | state-warm | ops/s | 24641 | 21726 | 24074 | -2% |
| State | transactions-concurrent-value | ops/s | 2233 | 2009 | 2152 | -4% |
| State | transactions-cross-path-read | ops/s | 4082 | 3418 | 3712 | -9% |
| State | transactions-large-batch | ops/s | 464 | 473 | 548 | +18% |
| State | transactions-single-target | ops/s | 851 | 811 | 880 | +3% |
| State | transactions-single-chain | ops/s | 2088 | 1742 | 1802 | -14% |
| State | transactions-state-chained-fail | ops/s | 1126 | 961 | 1013 | -10% |
| State | transactions-state-chained | ops/s | 2077 | 1554 | 1881 | -9% |
| State | transactions-state-overlapping | ops/s | 1126 | 1052 | 1013 | -10% |
| State | transactions-state | ops/s | 4426 | 4036 | 4143 | -6% |
| State | transactions-state-value | ops/s | 7006 | 8289 | 9100 | +30% |
| State | transactions-state-warm | ops/s | 4529 | 4220 | 4023 | -11% |

Повторы одиночных бенчмарков после фикса на чистых таблицах: `state` 12296, `transactions-single-chain` 1809, `transactions-state-chained` 1949, `transactions-cross-path-read` 3524, `transactions-state-warm` 4036, `Retry stress` 9791 / 27005.

**Вывод.** Холодные бенчмарки (`state`, `state-migration-concurrent`, `event-state`, `event-storage`) вернулись к уровню 10.2.2 в пределах шума. Транзакционные бенчмарки в этом прогоне на 6–14% ниже 10.2.2, что укладывается в наблюдаемый разброс между прогонами (5–15%), но полностью остаточное влияние 10.3 не исключено; `Retry stress` колеблется от 7.7k до 27k и для сравнения непригоден. Окончательную оценку даст прод-прогон после мержа.

Заметка: вместе с Orleans в `Directory.Packages.props` обновились Marten 9.29→9.33, Http.Resilience/ServiceDiscovery 10.9→10.10 и другие пакеты; на путь `state` они не влияют (DirectStorage работает через собственный `NpgsqlDataSource`, Npgsql 10.0.3 не менялся).
