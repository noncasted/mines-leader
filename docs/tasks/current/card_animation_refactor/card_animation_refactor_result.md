## Card Animation Refactor — Результат

### Статус: Phase 1-8 завершено.

### Что сделано (Phase 1-7)

- Удалены optional поля `TargetCells`/`OpenedCells`/`UpdatedFreeCells` из `ICardActionData` — интерфейс теперь содержит только `Guid TargetPlayer`
- Создан `IBoardCellsAnimator` interface и две реализации: `BoardCellsAnimator` (gameplay) и `MenuBoardCellsAnimator` (menu)
- `CardActionSnapshotHandler` упрощён: вызывает только `card.Use()` — все анимации теперь внутри `ICardActionSync`
- `MenuCardPreviewPlayer` упрощён: вызывает только `_syncRegistry.Dispatch()` — `targetFallback` и прямые вызовы анимаций удалены
- `MenuBoard`/`IMenuBoard` очищены: удалены `PlayTargetAnimation`/`PlayActionAnimation`/`ApplyUpdatedCells`/`PlayCellsAnimation`
- ~52 Snapshot-классов отрефакторены: каждый получил `IBoardCellsAnimator` в конструктор и сам контролирует порядок анимаций (target → action → кастомная логика)
- Backend `SnapshotApplier` адаптирован: чтение `UpdatedFreeCells`/`OpenedCells` через reflection (вместо доступа через удалённые свойства интерфейса)

### Что сделано (Phase 8)

- [x] Аудит backend карточек (`backend/Game/GamePlay/Cards/**/*.cs`) — составлена матрица "карта → гарантированные поля"
- [x] Синхронизированы shared snapshot типы:
  - Удалены неиспользуемые `TargetCells` из `Gravedigger`, `TrebuchetAimer`, `Purge`
  - Убран nullable (`?`) у ~30 полей которые backend всегда заполняет (`TargetCells`, `OpenedCells`, `UpdatedFreeCells`, `TakenCells`, `UnflaggedCells`, `FlaggedCells` и др.)
- [x] Убраны `if (x != null)` проверки в ~26 клиентских Snapshot.Sync — fail fast где backend гарантирует данные
- [x] `SnapshotApplier` — не требовал обновления (reflection читает `UpdatedFreeCells`/`OpenedCells`, удалённые поля не участвуют)
- [x] MemoryPack union IDs — не изменялись (union IDs привязаны к типам, не к полям)

### Evidence

- Backend: `dotnet build backend/backend.slnx` — 0 errors, 17 warnings (pre-existing)
- Backend: `dotnet test backend/backend.slnx` — 641 passed, 0 failed
- Client: `dotnet build client/client.slnx` — 0 errors, 62 warnings (pre-existing)

### Ключевые файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | Удалены `TargetCells`/`OpenedCells`/`UpdatedFreeCells` из `ICardActionData` (Phase 1); убран nullable у гарантированных полей, удалены неиспользуемые `TargetCells` (Phase 8) | Phase 1 + 8 | `dotnet build` 0 errors |
| `client/Assets/GamePlay/Boards/Cells/IBoardCellsAnimator.cs` | [новый] Интерфейс аниматора клеток | Phase 2 | — |
| `client/Assets/GamePlay/Boards/Cells/BoardCellsAnimator.cs` | [новый] Gameplay-реализация | Phase 2 | — |
| `client/Assets/Menu/Decks/Preview/Sync/MenuBoardCellsAnimator.cs` | [новый] Menu-реализация | Phase 2 | — |
| `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs` | Удалены методы анимации, оставлен только `card.Use()` | Phase 3 | — |
| `client/Assets/Menu/Decks/Preview/MenuCardPreviewPlayer.cs` | Удалены `_menuBoard.PlayTargetAnimation`/`PlayActionAnimation`, `targetFallback` | Phase 4 | — |
| `client/Assets/Menu/Decks/Preview/MenuBoard.cs` | Удалены методы анимации и `ApplyUpdatedCells` | Phase 4 | — |
| `client/Assets/GamePlay/Cards/Entities/Actions/*/*.cs` | ~52 Snapshot-классов: добавлен `IBoardCellsAnimator`, каждый контролирует свои анимации (Phase 5); убраны `!= null` проверки (Phase 8) | Phase 5 + 8 | `dotnet build` 0 errors |
| `client/Assets/GamePlay/Boards/BoardsServicesExtensions.cs` | Регистрация `BoardCellsAnimator` | Phase 6 | — |
| `client/Assets/Menu/Common/Loop/MenuLoopExtensions.cs` | Регистрация `MenuBoardCellsAnimator` | Phase 6 | — |
| `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` | Reflection-based чтение `UpdatedFreeCells`/`OpenedCells` | Phase 7 | `dotnet test` 641 passed |

