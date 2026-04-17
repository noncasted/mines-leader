## Задача: Миграция геймплея клиента с NetworkProperty<T> на снапшоты

### Цель

Полностью убрать `NetworkProperty<T>` из геймплейных объектов клиента и перевести всю синхронизацию состояния на снапшоты (`SharedMoveSnapshot` → `ISnapshotHandler<T>`).

Затрагивает 8 объектов в `client/Assets/GamePlay/`:

| Объект | NetworkProperty | Снапшот-хендлер |
|--------|-----------------|-----------------|
| `Boards/Root/Board.cs` | `BoardState` | уже есть `BoardSnapshotHandler` (покрывает клетки), но `BoardState` (Mines/Flags) — нет |
| `Cards/Hand/Hand.cs` | `PlayerHandState` | state не используется — просто убираем |
| `Cards/Deck/Deck.cs` | `PlayerDeckState` | нужен новый `DeckSnapshotHandler` |
| `Cards/Stash/Stash.cs` | `PlayerStashState` | нужен новый `StashSnapshotHandler` |
| `Players/Entity/Modifiers/PlayerModifiers.cs` | `PlayerModifiersState` | handler есть (`PlayerModifierSnapshotHandler`), но пустой — написать логику |
| `Loop/Context/GameState.cs` | `GameFlowState` | нет — добавить record + handler + backend-эмиссию |
| `Loop/Context/TimeLimitedGameRound.cs` | `TimeLimitedRoundState` | нет — добавить |
| `Loop/Context/LastManStandingRound.cs` | `LastManStandingRoundState` | нет — добавить |

Тип `NetworkProperty<T>` **не удаляем** — он ещё используется в `Menu/Social/Players/Entity/Movement/MenuPlayerMovement.cs` и в инфраструктуре `Common/Network/`.

### Контекст

**Снапшот-инфраструктура уже собрана:**
- `shared/Game/Snapshots/SharedMoveSnapshot.cs` — контейнер + MemoryPack union `IMoveSnapshotRecord` (сейчас 9 членов, слоты 0–8 заняты).
- `shared/Game/Snapshots/PlayerSnapshotRecord.cs` — вложенные записи (CardUse/CardAdd/CardRemove/Mana/Health/Moves/Modifier).
- `backend/Game/GamePlay/Context/MoveSnapshot.cs` — билдер снапшота с `RecordX()` методами, рассылается через `ISnapshotSender`.
- `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` — симметричное применение для diff-guard (для него в проекте есть `SnapshotDiffGuardTests`).
- `client/Assets/GamePlay/Services/SnapshotReceiver.cs` — `OneWayCommand<SharedMoveSnapshot>`, очередь + диспатч по типу.
- `client/Assets/GamePlay/Services/ISnapshotHandler.cs` — регистрация через `builder.AddSnapshotHandler<THandler, TRecord>()`.

**Текущий паттерн NetworkProperty:**
- Backend: `ValueProperty<TState>` + `BindProperty(_state)` в `Service`-классах (`GameFlow`, `TimeLimitedRound`, `LastManStandingRound`, плюс `Board`/`Hand`/`Deck`/`Stash`/`Modifiers` на стороне игрока).
- Client: `NetworkProperty<TState>` регистрируется через `builder.RegisterProperty<TState>(id)` (в `GamePlayerFactory.Build`) либо через `.WithProperty<TState>(id)` в `AddNetworkService` (в `PvPScopeExtensions`).
- Клиент подписывается на `_state.Advise(lifetime, ...)` в `OnLoaded`/`OnStarted`/`OnSetup`.

**Принцип миграции:**
- Серверная сторона рассылает события через `MoveSnapshot.RecordX()` → `SnapshotSender.Send()` (уже работает для mana/health/moves/cardAdd/cardRemove/cardUse/gameStarted/board).
- Клиент получает через `ISnapshotHandler<T>` и толкает в обычные `ViewableProperty`/`ViewableList` внутри бизнес-объектов (как уже сделано в `PlayerHealthSnapshotHandler`).
- Состояние в клиентских объектах должно быть локальным (свои `ViewableProperty`), а не `NetworkProperty`.

**Из `SharedMoveSnapshot`:** новые record-классы нужно добавить в union `IMoveSnapshotRecord` со следующими свободными тегами (9, 10, ...). Сейчас заняты 0–8.

### Шаги реализации

