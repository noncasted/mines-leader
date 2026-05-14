## Задача: Покрытие тестами EventState, EventStorage, StateStorage

### Цель
Покрыть тестами все пробелы, выявленные при code review EventState/EventStorage/StateStorage.

### Контекст
После рефакторинга EventState (удаление StartSession, WriteSession→Write, thread-safety, Apply exception, Write snapshot в handler) и добавления новых методов в EventStorage (Delete, ReadBatch, ReadAll) и StateStorage (ReadBatch/Delete для event state) — у многих сценариев нет тестов.

### Пробелы в покрытии

**EventState<T>:**
1. Append без Read() — должен кидать InvalidOperationException
2. Append с событием без Apply() — должен кидать InvalidOperationException
3. Write() без pending events — должен быть no-op (не падать)
4. Multiple Write() в одной транзакции — каждый Write должен корректно добавлять events в handler
5. Standalone concurrent calls — SemaphoreSlim защищает от race condition

**EventStorage:**
6. Delete(string streamId) — удаляет/архивирует stream
7. ReadBatch<TKey, TValue>(streamIds) — batch read aggregates
8. ReadAll<TKey, TValue>(prefix, keyType) — чтение по prefix с парсингом ключа

**StateStorage + event state:**
9. ReadBatch для IEventStateValue — ранее был NotSupportedException
10. Delete для IEventStateValue — должен роутить в EventStorage.Delete
11. ReadAll для IEventStateValue — должен делегировать в EventStorage.ReadAll
12. Write для IEventStateValue — должен кидать NotSupportedException

### Шаги реализации

**1. Расширить TestGrains**
- Добавить BadEvent / NoApplyEvent события
- Добавить в IEventTestGrain: AppendWithoutRead, AppendNoApplyEvent, WriteWithoutPending, AppendTwiceInTransaction
- Реализовать методы в EventTestGrain

**2. Тесты EventState (EventStateTests.cs)**
- AppendWithoutRead_Throws
- Append_MissingApplyMethod_Throws
- Write_NoPendingEvents_NoOp
- Transaction_MultipleWrites_AllEventsCommitted
- Standalone_ConcurrentAppends_NoRaceCondition

**3. Тесты EventStorage (EventStateTests.cs или отдельный класс)**
- Delete_RemovesStream
- ReadBatch_MultipleStreams_ReturnsAll
- ReadBatch_PartialMiss_ReturnsExisting
- ReadAll_ReturnsAggregatesByPrefix
- ReadAll_EmptyPrefix_YieldsNothing

**4. Тесты StateStorage + event state (StateStorageBatchTests.cs или отдельный класс)**
- ReadBatch_EventState_ReturnsAggregates
- Delete_EventState_RemovesStream
- ReadAll_EventState_ReturnsAggregates
- Write_EventState_ThrowsNotSupported

**5. Сборка и запуск**
- dotnet build
- dotnet test --filter FullyQualifiedName~EventStateTests
- dotnet test --filter FullyQualifiedName~EventStorageTests
- dotnet test --filter FullyQualifiedName~StateStorageEventTests
- Полный dotnet test

### Ключевые файлы
| Файл | Роль |
|------|------|
| `backend/Tools/Tests/Grains/TestGrains.cs` | Добавить новые тестовые grain-методы |
| `backend/Tools/Tests/State/EventStateTests.cs` | Основные тесты EventState |
| `backend/Tools/Tests/State/EventStorageTests.cs` | Тесты EventStorage (новый файл) |
| `backend/Tools/Tests/State/StateStorageEventTests.cs` | Тесты StateStorage + event state (новый файл) |

### Риски
- Marten schema — для новых тестов нужно, чтобы mt_doc_* таблицы создавались корректно
- Orleans test fixture — grain активация и deactivation между тестами
- Marten async enumeration — ReadAll возвращает IAsyncEnumerable, нужен корректный lifetime
