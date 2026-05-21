# Card Animation Refactor — Workflow

## Проблема

`ICardActionData` имеет optional default properties:
- `TargetCells => null`
- `OpenedCells => null`
- `UpdatedFreeCells => null`

Не все карты их предоставляют → общие анимации `PlayTargetAnimation` / `PlayActionAnimation` просто пропускаются (`if cells == null return`).

`CardActionSnapshotHandler` (геймплей) и `MenuCardPreviewPlayer` (меню) дублируют эту хрупкую логику:
```
PlayTargetAnimation(data)   // использует data.TargetCells — может быть null
PlayActionAnimation(data)   // использует data.OpenedCells — может быть null
sync.Sync(data)             // кастомные эффекты
```

## Цель

- Убрать optional поля из `ICardActionData`
- `ICardActionSync` полностью контролирует свою анимацию через инжектированный `IBoardCellsAnimator`
- `MenuCardPreviewPlayer` вызывает только `_syncRegistry.Dispatch(lifetime, action)`
- `CardActionSnapshotHandler` вызывает только `card.Use(lifetime, record.Data)` (внутри `_actionSync` сам делает ВСЁ)

## Архитектура

### Текущий поток

**Геймплей:**
```
CardActionSnapshotHandler.Handle(record)
  PlayTargetAnimation(record.Data)   // anim target cells
  PlayActionAnimation(record.Data)   // anim action cells
  card.Use(lifetime, record.Data)    // -> _actionSync.Sync() -> custom effects + logic
```

**Меню:**
```
MenuCardPreviewPlayer.RunLoopAsync()
  _menuBoard.PlayTargetAnimation(action, targetFallback)
  _menuBoard.PlayActionAnimation(action)
  _syncRegistry.Dispatch(lifetime, action)  // custom effects only
```

### Целевой поток

**Геймплей:**
```
CardActionSnapshotHandler.Handle(record)
  card.Use(lifetime, record.Data)    // -> _actionSync.Sync() -> anim + logic
```

**Меню:**
```
MenuCardPreviewPlayer.RunLoopAsync()
  _syncRegistry.Dispatch(lifetime, action)  // -> _actionSync.Sync() -> anim + logic
```

## Phase 1: Shared — ICardActionData cleanup

**File:** `shared/Game/Snapshots/CardActionSnapshotRecord.cs`

1.1. `ICardActionData` — убрать:
```csharp
IReadOnlyList<Position>? TargetCells => null;
IReadOnlyList<OpenedCell>? OpenedCells => null;
IReadOnlyList<OpenedCell>? UpdatedFreeCells => null;
```
Оставить только:
```csharp
Guid TargetPlayer { get; set; }
```

1.2. Snapshot типы (`CardActionSnapshot.Trebuchet` и т.д.) оставить как есть — у них уже есть свои специфичные поля.

**Backend impact:** none. `CardPreviewGenerator` генерирует snapshot'ы через `card.Use()` — типы уже содержат нужные данные.

## Phase 2: Client — IBoardCellsAnimator

### 2.1. Interface

**New file:** `client/Assets/GamePlay/Boards/Cells/IBoardCellsAnimator.cs`

```csharp
public interface IBoardCellsAnimator
{
    UniTask PlayTargetAnimation(IReadOnlyLifetime lifetime, IReadOnlyList<Position> positions);
    UniTask PlayActionAnimation(IReadOnlyLifetime lifetime, IReadOnlyList<Position> positions);
    UniTask OpenCells(IReadOnlyLifetime lifetime, IReadOnlyList<OpenedCell> cells);
    void AddEffect(Guid effectId, CellEffectType type, Position position);
    void FlagCell(Position position);
    void UnflagCell(Position position);
    void ExplodeCell(Position position, CellExplosionType type);
}
```

### 2.2. Gameplay implementation

**New file:** `client/Assets/GamePlay/Boards/Cells/BoardCellsAnimator.cs`

- Инжектирует `IGameContext`
- `GetPlayer(data.TargetPlayer).Board` для доступа к ячейкам
- `PlayTargetAnimation` / `PlayActionAnimation` — переносим из `CardActionSnapshotHandler.PlayCellsAnimation`
- `OpenCells` / `AddEffect` / `FlagCell` — переносим логику из snapshot'ов

### 2.3. Menu implementation

**New file:** `client/Assets/Menu/Screens/Cards/Preview/Sync/MenuBoardCellsAnimator.cs`

- Инжектирует `IMenuBoard`
- Адаптирует `IMenuBoard.PlayTargetAnimation` / `PlayActionAnimation`
- `OpenCells` / `AddEffect` — через `IMenuBoard` API

### 2.4. DI registration

**File:** `client/Assets/Menu/Common/Loop/MenuLoopExtensions.cs`
- `builder.Register<MenuBoardCellsAnimator>().As<IBoardCellsAnimator>();`

**Gameplay DI** (где регистрируются gameplay сервисы):
- `builder.Register<BoardCellsAnimator>().As<IBoardCellsAnimator>();`

## Phase 3: Gameplay — CardActionSnapshotHandler

**File:** `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs`

