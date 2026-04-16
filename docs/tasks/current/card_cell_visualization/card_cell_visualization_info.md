## Задача: Визуализация использования карт на поле

### Цель
Сейчас когда оппонент использует карту, которая меняет клетки на поле — поле просто молча меняется. Нужно добавить визуальный фидбек:

1. **Разделить снапшот карточного действия на две фазы:**
   - Фаза 1 (Target): отправить снапшот с клетками, которые были **выбраны** для действия (target cells)
   - Фаза 2 (Action): после манипуляций с полем отправить снапшот со всеми **заафекченными** клетками (включая клетки, открытые BoardRevealer)

2. **Создать компонент CellVisuals** с двумя анимациями:
   - `PlayCellTarget` — анимация "клетка выбрана как цель" (фаза 1)
   - `PlayCellAction` — анимация "клетка изменена" (фаза 2)

3. **Перейти на ручное управление снапшотами в картах:**
   - Сейчас MoveSnapshot автоматически слушает все BoardEvents и пишет общий снапшот
   - Нужно для карт вручную формировать снапшоты с разделением на target/action фазы
   - Собирать клетки открытые BoardRevealer в action-фазу

4. **CellOpenAction (ручное открытие клеток) оставить как есть**

### Контекст
- Текущая архитектура: `GameCommand` создает `MoveSnapshot`, подписывается на `BoardEvents`, после выполнения команды `SnapshotSender.Send()` шлет один снапшот
- Карты типа ZipZap уже используют `snapshot.Lock()/Unlock()` чтобы скрыть промежуточные состояния
- `CardUseCommand` вызывает `card.Use()`, получает `CardUseResult` с `ICardActionData`, записывает `RecordCardUse`
- На клиенте `SnapshotReceiver` ставит записи в очередь и обрабатывает последовательно через зарегистрированные хендлеры
- Один эффект пока для всех карт — детализация по типам карт будет позже

### Шаги реализации

**1. Новый тип снапшот-записи: BoardTargetSnapshot**
  1.1. Добавить `BoardTargetSnapshotRecord` в `shared/Game/Snapshots/BoardSnapshotRecord.cs` — запись с `IReadOnlyList<Position> Targets` и `Guid BoardOwnerId`
  1.2. Зарегистрировать в `MemoryPackUnion` в `SharedMoveSnapshot.cs` или `BoardSnapshotRecord.cs`

**2. Backend: ручное управление снапшотами в CardUseCommand**
  2.1. Изменить `CardUseCommand` (`backend/Game/GamePlay/Commands/CardUseCommand.cs`):
    - Перед `card.Use()` — лочить снапшот (`snapshot.Lock()`)
    - `card.Use()` возвращает target cells в `ICardActionData`
    - После `card.Use()` — записать `BoardTargetSnapshot` с target cells (если есть)
    - Анлочить снапшот (`snapshot.Unlock()`)
    - Вызвать `board.OnUpdated()` — теперь BoardEvents запишут все изменения (action cells)
    - Записать `RecordCardUse`
  2.2. Убрать `snapshot.Lock()/Unlock()` из `ZipZap.cs` — теперь это делает `CardUseCommand`
  2.3. Добавить в `ICardActionData` поле `IReadOnlyList<Position> TargetCells` — список клеток-целей

**3. Backend: собрать affected cells включая BoardRevealer**
  3.1. Модифицировать `BoardRevealer` (`backend/Game/GamePlay/Board/BoardRevealer.cs`):
    - Возвращать список открытых клеток (сейчас void)
    - Или: отслеживать revealed cells через `BoardEvents` после unlock
  3.2. Альтернатива: раз снапшот после unlock записывает все CellSet/CellFree/Mines — affected cells уже будут собраны автоматически. Нужно только разделение на две фазы.

**4. Shared: добавить TargetCells в ICardActionData**
  4.1. Добавить `IReadOnlyList<Position>? TargetCells` в `ICardActionData` (`shared/Game/Snapshots/CardActionSnapshotRecord.cs`)
  4.2. Каждая карта, модифицирующая поле, заполняет `TargetCells` — позиции, выбранные для действия

**5. Backend: обновить карты, меняющие поле**
  5.1. `ZipZap.cs` — Targets уже есть, убрать Lock/Unlock, заполнять TargetCells
  5.2. `Bloodhound.cs` — добавить TargetCells (клетки в rhombus)
  5.3. `ErosionDozer.cs` — добавить TargetCells
  5.4. `Trebuchet.cs` — добавить TargetCells (на доске оппонента)
  5.5. `OpponentBomb.cs` — добавить TargetCells
  5.6. Остальные карты, меняющие клетки — аналогично