**1. Новые снапшот-records в shared**

  1.1. Добавить в `shared/Game/Snapshots/PlayerSnapshotRecord.cs` классы:
  - `PlayerSnapshotRecord.DeckUpdate { PlayerId, int Count }`
  - `PlayerSnapshotRecord.StashUpdate { PlayerId, int Count }`
  1.2. Создать `shared/Game/Snapshots/GameFlowRecords.cs` [новый файл]:
  - `GameCompletedRecord { Guid Winner }`
  - `TimeLimitedRoundRecord { Guid CurrentPlayer, Dictionary<Guid, long> SecondsLeft }`
  - `LastManStandingRoundRecord { Guid CurrentPlayer, int CurrentRound, int SecondsLeft }`
  1.3. Обновить `[MemoryPackUnion(N, ...)]` в `shared/Game/Snapshots/SharedMoveSnapshot.cs`, добавив новые типы с тегами 9–13.
  1.4. Оценить, можно ли удалить теперь не используемые state-классы из `shared/Game/Player/*State.cs` и `shared/Game/Context/*State.cs` — отложить до завершения миграции: убирать только после того, как ни одна сборка клиента или бэка не ссылается на них.

**2. Backend: эмиссия новых records и удаление `ValueProperty`**

  2.1. Расширить `backend/Game/GamePlay/Context/MoveSnapshot.cs` методами:
  - `RecordDeckUpdate(IPlayer player, int count)`
  - `RecordStashUpdate(IPlayer player, int count)`
  - `RecordGameCompleted(Guid winner)`
  - `RecordTimeLimitedRound(Guid currentPlayer, Dictionary<Guid, long> secondsLeft)`
  - `RecordLastManStandingRound(Guid currentPlayer, int currentRound, int secondsLeft)`
  2.2. `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` — добавить ветки `case` для новых типов (минимум заглушки, т.к. `GameStateSnapshot` пока не содержит полей раунда; для deck/stash — если надо для diff-guard тестов — обновить соответствующие поля в `GameStateSnapshot`).
  2.3. `backend/Game/GamePlay/Context/GameFlow.cs`:
  - убрать `ValueProperty<GameFlowState> _state` и `BindProperty(_state)`;
  - после окончания раунда вместо `_state.Update(state => state.Winner = winner)` формировать `MoveSnapshot`, вызывать `RecordGameCompleted(winner)`, отправлять через `ISnapshotSender` (добавить зависимость).
  2.4. `backend/Game/GamePlay/Context/Rounds/TimeLimitedRound.cs`:
  - убрать `ValueProperty<TimeLimitedRoundState>` и `BindProperty`;
  - хранить `SecondsLeft` в приватном поле (`Dictionary<Guid, long>`), в каждом `_state.Update(...)` — эмитить `RecordTimeLimitedRound(...)` через `_snapshotSender.Send(...)`;
  - логику `IsGameOver`/`GetWinner`, читающую `_state.Value.SecondsLeft`, переписать на приватное поле.
  2.5. `backend/Game/GamePlay/Context/Rounds/LastManStandingRound.cs` — аналогично, но со своим набором полей (CurrentRound, SecondsLeft int).
  2.6. Проверить/обновить `SnapshotDiffGuardTests` — если state меняется, а snapshot не применяется в `GameStateSnapshot`, guard должен пропускать эти записи (как уже сделано для `GameStartedRecord`).