3.1. Удалить:
- `PlayTargetAnimation(ICardActionData)`
- `PlayActionAnimation(ICardActionData)`
- `PlayCellsAnimation(...)`

3.2. `Handle` метод оставить:
```csharp
public async UniTask Handle(PlayerSnapshotRecord.CardUse record)
{
    var player = _gameContext.GetPlayer(record.PlayerId);
    var card = player.Hand.Entries.First(t => t.Id == record.CardId)!;

    await card.Use(_lifetime, record.Data);

    // Card drop animation (unchanged)
    if (card is IRemoteCard rc) { ... }
    else { ... }
}
```

## Phase 4: Menu — MenuCardPreviewPlayer

**File:** `client/Assets/Menu/Screens/Cards/Preview/MenuCardPreviewPlayer.cs`

4.1. Удалить:
- `_menuBoard.PlayTargetAnimation(...)`
- `_menuBoard.PlayActionAnimation(...)`
- `targetFallback` логику

4.2. `RunLoopAsync` цикл:
```csharp
foreach (var action in bundle.Actions)
{
    if (lifetime.IsTerminated) return;
    await _syncRegistry.Dispatch(lifetime, action);
    if (lifetime.IsTerminated) return;
    await UniTask.Delay(InterActionDelayMs, cancellationToken: lifetime.Token);
}
```

## Phase 5: Refactor all ICardActionSync implementations

Каждый `Snapshot` класс в `client/Assets/GamePlay/Cards/Entities/Actions/*/*.cs`:

5.1. Добавить `IBoardCellsAnimator` в конструктор:
```csharp
public Snapshot(IBoardCellsAnimator animator, IGameContext gameContext)
{
    _animator = animator;
    _gameContext = gameContext;
}
```

5.2. `Sync` метод — сам контролирует порядок:
```csharp
// Example: Trebuchet
public async UniTask Sync(IReadOnlyLifetime lifetime, CardActionSnapshot.Trebuchet payload)
{
    // 1. Target animation (if card has target cells)
    if (payload.TargetCells != null && payload.TargetCells.Count > 0)
        await _animator.PlayTargetAnimation(lifetime, payload.TargetCells);

    // 2. Action animation (if card has opened cells)
    if (payload.OpenedCells != null && payload.OpenedCells.Count > 0)
    {
        var positions = payload.OpenedCells.Select(o => o.Position).ToList();
        await _animator.PlayActionAnimation(lifetime, positions);
    }

    // 3. Logic: open cells
    if (payload.UpdatedFreeCells != null)
    {
        foreach (var opened in payload.UpdatedFreeCells)
            _animator.OpenCell(opened.Position, opened.MinesAround);
    }
}
```

5.3. Карты без позиционных данных (CoinToss, Shield, Adrenaline и т.д.) — просто пропускают шаги 1-2 и делают только кастомные эффекты / логику.

### Files to update (all Snapshot classes):
- `CardTrebuchetAction.Snapshot`
- `CardBloodhoundAction.Snapshot`
- `CardZipZapAction.Snapshot` (custom lightning + camera shake)
- `CardSmokeAction.Snapshot` (area effect)
- `CardBlackoutAction.Snapshot`
- `CardFrostAction.Snapshot`
- `CardCoinTossAction.Snapshot` (random animator)
- `CardManaFountainAction.Snapshot`
- ... (все ~40 snapshot классов)

## Phase 6: Registry & DI

**File:** `client/Assets/Menu/Screens/Cards/Preview/Sync/MenuCardActionSyncRegistry.cs`
- `Dispatch` signature не меняется (просто вызывает `sync.Sync`)

**File:** `client/Assets/GamePlay/Cards/Entities/Actions/Common/ICardAction.cs`
- `Resolver.Sync` signature не меняется
- `ICardActionSync<T>` signature не меняется

**File:** `client/Assets/GamePlay/Cards/Entities/States/CardStatesExtensions.cs`
- `AddCardActionSync` — регистрация snapshot'ов с `IBoardCellsAnimator` DI

## Phase 7: Backend verification

**File:** `backend/Game/GamePlay/CardPreviews/CardPreviewGenerator.cs`
- Проверить, что все `CardUsePayload.*` snapshot'ы содержат нужные данные
- Если какой-то snapshot тип не передаёт `TargetCells` / `OpenedCells` — добавить в backend `card.Use()`

## Acceptance Criteria

- [ ] `ICardActionData` не имеет `TargetCells`/`OpenedCells`/`UpdatedFreeCells`
- [ ] `MenuCardPreviewPlayer` не вызывает `PlayTargetAnimation`/`PlayActionAnimation`
- [ ] `CardActionSnapshotHandler` не вызывает `PlayTargetAnimation`/`PlayActionAnimation`
- [ ] Каждый `ICardActionSync` сам вызывает `animator.PlayTargetAnimation` / `PlayActionAnimation` когда нужно
- [ ] Menu preview работает: hover карты → `_syncRegistry.Dispatch` → полный цикл анимаций
- [ ] Gameplay работает: snapshot → `card.Use()` → полный цикл анимаций
- [ ] Компиляция проходит без ошибок
