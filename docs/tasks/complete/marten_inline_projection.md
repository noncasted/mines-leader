## Marten inline projection + EventState refactoring

### Что сделано
- **Marten inline projections** — event-sourced grains пишут только в `mt_streams`/`mt_events`, inline projection обновляет snapshot в `mt_doc_*` автоматически
- **EventState API cleanup** — удалён `StartSession`, `WriteSession` переименован в `Write`, единый API для transactional и standalone режимов
- **Thread-safety** — `SemaphoreSlim _lock` защищает standalone вызовы `Read`/`Append`/`Write`
- **Transactional Write фиксация** — `Write()` в tx режиме передаёт pending events в `GrainTransactionHandler` и очищает список, предотвращая потерю
- **Full StateStorage support для event state** — `ReadBatch`, `Delete`, `ReadAll` теперь корректно работают с event-sourced типами
- **Исправлены баги** — missing `Write()` в `UserRating`/`UserLoot`/`Match`, partial mutation в `Append`, silent ignore событий без `Apply`

### Ключевые файлы
- `backend/Infrastructure/Orleans/State/Events/EventState.cs` — главный компонент event-sourced state
- `backend/Infrastructure/Orleans/State/Events/EventStorage.cs` — чтение/запись/удаление event streams через Marten
- `backend/Infrastructure/Orleans/State/StateStorage.cs` — unified storage API с routing direct/event-sourced
- `backend/Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs` — transactional coordinator с `_eventRecords`

### Заметки
- `EventState.Write()` в transactional режиме НЕ пишет в БД напрямую — он только передаёт pending events в `GrainTransactionHandler`. Реальная запись происходит в `Transactions.Process()`.
- `StateStorage.Write()` для event state выбрасывает `NotSupportedException` — event-sourced state должен записываться через `EventState.Write()` или `IEventStorage.Append()`.
- `Apply` метод на aggregate обязателен — отсутствие вызовет `InvalidOperationException` в `Append()`.
- `Read()` перед `Append()` обязателен — иначе `InvalidOperationException`.
