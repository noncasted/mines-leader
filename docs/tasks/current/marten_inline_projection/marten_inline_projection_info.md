## Задача: Адаптация Marten inline projections для event-sourced states

### Цель

1. Marten должен хранить **только последовательность событий** в `mt_streams` / `mt_events`.
2. **Inline projections** автоматически поддерживают snapshot-документы в таблицах `mt_doc_{Type}` (например `mt_doc_userstate`).
3. State-таблицы (`state_user_entity` и т.д.) используются **только для direct-state** grains (`IDirectStateValue`), не для event-sourced.
4. При записи событий Marten в той же транзакции пересобирает агрегат через `Apply()` и обновляет snapshot.
5. `StateCollectionUtils.Load()` и консоль читают snapshot из Marten (`session.Query<T>()`), а не из state-таблиц.
6. Stream key не содержит grain type (убрать `user/` префикс из `GrainId.ToString()`).
7. `UserState.Id` остаётся `string` и содержит полный stream key.
8. `StatesSetup` и `StatesCleanup` корректно работают с разделением direct / event-sourced.
9. Тесты проходят.

### Контекст

В проекте `/atlantis/server/backend/` уже реализован этот подход:
- `opts.Events.StreamIdentity = StreamIdentity.AsString`
- `opts.Events.AppendMode = EventAppendMode.Quick`
- `opts.Projections.Snapshot<TDocument>(SnapshotLifecycle.Inline)`

Там `session.Events.Append()` пишет события, а inline projection в той же транзакции обновляет `mt_doc_*`. `session.Load<T>()` читает snapshot без replay.

В текущем проекте `EventState<T>.WriteSession()` пишет только в Marten streams, snapshot в state-таблицы не попадает. `StateCollectionUtils.Load()` читает state-таблицы — они пустые для event-sourced grains. Нужно переключить чтение коллекций на Marten snapshot-таблицы.

### Шаги реализации

**1. Исправить формат stream key**
  1.1. `EventState.BuildStreamId()` — использовать `_context.GrainId.Key` (сырой `IdSpan`) вместо `_context.GrainId.ToString()` — `backend/Infrastructure/Orleans/State/Events/EventState.cs`
  1.2. Убедиться что `EventStorage.GetStreamIdsAsync()` `LIKE` фильтр работает с новым форматом — `backend/Infrastructure/Orleans/State/Events/EventStorage.cs`
  1.3. Убрать `FixAggregateIdentity` из `EventStorage` — Marten сам управляет `Id` — `backend/Infrastructure/Orleans/State/Events/EventStorage.cs`

**2. Настроить Marten inline projections**
  2.1. Найти где конфигурируется `IDocumentStore` — `backend/Infrastructure/Orleans/State/Events/MartenSetupExtensions.cs` или аналогичный файл
  2.2. Добавить `opts.Events.StreamIdentity = StreamIdentity.AsString`
  2.3. Добавить `opts.Events.AppendMode = EventAppendMode.Quick`
  2.4. Зарегистрировать `opts.Projections.Snapshot<T>(SnapshotLifecycle.Inline)` для всех `IEventStateValue` типов (`UserState`, `MatchAggregate`, `UserAuthAggregate`, `UserCardsState`, `UserDeckState`, `UserLootState`, `UserMatchHistoryAggregate`, `UserProgressionState`, `UserRatingState`)

**3. Очистить EventState от dual-write логики**
  3.1. Убрать `IStateStorage` зависимость из `EventState<T>` — `backend/Infrastructure/Orleans/State/Events/EventState.cs`
  3.2. Убрать `_stateStorage.Write()` из `EventState.WriteSession()` — `backend/Infrastructure/Orleans/State/Events/EventState.cs`
  3.3. Убрать `IGrainStateTransactionParticipant` — оставить только `IGrainEventTransactionParticipant` — `backend/Infrastructure/Orleans/State/Events/EventState.cs`
  3.4. Убрать `IStateStorage` из `EventStateFactory` — `backend/Infrastructure/Orleans/State/Events/EventStateAttributeMapper.cs`
  3.5. Убрать `handler.RecordStateChanged(this)` из `EventState.Read()` — `backend/Infrastructure/Orleans/State/Events/EventState.cs`
  3.6. Обновить `GrainTransactionHandler.CollectResult()` — не собирать `GetAggregate()` в `States` от event participants, оставить только события — `backend/Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs`

**4. Переписать EventStorage на Load/Query**
  4.1. `EventStorage.Read<T>()` — использовать `session.LoadAsync<T>(streamId)` вместо `AggregateStreamAsync<T>()` — `backend/Infrastructure/Orleans/State/Events/EventStorage.cs`
  4.2. `EventStorage.ReadAll<T>()` — использовать `session.Query<T>().Where(x => x.Id.StartsWith(prefix))` вместо raw SQL к `mt_streams` — `backend/Infrastructure/Orleans/State/Events/EventStorage.cs`

