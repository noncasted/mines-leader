## Задача: DurableQueue — Throw при 0 Subscribers

### Цель
DurableQueue.Push() должен бросать exception если нет подписчиков или все подписчики упали при доставке. SideEffectsWorker поймает exception → retry → доставка после resubscribe.

### Контекст
Часть Messaging Roadmap Phase 1 (P0). Без этого fix'а сообщения из side_effects_queue молча теряются при отсутствии observers.

### Шаги реализации

**1. NoSubscribersException и проверка в Push()**
  1.1. Добавить `NoSubscribersException` класс — `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`
  1.2. Throw в начале Push() если `_observers.Count == 0` — `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`
  1.3. Throw после delivery если все observers failed — `backend/Infrastructure/Messaging/Queues/DurableQueue.cs`

**2. Метрика**
  2.1. Добавить `DurableQueueNoSubscribers` counter — `backend/Common/Extensions/Metrics/BackendMetrics.cs`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Messaging/Queues/DurableQueue.cs` | Exception + проверки в Push() |
| `backend/Common/Extensions/Metrics/BackendMetrics.cs` | Новая метрика |
