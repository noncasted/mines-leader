# 04. Version-Based Idempotency в StateCollection

## Фаза: 2 (Ordering и consistency) | Приоритет: P1 | Оценка: 2-3 дня

## Проблема

`SideEffectsWorker` выполняет entries параллельно (`ExecuteEntry(entry, lifetime).NoAwait()`). Порядок записи в `side_effects_queue` сохраняется при `Read()` (`ORDER BY created_at`), но параллельное выполнение **не гарантирует порядок доставки**.

Для `StateCollection` это критично:
1. Grain обновляет state: Version 1 → пишет в DurableQueue
2. Grain обновляет state: Version 2 → пишет в DurableQueue
3. SideEffectsWorker достаёт оба параллельно
4. Version 2 доставляется и применяется первой
5. Version 1 доставляется и **перезаписывает** Version 2
6. StateCollection содержит устаревшие данные

Текущий `StateCollection.ListenUpdates`:
```csharp
_utils.ListenUpdates(lifetime, (key, value) => {
    this[key] = value;  // безусловная перезапись
    _updated.Invoke();
});
```

## Решение

Добавить timestamp или version в `StateCollectionUpdate`, применять только если новее текущего.

`IStateValue` уже содержит `int Version`, но это schema version (всегда 0 для данного типа), не instance version. Нужен отдельный механизм.

Простейший вариант: **timestamp-based**. При создании `StateCollectionUpdate` записывать `DateTime.UtcNow`, при применении — сравнивать с timestamp последнего применённого значения.

## Шаги реализации

### 1. Добавить UpdatedAt в StateCollectionUpdate

**Файл:** `backend/Infrastructure/Data/Collections/StateCollection.cs`

```csharp
[GenerateSerializer]
public class StateCollectionUpdate<TKey, TValue> {
    [Id(0)] public required TKey Key { get; init; }
    [Id(1)] public required TValue Value { get; init; }
    [Id(2)] public DateTime UpdatedAt { get; init; }
}
```

### 2. Добавить timestamp при создании update

**Файл:** `backend/Infrastructure/Data/Collections/StateCollection.cs`

В `StateCollectionUtils.PushUpdate()` и `PushTransactionalUpdate()`:
```csharp
new StateCollectionUpdate<TKey, TValue> {
    Key = key,
    Value = value,
    UpdatedAt = DateTime.UtcNow
}
```

### 3. Хранить последний timestamp для каждого key

**Файл:** `backend/Infrastructure/Data/Collections/StateCollection.cs`

В `StateCollection<TKey, TValue>`:
```csharp
private readonly Dictionary<TKey, DateTime> _lastUpdated = new();
```

### 4. Применять update только если новее

**Файл:** `backend/Infrastructure/Data/Collections/StateCollection.cs`

В `OnLocalSetupCompleted`, в callback `ListenUpdates`:
```csharp
await _utils.ListenUpdates(lifetime, (key, value, updatedAt) => {
    if (_lastUpdated.TryGetValue(key, out var last) && updatedAt <= last)
        return; // skip stale update
    
    this[key] = value;
    _lastUpdated[key] = updatedAt;
    _updated.Invoke();
});
```

### 5. Обновить ListenUpdates signature

**Файл:** `backend/Infrastructure/Data/Collections/StateCollection.cs`

```csharp
public Task ListenUpdates(IReadOnlyLifetime lifetime, Action<TKey, TValue, DateTime> onUpdate) {
    return _messaging.ListenDurableQueue<StateCollectionUpdate<TKey, TValue>>(lifetime,
        _queueId,
        update => onUpdate(update.Key, update.Value, update.UpdatedAt));
}
```

### 6. Метрика

В `BackendMetrics`:
```csharp
public static readonly Counter<long> StateCollectionStaleUpdate = ...;
```

## Ключевые файлы

| Файл | Изменение |
|------|-----------|
| `Infrastructure/Data/Collections/StateCollection.cs` | UpdatedAt field, timestamp tracking, conditional apply |

## Backward Compatibility

`StateCollectionUpdate` сериализуется Orleans serializer'ом. Добавление `[Id(2)]` — backward-compatible: старые сообщения без `UpdatedAt` десериализуются с `default(DateTime)` (01.01.0001). Для них `updatedAt <= last` будет true → пропуск. Это безопасно: к моменту деплоя старых сообщений в очереди не будет.

Альтернатива: при `UpdatedAt == default` — всегда применять (legacy path).

## Риски

- **Clock skew**: Если два silo имеют разное системное время — timestamp'ы некорректны. В рамках одного кластера с NTP расхождение < 1ms, проблемы нет
- **Одновременные update с тем же timestamp**: `updatedAt <= last` с `<=` гарантирует что при равных timestamp'ах второй update отбрасывается. Для DateTime.UtcNow коллизия маловероятна (100ns precision)
- **Initial load vs live updates**: При загрузке из DB (`_utils.Load`) timestamp не устанавливается. Нужно инициализировать `_lastUpdated` значениями из DB или `DateTime.MinValue`

## Тесты

- Unit: Два update с разными timestamp → применяется только более новый
- Unit: Update с `default(DateTime)` → обрабатывается корректно (legacy path)
- Integration: Параллельная отправка 100 updates → StateCollection содержит последнее значение
