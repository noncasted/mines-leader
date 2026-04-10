## Refactor StateStorage: Request Objects + Slim Interface

### Цель
Уменьшить растущий интерфейс IStateStorage, ввести request-объекты для Write/Delete, вынести convenience-методы в extensions.

### Изменения

#### Новые типы

| Тип | Назначение |
|-----|-----------|
| `StateWriteRequest` | Объединяет single/batch/transaction write в один request. Поле `Transaction` — optional (null = self-managed) |
| `StateDeleteRequest` | Объединяет single/batch delete в один request. Поле `Identities` — всегда список |

#### IStateStorage: 8 методов -> 5

| До | После | Что изменилось |
|----|-------|----------------|
| `Read<T>(StateIdentity)` | `Read<T>(StateIdentity)` | Без изменений, параметр переименован `stateIdentity` -> `identity` |
| `ReadRaw(StateIdentity)` | -- | Убран из интерфейса, стал `private` методом в `StateStorage` |
| `Read<TKey, TValue>(IReadOnlyList<StateIdentity>)` | `ReadBatch<TKey, TValue>(IReadOnlyList<StateIdentity>)` | Переименован для ясности |
| `ReadAll<TKey, TValue>(IReadOnlyLifetime)` | `ReadAll<TKey, TValue>(IReadOnlyLifetime)` | Без изменений |
| `Write(StateIdentity, IStateValue)` | -- | Убран из интерфейса -> extension |
| `Write(NpgsqlTransaction, IReadOnlyDictionary<StateIdentity, IStateValue>)` | -- | Убран из интерфейса -> extension |
| -- | `Write(StateWriteRequest)` | Единый метод записи |
| `Delete(StateIdentity)` | -- | Убран из интерфейса -> extension |
| `Delete(IReadOnlyList<StateIdentity>)` | -- | Убран из интерфейса -> extension |
| -- | `Delete(StateDeleteRequest)` | Единый метод удаления |

#### StateStorage implementation

| Метод | Что изменилось |
|-------|----------------|
| `ReadRaw` | `public` -> `private`. Никто снаружи не вызывал |
| `Read<T>` | Без изменений в логике |
| `ReadBatch<TKey,TValue>` | Переименован из `Read<TKey,TValue>`, логика та же |
| `ReadAll<TKey,TValue>` | Без изменений |
| `Write(StateWriteRequest)` | Объединяет оба старых Write. Проверяет `request.Transaction != null`: если есть — использует предоставленную транзакцию, если нет — создает свою. Batch-логика вынесена в `private WriteBatch()` |
| `Delete(StateDeleteRequest)` | Извлекает `request.Identities`, логика batch-удаления без изменений |
| `WriteBatch` (new private) | Вынесена batch write логика (группировка по table/extension, построение SQL, выполнение) |

#### StateStorageExtensions

| Метод | Статус |
|-------|--------|
| `Read<T>(GrainId)` | Сохранен, без изменений |
| `ReadRaw<T>(GrainId)` | Удален (никто не использовал) |
| `Write(StateIdentity, IStateValue)` | Новый extension — создает `StateWriteRequest` с single-entry dict |
| `Write(GrainId, IStateValue)` | Обновлен — создает `StateWriteRequest` |
| `Write(NpgsqlTransaction, IReadOnlyDictionary<StateIdentity, IStateValue>)` | Новый extension — оборачивает в `StateWriteRequest` с transaction |
| `Write(NpgsqlTransaction, IReadOnlyList<GrainStateRecord>)` | Обновлен — конвертирует records -> dict, оборачивает в `StateWriteRequest` |
| `Delete(StateIdentity)` | Новый extension — оборачивает в `StateDeleteRequest` |
| `Delete(IReadOnlyList<StateIdentity>)` | Новый extension — оборачивает в `StateDeleteRequest` |
| `Delete<T>(object key)` | Обновлен — создает `StateDeleteRequest` |
| `Delete<T>(IReadOnlyList<object> keys)` | Обновлен — создает `StateDeleteRequest` |
| `Read<TKey,TValue>(IReadOnlyList<StateIdentity>)` | Новый extension — алиас для `ReadBatch`, обратная совместимость |
| `ToIdentity(GrainId, GrainStateInfo)` | Без изменений |

### Затронутые файлы

| Файл | Изменение |
|------|-----------|
| `backend/Infrastructure/Orleans/State/StateStorage.cs` | Request types, новый интерфейс, рефакторинг implementation |
| `backend/Infrastructure/Orleans/State/StateStorageExtensions.cs` | Поглотил convenience-методы, удален ReadRaw extension |

### Consumer impact: ZERO

Все существующие вызовы сохраняют сигнатуру через extensions. Ни один consumer-файл не был изменен:

| Consumer | Вызов | Почему не сломался |
|----------|-------|--------------------|
| `State.cs` | `Read<T>(GrainId)`, `Write(GrainId, value)` | Extension-методы, были и остались |
| `Transactions.cs` | `Write(transaction, result.States)` | Extension оборачивает в StateWriteRequest |
| `AddressableState.cs` | `Read<T>(identity)`, `Write(identity, value)` | Read — на интерфейсе, Write — extension |
| `StateCollectionUtils.cs` | `ReadAll<TKey,TValue>(lifetime)` | На интерфейсе, без изменений |
| `BenchmarkStorage.cs` | `ReadAll(lifetime)`, `Write(identity, state)` | ReadAll — интерфейс, Write — extension |
| `TestCleanup.cs` | `Delete(identities)` | Extension оборачивает в StateDeleteRequest |
| `BotManagement.razor` | `Read<Guid, UserState>(identities)` | Extension-алиас для ReadBatch |
| `PlayersWidget.razor` | `Read<Guid, UserProgressionState>(identities)` | Extension-алиас для ReadBatch |

### Верификация

- `dotnet build backend/Infrastructure/Infrastructure.csproj` — Build succeeded
- `dotnet build backend/Meta/Meta.csproj` — Build succeeded
- `dotnet build backend/Tools/Benchmarks/Benchmarks.csproj` — Build succeeded
- `dotnet build backend/Console/Console.csproj` — Build succeeded
