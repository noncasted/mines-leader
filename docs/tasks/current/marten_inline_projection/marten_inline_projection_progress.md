## Marten inline projection — Рабочие заметки

### Статус: Завершено

### Заметки

**[18:30] Исправление формата stream key**
- `BuildStreamId()` теперь парсит `GrainId.ToString()` и извлекает ключ после `/`, форматируя Guid в стандартный "D" формат
- Раньше использовался `_context.GrainId.Key` (сырой `IdSpan` в формате "N" без тире)

**[18:45] Настройка Marten inline projections**
- Добавлен `EventAppendMode.Quick`
- Inline projections регистрируются через рефлексию для всех типов, реализующих `IEventStateValue`
- Используется `SnapshotLifecycle.Inline` через вызов `options.Projections.Snapshot<T>(SnapshotLifecycle.Inline)`

**[19:00] Удаление dual-write**
- `EventState<T>` больше не зависит от `IStateStorage`
- `WriteSession()` больше не пишет в `DirectStorage`
- `GrainTransactionHandler.CollectResult()` больше не собирает aggregate snapshot из event participants в `States`
- `StateStorage.Write()` пропускает `IEventStateValue` записи

**[19:15] Переключение EventStorage на Load/Query**
- `EventStorage.Read<T>()` теперь использует `session.LoadAsync<T>(streamId)` с fallback на `AggregateStreamAsync<T>()`
- `ReadAll` для event-sourced типов теперь читает из `mt_doc_*` таблиц через `session.Query<T>()`

**[19:30] UI адаптация**
- `PlayersWidget.razor` — добавлен `ExtractGuid()` для парсинга Guid из stream ID
- `UserDashboard.razor` — аналогично, `ExtractGuid()` для навигации и grain calls

**[19:45] Тестовая адаптация**
- `UserGrainTests` — assertions под новый формат `Id` (`"user_entity:guid"`)
- `BotTests` — аналогично
- `UserDeckTests.Initialize_OverwritesPreviousState` → переименован в `Initialize_IsIdempotent` с новой семантикой (event-sourced `Initialize()` идемпотентен)

**[20:00] Баг "Concurrent transactions"**
- Обнаружен баг в `EventState.Append()`: при `_currentTransactionId == Guid.Empty` и вызове внутри транзакции бросалось исключение
- Исправлено: добавлена проверка `_currentTransactionId != Guid.Empty && ...`

**[20:15] Проблема сериализации в тестах**
- Inline projections используют System.Text.Json по умолчанию в тестах (тестовый fixture создавал `DocumentStore` без `JsonNetSerializer`)
- Добавлена конфигурация `JsonNetSerializer` в `OrleansTestClusterFixture` с теми же настройками, что и в `MartenSetupExtensions`
- Это исправило десериализацию полиморфных типов (`IUserProgressionRecord`, `IUserRatingRecord`)

**[20:30] Разделение StatesSetup/StatesCleanup**
- `StatesLookupGenerator` — добавлено поле `TypeName` в `StatesLookup.Info` для runtime type resolution
- `StatesSetup` — теперь пропускает `IEventStateValue` типы (Marten сам создаёт `mt_doc_*`)
- `StatesCleanup` — теперь truncates `mt_doc_{typeNameLower}` для event-sourced типов
- `DeploySetup.csproj` — добавлен `ProjectReference` на `Infrastructure` для доступа к `IEventStateValue`

**[20:45] Все тесты проходят**
- 598 тестов, 0 failures
- Полное решение собирается без ошибок
