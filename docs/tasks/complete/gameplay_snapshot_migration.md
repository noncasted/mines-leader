## Gameplay Snapshot Migration

### Что сделано
- Все `NetworkProperty<T>` из геймплейных объектов клиента удалены (8 объектов: Board, Hand, Deck, Stash, PlayerModifiers, GameState, TimeLimitedRound, LastManStandingRound). Синхронизация полностью через snapshot records → `ISnapshotHandler<T>`.
- Shared: новые records `PlayerSnapshotRecord.DeckUpdate/StashUpdate/BoardStateUpdate` и `GameCompletedRecord/TimeLimitedRoundRecord/LastManStandingRoundRecord`. Union `IMoveSnapshotRecord` расширен слотами 9–14.
- Backend: `ValueProperty<TState>` убраны из `GameFlow`, `TimeLimitedRound`, `LastManStandingRound`, `Board`, `Hand`, `Deck`, `Stash`, `Modifiers`. Состояние хранится локально, каждое изменение эмитится через `ISnapshotSender` и `MoveSnapshot.RecordX`. `BoardMinesScanner.Recalculate` принимает опциональный `MoveSnapshot` и сам эмитит `BoardStateUpdate`.
- Client: 6 новых snapshot-хендлеров в `GamePlay/Sync/`, `PlayerModifierSnapshotHandler` заполнен реальной логикой. `IBoard.UpdateState`, `IDeck.SetCount`, `IStash.SetCount`, `IPlayerModifiers.Set` — публичные методы для хендлеров.
- Регистрации: 5× `RegisterProperty` убраны из `GamePlayerFactory`, 3× `.WithProperty` из `PvPScopeExtensions` (перешли на обычный `Register<T>`).

### Ключевые файлы
- `shared/Game/Snapshots/SharedMoveSnapshot.cs`, `PlayerSnapshotRecord.cs`, `GameFlowRecords.cs` (новый)
- `backend/Game/GamePlay/Context/MoveSnapshot.cs` — `RecordDeckUpdate/StashUpdate/BoardStateUpdate/GameCompleted/TimeLimitedRound/LastManStandingRound`
- `backend/Game/GamePlay/Board/BoardMinesScanner.cs` — локальные Mines/Flags + авто-эмиссия
- `client/Assets/GamePlay/Sync/GamePlaySyncExtensions.cs` — 6 новых `AddSnapshotHandler`
- `client/Assets/GamePlay/Loop/PvP/PvPScopeExtensions.cs` — обычный `Register<T>` вместо `AddNetworkService.WithProperty`

### Заметки
- `NetworkProperty<T>` остаётся в инфраструктуре `Common/Network/` и в `MenuPlayerMovement.cs` — не в scope задачи.
- `BoardStateUpdate` не планировался отдельно, но `IBoard.State` читается `BoardMinesCounterView` — пришлось сохранить `ViewableProperty<BoardState>` и эмитить апдейт из scanner-а.
- Shared state-классы (`PlayerHandState`, `PlayerDeckState`, `PlayerStashState`, `PlayerModifiersState`, `BoardState`, `GameFlowState`, `TimeLimitedRoundState`, `LastManStandingRoundState`) больше не используются и могут быть удалены отдельным проходом.
- Частота снапшотов таймера — раз в секунду; нормально для snapshot-слоя.