**6. Client: создать CellVisuals компонент**
  6.1. Создать `CellVisuals.cs` в `client/Assets/GamePlay/Boards/Cells/` [новый файл — добавить в .csproj]
    - MonoBehaviour с `[SerializeField]` анимациями
    - `PlayCellTarget(IReadOnlyLifetime)` — анимация "цель выбрана"
    - `PlayCellAction(IReadOnlyLifetime)` — анимация "клетка изменена"
  6.2. Добавить `[SerializeField] CellVisuals _visuals` в `CellView.cs`
  6.3. Публичный доступ `CellVisuals Visuals => _visuals`

**7. Client: новый снапшот-хендлер для target-фазы**
  7.1. Создать `BoardTargetSnapshotHandler` в `client/Assets/GamePlay/Sync/` [новый файл]
    - При получении `BoardTargetSnapshot` — вызвать `cell.Visuals.PlayCellTarget()` для каждой target-позиции
  7.2. Зарегистрировать в `GamePlaySyncExtensions.cs`

**8. Client: добавить action-анимацию в BoardSnapshotHandler**
  8.1. Модифицировать `BoardSnapshotHandler` (`client/Assets/GamePlay/Sync/BoardSnapshotHandler.cs`):
    - При обработке `CellFree`/`CellTaken` — вызывать `cell.Visuals.PlayCellAction()` если изменение вызвано картой (не ручным открытием)
  8.2. Или: action-анимация триггерится через `CardActionSnapshotHandler` после применения board changes

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Game/GamePlay/Commands/CardUseCommand.cs` | Центральная точка: разделение снапшотов на фазы |
| `backend/Game/GamePlay/Context/MoveSnapshot.cs` | Lock/Unlock механизм, запись рекордов |
| `backend/Game/GamePlay/Commands/Common/GameCommand.cs` | Создание snapshot, подписка на BoardEvents, отправка |
| `backend/Game/GamePlay/Board/BoardRevealer.cs` | Каскадное открытие клеток — нужно собрать affected cells |
| `backend/Game/GamePlay/Cards/Scout/ZipZap.cs` | Эталонная карта с Lock/Unlock — рефакторинг |
| `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | ICardActionData — добавить TargetCells |
| `shared/Game/Snapshots/BoardSnapshotRecord.cs` | Новый тип записи BoardTargetSnapshot |
| `client/Assets/GamePlay/Boards/Cells/CellView.cs` | Добавить CellVisuals |
| `client/Assets/GamePlay/Boards/Cells/CellAnimator.cs` | Образец анимационного компонента |
| `client/Assets/GamePlay/Sync/BoardSnapshotHandler.cs` | Привязка action-анимаций |
| `client/Assets/GamePlay/Sync/CardActionSnapshotHandler.cs` | Обработка card use — координация target/action |
| `client/Assets/GamePlay/Sync/GamePlaySyncExtensions.cs` | Регистрация нового хендлера |
| `client/Assets/GamePlay/Services/SnapshotReceiver.cs` | Очередь снапшотов — порядок обработки |

### Документация к прочтению
- `rules/REACTIVE.md` — ViewableProperty/EventSource для анимаций
- `rules/LIFETIMES.md` — Lifetime для анимаций (PlayCellTarget/PlayCellAction)
- `rules/CODE_STYLE.md` — стиль кода, member order
- `rules/COMMON_MISTAKES.md` — типичные ошибки

### Риски
1. **Порядок снапшотов критичен:** target-снапшот должен прийти ДО board-changes снапшота. На клиенте `SnapshotReceiver` обрабатывает очередь последовательно — порядок записей в `_records` определяет порядок обработки.
2. **Lock/Unlock в картах:** сейчас только ZipZap использует Lock/Unlock. При переносе в CardUseCommand нужно убедиться, что карты не делают своих board.OnUpdated() до unlock.
3. **CellOpenAction остается как есть:** нужен способ отличить board changes от ручного открытия vs от карты на клиенте, чтобы PlayCellAction не триггерился при ручном открытии.
4. **Карты-кроссборд:** Trebuchet, OpponentBomb и др. меняют доску оппонента — target/action снапшоты должны указывать правильный BoardOwnerId.