**3. Client: переписать 8 бизнес-объектов**

  3.1. `client/Assets/GamePlay/Boards/Root/Board.cs`:
  - убрать `NetworkProperty<BoardState> _state` из `Construct`/полей;
  - убрать `IViewableProperty<BoardState> State` из `IBoard`, если нет внешних читателей; иначе заменить на локальную `ViewableProperty<BoardState>`, обновляемую `BoardSnapshotHandler`. Перед удалением — найти использования `IBoard.State` в клиенте.
  3.2. `client/Assets/GamePlay/Cards/Hand/Hand.cs` — убрать параметр `NetworkProperty<PlayerHandState>` и поле `_state` (не используется).
  3.3. `client/Assets/GamePlay/Cards/Deck/Deck.cs`:
  - убрать `NetworkProperty<PlayerDeckState>`;
  - завести локальную `ViewableProperty<int> _size`, обновлять её из нового `DeckSnapshotHandler`;
  - в `OnLoaded` подписаться на `_size.Advise(...)` и звать `_view.UpdateAmount(...)`.
  3.4. `client/Assets/GamePlay/Cards/Stash/Stash.cs` — по аналогии с Deck.
  3.5. `client/Assets/GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs`:
  - убрать `NetworkProperty<PlayerModifiersState>`;
  - оставить `ViewableDictionary<PlayerModifier, float> _values` (уже есть), но обновлять из `PlayerModifierSnapshotHandler`;
  - добавить публичный метод `Set(PlayerModifier, float)` для хендлера (или передавать интерфейс записи).
  3.6. `client/Assets/GamePlay/Loop/Context/GameState.cs`:
  - убрать наследование `NetworkService` и `NetworkProperty<GameFlowState>`;
  - переделать в обычный `IScopeLoaded`/`IScopeSetup` сервис;
  - `GameCompletedSnapshotHandler` должен дёргать публичный метод `GameState.SetWinner(Guid)`.
  3.7. `client/Assets/GamePlay/Loop/Context/TimeLimitedGameRound.cs`:
  - убрать `NetworkProperty<TimeLimitedRoundState>`;
  - хранить `Dictionary<Guid, long>` SecondsLeft и Guid CurrentPlayer локально;
  - логику подстановки `_player`/`_roundTime` перенести в `TimeLimitedRoundSnapshotHandler`;
  - `IsTurnAllowed` переключить на локальное поле.
  3.8. `client/Assets/GamePlay/Loop/Context/LastManStandingRound.cs` — аналогично.

**4. Client: snapshot handlers**

  4.1. `client/Assets/GamePlay/Sync/PlayerModifierSnapshotHandler.cs` — заполнить логику: `player.Modifiers.Set(record.Modifier, record.Value)`.
  4.2. `client/Assets/GamePlay/Sync/DeckSnapshotHandler.cs` [новый файл] — обновляет размер деки локального объекта `Deck`.
  4.3. `client/Assets/GamePlay/Sync/StashSnapshotHandler.cs` [новый файл] — обновляет размер стэша.
  4.4. `client/Assets/GamePlay/Sync/GameCompletedSnapshotHandler.cs` [новый файл] — зовёт `GameState.SetWinner(record.Winner)`.
  4.5. `client/Assets/GamePlay/Sync/TimeLimitedRoundSnapshotHandler.cs` [новый файл] — апдейтит `TimeLimitedGameRound`.
  4.6. `client/Assets/GamePlay/Sync/LastManStandingRoundSnapshotHandler.cs` [новый файл] — апдейтит `LastManStandingRound`.

**5. Client: регистрация и удаление property-регистраций**

  5.1. `client/Assets/GamePlay/Sync/GamePlaySyncExtensions.cs` — добавить `AddSnapshotHandler<...>` для всех новых хендлеров.
  5.2. `client/Assets/GamePlay/Players/Services/Factory/GamePlayerFactory.cs` — убрать пять `builder.RegisterProperty<...>(...)` (Board/Modifiers/Hand/Stash/Deck).
  5.3. `client/Assets/GamePlay/Loop/PvP/PvPScopeExtensions.cs` — убрать `.WithProperty<GameFlowState>`, `.WithProperty<TimeLimitedRoundState>`, `.WithProperty<LastManStandingRoundState>`. Решить: остаётся ли `AddNetworkService` для `GameState`/раундов (если методы `Request/OneWay` через `NetworkService` ещё используются — оставить; иначе переводить на обычный `Register`). `TrySkip()` сейчас вызывает `_connection.Request(new SharedGameAction.SkipTurn())`, т.е. доступ к `INetworkConnection` уже через DI — `AddNetworkService` можно убрать.
  5.4. Файл `client/Assets/Common/Network/Services/NetworkServiceExtensions.cs` не трогаем (инфраструктура остаётся).

