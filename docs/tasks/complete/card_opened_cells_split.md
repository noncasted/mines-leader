## OpenedCells split

### Что сделано
- `ICardActionData` получил второе поле `UpdatedFreeCells` рядом с
  `OpenedCells`: теперь `OpenedCells` — только реально открытые клетки
  (анимация `PlayCellAction`), а `UpdatedFreeCells` — все клетки с
  пересчитанным `MinesAround` (включая free‑соседей бывших мин).
- Три карты (`Bloodhound`, `ErosionDozer`, `ZipZap`) раньше подмешивали
  `board.GetFreeNeighbours(...)` в `OpenedCells` — теперь соседи попадают
  только в `UpdatedFreeCells`. У остальных пяти reveal‑карт
  `UpdatedFreeCells = OpenedCells`.
- Клиентские `ICardActionSync.Sync` (8 файлов) обновляют `MinesAround` по
  `UpdatedFreeCells`. `CardActionSnapshotHandler.PlayActionAnimation`
  остался на `OpenedCells` — визуальный шум на соседях устранён.
- `SnapshotApplier.ApplyCardUseReveals` читает
  `UpdatedFreeCells ?? OpenedCells` для обратной совместимости со старыми
  снапшотами.

### Ключевые файлы
- `shared/Game/Snapshots/CardActionSnapshotRecord.cs` — контракт
  `ICardActionData` и 8 partial‑классов.
- `backend/Game/GamePlay/Cards/Scout/{Bloodhound,ErosionDozer,ZipZap}.cs` —
  источники split между `OpenedCells` и `UpdatedFreeCells`.
- `backend/Game/GamePlay/Snapshots/SnapshotApplier.cs` — чтение нового поля
  с fallback.
- `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs` — анимация по
  `OpenedCells`, не менялась, но теперь получает «чистый» набор.

### Заметки
- MemoryPack генерит Id по порядку свойств — `UpdatedFreeCells` добавлен
  **строго после** `OpenedCells` во всех 8 классах.
- `BoardSnapshotRecord.MinesAround` продолжает дублировать апдейты соседей
  через board‑снапшот; это идемпотентно и не создаёт багов.
