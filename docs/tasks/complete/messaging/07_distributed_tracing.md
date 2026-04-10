# 07. Distributed Tracing: TransactionId Propagation

## Фаза: 3 (Observability) | Приоритет: P2 | Оценка: 2-3 дня

## Проблема

Сейчас путь сообщения невозможно проследить:

```
Transaction (Guid A) → PushTransactional → side_effects_queue → SideEffectsWorker → DurableQueue.Push → Observer → Consumer
```

На каждом этапе теряется связь с исходной транзакцией:
- `DurableQueueSideEffect` не содержит `TransactionId`
- `SideEffectsWorker` логирует `entry.Id` (side effect ID), но не transaction ID
- `DurableQueue.Push` не логирует source
- Consumer callback не знает откуда пришло сообщение

Результат: при проблемах с доставкой невозможно понять какая транзакция породила сообщение и где оно потерялось.

## Решение

Пропагировать `TransactionId` (или `CorrelationId` для не-транзакционных) через всю цепочку доставки. Интеграция с OpenTelemetry Activity API.

## Шаги реализации

### 1. Добавить CorrelationId в ISideEffect

**Файл:** `backend/Infrastructure/Orleans/SideEffects/ISideEffect.cs`

```csharp
public interface ISideEffect {
    Guid CorrelationId { get; set; }
    Task Execute(IOrleans orleans);
}
```

### 2. Заполнять CorrelationId при создании side effect

**Файл:** `backend/Infrastructure/Messaging/Queues/DurableQueueSideEffect.cs`

В `DurableQueueSideEffect`:
```csharp
[Id(2)] public Guid CorrelationId { get; set; }
```

**Файл:** `backend/Infrastructure/Messaging/Queues/DurableQueueClient.cs`

В `PushTransactional`:
```csharp
var sideEffect = new DurableQueueSideEffect {
    QueueName = id.ToRaw(),
    Message = message,
    CorrelationId = TransactionContextProvider.Current?.Id ?? Guid.NewGuid()
};
```

В `PushDirect`:
```csharp
CorrelationId = Activity.Current?.TraceId != default
    ? new Guid(Activity.Current.TraceId.ToByteArray().Take(16).ToArray())
    : Guid.NewGuid()
```

### 3. Логировать CorrelationId в SideEffectsWorker

**Файл:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`

В `ExecuteEntry`:
```csharp
using var scope = _logger.BeginScope(new Dictionary<string, object> {
    ["CorrelationId"] = entry.Effect.CorrelationId,
    ["SideEffectId"] = entry.Id
});
```

### 4. Пропагировать в DurableQueue.Push

**Файл:** `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`

Опционально: добавить `CorrelationId` в observer `Send()` через обёртку, или через structured logging.

### 5. Добавить в side_effects таблицы

SQL migration: добавить `correlation_id UUID` column в `side_effects_queue`, `side_effects_processing`, `side_effects_retry_queue`.

### 6. Обновить SideEffectsStorage.Write

**Файл:** `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`

Записывать `correlation_id` при INSERT.

## Ключевые файлы

| Файл | Изменение |
|------|-----------|
| `Infrastructure/Orleans/SideEffects/ISideEffect.cs` | CorrelationId property |
| `Infrastructure/Messaging/Queues/DurableQueueSideEffect.cs` | [Id(2)] CorrelationId |
| `Infrastructure/Messaging/Queues/DurableQueueClient.cs` | Заполнение CorrelationId |
| `Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Structured logging scope |
| `Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | correlation_id в SQL |

## Результат

Логи становятся коррелированными:
```
[Transaction] Process started TransactionId=ABC
[SideEffects] Executing CorrelationId=ABC SideEffectId=XYZ
[DurableQueue] Pushed to queue-name CorrelationId=ABC
[StateCollection] Updated key=123 CorrelationId=ABC
```

Одним запросом в логах можно найти весь путь сообщения.

## Риски

- **ISideEffect interface change**: Добавление property в interface — breaking change для существующих реализации. Альтернатива: abstract base class `SideEffectBase` с default implementation, или отдельный `ICorrelatedSideEffect` interface
- **DB migration**: Добавление column — online, nullable, без downtime

## Тесты

- Integration: Создать транзакцию → push side effect → проверить что CorrelationId == TransactionId в логах