### Заметки

- Snapshot типы теперь синхронизированы с backend: nullable только там, где backend действительно может не отдать поле
- Карты без позиционных данных (`Medic`, `Siphon`, `Overclock` и т.д.) имеют snapshot типы только с `TargetPlayer` — без `TargetCells`
- Карты `Gravedigger`, `TrebuchetAimer`, `Purge` больше не имеют `TargetCells` в snapshot типе (backend никогда его не заполнял)
- ZipZap сохранил кастомную логику: `IGameContext` нужен для получения `IBoardCell` объектов для lightning line VFX, `IBoardCellsAnimator` используется для `ExplodeCell` и `OpenCells`
- Backend `SnapshotApplier` использует `BindingFlags.Public | BindingFlags.Instance` для поиска свойств на конкретных типах snapshot'ов
- Все 641 backend-теста проходят после Phase 1-8
- Client solution собирается без ошибок после Phase 1-8

### Отличия от плана

- Phase 8 (синхронизация snapshot типов) не была в оригинальном workflow — добавлена по запросу пользователя для fail fast подхода

### Карты с изменёнными snapshot полями (Phase 8)

| Snapshot тип | Изменения |
|-------------|-----------|
| `Gravedigger` | Удалено `TargetCells` |
| `TrebuchetAimer` | Удалено `TargetCells` |
| `Purge` | Удалено `TargetCells` |
| `ZipZap`, `Bloodhound`, `ErosionDozer`, `OpponentBomb` | `TargetCells`, `OpenedCells`, `UpdatedFreeCells` → non-nullable |
| `Trebuchet`, `ChainReaction`, `CarpetBomb`, `FortuneBlast`, `MineCluster` | `TargetCells`, `TakenCells`, `UpdatedFreeCells` → non-nullable |
| `OpponentFlagErase` | `TargetCells`, `UnflaggedCells`, `UpdatedFreeCells` → non-nullable |
| `OpponentFlagReshuffle` | `TargetCells`, `FlaggedCells`, `UnflaggedCells`, `UpdatedFreeCells` → non-nullable |
| `Smoke`, `FogOfWar`, `Blackout`, `Frost`, `ChaosFog`, `ThermalVision`, `FortuneCookie` | `TargetCells` → non-nullable |
| `MinefieldScout`, `Sonar`, `Excavator`, `ChaosDiamond`, `ChaosScout` | `TargetCells`, `OpenedCells`, `UpdatedFreeCells`, `FlaggedCells` → non-nullable |

### Следующий шаг

Нет. Phase 1-8 полностью завершены. Сборка и тесты проходят.
</thinking>

<function=write>
<parameter=path>docs/tasks/current/card_animation_refactor/card_animation_refactor_result.md</parameter>
<parameter=content>## Card Animation Refactor — Результат

### Статус: Phase 1-8 завершено.

### Что сделано (Phase 1-7)

