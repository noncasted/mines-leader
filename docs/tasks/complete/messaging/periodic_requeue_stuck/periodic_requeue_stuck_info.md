## Задача: Периодический RequeueStuck в SideEffectsWorker

### Цель
Добавить периодическую проверку застрявших записей в `side_effects_processing` и перемещение их обратно в очередь.

### Контекст
Часть Messaging Roadmap Phase 1 (P0). `RequeueStuck()` вызывается только при старте. Если silo падает посреди обработки — записи в processing застревают навсегда.

### Шаги реализации

**1. Новый метод RequeueStuckOlderThan**
  1.1. Добавить в ISideEffectsStorage и SideEffectsStorage — `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs`

**2. Конфигурация**
  2.1. StuckCheckIntervalSeconds, StuckThresholdMinutes — `backend/Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs`

**3. Периодический вызов в Loop**
  3.1. _lastStuckCheck трекинг и вызов — `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs`

### Ключевые файлы

| Файл | Роль |
|------|------|
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsStorage.cs` | Новый метод |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs` | Конфигурация |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Периодический вызов |
