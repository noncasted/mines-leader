# Test Roadmap

Кастомная система стейта поверх Orleans + Postgres. Кастомный тест-фреймворк (`ClusterTestRoot`/`ClusterTestNode`)
запускает тесты на живом кластере — это интеграционные тесты, не юниты.

---

## Что есть сейчас

### State (группа `State`)

| Тест | Что проверяет |
|---|---|
| `StateTest` | Не-транзакционный Read/Write, вложенные объекты, grain references в стейте |
| `TransactionStateTest` | Транзакция на один грейн с rollback |
| `TransactionStateChainedTest` | Транзакция на N грейнов, успех |
| `TransactionStateChainedFailTest` | Rollback при исключении, проверяет значения до и после |
| `TransactionStateOverlappingTest` | Две конкурентные транзакции на пересекающихся грейнах |
| `TransactionSingleTargetTest` | Конкурентный доступ к одному грейну |
| `TransactionSingleChainTest` | Повторные chained-операции на одном грейне |
| `StateMigrationTest` | Миграция стейта между версиями |

### Messaging (группа `Messaging`)

| Тест | Что проверяет |
|---|---|
| `MessagingDirectQueueStressTest` | Durable queue — direct push, доставка |
| `MessagingTransactionalQueueStressTest` | Durable queue — push внутри транзакции |
| `MessagePipeSendStressTest` | Runtime pipe — fire-and-forget |
| `MessagePipeSendResponseStressTest` | Runtime pipe — request/response |
| `RuntimeChannelStressTest` | Runtime channel — broadcast |

---

### State — новые (реализовано)

| Тест | Что проверяет |
|---|---|
| `StatePersistenceTest` | Запись → деактивация → чтение из Postgres |
| `TransactionStateValueTest` | N последовательных транзакций → итоговое значение == N |
| `TransactionConcurrentValueTest` | K параллельных транзакций → итоговое значение == K |
| `TransactionTakeoverTest` | Force takeover зависшей транзакции (>30s) |
| `TransactionCrossPathReadTest` | Транзакционная запись → нетранзакционное чтение |
| `StateCollectionSyncTest` | StateCollection синхронизация между сервисами (Root + Node) |
| `TransactionEmptyTest` | Пустая транзакция (0 grain-вызовов) |
| `TransactionLargeBatchTest` | Транзакция с 50+ грейнами |
| `TransactionRollbackRetryTest` | Rollback → повторная транзакция без ожидания takeover |
| `TransactionMidChainFailTest` | Исключение в середине цепочки → оба грейна откатились |
| `StateMigrationConcurrentTest` | Миграция стейта под конкурентной нагрузкой |
| `SideEffectExecutionTest` | Side effect регистрация → выполнение через SideEffectsWorker |

### Messaging — новые (реализовано)

| Тест | Что проверяет |
|---|---|
| `DurableQueueDeliveryTest` | Отправка и доставка всех сообщений через durable queue |

### Game (группа `Game`, реализовано)

| Тест | Что проверяет |
|---|---|
| `BoardGenerationTest` | Генерация доски, количество мин, безопасность стартовой позиции |
| `BoardRevealTest` | Flood-fill reveal, идемпотентность повторного reveal |
| `CellStateTest` | Переходы Taken↔Free, флаги, мины, взрыв |
| `PlayerStatsTest` | Health/Mana/Moves: damage, heal, use, restore, clamp, lock |
| `DeckHandStashTest` | Дека: init/draw/cycle, Hand: add/remove, Stash: LIFO/collect |

### Meta (группа `Meta`, реализовано)

| Тест | Что проверяет |
|---|---|
| `UserProgressionRatingTest` | XP и Rating: добавление Win/Loss записей, кумулятивный результат |
| `UserDeckTest` | Инициализация дек, обновление, персистентность |
| `MatchRecordingTest` | Создание матча → завершение → XP/Rating/History у обоих игроков |

### Инфраструктура тестов (реализовано)

