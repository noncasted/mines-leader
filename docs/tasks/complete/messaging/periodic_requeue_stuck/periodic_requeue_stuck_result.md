## Periodic RequeueStuck — Результат

### Статус: Завершено

### Что сделано
1. Новый метод `RequeueStuckOlderThan(TimeSpan age)` в `ISideEffectsStorage` и `SideEffectsStorage`
2. SQL: DELETE FROM processing WHERE processing_started_at < cutoff → INSERT INTO queue
3. Конфигурация: `StuckCheckIntervalSeconds = 60`, `StuckThresholdMinutes = 5` в `SideEffectsOptions`
4. Периодический вызов в `SideEffectsWorker.Loop()` с `_lastStuckCheck` tracking

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | RequeueStuckOlderThan() method + interface |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs` | StuckCheckIntervalSeconds, StuckThresholdMinutes |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | _lastStuckCheck field, periodic call in Loop() |
