## Задача: Исправление багов в Messaging Infrastructure

### Цель
Устранить четыре production-баги и четыре структурных проблемы в слое messaging: мёртвые observers накапливаются вечно (BUG #1), grain может получить `SemaphoreFullException` после force-takeover транзакции (BUG #2), `RuntimePipe.Send` может зависнуть навсегда (BUG #3), race condition при параллельном создании consumers (BUG #4), плюс четыре улучшения надёжности.

### Шаги реализации

**КРИТИЧЕСКИЕ БАГИ**

1. Заменить `try/catch` вокруг `Task`-создания на `async Task SendSafe()` локальную функцию — `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` (строки 69–90)
2. То же исправление в `Push()` — `backend/Infrastructure/Messaging/Queues/MessageQueue.cs` (строки 62–87)
3. Исправить `OnSuccess()`: заменить `throw new Exception(...)` на `return Task.CompletedTask` при ID mismatch — `backend/Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs` (строки 199–208)
4. Добавить `SemaphoreSlim _createLock` и double-checked lock в `GetOrCreateConsumer` — `backend/Infrastructure/Messaging/Queues/MessageQueueClient.cs` (строки 41–75)
5. То же исправление — `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` (строки 37–71)

**УЛУЧШЕНИЯ**

6. Создать `RuntimePipeOptions` с `ObserverKeepAliveMinutes` и `SendTimeoutSeconds` — `backend/Infrastructure/Messaging/Pipes/RuntimePipeOptions.cs` [новый файл — добавить в `Infrastructure.csproj`]
7. Добавить `.WaitAsync(timeout)` в `RuntimePipe.Send()`, внедрить `IRuntimePipeConfig` в конструктор — `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs`
8. Заменить `throw new Exception(...)` на `this.DeactivationControl.DelayDeactivation(...)` в `OnDeactivateAsync` — `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs` (строка 30) и `backend/Infrastructure/Messaging/Queues/MessageQueue.cs` (строка 35)
9. Сделать `Resubscribe()` методы `async Task` (сейчас `try/catch` тоже не ловит async), добавить счётчик `_consecutiveFailures` и throttled logging — `MessageQueueClient.cs`, `RuntimeChannelClient.cs`, `MessagePipeClient.cs`
10. Добавить поле `EmptyScanDelay` в options и adaptive delay в Loop — `backend/Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs` и `SideEffectsWorker.cs`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Messaging/Channels/RuntimeChannel.cs` | BUG #1: grain Publish — async try/catch |
| `backend/Infrastructure/Messaging/Queues/MessageQueue.cs` | BUG #1: grain Push — async try/catch; Impr. A: DeactivationControl |
| `backend/Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs` | BUG #2: OnSuccess при force-takeover |
| `backend/Infrastructure/Messaging/Queues/MessageQueueClient.cs` | BUG #4: race condition + Impr. C: async Resubscribe |
| `backend/Infrastructure/Messaging/Channels/RuntimeChannelClient.cs` | BUG #4: race condition + Impr. C: async Resubscribe |
| `backend/Infrastructure/Messaging/Pipes/MessagePipe.cs` | BUG #3: таймаут Send; Impr. A: DeactivationControl; Impr. B: конфигурация |
| `backend/Infrastructure/Messaging/Pipes/MessagePipeClient.cs` | Impr. C: async Resubscribe |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsOptions.cs` | Impr. F: добавить EmptyScanDelay |
| `backend/Infrastructure/Orleans/SideEffects/SideEffectsWorker.cs` | Impr. F: adaptive polling |

### Документация к прочтению
- `rules/CODE_STYLE.md` — порядок членов класса, NoAwait, паттерн локальных функций

### Риски
- BUG #2: `OnSuccess` сейчас бросает при ID mismatch — `Transactions.cs` перехватывает это в общем `catch`, вызывает `Rollback()` → `OnFailure()`. После исправления (return CompletedTask) `Rollback` вызван не будет — убедиться что в `Transactions.cs` это ожидаемое поведение и не нужна дополнительная очистка
- Improvement A: `this.DeactivationControl` доступен только в Orleans 7+; проверить что метод `DelayDeactivation(TimeSpan)` есть в используемой версии Orleans
- Improvement B: новый файл `RuntimePipeOptions.cs` нужно зарегистрировать в DI аналогично `DurableQueueOptions` — найти где `DurableQueueOptions` регистрируется в `MessagingExtensions.cs` и добавить рядом
