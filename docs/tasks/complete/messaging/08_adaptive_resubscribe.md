# 08. Adaptive Resubscribe Interval

## Фаза: 3 (Observability) | Приоритет: P2 | Оценка: 1 день

## Проблема

Resubscribe loop во всех трёх клиентах (`RuntimeChannelClient`, `RuntimePipeClient`, `DurableQueueClient`) — фиксированные 10 секунд:

```csharp
await Task.Delay(TimeSpan.FromSeconds(10), lifetime.Token);
```

Проблемы:
- **При стабильной работе**: 10s слишком часто. Каждый resubscribe — grain call на `AddObserver`. При 50 подписок × 6 секунд = 300 grain calls/min лишней нагрузки
- **При сбое**: 10s слишком редко. Observer мёртв → 10 секунд пропущенных сообщений
- **Thundering herd**: При массовом restart silo все клиенты resubscribe одновременно каждые 10s

## Решение

Adaptive interval с jitter:
- **Success path**: Постепенно увеличивать интервал до 30-60 секунд
- **Failure path**: Немедленный retry, затем exponential backoff от 1s до 30s
- **Jitter**: ±20% рандом для предотвращения синхронных волн

## Шаги реализации

### 1. Создать AdaptiveInterval utility

**Файл:** `backend/Infrastructure/Messaging/AdaptiveInterval.cs` (новый)

```csharp
public class AdaptiveInterval {
    public AdaptiveInterval(
        TimeSpan minInterval,
        TimeSpan maxInterval,
        TimeSpan failureBaseInterval,
        double jitterFactor = 0.2) { ... }
    
    private int _consecutiveSuccesses;
    private int _consecutiveFailures;
    
    public void RecordSuccess() {
        _consecutiveSuccesses++;
        _consecutiveFailures = 0;
    }
    
    public void RecordFailure() {
        _consecutiveFailures++;
        _consecutiveSuccesses = 0;
    }
    
    public TimeSpan GetNextDelay() {
        TimeSpan baseDelay;
        
        if (_consecutiveFailures > 0) {
            // Exponential backoff: 1s, 2s, 4s, 8s, ... capped at maxInterval
            baseDelay = TimeSpan.FromSeconds(
                Math.Min(_failureBaseInterval.TotalSeconds * (1 << Math.Min(_consecutiveFailures - 1, 5)),
                         _maxInterval.TotalSeconds));
        } else {
            // Gradual increase: 10s → 15s → 20s → ... → 60s
            var step = Math.Min(_consecutiveSuccesses, 10);
            var range = _maxInterval - _minInterval;
            baseDelay = _minInterval + TimeSpan.FromSeconds(range.TotalSeconds * step / 10);
        }
        
        // Jitter: ±20%
        var jitter = 1.0 + (Random.Shared.NextDouble() * 2 - 1) * _jitterFactor;
        return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * jitter);
    }
}
```

### 2. Заменить фиксированный delay в RuntimeChannelClient

**Файл:** `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`

```csharp
private readonly AdaptiveInterval _resubscribeInterval = new(
    minInterval: TimeSpan.FromSeconds(10),
    maxInterval: TimeSpan.FromSeconds(60),
    failureBaseInterval: TimeSpan.FromSeconds(1));

private async Task ResubscribeLoop(IReadOnlyLifetime lifetime) {
    while (lifetime.IsTerminated == false) {
        var allSucceeded = true;
        
        await Task.WhenAll(_listeners.Select(async t => {
            try {
                await t.Value.Resubscribe();
            } catch {
                allSucceeded = false;
            }
        }));
        
        if (allSucceeded)
            _resubscribeInterval.RecordSuccess();
        else
            _resubscribeInterval.RecordFailure();
        
        await Task.Delay(_resubscribeInterval.GetNextDelay(), lifetime.Token);
    }
}
```

### 3. Аналогично для DurableQueueClient и RuntimePipeClient

Те же изменения в `ResubscribeLoop()`.

## Ключевые файлы

| Файл | Изменение |
|------|-----------|
| `Infrastructure/Messaging/AdaptiveInterval.cs` | Новый utility class |
| `Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` | Adaptive interval в ResubscribeLoop |
| `Infrastructure/Messaging/Queues/DurableQueueClient.cs` | Adaptive interval в ResubscribeLoop |
| `Infrastructure/Messaging/Pipes/MessagePipeClient.cs` | Adaptive interval в ResubscribeLoop |

## Поведение

| Состояние | Интервал | Пример |
|-----------|----------|--------|
| Стабильная работа (10+ success) | 60s + jitter | 48-72s |
| Только стартовали | 10s + jitter | 8-12s |
| Первый failure | 1s + jitter | 0.8-1.2s |
| Второй failure | 2s + jitter | 1.6-2.4s |
| Пятый failure | 16s + jitter | 12.8-19.2s |
| Шестой+ failure | 32s (capped) + jitter | 25.6-38.4s |

## Риски

- **Observer expiration**: Если grain деактивируется через `ObserverKeepAliveMinutes = 3`, а resubscribe interval вырос до 60s — observer может быть удалён. 60s << 180s (3 min), проблемы нет. Но нужно гарантировать `maxInterval < ObserverKeepAliveMinutes * 60 / 2`
- **Failure detection delay**: При adaptive interval в 60s, failure detection = до 60s. С catch-up (задача 01) это допустимо

## Тесты

- Unit: AdaptiveInterval — проверить exponential backoff, gradual increase, jitter range
- Unit: 1000 вызовов GetNextDelay — все в допустимом диапазоне