**5. Переключить StateStorage.ReadAll на EventStorage для event-sourced**
  5.1. `StateStorage.ReadAll()` для `IEventStateValue` — делегировать в `EventStorage.ReadAll()` — `backend/Infrastructure/Orleans/State/StateStorage.cs`
  5.2. `StateStorage.Write()` — не писать `IEventStateValue` records в `DirectStorage`, оставить только `IDirectStateValue` — `backend/Infrastructure/Orleans/State/StateStorage.cs`

**6. Разделить StatesSetup и StatesCleanup на direct / event-sourced**
  6.1. `StatesSetup` — создавать таблицы только для `IDirectStateValue`, пропускать `IEventStateValue` — `backend/Tools/DeploySetup/StatesSetup.cs`
  6.2. `StatesCleanup` — добавить truncate для `mt_doc_*` таблиц всех `IEventStateValue` типов — `backend/Tools/DeploySetup/StatesCleanup.cs`

**7. Адаптировать UI и тесты**
  7.1. `PlayersWidget.razor` — `CopyId` принимает `string`, `Guid.Parse` убрать — `backend/Console/Home/PlayersWidget.razor`
  7.2. `UserDashboard.razor` — адаптировать `Id` как `string` — `backend/Console/Game/User/UserDashboard.razor`
  7.3. `UserGrainTests.cs` — `state!.Id` assertions под новый формат — `backend/Tools/Tests/Meta/UserGrainTests.cs`
  7.4. `BotTests.cs` — `userState!.Id` assertions под новый формат — `backend/Tools/Tests/Meta/BotTests.cs`

**8. Тесты и верификация**
  8.1. Запустить `UserGrainTests` — проверить что `GetState()` возвращает корректный snapshot
  8.2. Запустить `StateCollectionTests` — проверить что `StateCollectionUtils.Load()` читает из Marten
  8.3. Запустить `EventStateTests` — проверить что events всё ещё пишутся
  8.4. Собрать полное решение `dotnet build backend.slnx`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Infrastructure/Orleans/State/Events/EventState.cs` | Главный event-sourced state компонент. Убрать dual-write, упростить до чистого event participant |
| `backend/Infrastructure/Orleans/State/Events/EventStorage.cs` | Переключить с `AggregateStreamAsync` на `LoadAsync` / `Query` |
| `backend/Infrastructure/Orleans/State/Events/EventStateAttributeMapper.cs` | Убрать `IStateStorage` из фабрики |
| `backend/Infrastructure/Orleans/State/StateStorage.cs` | Разделить routing: direct → DirectStorage, event-sourced → EventStorage |
| `backend/Infrastructure/Orleans/Transactions/GrainTransactionHandler.cs` | Не собирать aggregate snapshot из event participant в States |
| `backend/Tools/DeploySetup/StatesSetup.cs` | Создавать таблицы только для direct states |
| `backend/Tools/DeploySetup/StatesCleanup.cs` | Добавить очистку `mt_doc_*` таблиц |
| `backend/Console/Home/PlayersWidget.razor` | UI адаптация под `string Id` |
| `backend/Console/Game/User/UserDashboard.razor` | UI адаптация под `string Id` |
| `backend/Tools/Tests/Meta/UserGrainTests.cs` | Assertions под новый формат |
| `backend/Tools/Tests/Meta/BotTests.cs` | Assertions под новый формат |

### Документация к прочтению

- `docs/db/docs/COMMON_ORLEANS.md` — Grain, State, `[Transaction]`, Orleans backend patterns
- `/atlantis/server/backend/Orchestration/Extensions/MartenExtensions.cs` — референсная реализация inline projections

### Риски

1. **Stream key mismatch.** Если `BuildStreamId()` и `session.Load<T>()` используют разные ключи — snapshot не найдётся. Нужно единообразие.
2. **Marten schema auto-create.** Inline projection таблицы `mt_doc_*` создаются Marten автоматически при первой записи. `StatesSetup` не должен пытаться создавать их.
3. **Console UI зависимости.** `PlayersWidget` и `UserDashboard` используют `user.Id` как `Guid` в нескольких местах (навигация, grain calls). Все вызовы `Orleans.GetGrain<IUserProgression>(user.Id)` требуют `Guid` — нужно парсить из stream key.
4. **Транзакции.** Сейчас `Transactions.Process()` пишет `States` + `Events` в одной DB транзакции. После миграции event-sourced snapshot пишется Marten (в событиях), а direct states — через `DirectStorage`. Нужно убедиться что atomicity сохраняется.
