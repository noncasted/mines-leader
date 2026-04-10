## DurableQueue No Subscribers — Результат

### Статус: Завершено

### Что сделано
1. Добавлен `NoSubscribersException` класс
2. `Push()` бросает exception при 0 observers в начале метода
3. `Push()` бросает exception если все observers упали при delivery (проверка `_observers.Count == 0` после removal)
4. Новая метрика `DurableQueueNoSubscribers`

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Infrastructure/Messaging/Queues/DurableQueue.cs` | NoSubscribersException, двойная проверка в Push() |
| `backend/Common/Extensions/Metrics/BackendMetrics.cs` | DurableQueueNoSubscribers counter |

### Заметки
Исправлен logic bug: первоначальная проверка "all failed" использовала `toRemove.Count == _observers.Count + toRemove.Count` — некорректно после removal. Заменено на `_observers.Count == 0`.
