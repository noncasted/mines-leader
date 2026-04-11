# Дизайн-решения по картам

## Временные бонусы через PlayerModifier

Для всех карт, которые временно увеличивают ману/ходы/HP, используем систему `IModifiers` с отдельными `PlayerModifier` значениями:

```csharp
public enum PlayerModifier
{
    TrebuchetBoost = 0,
    AdditionalMana = 1,      // temp mana bonus (ManaSurge, ManaFountain, BloodPact, etc.)
    AdditionalMoves = 2,     // temp moves bonus (if needed beyond SetCurrent)
    AdditionalHealth = 3,    // temp health bonus (if needed)
}
```

### Как это работает

1. Карта устанавливает модификатор: `owner.Modifiers.Set(PlayerModifier.AdditionalMana, 3)`
2. Система Mana/Moves/Health учитывает модификатор в расчетах:
   - `Mana.SetCurrent()` — cap = `max + Modifiers.Get(AdditionalMana)`
   - `Moves.SetCurrent()` — аналогично с AdditionalMoves
   - `Health` — аналогично с AdditionalHealth
3. Модификатор снимается через систему эффектов (IRoundActionService.Schedule):
   - Карта создает DisposeAction, который вызывает `Modifiers.Reset(PlayerModifier.AdditionalMana)`
   - IRoundActionService вызывает DisposeAction через N раундов (обычно 0 = конец текущего хода)

### Затронутые карты

| Карта | Модификатор | Значение |
|-------|------------|----------|
| ManaSurge | AdditionalMana | +3 |
| ManaFountain | AdditionalMana | +1..+5 (d5) |
| BloodPact | AdditionalMana | +3 |
| DoubleOrNothing | AdditionalMana | x2 current / 0 |
| GamblersRuin | AdditionalMana | +2 (при орле) |

### Затронутые файлы при реализации

- `shared/Game/Cards/PlayerModifier.cs` -- добавить новые значения enum
- `backend/Game/GamePlay/Players/Mana.cs` -- учитывать AdditionalMana в SetCurrent (cap = max + additional)
- `backend/Game/GamePlay/Players/Moves.cs` -- учитывать AdditionalMoves (если нужно)
- `backend/Game/GamePlay/Players/Health.cs` -- учитывать AdditionalHealth (если нужно)
- Каждая карта создает свой DisposeAction через IRoundActionService для снятия модификатора
