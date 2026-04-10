# 05. Timeout на Channel Observer Delivery

## Фаза: 2 (Ordering и consistency) | Приоритет: P1 | Оценка: 1 день

## Проблема

`RuntimeChannel.Publish()` делает `Task.WhenAll` по всем observers:
```csharp
await Task.WhenAll(_observers.Values.Select(data => SendSafe(data)));
```

Если один observer "зависает" (сеть, перегруженный silo) — **все остальные observers ждут**. Publish блокируется на неопределённое время.

В `RuntimePipe` уже есть `SendTimeoutSeconds = 30`, но в `RuntimeChannel` таймаута нет.

## Решение

Добавить per-observer timeout в `RuntimeChannel.Publish()`. При timeout — observer удаляется (считается мёртвым).

## Шаги реализации

### 1. Добавить DeliveryTimeoutSeconds в RuntimeChannelOptions

**Файл:** `backend/Infrastructure/Messaging/Channels/RuntimeChannelOptions.cs`

```csharp
public int DeliveryTimeoutSeconds { get; set; } = 5;
```

5 секунд — достаточно для in-cluster delivery, но не блокирует весь broadcast надолго.

### 2. Инжектировать config в Publish и добавить timeout

**Файл:** `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`

В `SendSafe`:
```csharp
async Task SendSafe(ObserverData data) {
    try {
        var timeout = TimeSpan.FromSeconds(_config.Value.DeliveryTimeoutSeconds);
        var deliveryTask = data.Observer.Send(message);
        
        if (await Task.WhenAny(deliveryTask, Task.Delay(timeout)) != deliveryTask) {
            toRemove.Add(data.Id);
            BackendMetrics.ChannelDeliveryTimeout.Add(1);
            _logger.LogWarning("[Messaging] [Channel] Delivery timeout to observer on {ChannelName}",
                this.GetPrimaryKeyString());
            return;
        }
        
        await deliveryTask; // propagate exception if any
    }
    catch (Exception e) {
        toRemove.Add(data.Id);
        BackendMetrics.ChannelDeliveryFailure.Add(1);
        _logger.LogError(e, "[Messaging] [Channel] Delivering message from {ChannelName} to observer failed",
            this.GetPrimaryKeyString());
    }
}
```

### 3. Метрика

В `BackendMetrics`:
```csharp
public static readonly Counter<long> ChannelDeliveryTimeout = ...;
```

## Ключевые файлы

| Файл | Изменение |
|------|-----------|
| `Infrastructure/Messaging/Channels/RuntimeChannel.cs` | Timeout в SendSafe |
| `Infrastructure/Messaging/Channels/RuntimeChannelOptions.cs` | DeliveryTimeoutSeconds |

## Связь с задачей 01 (Catch-Up)

После реализации catch-up (задача 01), timeout + removal observer'а перестаёт быть потерей сообщений: при resubscribe клиент получит пропущенные через catch-up. Это делает aggressive timeout безопасным.

## Риски

- **Task.Delay на каждый observer**: Создаёт timer per observer per publish. При 100 observers × 1000 msg/sec = 100K timers/sec. Для текущей нагрузки (~10 observers на channel) не проблема. При масштабировании — использовать `CancellationTokenSource` с timeout вместо `Task.WhenAny`
- **False positive timeout**: Если silo под нагрузкой, 5s может быть мало. Мониторить `ChannelDeliveryTimeout` метрику, при необходимости увеличить

## Тесты

- Unit: Observer, который не отвечает → timeout → removal
- Unit: Все observers отвечают < timeout → нормальная доставка
- Integration: Медленный observer не блокирует быстрые
