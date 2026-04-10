# 02. DurableQueue.Push — Throw при 0 Subscribers

## Фаза: 1 (Надёжность доставки) | Приоритет: P0 | Оценка: 0.5 дня

## Проблема

`DurableQueueSideEffect.Execute()` вызывает `DurableQueue.Push(message)`. Если в момент Push **ни одного observer'а нет** (сервис ещё не стартовал, resubscribe не произошёл):
1. `Push()` итерирует пустой `_observers` dictionary
2. Ничего не делает
3. Возвращает `Task.CompletedTask`
4. `SideEffectsWorker` вызывает `CompleteProcessing()` — удаляет запись из `side_effects_processing`
5. **Сообщение потеряно навсегда**

Аналогичная проблема: если все observers упали при delivery — `toRemove` содержит все ID, они удаляются, `Push` завершается "успешно".

## Решение

`DurableQueue.Push()` должен бросать exception если доставка не удалась ни одному subscriber'у. `SideEffectsWorker` поймает exception → `FailProcessing()` → retry через incremental delay → к тому моменту observer resubscribe пройдёт.

## Шаги реализации

### 1. Добавить NoSubscribersException

**Файл:** `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`

```csharp
public class NoSubscribersException : Exception {
    public NoSubscribersException(string queueName)
        : base($"[DurableQueue] No active subscribers for queue '{queueName}'") { }
}
```

### 2. Изменить DurableQueue.Push()

**Файл:** `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`

```csharp
public async Task Push(object message) {
    BackendMetrics.DurableQueuePushed.Add(1);
    BackendMetrics.DurableQueueObserverCount.Record(_observers.Count);

    if (_observers.Count == 0)
        throw new NoSubscribersException(this.GetPrimaryKeyString());

    // ... existing delivery logic ...

    // After delivery: check if ALL failed
    if (toRemove != null && toRemove.Count == _observers.Count)
        throw new NoSubscribersException(this.GetPrimaryKeyString());
    
    // Remove failed observers
    if (toRemove != null)
        foreach (var id in toRemove)
            _observers.Remove(id);
}
```

### 3. Добавить метрику

В `BackendMetrics`:
```csharp
public static readonly Counter<long> DurableQueueNoSubscribers = ...;
```

Инкрементировать перед throw в `Push()`.

## Ключевые файлы

| Файл | Изменение |
|------|-----------|
| `Infrastructure/Messaging/Queues/DurableQueue.cs` | NoSubscribersException, проверка в Push() |

## Как это работает с SideEffectsWorker

1. `SideEffectsWorker.ExecuteEntry()` вызывает `entry.Effect.Execute(orleans)` → `DurableQueue.Push()`
2. `Push()` бросает `NoSubscribersException`
3. `catch` в `ExecuteEntry` → `FailProcessing(entry.Id, retryCount, maxRetryCount, incrementalRetryDelay)`
4. Запись перемещается в `side_effects_retry_queue` с `retry_after = now + (retryCount+1) * 30s`
5. Через 30 секунд `RequeueReady()` вернёт запись в `side_effects_queue`
6. К этому моменту observer resubscribe (10s interval) уже восстановит подписку
7. Повторная доставка успешна

## Риски

- **Thundering herd**: Если много сообщений скопилось в retry, а observer только появился — все retry прилетят одновременно. Текущий `ConcurrentExecutions = 50` ограничивает, но мониторить
- **Max retry exhaustion**: `MaxRetryCount = 5` при `IncrementalRetryDelay = 30s` — максимум ~2.5 минуты на восстановление подписчика. Для штатного restart silo достаточно, для длительного outage — нет. Рассмотреть увеличение `MaxRetryCount` для durable queue effects

## Тесты

- Integration: Push в очередь без подписчиков → проверить что retry срабатывает и доставка происходит после появления подписчика
- Integration: Все подписчики fail при delivery → тот же retry flow