- Удалены optional поля `TargetCells`/`OpenedCells`/`UpdatedFreeCells` из `ICardActionData` — интерфейс теперь содержит только `Guid TargetPlayer`
- Создан `IBoardCellsAnimator` interface и две реализации: `BoardCellsAnimator` (gameplay) и `MenuBoardCellsAnimator` (menu)
- `CardActionSnapshotHandler` упрощён: вызывает только `card.Use()` — все анимации теперь внутри `ICardActionSync`
- `MenuCardPreviewPlayer` упрощён: вызывает только `_syncRegistry.Dispatch()` — `targetFallback` и прямые вызовы анимаций удалены
- `MenuBoard`/`IMenuBoard` очищены: удалены `PlayTargetAnimation`/`PlayActionAnimation`/`ApplyUpdatedCells`/`PlayCellsAnimation`
- ~52 Snapshot-классов отрефакторены: каждый получил `IBoardCellsAnimator` в конструктор и сам контролирует порядок анимаций (target → action → кастомная логика)
- Backend `SnapshotApplier` адаптирован: чтение `UpdatedFreeCells`/`OpenedCells` через reflection (вместо доступа через удалённые свойства интерфейса)

### Что сделано (Phase 8)

- [x] Аудит backend карточек (`backend/Game/GamePlay/Cards/**/*.cs`) — составлена матрица "карта → гарантированные поля"
- [x] Синхронизированы shared snapshot типы:
  - Удалены неиспользуемые `TargetCells` из `Gravedigger`, `TrebuchetAimer`, `Purge`
  - Убран nullable (`?`) у ~30 полей которые backend всегда заполняет (`TargetCells`, `OpenedCells`, `UpdatedFreeCells`, `TakenCells`, `UnflaggedCells`, `FlaggedCells` и др.)
- [x] Убраны `if (x != null)` проверки в ~26 клиентских Snapshot.Sync — fail fast где backend гарантирует данные
- [x] `SnapshotApplier` — не требовал обновления (reflection читает `UpdatedFreeCells`/`OpenedCells`, удалённые поля не участвуют)
- [x] MemoryPack union IDs — не изменялись (union IDs привязаны к типам, не к полям)

### Evidence

- Backend: `dotnet build backend/backend.slnx` — 0 errors, 17 warnings (pre-existing)
- Backend: `dotnet test backend/backend.slnx` — 641 passed, 0 failed
- Client: `dotnet build client/client.slnx` — 0 errors, 62 warnings (pre-existing)

### Ключевые файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | Удалены `TargetCells`/`OpenedCells`/`UpdatedFreeCells` из `ICardActionData` (Phase 1); убран nullable у гарантированных полей, удалены неиспользуемые `TargetCells` (Phase 8) | Phase 1 + 8 | `dotnet build` 0 errors |
| `client/Assets/GamePlay/Boards/Cells/IBoardCellsAnimator.cs` | [новый] Интерфейс аниматора клеток | Phase 2 | — |
| `client/Assets/GamePlay/Boards/Cells/BoardCellsAnimator.cs` | [новый] Gameplay-реализация | Phase 2 | — |
| `client/Assets/Menu/Decks/Preview/Sync/MenuBoardCellsAnimator.cs` | [новый] Menu-реализация | Phase 2 | — |
| `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs` | Удалены методы анимации, оставлен только `card.Use()` | Phase 3 | — |
| `client/Assets/Menu/Decks/Preview/MenuCardPreviewPlayer.cs` | Удалены `_menuBoard.PlayTargetAnimation`/`PlayActionAnimation`, `targetFallback` | Phase 4 | — |
| `client/Assets/Menu/Decks/Preview/MenuBoard.cs` | Удалены методы анимации и `ApplyUpdatedCells` | Phase 4 | — |
| `client/Assets/GamePlay/Cards/Entities/Actions/*/*.cs` | ~52 Snapshot-классов: добавлен `IBoardCellsAnimator`, каждый контролирует свои анимации (Phase 5); убраны `!= null` проверки (Phase 8) | Phase 5 + 8 | `dotnet build` 0 errors |
| `client/Assets/GamePlay/Boards/BoardsServicesExtensions.cs` | Регистрация `BoardCellsAnimator` | Phase 6 | — |
| `client/Assets/Menu/Common/Loop/MenuLoopExtensions.cs` | Регистрация `MenuBoardCellsAnimator` | Phase 6 | — |
| `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` | Reflection-based чтение `UpdatedFreeCells`/`OpenedCells` | Phase 7 | `dotnet test` 641 passed |

