## Отображение текущих модификаторов игрока

### Что сделано
- Backend хранит модификаторы как `Dictionary<Guid, IModifierSource>`; публичный `Values` — сумма источников.
- Каждый раунд `ModifierRoundAction.Tick()` уменьшает `TurnsToEnd` и шлёт `DurationalModifierOverview` на клиент.
- Overlay `Game_Overlay`: столбец иконок слева вверху, счётчик ходов, тултип из `ModifierDescriptionsConfig`.

### Ключевые файлы
- `backend/Game/GamePlay/Players/Modifiers.cs`
- `shared/Game/Player/DurationalModifierOverview.cs`
- `client/Assets/GamePlay/UI/Overlay/PlayerModifiersView.cs`
- `client/Assets/GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs`

### Заметки
- Удаление overview сигнализируется `TurnsToEnd == 0`, отдельного `IsRemoved` нет.
- Обновлять overview inplace через `ViewableList.NotifyChangedAt`, не RemoveAt+Add — иначе мигает UI и ломаются item lifetimes.
- Shield без длительности (`TurnsToEnd = -1`); `OpenCellCommand` снимает один источник через `RemoveOne`.