| Файл | Что добавлено |
|---|---|
| `TestAssert.cs` | Assertion helpers: Equal, True, Throws, GreaterThan, Contains, NotNull |
| `TestGroups.cs` | Группы Game и Meta |
| `TransactionTestsGrain.cs` | Методы `IncrementWithDelay`, `Deactivate` |

---

## Ещё не реализовано

### ~~Side effects retry и transactional side effects~~ ✓ DONE

Реализовано в `SideEffectTests.cs` — 14 тестов:
- `FailAndRetry_EventuallySucceeds` — retry при ошибке
- `MaxRetriesExceeded_Dropped` — удаление после max retries
- `TransactionalFails_RetryAndEventualSuccess` — транзакционный retry
- `TransactionRollback_SideEffectNotEnqueued` — откат не ставит в очередь
- И ещё 10 тестов (batch, isolation, crash recovery и др.)

---

### Игровая логика — расширение покрытия

---

#### Card effects: корректность паттернов

Каждая карта имеет пространственный паттерн (Rhombus, Circle) и эффект. Минимум:

| Карта | Что проверить |
|---|---|
| `Trebuchet` | Расстановка мин в паттерне, Y-axis группировка |
| `ZipZap` | Chain через мины, boost от TrebuchetAimer |
| `Bloodhound` | Reveal на чужой доске, конвертация Taken → Free |
| `ErosionDozer` | Proximity-based reveal, корректный порядок |
| `Smoke` | Timed effect: наложение и снятие через `RoundActionService` |
| `GraveDigger` | Возврат карты из Stash в Hand |
| `OpponentFlagReshuffle` | Рандомизация флагов в паттерне |

---

#### Game flow: полный матч (e2e)

End-to-end тест полного матча:
1. Создание сессии через `SessionFactory`
2. Подключение двух игроков
3. `PlayerReadyCommand` от обоих
4. Несколько ходов (open cell, use card)
5. Один игрок умирает (HP = 0) или время истекает
6. Матч завершается, результат записывается в `Match` grain

---

#### Matchmaking

- Два игрока ставятся в очередь → матч создаётся
- Игрок отключается из очереди → матч не создаётся
- Конкурентные matchmaking requests → нет дублирования матчей

---

### Хаос и надёжность (перед production)

---

#### Деактивация грейна во время транзакции

Orleans может деактивировать idle грейн. Если грейн-участник деактивируется
между `Join()` и `CollectStates()` — что произойдёт?

**Ожидание:** транзакция должна зафейлиться, rollback, retry.

---

#### Потеря Postgres-соединения во время коммита

`NpgsqlTransaction.CommitAsync()` бросает исключение → все участники должны получить
`OnFailure()`, семафоры отпущены, in-memory кэш сброшен.

---

#### Messaging: потеря и дублирование

- Runtime pipe/channel: сообщение теряется при падении получателя → sender получает ошибку
  или timeout (не зависает навсегда)
- Durable queue: при перезапуске consumer может получить дубль → idempotent обработка

---

#### Нагрузочный тест: много параллельных матчей

50+ параллельных матчей на кластере. Проверяет:
- Нет deadlock'ов в транзакциях
- StateCollection не отстаёт критично
- Side effects не копятся в очереди
- Memory не утекает (Lifetime'ы терминируются)

---

## Улучшения тест-фреймворка

Реализовано:
- `TestAssert` — assertion helpers (Equal, True, Throws, GreaterThan, Contains, NotNull)

Ещё нужно:

1. **Grain test utilities** — хелпер для: создать грейн → выполнить действия → прочитать стейт →
   проверить. Без написания полного `Root`/`Node` на каждый тест.

2. **Таймауты тестов** — сейчас зависший тест висит вечно. Нужен глобальный timeout
   с автоматическим `OperationStatus.Failed`.

3. **Test isolation** — тесты используют общие грейны по GuidKey. Если тесты запускаются
   параллельно, они могут конфликтовать. Каждый тест должен использовать уникальные ID.
