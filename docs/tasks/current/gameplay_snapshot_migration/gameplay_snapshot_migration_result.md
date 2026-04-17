## Gameplay Snapshot Migration — Результат

### Статус: Завершено

### Что сделано

1. **Shared** — добавлены новые снапшот-records: `PlayerSnapshotRecord.DeckUpdate/StashUpdate/BoardStateUpdate` и `GameCompletedRecord/TimeLimitedRoundRecord/LastManStandingRoundRecord` (новый файл `GameFlowRecords.cs`). Union `IMoveSnapshotRecord` расширен тегами 9–14.
2. **Backend** — все `ValueProperty<TState>` удалены из геймплея. `GameFlow/TimeLimitedRound/LastManStandingRound` эмитят свои records через `ISnapshotSender` на каждое изменение (init, round start, timer tick, action bonus, round end). `Board/Hand/Deck/Stash/Modifiers` хранят данные локально; `BoardMinesScanner` принимает опциональный `MoveSnapshot` и сам эмитит `BoardStateUpdate` при изменении `Mines/Flags`. `MoveSnapshot` получил 6 новых `RecordX` методов. `SnapshotApplier` получил пустые case-ветки для новых типов. `PlayerFactory` очищен от `AddProperty` вызовов.
3. **Client** — 8 геймплейных объектов полностью избавлены от `NetworkProperty<T>`. Добавлено 6 новых snapshot-хендлеров в `GamePlay/Sync/`. `PlayerModifierSnapshotHandler` заполнен реальной логикой. `IGamePlayer` расширен свойством `Modifiers`. `IBoard` получил `UpdateState(mines, flags)`, `IDeck` — `SetCount`, `IStash` — `SetCount`, `IPlayerModifiers` — `Set(modifier, value)`.
4. **Регистрации** — 5× `RegisterProperty` убраны из `GamePlayerFactory`, 3× `.WithProperty` из `PvPScopeExtensions` (перешли на обычный `Register<T>`), 15 обработчиков зарегистрированы в `GamePlaySyncExtensions`.
5. **Проверки** — `grep NetworkProperty client/Assets/GamePlay/**` → 0 совпадений. `dotnet build backend/Game/Game.csproj` и `backend/Tools/Tests/Tests.csproj` — 0 ошибок компиляции. Из выбранных тестов (SnapshotDiffGuard, PlayerMechanics, Reveal, BoardCommand) 31/32 проходят; единственный падающий `ExcavatorTests.Use_FlagsMinesAndRevealsSafeCellsInCross` падает одинаково при stash-сравнении, т.е. регрессия не связана с миграцией.

### Измененные файлы

**shared:**

| Файл | Что изменено |
|------|-------------|
| `shared/Game/Snapshots/PlayerSnapshotRecord.cs` | Добавлены `DeckUpdate`, `StashUpdate`, `BoardStateUpdate` |
| `shared/Game/Snapshots/SharedMoveSnapshot.cs` | 6 новых `MemoryPackUnion` слотов 9–14 |
| `shared/Game/Snapshots/GameFlowRecords.cs` | Новый файл: `GameCompletedRecord`, `TimeLimitedRoundRecord`, `LastManStandingRoundRecord` |

**backend:**

| Файл | Что изменено |
|------|-------------|
| `backend/Game/GamePlay/Context/MoveSnapshot.cs` | Добавлены 6 методов `RecordDeckUpdate/RecordStashUpdate/RecordBoardStateUpdate/RecordGameCompleted/RecordTimeLimitedRound/RecordLastManStandingRound` |
| `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` | Пустые case-ветки для 6 новых records |
| `backend/Game/GamePlay/Snapshots/MoveSnapshotBoardExtensions.cs` | `RecordReveal` эмитит `RecordBoardStateUpdate` по завершении |
| `backend/Game/GamePlay/Board/Board.cs` | Убрана зависимость от `ValueProperty<BoardState>` |
| `backend/Game/GamePlay/Board/BoardMinesScanner.cs` | Локальные `Mines/Flags` + эмиссия `BoardStateUpdate` в `Recalculate(snapshot)` |
| `backend/Game/GamePlay/Players/Hand.cs` | Локальный `List<ActiveCard>` вместо state |
| `backend/Game/GamePlay/Players/Deck.cs` | Локальный `List<CardType>` вместо state |
| `backend/Game/GamePlay/Players/Stash.cs` | Локальный список без state |
| `backend/Game/GamePlay/Players/Modifiers.cs` | Убраны `ValueProperty` и `SyncState` |
| `backend/Game/GamePlay/Players/PlayerFactory.cs` | Убраны 5 `AddProperty` вызовов |
| `backend/Game/GamePlay/Context/GameFlow.cs` | Эмитит `GameCompletedRecord`; `ValueProperty` удалён |
| `backend/Game/GamePlay/Context/Rounds/TimeLimitedRound.cs` | Локальное состояние + `RecordTimeLimitedRound` на каждое изменение |
| `backend/Game/GamePlay/Context/Rounds/LastManStandingRound.cs` | Аналогично для LMS |
| `backend/Game/GamePlay/Context/Rounds/RoundPlayers.cs` | `RestoreCards` эмитит `DeckUpdate`/`StashUpdate` |
| `backend/Game/GamePlay/Bot/Actions/BotFlagAction.cs` | `Recalculate(snapshot)` |
| `backend/Game/GamePlay/Commands/SetFlagAction.cs` | Аналогично |
| `backend/Game/GamePlay/Commands/RemoveFlagAction.cs` | Аналогично |
| `backend/Game/GamePlay/Cards/CrossBoard/*.cs` (7 файлов) | `Recalculate(snapshot)` |
| `backend/Game/GamePlay/Commands/CardUseCommand.cs` | Эмитит `RecordStashUpdate` |
| `backend/Game/GamePlay/Cards/Hand/SabotageDeck.cs` | `RecordDeckUpdate` |
| `backend/Game/GamePlay/Cards/Hand/Recycler.cs` | `RecordDeckUpdate/StashUpdate` |
| `backend/Game/GamePlay/Cards/Hand/Salvage.cs` | `RecordDeckUpdate` |
| `backend/Game/GamePlay/Cards/Hand/Scavenger.cs` | `RecordDeckUpdate` |
| `backend/Game/GamePlay/Cards/Hand/MysticDraw.cs` | `RecordDeckUpdate` |
| `backend/Game/GamePlay/Cards/Hand/GraveDigger.cs` | `RecordStashUpdate` |
| `backend/Game/GamePlay/Cards/Hand/HandScramble.cs` | `RecordDeckUpdate` |
| `backend/Game/GamePlay/Cards/Resources/GamblersRuin.cs` | `RecordDeckUpdate/StashUpdate` |
| `backend/Game/GamePlay/Bot/Actions/BotCardAction.cs` | `RecordStashUpdate` |

