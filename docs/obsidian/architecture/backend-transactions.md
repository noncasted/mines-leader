# Транзакции

Кастомная ACID-транзакционная система поверх Orleans.

## Обзор

Не используется встроенный Orleans Transactions. Вместо этого — собственная реализация через `ITransactions`, обеспечивающая:
- **Atomicity** — все изменения или ничего
- **Consistency** — состояние валидно после коммита
- **Isolation** — через TransactionContext (thread-local)
- **Durability** — PostgreSQL + JSONB

---

## Поток транзакции

```mermaid
sequenceDiagram
    participant C as Caller
    participant T as ITransactions
    participant G1 as Grain 1
    participant G2 as Grain 2
    participant DB as PostgreSQL

    C->>T: InTransaction(async () => ...)
    T->>T: Создать TransactionContext

    T->>G1: Action (в контексте транзакции)
    G1->>G1: state.Update(...) -> записать в контекст
    T->>G2: Action (в контексте транзакции)
    G2->>G2: state.Update(...) -> записать в контекст

    T->>G1: CollectResult()
    T->>G2: CollectResult()

    T->>DB: BEGIN TRANSACTION
    T->>DB: Write G1 state
    T->>DB: Write G2 state
    T->>DB: Execute side effects
    T->>DB: COMMIT

    T->>G1: OnSuccess()
    T->>G2: OnSuccess()

    Note over T,DB: При ошибке: ROLLBACK + OnFailure() для всех
```

---

## Компоненты

### ITransactions
Оркестратор транзакций.

| Фаза | Описание |
|------|----------|
| 1. Execution | Выполнение бизнес-логики с TransactionContextProvider |
| 2. Collect | Сбор изменений состояний от всех участников |
| 3. Commit | DB-транзакция: записи стейтов + side effects |
| 4. Callback | Уведомление участников (OnSuccess/OnFailure) |

### TransactionContext
Per-transaction state holder (thread-local через `TransactionContextProvider`):

```
TransactionContext:
  Participants: Dictionary<GrainId, IGrainTransactionHandler>
  SideEffects: List<ISideEffect>
```

### IGrainTransactionHandler
Extension для грейнов, участвующих в транзакциях:

| Метод | Описание |
|-------|----------|
| `OnSuccess()` | Вызывается после успешного коммита |
| `OnFailure()` | Вызывается при откате |
| `CollectResult()` | Собирает изменённые стейты |

---

## Пример: Match.OnComplete()

```mermaid
graph TD
    A[Match.OnComplete] -->|Transaction| B[Читать колоды игроков]
    B --> C[Обновить MatchState]
    C --> D[Winner: Rating.AddRecord WIN]
    C --> E[Winner: Progression.AddRecord WIN]
    C --> F[Winner: MatchHistory.Add]
    C --> G[Loser: Rating.AddRecord LOSS]
    C --> H[Loser: Progression.AddRecord LOSS]
    C --> I[Loser: MatchHistory.Add]

    D --> J[Все стейты собраны]
    E --> J
    F --> J
    G --> J
    H --> J
    I --> J

    J --> K[DB: BEGIN]
    K --> L[Write: MatchState]
    K --> M[Write: RatingState x2]
    K --> N[Write: ProgressionState x2]
    K --> O[Write: MatchHistoryState x2]
    L --> P[COMMIT]
    M --> P
    N --> P
    O --> P
```

7 стейтов обновляются атомарно в одной DB-транзакции.

---

## Атрибут [Transaction]

> Это **кастомный** атрибут из `Infrastructure`, не Orleans-native.

Помечает методы грейна, вызываемые внутри транзакционного контекста. Все User-грейны помечены `[Transaction]`.

```csharp
[Transaction]
public async Task SetName(string name) {
    await _state.Update(s => { s.Name = name; });
}
```

---

## Side Effects

Побочные эффекты, выполняемые после коммита транзакции:

1. Регистрируются во время транзакции через `TransactionContext.SideEffects`
2. Записываются в БД вместе со стейтами
3. Обрабатываются `SideEffectsWorker` (IHostedService)
4. Конфигурируются через `SideEffectsOptions`

Примеры: push-уведомления через DurableQueue, обновление StateCollection.

---

## State Storage

### PostgreSQL
- JSONB-хранилище с версионированием
- ADO.NET (Npgsql)
- Таблица на каждый тип стейта (из `StatesLookup`)

### Миграции
`IStateMigrations` автоматически мигрирует старые версии стейтов при чтении:
- Каждый стейт имеет `Version` (int)
- При несовпадении версии — миграционная цепочка

### Кеширование
`StateStorageCache` кеширует повторные чтения в рамках запроса.

## Ключевые файлы

| Файл | Описание |
|------|----------|
| `backend/Infrastructure/Orleans/Transactions/Transactions.cs` | Оркестратор транзакций |
| `backend/Infrastructure/Orleans/Transactions/TransactionContext.cs` | Контекст транзакции |
| `backend/Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs` | Grain extension |
| `backend/Infrastructure/Orleans/State/StateStorage.cs` | PostgreSQL persistence |
| `backend/Infrastructure/Orleans/State/StateMigrations.cs` | Миграции версий |
| `backend/Infrastructure/Orleans/State/StateSerializer.cs` | MemoryPack сериализация |
| `backend/Infrastructure/Orleans/Utils/OrleansUtils.cs` | IOrleans: InTransaction() |