**6. Проверки**

  6.1. Поиск остаточных `NetworkProperty<...>` в `client/Assets/GamePlay/` — должно вернуть 0 совпадений.
  6.2. Сборка backend (`dotnet build backend/Game/Game.csproj`).
  6.3. Прогнать `backend/Tools/Tests/Game/SnapshotDiffGuardTests.cs` — diff-guard должен пропускать новые записи (с учётом обновлений в SnapshotApplier).

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `shared/Game/Snapshots/SharedMoveSnapshot.cs` | Расширить union `IMoveSnapshotRecord` новыми членами |
| `shared/Game/Snapshots/PlayerSnapshotRecord.cs` | Добавить `DeckUpdate`/`StashUpdate` |
| `shared/Game/Snapshots/GameFlowRecords.cs` | Новый файл с records для GameState и раундов |
| `backend/Game/GamePlay/Context/MoveSnapshot.cs` | Добавить `RecordX` методы-эмиттеры |
| `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` | Добавить ветки для новых records |
| `backend/Game/GamePlay/Context/GameFlow.cs` | Убрать `ValueProperty<GameFlowState>`, эмитить snapshot |
| `backend/Game/GamePlay/Context/Rounds/TimeLimitedRound.cs` | Убрать ValueProperty, эмитить каждый апдейт |
| `backend/Game/GamePlay/Context/Rounds/LastManStandingRound.cs` | То же |
| `client/Assets/GamePlay/Sync/GamePlaySyncExtensions.cs` | Регистрация новых хендлеров |
| `client/Assets/GamePlay/Players/Services/Factory/GamePlayerFactory.cs` | Убрать 5× `RegisterProperty` |
| `client/Assets/GamePlay/Loop/PvP/PvPScopeExtensions.cs` | Убрать 3× `.WithProperty` |
| `client/Assets/GamePlay/Boards/Root/Board.cs` | Убрать `NetworkProperty<BoardState>` |
| `client/Assets/GamePlay/Cards/Hand/Hand.cs` | Убрать неиспользуемый `NetworkProperty<PlayerHandState>` |
| `client/Assets/GamePlay/Cards/Deck/Deck.cs` | Перейти на snapshot |
| `client/Assets/GamePlay/Cards/Stash/Stash.cs` | То же |
| `client/Assets/GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs` | То же |
| `client/Assets/GamePlay/Loop/Context/GameState.cs` | Снять `NetworkService`, перейти на snapshot |
| `client/Assets/GamePlay/Loop/Context/TimeLimitedGameRound.cs` | То же |
| `client/Assets/GamePlay/Loop/Context/LastManStandingRound.cs` | То же |
| `client/Assets/GamePlay/Sync/PlayerModifierSnapshotHandler.cs` | Заполнить пустой handler |
| `client/Assets/GamePlay/Sync/DeckSnapshotHandler.cs` | Новый |
| `client/Assets/GamePlay/Sync/StashSnapshotHandler.cs` | Новый |
| `client/Assets/GamePlay/Sync/GameCompletedSnapshotHandler.cs` | Новый |
| `client/Assets/GamePlay/Sync/TimeLimitedRoundSnapshotHandler.cs` | Новый |
| `client/Assets/GamePlay/Sync/LastManStandingRoundSnapshotHandler.cs` | Новый |

### Документация к прочтению

- `docs/GAMEPLAY.md` — раздел про snapshot-синхронизацию и backend-эмиссию.
- `docs/COMMON_LIFETIMES.md` — хендлеры должны использовать lifetime правильно (там, где требуется подписка).
- `docs/COMMON_REACTIVE_BASICS.md` — замена `NetworkProperty` на `ViewableProperty` внутри объектов.
- `docs/CODE_STYLE_FULL.md` — порядок членов в новых классах, naming.

### Риски

- **`IBoard.State` как публичный API.** Перед удалением — проверить использования (`Grep "IBoard.State"`, `".State.Advise"`). Если используется UI — оставить как `ViewableProperty<BoardState>`, обновляемую локально по snapshot-событиям (подсчёт через `SharedBoardSnapshot.Records` с фильтрацией флагов) — это отдельная подзадача, при необходимости добавим запись `BoardStateUpdate` для Mines/Flags.
- **`AddNetworkService` без `.WithProperty`.** Убедиться, что `NetworkServiceResolver` корректно работает с пустым словарём properties, или полностью отказаться от `AddNetworkService` для `GameState`/раундов и регистрировать их как обычные сервисы. Вероятное решение: регистрировать через обычный `builder.Register<T>()` с `ISessionStartHandler` или `IScopeSetup`.
- **MemoryPack union теги.** Не переупорядочивать существующие 0–8, новые добавлять только в конец. Проверить совместимость с сохранёнными в логах снапшотами (если такие есть).
- **Backend ValueProperty-связь.** На бэке `ValueProperty` + `BindProperty` могут участвовать в механизме персиста/восстановления сессии — проверить, не сломается ли rejoin матча. Если да, переписать логику восстановления через `MoveSnapshot` replay.
- **Частотa снапшотов таймера.** Раунд таймер тикает раз в секунду → каждую секунду будет улетать snapshot. Это нормальная частота для снапшот-слоя, но тесты diff-guard могут замедлиться.
