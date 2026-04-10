## Задача: Sequence Numbers + Ring Buffer + Catch-Up в RuntimeChannel

### Цель
Устранить молчаливую потерю сообщений при обрыве observer references в RuntimeChannel. Реализовать:
1. Монотонный sequence number на каждый Publish
2. Ring buffer фиксированного размера (1024) в grain'е для хранения последних сообщений
3. Tracking lastSeenSequence на стороне observer'а
4. CatchUp механизм при resubscribe — channel отправляет пропущенные из ring buffer
5. GapDetected flag если ring buffer не покрывает запрошенный sequence

### Контекст
Observer references — прямые TCP-ссылки на процесс. Умирают при перезапуске silo, network partition, GC. Текущий resubscribe loop (10s) не компенсирует потерянные сообщения между поломкой и восстановлением.

Часть Messaging Roadmap Phase 1 (P0). Детальный план: `docs/tasks/messaging/01_channel_sequence_catchup.md`

### Шаги реализации

**1. SequencedMessage и CatchUpResult — типы данных**
  1.1. Добавить `SequencedMessage` class с `[GenerateSerializer]` — `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`
  1.2. Добавить `CatchUpResult` class с `[GenerateSerializer]` — `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`

**2. Ring buffer в RuntimeChannel grain**
  2.1. Добавить `_sequenceNumber`, `_buffer[]`, `_bufferCount` поля — `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`
  2.2. Изменить `Publish()` — инкремент sequence, запись в buffer, отправка `SequencedMessage` — `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`
  2.3. Добавить `CatchUp()` метод в `IRuntimeChannel` и реализацию — `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs`

**3. Observer sequence tracking**
  3.1. Добавить `LastSeenSequence` property в `RuntimeChannelObserver` — `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs`
  3.2. Обновить `Send()` — извлекать sequence из `SequencedMessage` — `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs`

**4. Client-side catch-up при resubscribe**
  4.1. Изменить `Listener.Resubscribe()` — вызывать `CatchUp()` после `AddObserver()` — `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs`

**5. Конфигурация**
  5.1. Добавить `CatchUpBufferSize` в `RuntimeChannelOptions` — `backend/Infrastructure/Messaging/Channels/RuntimeChannelOptions.cs`

**6. Метрики**
  6.1. Добавить `ChannelCatchUpExecuted`, `ChannelCatchUpMessages`, `ChannelGapDetected` — `backend/Common/Extensions/Metrics/BackendMetrics.cs`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` | Grain: ring buffer, SequencedMessage, CatchUp, Publish |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` | Client: catch-up при resubscribe |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs` | Observer: LastSeenSequence tracking |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelOptions.cs` | Config: CatchUpBufferSize |
| `backend/Common/Extensions/Metrics/BackendMetrics.cs` | Метрики catch-up |

### Документация к прочтению
- `rules/ORLEANS_GRAINS.md` — grain pattern, [AlwaysInterleave], constructor injection
- `rules/CODE_STYLE.md` — member order, naming

### Риски
- **[AlwaysInterleave] concurrency**: Publish и CatchUp оба `[AlwaysInterleave]` — ring buffer read/write может быть concurrent. Решение: grain single-threaded для основных вызовов, Publish interleave но пишет в buffer sequentially. CatchUp читает snapshot.
- **Memory**: 1024 * avg payload size на channel grain. Для типичных payload'ов (~1KB) = ~1MB. Приемлемо.
- **Backward compat**: Observer'ы получают `SequencedMessage` вместо raw object — нужен unwrap на клиенте.
