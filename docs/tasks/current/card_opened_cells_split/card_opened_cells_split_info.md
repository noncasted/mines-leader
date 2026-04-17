## Задача: Разделить OpenedCells на OpenedCells + UpdatedFreeCells для ICardActionData

### Цель

Текущий `ICardActionData.OpenedCells` смешивает два разных множества клеток:
- клетки, которые мы реально открыли (перешли из Taken → Free);
- соседние уже-Free клетки, чей `MinesAround` изменился из‑за исчезновения мины.

Из‑за этого клиент проигрывает анимацию `PlayCellAction` на всех таких клетках,
что создаёт визуальный шум для соседей, которые не были открыты.

Надо разделить данные на два независимых поля:
- `OpenedCells` — только реально открытые клетки (и бывшие минные, которые мы
  превратили в Free). Именно они получают анимацию `PlayCellAction` на клиенте.
- `UpdatedFreeCells` — `OpenedCells` + соседи, у которых пересчитался
  `MinesAround`. Именно по этому списку клиент вызывает
  `cell.EnsureFree().OnMinesUpdated(...)`.

### Контекст

- Анимационный путь:
  `CardActionSnapshotHandler.PlayActionAnimation` в
  `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs:92` берёт
  `data.OpenedCells?.Select(o => o.Position)` и дёргает `PlayCellAction` — это
  должно остаться на `OpenedCells` (но уже без соседей).
- Апдейт визуалов клеток живёт в восьми `ICardActionSync<T>.Sync` под
  `client/Assets/GamePlay/Cards/Entities/Actions/...` — они должны
  переключиться на `payload.UpdatedFreeCells`.
- На бэкенде ровно три карты сейчас подмешивают соседей через
  `board.GetFreeNeighbours(...)` — **Bloodhound**, **ErosionDozer**, **ZipZap**.
  Остальные пять reveal‑карт (**OpponentBomb**, **MinefieldScout**,
  **Excavator**, **ChaosDiamond**, **ChaosScout**) просто возвращают то, что
  вернул `board.Revealer.Reveal(...)`; для них `UpdatedFreeCells` совпадает с
  `OpenedCells`.
- `SnapshotApplier.ApplyCardUseReveals` в
  `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs:87` тоже читает
  `OpenedCells` — его нужно переключить на `UpdatedFreeCells`, чтобы
  `GameStateSnapshot` корректно обновлял `MinesAround` у соседей.
- `SnapshotDiffGuardTests.BloodhoundSnapshotSequenceTests` проверяет
  `data.OpenedCells.Should().NotBeEmpty()` — он продолжит работать, но имеет
  смысл прогнать набор тестов.

### Шаги реализации

**1. Расширить shared‑контракт**
  1.1. В `shared/Game/Snapshots/CardActionSnapshotRecord.cs` добавить в
       `ICardActionData` свойство `IReadOnlyList<OpenedCell>? UpdatedFreeCells => null;`
       рядом с `OpenedCells`.
  1.2. В восьми партиальных классах, у которых есть `OpenedCells`, добавить
       публичное свойство `UpdatedFreeCells`: `ZipZap`, `Bloodhound`,
       `ErosionDozer`, `OpponentBomb`, `MinefieldScout`, `Excavator`,
       `ChaosDiamond`, `ChaosScout`. Атрибуты `[MemoryPackable]` уже стоят — новые
       `Id` назначать не требуется, MemoryPack генерит их по порядку, но
       порядок свойств должен быть идентичным между клиентом и бэкендом (обе
       стороны используют один и тот же `shared/` — нарушений не возникнет).

**2. Заполнять оба поля на бэкенде**
  Для каждого файла ниже: собрать `openedCells` по набору «реально открытых +
  бывших минных» позиций; собрать `updatedFreeCells` = `openedCells` ∪
  `GetFreeNeighbours(...)` (там, где это было раньше). В `RecordCardUse`
  передавать оба поля.

  2.1. `backend/Game/GamePlay/Cards/Scout/Bloodhound.cs` — `OpenedCells`
       берутся из `revealed ∪ minePositions`, `UpdatedFreeCells` добавляют
       `board.GetFreeNeighbours(minePositions)`.
  2.2. `backend/Game/GamePlay/Cards/Scout/ErosionDozer.cs` — аналогично
       Bloodhound.
  2.3. `backend/Game/GamePlay/Cards/Scout/ZipZap.cs` — `OpenedCells` из
       `revealed ∪ targetPositions`, `UpdatedFreeCells` добавляют
       `board.GetFreeNeighbours(targetPositions)`.
  2.4. `backend/Game/GamePlay/Cards/CrossBoard/OpponentBomb.cs` —
       `OpenedCells = revealed`, `UpdatedFreeCells = OpenedCells`.
  2.5. `backend/Game/GamePlay/Cards/Scout/MinefieldScout.cs` — как OpponentBomb.
  2.6. `backend/Game/GamePlay/Cards/Scout/Excavator.cs` — как OpponentBomb.
  2.7. `backend/Game/GamePlay/Cards/Scout/ChaosDiamond.cs` — как OpponentBomb.
  2.8. `backend/Game/GamePlay/Cards/Scout/ChaosScout.cs` — как OpponentBomb.

