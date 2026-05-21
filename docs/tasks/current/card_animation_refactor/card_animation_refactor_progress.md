---
task: card_animation_refactor
updated: 2026-05-21
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| Phase 1: ICardActionData cleanup | [x] | `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | — |
| Phase 2: IBoardCellsAnimator | [x] | 3 новых файла + DI регистрации | — |
| Phase 3-4: Упрощение хендлеров | [x] | Handler/PreviewPlayer/Board очищены | — |
| Phase 5: Рефактор Snapshot классов | [x] | ~52 файла обновлены | — |
| Phase 6: Registry & DI | [x] | DI регистрации обновлены | — |
| Phase 7: Backend | [x] | 0 errors, 641 tests passed | — |
| Phase 8: Синхронизация snapshot типов | [x] | Backend audit complete, nullable removed, null checks removed, 0 errors | — |

## Заметки

### [2026-05-21] Phase 8 — Завершено

**Выполнено:**
1. Аудит backend (`backend/Game/GamePlay/Cards/**/*.cs`) — составлена полная матрица "карта → какие поля гарантированы"
2. Обновлены shared snapshot типы (`shared/Game/Snapshots/CardActionSnapshotRecord.cs`):
   - Удалены неиспользуемые поля: `TargetCells` из `Gravedigger`, `TrebuchetAimer`, `Purge`
   - Убран `?` (nullable) у полей которые backend всегда устанавливает: `TargetCells`, `OpenedCells`, `UpdatedFreeCells`, `TakenCells`, `UnflaggedCells`, `FlaggedCells` и др.
3. Обновлены клиентские Snapshot.Sync методы (~26 файлов):
   - Убраны `if (payload.X != null)` проверки там, где backend гарантирует данные
   - Удалены блоки `PlayTargetAnimation` у карт `Gravedigger`, `TrebuchetAimer`, `Purge` (поле удалено из snapshot)
   - Сохранены проверки `Count > 0` для методов анимаций (empty list → skip animation)
4. `SnapshotApplier` — не требовал обновления (reflection читает `UpdatedFreeCells`/`OpenedCells`, удалённые поля — `TargetCells` — не участвуют в reflection)
5. MemoryPack union IDs — не изменялись (union IDs привязаны к типам, не к полям внутри типов)

**Evidence:**
- Backend: `dotnet build` — 0 errors, 17 warnings (pre-existing)
- Backend: `dotnet test` — 641 passed, 0 failed
- Client: `dotnet build client/client.slnx` — 0 errors, 62 warnings (pre-existing)

**Карты с удалёнными/изменёнными полями:**
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

### [2026-05-21 00:33] Разделение сессии
**Текущий момент остановки:** Начат Phase 8 — синхронизация snapshot типов между backend и client. Пользователь указал, что в клиентских Snapshot.Sync методах остались проверки `if (payload.TargetCells != null ...)` которые по подходу fail fast должны быть убраны (если backend не отдал поле — это ошибка backend, падаем). Также в shared snapshot типах остались nullable (`IReadOnlyList<Position>?`) на полях которые backend всегда отдаёт.

**Важные находки:**
- Все 641 backend-тест проходят после Phase 1-7
- `SnapshotApplier` уже использует reflection — при удалении полей из snapshot типов нужно обновить `GetOpenedCells`
- В `ZipZapAction.cs` остался единственный прямой доступ к `_context.GetPlayer().Board` — это ожидаемо (lightning line VFX)
- Поля `TargetCells`/`OpenedCells`/`UpdatedFreeCells` удалены из `ICardActionData` interface, но остались в конкретных snapshot типах
- Карты без позиционных данных (Medic, Siphon и т.д.) НЕ имеют `TargetCells` в snapshot типе — для них ничего менять не нужно

**Измененные файлы (Phase 8):**
| Файл | Что изменено |
|------|-------------|
| `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | Убран nullable у гарантированных полей; удалены неиспользуемые `TargetCells` |
| `client/Assets/GamePlay/Cards/Entities/Actions/*/*.cs` | ~26 файлов — убраны `!= null` проверки, удалены лишние блоки |

### [2026-05-21 00:33] Предыдущие заметки

#### Phase 1: Shared — ICardActionData cleanup
- [x] Удалены `TargetCells`, `OpenedCells`, `UpdatedFreeCells` из `ICardActionData`
- [x] Snapshot типы оставлены без изменений

#### Phase 2: Client — IBoardCellsAnimator
- [x] Создан `IBoardCellsAnimator` interface (`client/Assets/GamePlay/Boards/Cells/`)
- [x] Создан `BoardCellsAnimator` (gameplay, через `IGameContext`)
- [x] Создан `MenuBoardCellsAnimator` (menu, через `IMenuBoard`)
- [x] Зарегистрированы в DI (`BoardsServicesExtensions.cs`, `MenuLoopExtensions.cs`)
- [x] Созданы `.meta` файлы для новых Unity-скриптов

#### Phase 3-4: Упрощение хендлеров
- [x] `CardActionSnapshotHandler` — удалены `PlayTargetAnimation`/`PlayActionAnimation`, оставлен только `card.Use()`
- [x] `MenuCardPreviewPlayer` — удалены `_menuBoard.PlayTargetAnimation`/`PlayActionAnimation`, `targetFallback`, оставлен только `_syncRegistry.Dispatch()`
- [x] `MenuBoard` — удалены `PlayTargetAnimation`/`PlayActionAnimation`/`ApplyUpdatedCells`/`PlayCellsAnimation`
- [x] `IMenuBoard` — удалены соответствующие методы из интерфейса
- [x] Убран неиспользуемый `using System.Linq` из `MenuBoard.cs`

