## StateCollection Idempotency — Результат

### Статус: Завершено

### Что сделано
1. `[Id(2)] DateTime UpdatedAt` добавлен в `StateCollectionUpdate<TKey, TValue>`
2. `PushUpdate` и `PushTransactionalUpdate` устанавливают `UpdatedAt = DateTime.UtcNow`
3. `StateCollection` хранит `_lastUpdated` dictionary для tracking последнего timestamp по ключу
4. `ListenUpdates` callback проверяет: если `updatedAt != default` и `updatedAt <= last` — пропускает stale update
5. Legacy path: сообщения без `UpdatedAt` (default) всегда применяются — backward compatible

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Infrastructure/Data/Collections/StateCollection.cs` | UpdatedAt field, timestamp tracking, conditional apply, updated ListenUpdates signature |