### Заметки

- Snapshot типы теперь синхронизированы с backend: nullable только там, где backend действительно может не отдать поле
- Карты без позиционных данных (`Medic`, `Siphon`, `Overclock` и т.д.) имеют snapshot типы только с `TargetPlayer` — без `TargetCells`
- Карты `Gravedigger`, `TrebuchetAimer`, `Purge` больше не имеют `TargetCells` в snapshot типе (backend никогда его не заполнял)
- ZipZap сохранил кастомную логику: `IGameContext` нужен для получения `IBoardCell` объектов для lightning line VFX, `IBoardCellsAnimator` используется для `ExplodeCell` и `OpenCells`
- Backend `SnapshotApplier` использует `BindingFlags.Public | BindingFlags.Instance` для поиска свойств на конкретных типах snapshot'ов
- Все 641 backend-теста проходят после Phase 1-8
- Client solution собирается без ошибок после Phase 1-8

### Отличия от плана

- Phase 8 (синхронизация snapshot типов) не была в оригинальном workflow — добавлена по запросу пользователя для fail fast подхода

### Карты с изменёнными snapshot полями (Phase 8)

| Snapshot тип | Изменения |
|-------------|-----------|
| `Gravedigger` | Удалено `TargetCells` |
| `TrebuchetAimer` | Удалено `TargetCells` |
| `Purge` | Удалено `TargetCells` |
| `ZipZap`, `Bloodhound`, `ErosionDozer`, `OpponentBomb` | `TargetCells`, `OpenedCells`, `UpdatedFreeCells` → non-nullable |
| `Trebuchet`, `ChainReaction`, `CarpetBomb`, `FortuneBlast`, `MineCluster` | `TargetCells`, `TakenCells`, `UpdatedFreeCells` → non-nullable |
| `OpponentFlagErase` | `TargetCells`, `UnflaggedCells`, `UpdatedFreeCells` → non-nullable |
| `OpponentFlagReshuffle` | `TargetCells`, `FlaggedCells`, `UnflaggedCells`, `UpdatedFreeCells` → non-nullable |
| `Smoke`, `FogOfWar`, `Blackout`, `Frost`, `ChaosFog`, `ThermalVision`, `FortuneCookie` | `TargetCells` → non-nullable |
| `MinefieldScout`, `Sonar`, `Excavator`, `ChaosDiamond`, `ChaosScout` | `TargetCells`, `OpenedCells`, `UpdatedFreeCells`, `FlaggedCells` → non-nullable |


### Bugfixes (пост-Phase 8)

**Bug 1: ZipZap — пропала анимация в меню**
- Причина: `CardZipZapAction.Snapshot` отсутствовал в `MenuCardActionSyncExtensions.cs`.
- Фикс: добавлена регистрация в menu DI.

**Bug 2: OpponentBomb — сплошное заполненное поле в preview**
- Причина: layout в `CardPreviewScenarios.cs` был полностью из `t` (все клетки открыты).
- Фикс: layout переделан на смешанное поле с `_`, `t`, `m`, `x` — взрыв виден на живом поле.

**Bug 3: ChaosDiamond/ChaosScout/карты с кубиком или монеткой — нет броска в меню**
- Причина: `MenuPreviewCardRandomAnimator` (no-op) перекрывал `CardRandomAnimator` на сцене `Menu_Board`.
- Фикс: удалена no-op регистрация, удален файл, объект `Random` перемещен в центр доски `(3.5, 3.5, 0)`, сцена сохранена.

**Bug 4: Blackout и BlackoutMax — одинаковый размер**
- Причина: backend `Blackout.cs` жестко брал `_configs.Value.Blackout_Normal`, игнорируя `payload.Type`.
- Фикс: выбор config по `payload.Type == CardType.Blackout_Max`.

**Примечание:** та же ошибка (жесткое `_Normal`) обнаружена и в `Smoke.cs`, `Frost.cs`, `FogOfWar.cs`.

### Следующий шаг

Нет. Phase 1-8 полностью завершены. Сборка и тесты проходят.
