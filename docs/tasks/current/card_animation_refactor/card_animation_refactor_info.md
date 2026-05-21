## Card Animation Refactor

### Цель
Убрать optional поля `TargetCells`/`OpenedCells`/`UpdatedFreeCells` из `ICardActionData` и передать полный контроль над анимациями в `ICardActionSync`. Затем синхронизировать snapshot типы между backend и client, убрав nullable там, где backend гарантирует данные.

### Проблема
`ICardActionData` имел optional default properties:
- `TargetCells => null`
- `OpenedCells => null`
- `UpdatedFreeCells => null`

Не все карты их предоставляли, поэтому общие анимации `PlayTargetAnimation`/`PlayActionAnimation` просто пропускались (`if cells == null return`).

`CardActionSnapshotHandler` (геймплей) и `MenuCardPreviewPlayer` (меню) дублировали эту хрупкую логику:
```
PlayTargetAnimation(data)   // использует data.TargetCells — может быть null
PlayActionAnimation(data)   // использует data.OpenedCells — может быть null
sync.Sync(data)             // кастомные эффекты
```

### Решение
- `ICardActionSync` полностью контролирует свою анимацию через инжектированный `IBoardCellsAnimator`
- `MenuCardPreviewPlayer` вызывает только `_syncRegistry.Dispatch(lifetime, action)`
- `CardActionSnapshotHandler` вызывает только `card.Use(lifetime, record.Data)` (внутри `_actionSync` сам делает ВСЁ)
- Snapshot типы синхронизированы с backend: nullable только там, где бэкенд действительно может не отдать поле
- Все проверки на null убраны где backend гарантирует данные — fail fast

### Архитектура

**Текущий поток (gameplay):**
```
CardActionSnapshotHandler.Handle(record)
  PlayTargetAnimation(record.Data)   // anim target cells
  PlayActionAnimation(record.Data)   // anim action cells
  card.Use(lifetime, record.Data)    // -> _actionSync.Sync() -> custom effects + logic
```

**Целевой поток (gameplay):**
```
CardActionSnapshotHandler.Handle(record)
  card.Use(lifetime, record.Data)    // -> _actionSync.Sync() -> anim + logic
```

**Текущий поток (menu):**
```
MenuCardPreviewPlayer.RunLoopAsync()
  _menuBoard.PlayTargetAnimation(action, targetFallback)
  _menuBoard.PlayActionAnimation(action)
  _syncRegistry.Dispatch(lifetime, action)  // custom effects only
```

**Целевой поток (menu):**
```
MenuCardPreviewPlayer.RunLoopAsync()
  _syncRegistry.Dispatch(lifetime, action)  // -> _actionSync.Sync() -> anim + logic
```

### План реализации

**Phase 1: Shared — ICardActionData cleanup** [x]
- [x] Удалить `TargetCells`/`OpenedCells`/`UpdatedFreeCells` из `ICardActionData`
- [x] Оставить snapshot типы как есть

**Phase 2: Client — IBoardCellsAnimator** [x]
- [x] Создать `IBoardCellsAnimator` interface
- [x] Создать `BoardCellsAnimator` (gameplay)
- [x] Создать `MenuBoardCellsAnimator` (menu)
- [x] Зарегистрировать в DI

**Phase 3-4: Упрощение хендлеров** [x]
- [x] Упростить `CardActionSnapshotHandler`
- [x] Упростить `MenuCardPreviewPlayer`
- [x] Удалить методы из `MenuBoard`/`IMenuBoard`

**Phase 5: Рефактор Snapshot классов** [x]
- [x] Добавить `IBoardCellsAnimator` во все ~52 Snapshot класса
- [x] Каждый Snapshot контролирует свои анимации

**Phase 6: Registry & DI** [x]
- [x] Обновить DI регистрации

**Phase 7: Backend** [x]
- [x] Исправить `SnapshotApplier`
- [x] Сборка и тесты проходят

**Phase 8: Синхронизация snapshot типов (НОВАЯ)** [x]
zo|- [x] Аудит backend: какие поля каждая карта реально отдаёт
wo|- [x] Аудит shared snapshot типов: убрать nullable где backend гарантирует данные
dp|- [x] Убрать ненужные проверки на null в клиентских Snapshot.Sync
vx|- [x] Обновить MemoryPack union IDs если порядок свойств изменился
my|- [x] Сборка и тесты проходят

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | Контракт `ICardActionData` и snapshot типы |
| `client/Assets/GamePlay/Boards/Cells/IBoardCellsAnimator.cs` | [новый] Интерфейс аниматора клеток |
| `client/Assets/GamePlay/Boards/Cells/BoardCellsAnimator.cs` | [новый] Gameplay-реализация аниматора |
| `client/Assets/Menu/Decks/Preview/Sync/MenuBoardCellsAnimator.cs` | [новый] Menu-реализация аниматора |
| `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs` | Убрать PlayTargetAnimation/PlayActionAnimation |
| `client/Assets/Menu/Decks/Preview/MenuCardPreviewPlayer.cs` | Убрать PlayTargetAnimation/PlayActionAnimation, targetFallback |
| `client/Assets/Menu/Decks/Preview/MenuBoard.cs` | Убрать PlayTargetAnimation/PlayActionAnimation/ApplyUpdatedCells |
| `client/Assets/GamePlay/Cards/Entities/Actions/*/*.cs` | ~52 Snapshot-классов |
| `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` | Reflection-based чтение полей |
| `backend/Game/GamePlay/Cards/**/*.cs` | Источник truth для snapshot полей |

### Риски
- MemoryPack генерирует Id по порядку свойств — изменение порядка или удаление свойств требует перегенерации union IDs
- Backend `SnapshotApplier` читает поля через reflection — при удалении полей из snapshot типов нужно обновить reflection код
- Карты без позиционных данных (Medic, Siphon и т.д.) не должны получить обязательные поля
- ZipZap кастомная логика — lightning lines требуют прямого доступа к board cells

### Документация
- `.agents/docs/COMMON_LIFETIMES.md` — Lifetime, Advise, подписки
- `.agents/docs/COMMON_CONTAINER.md` — VContainer DI
- `.agents/docs/COMMON_REACTIVE_BASICS.md` — IViewableProperty
- `.agents/docs/GAMEPLAY.md` — game flow, board, cards, snapshot sync
