# 06. Retry с Backoff для RuntimePipe Caller

## Фаза: 2 (Ordering и consistency) | Приоритет: P1 | Оценка: 1 день

## Проблема

`RuntimePipe` grain хранит **один** observer (handler). Если silo с handler'ом перезапускается:

1. Observer reference мёртв
2. Все `Send` вызовы получают exception (observer not found или timeout)
3. Resubscribe loop на handler стороне восстановит observer через ~10 секунд
4. В течение этих 10 секунд **все RPC вызовы через pipe падают**

Текущие use cases: `MatchFactory` → `SessionEndpoints` через pipe для создания матчей. Сбой = игрок не может начать матч.

`RuntimePipeClient.Send()` сейчас:
```csharp
public Task<TResponse> Send<TResponse>(IRuntimePipeId id, object message) {
    var rawId = id.ToRaw();
    var pipe = _orleans.GetGrain<IRuntimePipe>(rawId);
    return pipe.Send<TResponse>(message);
}
```

Нет retry — exception прокидывается caller'у.

## Решение

Добавить retry с exponential backoff в `RuntimePipeClient.Send()`. Configurable через `RuntimePipeOptions`.

## Шаги реализации

### 1. Добавить retry параметры в RuntimePipeOptions

**Файл:** `backend/Infrastructure/Messaging/Pipes/RuntimePipeOptions.cs`

```csharp
public int SendRetryCount { get; set; } = 3;
public int SendRetryBaseDelayMs { get; set; } = 500; // 500ms, 1s, 2s
```

### 2. Добавить retry logic в RuntimePipeClient.Send()

**Файл:** `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs`

```csharp
public async Task<TResponse> Send<TResponse>(IRuntimePipeId id, object message) {
    var rawId = id.ToRaw();
    var pipe = _orleans.GetGrain<IRuntimePipe>(rawId);
    var options = _config.Value;
    
    Exception? lastException = null;
    
    for (var attempt = 0; attempt <= options.SendRetryCount; attempt++) {
        try {
            return await pipe.Send<TResponse>(message);
        }
        catch (Exception e) {
            lastException = e;
            BackendMetrics.PipeRetry.Add(1);
            
            if (attempt < options.SendRetryCount) {
                var delay = options.SendRetryBaseDelayMs * (1 << attempt); // exponential
                _logger.LogWarning(e,
                    "[Messaging] [Pipe] Send to {PipeId} failed (attempt {Attempt}/{Max}), retrying in {Delay}ms",
                    rawId, attempt + 1, options.SendRetryCount + 1, delay);
                await Task.Delay(delay);
            }
        }
    }
    
    throw lastException!;
}
```

### 3. Инжектировать config в RuntimePipeClient

**Файл:** `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs`

Добавить `IRuntimePipeConfig _config` в конструктор.

### 4. Метрика

В `BackendMetrics`:
```csharp
public static readonly Counter<long> PipeRetry = ...;
```

## Ключевые файлы

| Файл | Изменение |
|------|-----------|
| `Infrastructure/Messaging/Pipes/MessagePipeClient.cs` | Retry loop в Send(), инжекция config |
| `Infrastructure/Messaging/Pipes/RuntimePipeOptions.cs` | SendRetryCount, SendRetryBaseDelayMs |

## Расчёт timing

С дефолтными значениями:
- Attempt 1: immediate → fail
- Wait 500ms
- Attempt 2: → fail
- Wait 1000ms
- Attempt 3: → fail
- Wait 2000ms
- Attempt 4 (last): → fail → throw

Total worst case: 3.5 секунды. Resubscribe interval 10 секунд → не всегда хватит. Но с adaptive resubscribe (задача 08) — failure-triggered resubscribe будет быстрее.

Альтернатива: увеличить `SendRetryCount = 5` с `SendRetryBaseDelayMs = 200`:
- 200ms + 400ms + 800ms + 1600ms + 3200ms = 6.2s total, 6 attempts

## Риски

- **Retry storm**: Если handler действительно мёртв (не перезапуск, а выведен), retry создаёт нагрузку на pipe grain. Exponential backoff + `SendRetryCount = 3` ограничивает
- **Idempotency**: Retry означает что handler может получить один и тот же запрос дважды (если timeout на первой попытке, но handler всё-таки обработал). Request handlers должны быть идемпотентны. Для `MatchFactory.CreateWithBot` это уже так (создание матча идемпотентно по userId)

## Тесты

- Unit: Handler отвечает на 3-й attempt → success, проверить retry count
- Unit: Все attempts fail → exception пробрасывается
- Integration: Restart silo с handler'ом → caller переживает через retry
