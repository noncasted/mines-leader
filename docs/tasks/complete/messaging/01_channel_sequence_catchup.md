# 01. Sequence Numbers + Ring Buffer + Catch-Up в RuntimeChannel

## Фаза: 1 (Надёжность доставки) | Приоритет: P0 | Оценка: 3-5 дней

## Проблема

`RuntimeChannel` доставляет сообщения через `IGrainObserver` references. Observer references — прямые TCP-ссылки на процесс. Они молча умирают при:
- Перезапуске silo (деплой, crash)
- Network partition
- GC observer grain'а

Текущая митигация: resubscribe loop каждые 10 секунд в `RuntimeChannelClient`. Между поломкой observer'а и resubscribe **сообщения теряются молча** — ни retry, ни уведомления.

## Решение: Sequence Numbers + Ring Buffer + Catch-Up

### Концепция

1. Каждый `RuntimeChannel` grain хранит монотонно растущий `_sequenceNumber`
2. При каждом `Publish()` — инкремент sequence, сообщение + sequence сохраняется в ring buffer
3. Каждый observer на клиентской стороне трекает `_lastSeenSequence`
4. При resubscribe клиент передаёт `lastSeenSequence` → channel отправляет пропущенные из ring buffer
5. Ring buffer фиксированного размера (~1000 сообщений) — старые вытесняются

### Гарантии

- **При кратковременном сбое** (< 1000 сообщений пропущено): полный catch-up, 0 потерь
- **При длительном сбое** (> 1000 сообщений): catch-up последних 1000, клиент получает флаг `gapDetected` для полной ресинхронизации
- **При нормальной работе**: нулевой overhead — observer получает сообщения в реальном времени как раньше

## Шаги реализации

### 1. Добавить SequencedMessage wrapper

Создать обёртку для сообщений с sequence number.

```csharp
// В RuntimeChannel.cs
[GenerateSerializer]
public class SequencedMessage {
    [Id(0)] public long Sequence { get; set; }
    [Id(1)] public object Payload { get; set; } = null!;
}
```

### 2. Добавить ring buffer в RuntimeChannel grain

**Файл:** `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`

```csharp
private long _sequenceNumber;
private readonly SequencedMessage[] _buffer = new SequencedMessage[1024];
private int _bufferHead; // next write position

private void BufferMessage(object message) {
    _sequenceNumber++;
    var entry = new SequencedMessage { Sequence = _sequenceNumber, Payload = message };
    _buffer[_bufferHead % _buffer.Length] = entry;
    _bufferHead++;
}
```

### 3. Изменить Publish() — записывать в buffer перед fan-out

**Файл:** `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`

В начале `Publish()`:
```csharp
BufferMessage(message);
```

Observer'ам отправляется `SequencedMessage` вместо raw `object`.

### 4. Добавить CatchUp метод в IRuntimeChannel

**Файл:** `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`

```csharp
// В IRuntimeChannel interface:
[AlwaysInterleave]
Task<CatchUpResult> CatchUp(Guid observerId, long lastSeenSequence);

// CatchUpResult:
[GenerateSerializer]
public class CatchUpResult {
    [Id(0)] public IReadOnlyList<SequencedMessage> Messages { get; set; } = Array.Empty<SequencedMessage>();
    [Id(1)] public bool GapDetected { get; set; } // true если ring buffer не покрывает запрошенный sequence
    [Id(2)] public long CurrentSequence { get; set; }
}
```

Реализация: пройти ring buffer от `lastSeenSequence + 1` до `_sequenceNumber`, вернуть список. Если `lastSeenSequence` < oldest в buffer — `GapDetected = true`.

### 5. Изменить RuntimeChannelObserver — трекать sequence

**Файл:** `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs`

```csharp
public long LastSeenSequence { get; private set; }

// В Send():
if (message is SequencedMessage seq) {
    LastSeenSequence = seq.Sequence;
    _handler(seq.Payload);
} else {
    _handler(message); // backward compat
}
```

### 6. Изменить RuntimeChannelClient.Resubscribe — вызывать CatchUp

**Файл:** `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`

После `AddObserver()` в `Resubscribe()`:
```csharp
if (ObserverSource.LastSeenSequence > 0) {
    var catchUp = await Channel.CatchUp(ObserverSource.Id, ObserverSource.LastSeenSequence);
    
    if (catchUp.GapDetected)
        Logger.LogWarning("[Messaging] [Channel] Gap detected on {ChannelId}, missed messages before seq {Seq}",
            Id.ToRaw(), catchUp.Messages.FirstOrDefault()?.Sequence);
    
    foreach (var msg in catchUp.Messages)
        ObserverSource.Send(msg); // replay через тот же handler
}
```

### 7. Добавить buffer size в RuntimeChannelOptions

**Файл:** `backend/Infrastructure/Messaging/Channels/RuntimeChannelOptions.cs`

```csharp
public int CatchUpBufferSize { get; set; } = 1024;
```

### 8. Метрики

Добавить в `BackendMetrics`:
- `ChannelCatchUpExecuted` — counter: сколько раз выполнен catch-up
- `ChannelCatchUpMessages` — histogram: сколько сообщений в каждом catch-up
- `ChannelGapDetected` — counter: сколько раз ring buffer не покрыл gap

## Ключевые файлы

| Файл | Изменение |
|------|-----------|
| `Infrastructure/Messaging/Channels/RuntimeChannel.cs` | Ring buffer, SequencedMessage, CatchUp(), изменённый Publish() |
| `Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` | CatchUp вызов в Resubscribe() |
| `Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs` | LastSeenSequence tracking |
| `Infrastructure/Messaging/Channels/RuntimeChannelOptions.cs` | CatchUpBufferSize |

## Риски

- **Backward compatibility**: Observer'ы которые не понимают `SequencedMessage` — обеспечить fallback через `is` check
- **Memory**: 1024 сообщений на channel grain. Для крупных payload'ов (user projections) может быть значительно. Мониторить через grain memory metrics
- **Concurrency**: `Publish` помечен `[AlwaysInterleave]` — ring buffer write должен быть thread-safe. Использовать simple array + modular index (grain single-threaded для non-interleave, но Publish interleave)
- **CatchUp во время Publish**: CatchUp тоже `[AlwaysInterleave]` — нужен lock или immutable snapshot buffer

## Тесты

- Unit: ring buffer wrap-around, catch-up с gap и без
- Integration: restart observer, проверить что catch-up доставляет пропущенные
- Нагрузочный: 10k messages/sec, проверить что buffer не деградирует
