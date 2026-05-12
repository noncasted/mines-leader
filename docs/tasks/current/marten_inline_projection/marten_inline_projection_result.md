## Marten inline projection — Результат

### Статус: Завершено

### Что сделано
1. **Исправлен формат stream key** — `BuildStreamId()` парсит `GrainId.ToString()` и форматирует Guid в стандартный "D" формат
2. **Настроены Marten inline projections** — добавлен `EventAppendMode.Quick`, зарегистрированы `SnapshotLifecycle.Inline` для всех `IEventStateValue` типов через рефлексию
3. **Удалён dual-write** — `EventState` больше не пишет в `DirectStorage`, `CollectResult()` не собирает aggregates из event participants
4. **Переключено чтение на snapshot** — `EventStorage.Read()` через `LoadAsync` + fallback, `ReadAll()` через `Query`
5. **Разделено чтение коллекций** — `StateStorage.ReadAll()` для event-sourced типов делегирует в `EventStorage.ReadAll()`
6. **Разделены StatesSetup/StatesCleanup** — `StatesSetup` пропускает `IEventStateValue` типы (Marten сам создаёт таблицы), `StatesCleanup` очищает `mt_doc_*` для event-sourced типов
7. **UI адаптирован** — `PlayersWidget` и `UserDashboard` используют `ExtractGuid()` для парсинга stream ID
8. **Тесты адаптированы** — assertions под формат `user_entity:guid`, `Initialize_IsIdempotent`
9. **Исправлен баг** — `EventState.Append()` при `_currentTransactionId == Guid.Empty` бросал "Concurrent transactions"
10. **Тестовый fixture** — `JsonNetSerializer` для корректной десериализации полиморфных типов в inline projections

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `backend/Infrastructure/Orleans/State/Events/EventState.cs` | Убран `IStateStorage`, упрощён `WriteSession()`, исправлен `BuildStreamId()`, исправлен `Append()` |
| `backend/Infrastructure/Orleans/State/Events/EventStateAttributeMapper.cs` | Убран `IStateStorage` из фабрики |
| `backend/Infrastructure/Orleans/State/Events/EventStorage.cs` | `Read()` → `LoadAsync` + fallback, `ReadAll()` через `Query` |
| `backend/Infrastructure/Orleans/State/Events/MartenSetupExtensions.cs` | `EventAppendMode.Quick`, inline projections через рефлексию |
| `backend/Infrastructure/Orleans/State/StateStorage.cs` | Разделение: direct → `DirectStorage`, event-sourced → `EventStorage.ReadAll()` |
| `backend/Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs` | `CollectResult()` не собирает aggregates из event participants |
| `backend/Infrastructure/Orleans/State/StateInterfaces.cs` | `IEventStateValue` требует `string Id { get; }` |
| `backend/Console/Home/PlayersWidget.razor` | `ExtractGuid()` для парсинга stream ID |
| `backend/Console/Game/User/UserDashboard.razor` | `ExtractGuid()` для парсинга stream ID |
| `backend/Tools/Tests/Meta/UserGrainTests.cs` | Assertions под `user_entity:guid` |
| `backend/Tools/Tests/Meta/BotTests.cs` | Assertions под `user_entity:guid` |
| `backend/Tools/Tests/Meta/MetaGrainTests.cs` | `Initialize_OverwritesPreviousState` → `Initialize_IsIdempotent` |
| `backend/Tools/Tests/Fixtures/OrleansTestClusterFixture.cs` | `JsonNetSerializer` и inline projections для тестового store |
| `backend/Tools/Generators/StatesLookupGenerator.cs` | Добавлено поле `TypeName` в `StatesLookup.Info` |
| `backend/Tools/DeploySetup/StatesSetup.cs` | Пропускает `IEventStateValue` типы при создании таблиц |
| `backend/Tools/DeploySetup/StatesCleanup.cs` | Очищает `mt_doc_*` для event-sourced типов |
| `backend/Tools/DeploySetup/DeploySetup.csproj` | Добавлен `ProjectReference` на `Infrastructure` |

### Отличия от плана
- `EventStorage.Read()` — добавлен fallback на `AggregateStreamAsync` для надёжности
- Исправлен баг `EventState.Append()` при `_currentTransactionId == Guid.Empty`, не предусмотренный планом
- Тестовый fixture потребовал `JsonNetSerializer` для полиморфных типов в inline projections
- `StatesLookupGenerator` — добавлено поле `TypeName` для runtime type resolution в `StatesSetup`/`StatesCleanup`

### Нерешенные вопросы
- Нет
