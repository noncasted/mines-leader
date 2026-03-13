# State Infrastructure — Roadmap

Кастомная система стейта поверх Orleans + Postgres. Ключевое преимущество над Orleans built-in transactions:
все участники коммитятся в одной Postgres-транзакции — атомарность гарантируется на уровне WAL, а не
распределённым 2PC. Это исключает частичный коммит при краше сервера.

Ниже — список улучшений от наиболее критичных к менее срочным.

---

## 1. CancellationToken в транзакциях

**Что:** `Transactions.Run()` принимает `CancellationToken`. Все grain-вызовы внутри транзакции получают токен.
При отмене вызывается Rollback → `OnFailure` на всех участниках.

**Зачем:** Сейчас зависшая транзакция занимает семафор до 10s, после чего следующая делает force takeover.
С токеном — транзакция сама себя отменяет корректно, без принудительного захвата.

**Затрагивает:** `Transactions.cs`, `GrainTransactionHandler.cs`, `State.cs`

---

## 2. Transaction Callbacks

**Что:** Колбеки, которые выполняются после успешного коммита транзакции. Если колбек падает —
вся транзакция откатывается.

```csharp
await _transactions.Run(async () => {
    await grain.Increment();
}, onCommit: async () => {
    await messageBus.Publish(new IncrementedEvent());
});
```

**Зачем:** Паттерн "обновить стейт + отправить событие" сейчас требует ручной обработки ошибок.
Колбек даёт гарантию: событие отправлено тогда и только тогда, когда стейт закоммичен.

**Затрагивает:** `Transactions.cs`, `ITransactions` интерфейс

---

## 3. Transaction Side Effects (Outbox Pattern)

**Что:** Обязательные шаги после транзакции, которые записываются в БД вместе с основным коммитом
и затем выполняются с бесконечным ретраем до успеха.

```csharp
await _transactions.Run(async () => {
    await grain.Increment();
    transaction.AddSideEffect(new SendEmailEffect { To = "user@example.com" });
});
// side effect записан в отдельную таблицу в той же Postgres-транзакции
// фоновый воркер читает необработанные side effects и выполняет их
```

**Зачем:** В отличие от callbacks, side effects переживают краш сервера. Если сервер упал после коммита
но до выполнения side effect — воркер выполнит его при следующем старте. Гарантия at-least-once.

**Требует:**
- Таблица `side_effects` (id, type, payload, created_at, processed_at)
- `ISideEffectHandler<T>` интерфейс для регистрации обработчиков
- Фоновый воркер с ретраем и экспоненциальным backoff
- Idempotency key на каждом side effect, чтобы at-least-once не приводил к дублям

**Затрагивает:** `GrainStateStorage.cs`, `Transactions.cs`, новый `SideEffectWorker`

---

## 4. Логирование

**Что:** Заменить `Console.WriteLine` в `GrainStateStorage.cs` на `ILogger`. Добавить structured logging
в ключевых точках транзакций.

Минимальный набор событий для логирования:
- Старт и завершение транзакции (с длительностью)
- Rollback (с причиной)
- Force takeover зависшей транзакции (с временем ожидания и ID жертвы)
- Ошибка при записи в БД

**Затрагивает:** `GrainStateStorage.cs`, `GrainTransactionHandler.cs`, `Transactions.cs`

---

## 5. Метрики

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

## 6. Версионирование и миграция стейтов

**Что:** Механизм для изменения схемы стейта без потери данных и ручного SQL.

**Проблема:** Сейчас если добавить поле в `UserState`, старые записи в БД десериализуются без него
(работает через `MissingMemberHandling.Ignore`). Если переименовать поле — старые данные теряются молча.
Если удалить поле — в БД остаётся мусор.

**Решение-минимум:** Версия схемы в каждой записи + `IMigration<TFrom, TTo>` интерфейс:
```csharp
public class UserStateV2Migration : IMigration<UserStateV1, UserStateV2> {
    public UserStateV2 Migrate(UserStateV1 old) => new() { FullName = old.Name };
}
```
При чтении: если версия записи меньше текущей — прогнать через цепочку миграций перед возвратом.

**Затрагивает:** `GrainStateStorage.cs`, `StateSerializer.cs`, новый `MigrationRegistry`

---

## 7. Безопасность сериализатора

**Что:** Заменить `TypeNameHandling.All` на `TypeNameHandling.Auto` + `ISerializationBinder`
с allowlist разрешённых типов.

**Зачем:** `TypeNameHandling.All` позволяет JSON с полем `$type` инстанциировать произвольные .NET типы.
Если хоть какие-то пользовательские данные попадают в стейт — это потенциальный вектор атаки.

**Затрагивает:** `StateSerializer.cs`

---

## 8. Робастность сериализации GrainId / GrainReference

**Что:** В `GrainIdConverter` и `GrainReferenceJsonConverter` используется `Split(':')` без ограничения
на количество частей. Orleans type aliases могут содержать `:`, что приведёт к неверному парсингу.

**Фикс:** `Split(':', count: 2)` для GrainId, `Split(':', count: 3)` для GrainReference.

**Затрагивает:** `StateSerializer.cs`

---

## 9. Read-only транзакции

**Что:** Транзакции, которые только читают стейт и не захватывают семафор на запись.

**Зачем:** Сейчас любая транзакция, даже читающая, блокирует грейн на всё время выполнения.
Read-only транзакции могут выполняться параллельно, не мешая друг другу.

**Реализация:** `[Transaction(ReadOnly = true)]` атрибут. `GrainTransactionHandler.Join` при read-only
не захватывает `_lock`, только регистрирует участника. `CollectStates` возвращает пустой список.

**Затрагивает:** `TransactionAttribute.cs`, `GrainTransactionHandler.cs`

---

## 10. Конфигурируемые таймауты

**Что:** Сейчас таймауты захвата семафора (10s) и порог takeover (30s) — хардкод в `GrainTransactionHandler`.
Вынести в `TransactionOptions` через `IOptions<T>`.

**Затрагивает:** `GrainTransactionHandler.cs`, регистрация сервисов


## 11. Lock-Маршруты транзакций

**Что:** Перед началом транзакции мы берем несколько блокировок, блокировки берутся либо все, либо ни одной.
Таким образом мы максимально предотвращаем deadlock, так как транзакции будут ждать друг друга на этапе получения блокировок, а не уже внутри транзакции.

**Затрагивает:** `Transactions.cs`, новый сервис 'IClusterLocks'