#### Phase 5: Рефактор ~52 Snapshot классов
- [x] Все Snapshot-классы получили `IBoardCellsAnimator` в конструктор
- [x] Карты с `TargetCells` + `OpenedCells` — добавлены `PlayTargetAnimation` + `PlayActionAnimation` в начало `Sync`
- [x] Карты только с `TargetCells` — добавлена `PlayTargetAnimation`
- [x] Карты без позиционных данных (Medic, Siphon и т.д.) — `IBoardCellsAnimator` добавлен для консистентности
- [x] ZipZap — сохранена кастомная логика молнии + camera shake, `_animator` для взрывов и `OpenCells`
- [x] Smoke/Blackout/Frost/FogOfWar/ChaosFog/ThermalVision/FortuneCookie — `AddEffect` через `_animator`
- [x] MinefieldScout/Sonar/Excavator/ChaosDiamond/ChaosScout — `FlagCell` через `_animator`
- [x] OpponentFlagErase/OpponentFlagReshuffle — `UnflagCell` через `_animator`
- [x] Trebuchet/ChainReaction/MineCluster/CarpetBomb/FortuneBlast — `EnsureTaken` через `_animator`
- [x] Убраны лишние `using GamePlay.Loop` и `using GamePlay.Boards` где `_gameContext` больше не нужен

#### Phase 6: DI регистрация
- [x] `MenuLoopExtensions.cs` — добавлена регистрация `MenuBoardCellsAnimator`
- [x] `BoardsServicesExtensions.cs` — добавлена регистрация `BoardCellsAnimator`
- [x] Проверено: `CardStatesExtensions.AddCardActionSync` использует `AddCardActionSyncResolver` — DI резолвит `IBoardCellsAnimator` автоматически

#### Phase 7: Backend
- [x] Исправлен `SnapshotApplier.ApplyCardUseReveals` — теперь использует reflection для чтения `UpdatedFreeCells`/`OpenedCells`
- [x] Добавлен `using System.Reflection`
- [x] Сборка backend проходит без ошибок (0 errors, 17 warnings — pre-existing)
- [x] Все 641 backend-теста проходят

#### Финальная проверка Phase 1-7
- [x] `ICardActionData` не имеет `TargetCells`/`OpenedCells`/`UpdatedFreeCells`
- [x] `MenuCardPreviewPlayer` не вызывает `PlayTargetAnimation`/`PlayActionAnimation`
- [x] `CardActionSnapshotHandler` не вызывает `PlayTargetAnimation`/`PlayActionAnimation`
- [x] Каждый `ICardActionSync` сам вызывает `animator.PlayTargetAnimation`/`PlayActionAnimation` когда нужно
- [x] Menu preview: `_syncRegistry.Dispatch` → полный цикл анимаций
- [x] Gameplay: `card.Use()` → полный цикл анимаций
- [x] Компиляция backend без ошибок
- [x] Все backend тесты проходят

### [2026-05-21] Bugfixes — 4 бага

**Bug 1: ZipZap — пропала анимация в меню**
- Причина: `CardZipZapAction.Snapshot` отсутствовал в `MenuCardActionSyncExtensions.cs` (регистрация menu DI).
- Фикс: добавлена строка `builder.AddCardActionSyncResolver<CardZipZapAction.Snapshot, CardActionSnapshot.ZipZap>()`.

**Bug 2: OpponentBomb — сплошное заполненное поле в preview**
- Причина: layout в `CardPreviewScenarios.cs` был полностью из `t` (все клетки открыты), неинтересный визуально.
- Фикс: layout переделан на смешанное поле с `_` (закрытые), `t` (открытые без мин), `m` (открытые с минами) и `x` (target с миной). Взрыв теперь виден на живом поле.

**Bug 3: ChaosDiamond/ChaosScout/карты с кубиком или монеткой — нет броска в меню**
- Причина: в `MenuLoopExtensions.cs` регистрировался `MenuPreviewCardRandomAnimator` — no-op реализация `ICardRandomAnimator`, которая молча завершала задачи. Она перекрывала `CardRandomAnimator` (MonoBehaviour со SpriteRenderer и анимацией), уже присутствующий на сцене `Menu_Board`.
- Фикс:
  - Удалена регистрация `MenuPreviewCardRandomAnimator` из `MenuLoopExtensions.cs`.
  - Удален файл `MenuPreviewCardRandomAnimator.cs`.
  - Объект `Random` на сцене `Menu_Board` перемещен в центр доски `(3.5, 3.5, 0)` (был `(10, 2, 0)` — за пределами камеры).
  - Сцена `Menu_Board` сохранена.

**Bug 4: Blackout и BlackoutMax — одинаковый размер**
- Причина: backend `Blackout.cs` жестко брал `_configs.Value.Blackout_Normal`, игнорируя `payload.Type`. Из-за этого Blackout_Max использовал тот же config что и обычный Blackout.
- Фикс: `var config = payload.Type == CardType.Blackout_Max ? _configs.Value.Blackout_Max : _configs.Value.Blackout_Normal;`.

**Примечание:** при аудите backend обнаружена та же ошибка (жесткое `_Normal`) и в `Smoke.cs`, `Frost.cs`, `FogOfWar.cs` — все они игнорируют `_Max` версии карт.

**Evidence:**
- Backend: `dotnet build` — 0 errors
- Backend: `dotnet test` — 641 passed
- Client: `dotnet build client/client.slnx` — 0 errors
- Unity: compilation — 0 errors
