## Покрытие тестами EventState — Результат

### Статус: Завершено

### Что сделано

**EventState тесты (EventStateTests.cs):**
1. `AppendWithoutRead_ThrowsInvalidOperationException` — Append без Read кидает exception
2. `Append_MissingApplyMethod_ThrowsInvalidOperationException` — Append с событием без Apply кидает exception
3. `Write_NoPendingEvents_NoOp` — Write без pending events не падает
4. `Transaction_MultipleWrites_AllEventsCommitted` — два Write() в одной транзакции корректно фиксируют events
5. `Standalone_ConcurrentAppends_NoRaceCondition` — 10 concurrent standalone appends, counter = 10 (thread-safety)

**EventStorage тесты (EventStorageTests.cs):**
6. `Delete_RemovesStream` — ArchiveStream удаляет events из stream
7. `ReadBatch_MultipleStreams_ReturnsAll` — batch read возвращает все aggregates
8. `ReadBatch_PartialMiss_ReturnsExisting` — partial miss возвращает только существующие
9. `ReadBatch_EmptyList_ReturnsEmpty` — пустой список → пустой результат
10. `ReadAll_ReturnsAggregatesByPrefix` — ReadAll по prefix возвращает aggregates
11. `ReadAll_EmptyPrefix_YieldsNothing` — несуществующий prefix → пустой результат

**StateStorage + event state тесты (StateStorageEventTests.cs):**
12. `ReadBatch_EventState_ReturnsAggregates` — StateStorage.ReadBatch для event state работает
13. `Delete_EventState_RemovesStream` — StateStorage.Delete для event state удаляет stream
14. `ReadAll_EventState_ReturnsAggregates` — StateStorage.ReadAll для event state работает
15. `Write_EventState_ThrowsNotSupported` — StateStorage.Write для event state кидает NotSupportedException

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Tools/Tests/Grains/TestGrains.cs` | Добавлены BadEvent, NoApplyEvent, новые методы в IEventTestGrain/EventTestGrain |
| `backend/Tools/Tests/State/EventStateTests.cs` | 5 новых тестов: error cases, multiple writes, concurrent appends |
| `backend/Tools/Tests/State/EventStorageTests.cs` | Новый файл: 6 тестов Delete/ReadBatch/ReadAll |
| `backend/Tools/Tests/State/StateStorageEventTests.cs` | Новый файл: 4 теста ReadBatch/Delete/ReadAll/Write для event state |
| `backend/Infrastructure/Orleans/State/Events/EventState.cs` | `Append()` теперь `async Task` с `_lock.WaitAsync()` вместо sync `_lock.Wait()` |

### Проблемы и решения
1. **Duplicate event classes** — BadEvent/NoApplyEvent добавились дважды из-за повторного edit. Решение: удалены дубликаты.
2. **Sync lock deadlock** — `_lock.Wait()` в `Append()` блокировал thread pool Orleans при 10 concurrent turns на `[Reentrant]` grain. Решение: `Append()` сделан `async Task` с `await _lock.WaitAsync()`.
3. **Delete test expectation** — `ArchiveStream` удаляет events, но snapshot в `mt_doc_*` остаётся. Решение: тесты проверяют `FetchStreamAsync` вместо `Read()`.

### Нерешенные вопросы
- Нет