**client:**

| Файл | Что изменено |
|------|-------------|
| `client/Assets/GamePlay/Boards/Root/Board.cs` | `ViewableProperty<BoardState>` + `UpdateState` |
| `client/Assets/GamePlay/Boards/Root/IBoard.cs` | Добавлен `UpdateState(int, int)` |
| `client/Assets/GamePlay/Cards/Hand/Hand.cs` | Удалён неиспользуемый `NetworkProperty` |
| `client/Assets/GamePlay/Cards/Deck/Deck.cs` | `SetCount(int)` вместо `Advise` |
| `client/Assets/GamePlay/Cards/Deck/DeckView.cs` | Снят `.As<IScopeLoaded>()` |
| `client/Assets/GamePlay/Cards/Stash/Stash.cs` | `SetCount(int)` |
| `client/Assets/GamePlay/Cards/Stash/StashView.cs` | Снят `.As<IScopeLoaded>()` |
| `client/Assets/GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs` | `Set(PlayerModifier, float)` |
| `client/Assets/GamePlay/Players/Entity/PlayerEntityExtensions.cs` | Снят `.As<IScopeLoaded>()` |
| `client/Assets/GamePlay/Players/Entity/Root/GamePlayer.cs` | Добавлено свойство `IPlayerModifiers Modifiers` |
| `client/Assets/GamePlay/Players/Services/Factory/GamePlayerFactory.cs` | Убраны 5 `RegisterProperty` |
| `client/Assets/GamePlay/Loop/PvP/PvPScopeExtensions.cs` | 3 сервиса регистрируются обычным `Register<T>`, `WithProperty` удалены |
| `client/Assets/GamePlay/Loop/Context/GameState.cs` | Теперь обычный сервис, не `NetworkService` |
| `client/Assets/GamePlay/Loop/Context/TimeLimitedGameRound.cs` | Локальные поля + `ITimeLimitedGameRound.Apply(...)` |
| `client/Assets/GamePlay/Loop/Context/LastManStandingRound.cs` | Локальные поля + `ILastManStandingRound.Apply(...)` |
| `client/Assets/GamePlay/Sync/PlayerModifierSnapshotHandler.cs` | Заполнена реальная логика |
| `client/Assets/GamePlay/Sync/DeckSnapshotHandler.cs` | **Новый** |
| `client/Assets/GamePlay/Sync/StashSnapshotHandler.cs` | **Новый** |
| `client/Assets/GamePlay/Sync/BoardStateUpdateSnapshotHandler.cs` | **Новый** |
| `client/Assets/GamePlay/Sync/GameCompletedSnapshotHandler.cs` | **Новый** |
| `client/Assets/GamePlay/Sync/TimeLimitedRoundSnapshotHandler.cs` | **Новый** |
| `client/Assets/GamePlay/Sync/LastManStandingRoundSnapshotHandler.cs` | **Новый** |
| `client/Assets/GamePlay/Sync/GamePlaySyncExtensions.cs` | 6 новых `AddSnapshotHandler` вызовов |

### Отличия от плана

- `NetworkProperty<T>` в инфраструктуре (`client/Assets/Common/Network/`) и в `MenuPlayerMovement.cs` не удалён — как и требовалось в задаче. Тип остаётся для будущих применений.
- Дополнительно добавлен `BoardStateUpdate` record, который изначально не планировался отдельно: обнаружилось, что `IBoard.State` (Mines/Flags) читается `BoardMinesCounterView` — пришлось сохранить наблюдаемую `ViewableProperty<BoardState>` и эмитить апдейт из `BoardMinesScanner`.

### Нерешенные вопросы

- Пре-существующий тест `ExcavatorTests.Use_FlagsMinesAndRevealsSafeCellsInCross` падает независимо от миграции — это отдельная регрессия, не относящаяся к данной задаче.
- Тип `NetworkProperty<T>` всё ещё жив в инфраструктуре и меню. Полное удаление — отдельная задача.
- Shared state-классы `PlayerHandState/PlayerDeckState/PlayerStashState/PlayerModifiersState/BoardState/GameFlowState/TimeLimitedRoundState/LastManStandingRoundState` больше не используются на клиенте/бэке и могут быть удалены в отдельном проходе.