**3. Обновить клиентский апдейт клеток**
  В каждом файле заменить цикл `foreach (var opened in payload.OpenedCells)` на
  `payload.UpdatedFreeCells` в блоке, который вызывает
  `cell.EnsureFree().OnMinesUpdated(opened.MinesAround)`. Null‑чек перенести
  на `UpdatedFreeCells`.

  3.1. `client/Assets/GamePlay/Cards/Entities/Actions/Scout/CardBloodhoundAction.cs`
  3.2. `client/Assets/GamePlay/Cards/Entities/Actions/Scout/CardErosionDozerAction.cs`
  3.3. `client/Assets/GamePlay/Cards/Entities/Actions/ZipZap/CardZipZapAction.cs`
  3.4. `client/Assets/GamePlay/Cards/Entities/Actions/CrossBoard/CardOpponentBombAction.cs`
  3.5. `client/Assets/GamePlay/Cards/Entities/Actions/Scout/CardMinefieldScoutAction.cs`
  3.6. `client/Assets/GamePlay/Cards/Entities/Actions/Scout/CardExcavatorAction.cs`
  3.7. `client/Assets/GamePlay/Cards/Entities/Actions/Scout/CardChaosDiamondAction.cs`
  3.8. `client/Assets/GamePlay/Cards/Entities/Actions/Scout/CardChaosScoutAction.cs`

**4. Убедиться, что анимация играется только по OpenedCells**
  4.1. `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs:92`
       `PlayActionAnimation` уже использует `data.OpenedCells` —
       изменения кода не нужны, но нужно убедиться, что после шага 2 туда
       больше не попадают соседи.

**5. Переключить GameStateSnapshot на UpdatedFreeCells**
  5.1. `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs:87`
       `ApplyCardUseReveals` заменить чтение `data.OpenedCells` на
       `data.UpdatedFreeCells ?? data.OpenedCells` (fallback на случай старых
       снапшотов). Это гарантирует, что `MinesAround` соседей попадёт в
       `GameStateSnapshot`.

**6. Проверить тесты**
  6.1. Прогнать `backend/Tools/Tests/Game/SnapshotDiffGuardTests.cs` —
       `BloodhoundSnapshotSequenceTests` по‑прежнему должен видеть непустой
       `OpenedCells`.
  6.2. Прогнать `backend/Tools/Tests/Game/MineClusterTests.cs` и
       `backend/Tools/Tests/Game/PlayerCardTests.cs` — не должно быть регрессий.

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | Определение `ICardActionData` и восьми data‑классов; сюда добавляем `UpdatedFreeCells`. |
| `backend/Game/GamePlay/Cards/Scout/Bloodhound.cs`, `ErosionDozer.cs`, `ZipZap.cs` | Карты, которые сейчас подмешивают `GetFreeNeighbours(...)` в `OpenedCells`. |
| `backend/Game/GamePlay/Cards/CrossBoard/OpponentBomb.cs`, `backend/Game/GamePlay/Cards/Scout/MinefieldScout.cs`, `Excavator.cs`, `ChaosDiamond.cs`, `ChaosScout.cs` | Остальные reveal‑карты с `OpenedCells`; у них `UpdatedFreeCells == OpenedCells`. |
| `backend/Game/GamePlay/Board/Extensions/BoardPositionsExtensions.cs:58` | `GetFreeNeighbours` — остаётся как есть, просто меняется точка потребления. |
| `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` | `ApplyCardUseReveals` читает `OpenedCells` — переключаем на `UpdatedFreeCells`. |
| `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs` | `PlayActionAnimation` берёт позиции из `data.OpenedCells` — после шага 2 будет играть только по реально открытым клеткам. |
| Восемь файлов в `client/Assets/GamePlay/Cards/Entities/Actions/...` | `ICardActionSync.Sync` обновляет визуал клеток — переключаем цикл на `UpdatedFreeCells`. |

### Документация к прочтению

- `.claude/docs/GAMEPLAY.md` — раздел Snapshot Sync, чтобы не сломать
  инвариант «state change → snapshot.Record*».
- `.claude/docs/CLAUDE_MISTAKES.md` (Lesson 7, пример с Bloodhound) — там
  зафиксирован контракт, по которому reveal‑карты отдают позиции через
  `CardActionSnapshot.X.OpenedCells`; обновить понимание: теперь поля два.

### Риски

- **Обратная совместимость MemoryPack**: новое свойство в partial‑классе
  добавляется в конец, старые сериализованные снапшоты десериализуются с
  `UpdatedFreeCells == null`. Поэтому в `SnapshotApplier` оставить fallback
  на `OpenedCells`, иначе `GameStateSnapshot` потеряет `MinesAround` на
  старых записях.
- **Порядок полей в partial‑классах** критичен для MemoryPack‑генерации —
  добавлять `UpdatedFreeCells` **после** `OpenedCells` во всех восьми
  классах.
- **Дубли между карт‑снапшотом и board‑снапшотом**: `MinesAround` соседей
  уже летит через `BoardSnapshotRecord.MinesAround` (BoardRevealer
  вызывает `_scanner.Recalculate(snapshot)`). Клиентский
  `BoardSnapshotHandler.MinesAround.Execute` тоже дёргает
  `EnsureFree().OnMinesUpdated(...)`. Это не проблема —
  идемпотентно, — но стоит держать в голове, что часть апдейтов
  продублируется.
