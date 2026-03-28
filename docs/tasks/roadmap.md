# State Infrastructure — Roadmap

Кастомная система стейта поверх Orleans + Postgres. Ключевое преимущество над Orleans built-in transactions:
все участники коммитятся в одной Postgres-транзакции — атомарность гарантируется на уровне WAL, а не
распределённым 2PC. Это исключает частичный коммит при краше сервера.

Ниже — список улучшений от наиболее критичных к менее срочным.

---

## 1. CancellationToken в транзакциях

**Что:** `Transactions.Process()` принимает `CancellationToken`. Все grain-вызовы внутри транзакции получают токен.
При отмене вызывается Rollback → `OnFailure` на всех участниках.

**Зачем:** Сейчас зависшая транзакция занимает семафор до 10s, после чего следующая делает force takeover.
С токеном — транзакция сама себя отменяет корректно, без принудительного захвата.

**Затрагивает:** `Transactions.cs`, `GrainTransactionHandler.cs`, `State.cs`

---

## ~~2. Логирование в GrainTransactionHandler~~ ✓ DONE

---

## 3. Метрики

**Что:** Counters и histograms через `System.Diagnostics.Metrics` (совместимо с OpenTelemetry).

Минимальный набор:
| Метрика | Тип | Описание |
|---|---|---|
| `transactions.total` | Counter | Всего транзакций |
| `transactions.rollbacks` | Counter | Откатов |
| `transactions.duration` | Histogram | Длительность транзакции |
| `transactions.takeovers` | Counter | Force takeover зависших транзакций |
| `state.reads` / `state.writes` | Counter | Операции с БД |
| `state.write_batch_size` | Histogram | Участников за одну Postgres-транзакцию |

**Затрагивает:** `Transactions.cs`, `GrainStateStorage.cs`, `GrainTransactionHandler.cs`

---

## 4. Конфигурируемые таймауты транзакций

**Что:** Таймауты в `GrainTransactionHandler` захардкожены — `3s` на ожидание семафора и `30s` grace period
для force takeover. Вынести в `TransactionOptions` через `IOptions<T>`.

**Зачем:** Для production нужна возможность тюнинга без перекомпиляции.
Side effects уже имеют `SideEffectsOptions` — транзакции должны быть аналогичны.

**Затрагивает:** `GrainTransactionHandler.cs`, регистрация сервисов

---

## 5. Безопасность сериализатора

**Что:** Заменить `TypeNameHandling.All` на `TypeNameHandling.Auto` + `ISerializationBinder`
с allowlist разрешённых типов.

**Зачем:** `TypeNameHandling.All` позволяет JSON с полем `$type` инстанциировать произвольные .NET типы.
Если хоть какие-то пользовательские данные попадают в стейт — это потенциальный вектор атаки.
Сейчас `SerializationBinder = null` — никакой защиты нет.

**Затрагивает:** `StateSerializer.cs`

---

## ~~6. Робастность сериализации GrainId / GrainReference~~ ✓ DONE

---

## 7. Read-only транзакции

**Что:** Транзакции, которые только читают стейт и не захватывают семафор на запись.

**Зачем:** Сейчас любая транзакция, даже читающая, блокирует грейн на всё время выполнения.
Read-only транзакции могут выполняться параллельно, не мешая друг другу.

**Реализация:** `[Transaction(ReadOnly = true)]` атрибут. `GrainTransactionHandler.Join` при read-only
не захватывает `_lock`, только регистрирует участника. `CollectStates` возвращает пустой список.

**Затрагивает:** `TransactionAttribute.cs`, `GrainTransactionHandler.cs`

---

## 8. Lock-Маршруты транзакций

**Что:** Перед началом транзакции берутся несколько блокировок — либо все, либо ни одной.
Таким образом deadlock предотвращается на этапе получения блокировок, а не внутри транзакции.

**Затрагивает:** `Transactions.cs`, новый сервис `IClusterLocks`
