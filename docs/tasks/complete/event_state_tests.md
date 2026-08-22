## Покрытие тестами EventState

### Что сделано
- 15 тестов на пробелы EventState / EventStorage / StateStorage (Append без Read, missing Apply, concurrent appends, Delete/ReadBatch/ReadAll).
- `EventState.Append()` стал `async Task` с `WaitAsync()` — sync `Wait()` дедлочил thread pool на `[Reentrant]` grain.

### Ключевые файлы
- `backend/Tools/Tests/State/EventStateTests.cs`
- `backend/Tools/Tests/State/EventStorageTests.cs`
- `backend/Tools/Tests/State/StateStorageEventTests.cs`
- `backend/Infrastructure/Orleans/State/Events/EventState.cs`

### Заметки
- `ArchiveStream` удаляет events, snapshot в `mt_doc_*` остаётся — Delete-тесты проверяют `FetchStreamAsync`, не `Read()`.
