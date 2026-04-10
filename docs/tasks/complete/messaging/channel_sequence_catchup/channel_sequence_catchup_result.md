## Channel Sequence Catch-Up — Результат

### Статус: Завершено

### Что сделано
1. Добавлены `SequencedMessage` и `CatchUpResult` serializable классы
2. Ring buffer (configurable size, default 1024) в `RuntimeChannel` grain с инициализацией в `OnActivateAsync`
3. `Publish()` теперь инкрементирует sequence, пишет в buffer, отправляет `SequencedMessage` observers
4. `CatchUp()` метод — возвращает пропущенные сообщения из buffer с gap detection
5. `RuntimeChannelObserver` трекает `LastSeenSequence`, unwraps `SequencedMessage` для consumer'а
6. `RuntimeChannelClient.Resubscribe()` вызывает `CatchUp()` при наличии `LastSeenSequence`
7. `CatchUpBufferSize` добавлен в `RuntimeChannelOptions`
8. 3 новых метрики: `ChannelCatchUpExecuted`, `ChannelCatchUpMessages`, `ChannelGapDetected`

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` | SequencedMessage, CatchUpResult, ring buffer, CatchUp(), modified Publish() |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelObserver.cs` | LastSeenSequence tracking, SequencedMessage unwrap |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` | CatchUp call in Resubscribe() |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelOptions.cs` | CatchUpBufferSize config |
| `backend/Common/Extensions/Metrics/BackendMetrics.cs` | 3 new channel catch-up metrics |
