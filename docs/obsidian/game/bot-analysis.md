# Аналитика бота: почему проигрывает и как улучшить

## Исполнительная сводка

Бот проигрывает из-за **багнутого constraint-solving** и **непонимания экономики хода**. Флаги не тратят moves, но бот ограничен `FlagsPerRound = 3`. Человек ставит 7+ флагов за раунд, бот — только 3. Основная причина поражений: `Winner=Human | Reason=All opponent mines flagged`.

---

## 1. Почему бот проигрывал (анализ логов 2026-05-03)

### 1.1. Флаги бесплатны, но бот ставил только 3 за раунд

| Действие | Тратит moves | Лимит в конфиге |
|----------|-------------|-----------------|
| `SetFlag` | **Нет** | `FlagsPerRound = 3` |
| `OpenCell` | **Да** | `CellsOpenPerRound = 3` |
| `UseCard` | **Да** | `CardsUsePerRound = 4` |

Человек в Round 1 ставит **7 флагов + 1 открытие** — флаги не тратят moves, а бот искусственно ограничен 3 флагами.

### 1.2. Победа — через правильную разметку, не через открытие

`GetFlagWinner()` проверяет **только Taken ячейки**:

```csharp
// Lose conditions in GetFlagWinner()
HasMine == true  && IsFlagged == false  -> lose
HasMine == false && IsFlagged == true   -> lose
```

**Free ячейки игнорируются.** Открывать клетки для победы не нужно. Достаточно правильно разметить все закрытые ячейки.

### 1.3. Багнутый constraint-solving

Оригинальный `TryGetMineToFlag` не проверял условие constraint. Он просто брал ЛЮБУЮ незамеченную мину из соседей Free ячейки — даже если Free ячейка показывает 2, а вокруг 5 закрытых ячеек. Для observer это выглядело как "бот знает, где мины", но на самом деле это был чит.

Настоящий constraint-solving:

```csharp
// Level 1: все нефлаговые соседи = мины, если MinesAround == flags + unflagged
if (MinesAround == flaggedCount + unflaggedCount) -> flag all unflagged

// Level 1 (safe): все нефлаговые = безопасны, если MinesAround == flags
if (MinesAround == flaggedCount) -> open all unflagged
```

### 1.4. Неэффективное использование moves

Бот тратит moves на открытие клеток, хотя мог бы:
1. Поставить все флаги (бесплатно)
2. Использовать карты на уничтожение мин (ErosionDozer, ZipZap, Bloodhound)
3. Открывать клетки только если moves остались и нечего флажить

---

## 2. Что изменено в коде

### 2.1. `BotFlagAction.cs` — правильный constraint-solving

```csharp
// Старое: перебирал ВСЕ Taken ячейки, брал любую HasMine (чит)
// Новое: Level 1 + Level 2 constraint-solving
//   Level 1: single Free cell — MinesAround == flags + unflagged
//   Level 2: overlapping Free cells (subset reasoning)
//   HasMine используется ТОЛЬКО как oracle для проверки выводов
```

### 2.2. `BotRunner.cs` — фазы в правильном порядке

```csharp
// Старый порядок: карта -> флаг -> клетка -> карта -> флаг -> клетка
// Новый порядок:
//   Phase 1: Flags (бесплатные, критичные для победы)
//   Phase 2: Cards (тратят moves и mana)
//   Phase 3: Cells (если moves остались)
```

### 2.3. `BotCellAction.cs` — правильный constraint-solving + smart skip

```csharp
// Hard: skip opening если constraint-solving ничего не нашел
//   (бот не открывает клетки, если нет логически безопасных)
// Medium: constraint-solving для безопасных, fallback на случайное (как человек)
// Easy: случайное открытие (как новичок)
```

### 2.4. Стратегии карт

| Стратегия | Изменение |
|-----------|-----------|
| **ErosionDozer** | Выбирает позицию с максимальным количеством закрытых ячеек вокруг |
| **ZipZap** | Убран лишний `hasAdjacentFree`, ищет любую неразмеченную мину |
| **Smoke** | Приоритет снижен до 1.5 (не помогает победить по флагам) |
| **OpponentBomb** | Приоритет 0 если у бота есть неразмеченные мины |

---

## 3. Система уровней сложности (уже в коде)

В проекте уже реализована система профилей через `IBotProfileStrategy`:

```csharp
public interface IBotProfileStrategy
{
    bool CanUseMineKnowledge { get; }       // Знать HasMine (Hard)
    bool CanUseConstraintFlagging { get; }  // Constraint-solving (Medium/Hard)
    bool CanUseOptimalCards { get; }        // Оптимальные позиции карт (Medium/Hard)
    float FlagUtilityMultiplier { get; }
    float OpenUtilityMultiplier { get; }
}
```

Профиль выбирается через `BotConfigOptions.CurrentProfile`.

### 3.1. Easy — "Ученик сапера"

**Концепция:** Играет как человек-новичок. Не знает о победе по флагам.

| Параметр | Значение |
|----------|----------|
| `FlagsPerRound` | 2 |
| `CellsOpenPerRound` | 3 |
| `CardsUsePerRound` | 1 |
| `ActionDelay` | 1.0с |
| `CanUseMineKnowledge` | **false** |
| `CanUseConstraintFlagging` | **false** |
| `CanUseOptimalCards` | **false** |
| Флаги | Только если явно видит 1 закрытую ячейку рядом с "1" |
| Карты | Случайный выбор |
| Открытие | Случайные закрытые клетки (может наступить на мину) |

**Поведение:**
- Не ставит флаг, если не уверен
- Иногда наступает на мины
- Использует карты без стратегии
- Не оптимизирует под победу по флагам

### 3.2. Medium — "Опытный игрок"

**Концепция:** Знает правила, использует constraint-solving level 1, понимает экономику moves.

| Параметр | Значение |
|----------|----------|
| `FlagsPerRound` | 5 |
| `CellsOpenPerRound` | 2 |
| `CardsUsePerRound` | 2 |
| `ActionDelay` | 0.5с |
| `CanUseMineKnowledge` | **false** |
| `CanUseConstraintFlagging` | **true** |
| `CanUseOptimalCards` | **true** |
| Флаги | Constraint-solving level 1 |
| Карты | Учитывает mana-эффективность и ситуацию на доске |
| Открытие | Safe neighbours через constraint-solving, fallback на случайное |

**Поведение:**
- Ставит флаги логически (level 1)
- Использует карты разумно
- Может открывать случайные клетки, если нечего флажить
- Понимает, что флаги бесплатные

### 3.3. Hard — "Идеальный сапер"

**Концепция:** Constraint-solving level 1 + level 2. Использует `HasMine` только как oracle для проверки выводов. Не флажит все мины сразу — только те, которые можно логически доказать.

| Параметр | Значение |
|----------|----------|
| `FlagsPerRound` | 50 |
| `CellsOpenPerRound` | 1 |
| `CardsUsePerRound` | 2 |
| `ActionDelay` | 0.3с |
| `CanUseMineKnowledge` | **true** |
| `CanUseConstraintFlagging` | **true** |
| `CanUseOptimalCards` | **true** |
| Флаги | Constraint-solving level 1 + level 2 (subset reasoning) |
| Карты | Оптимальный выбор позиций и приоритетов |
| Открытие | Только если constraint-solving нашел безопасную клетку |

**Поведение:**
- Делает level 2 constraint-solving (перекрывающиеся Free ячейки)
- Использует `HasMine` только для проверки, что логический вывод верен
- Не флажит случайные мины — только доказанные
- Не открывает клетки без логического обоснования
- Приоритет: флаги (constraint) -> карты -> открытие (constraint)

---

## 4. Constraint-solving в коде

### 4.1. Level 1 — одиночная Free ячейка

```csharp
// Flag: если MinesAround == flags + unflagged -> все unflagged = мины
var minesRemaining = freeCell.MinesAround - flaggedCount;
if (minesRemaining == unflaggedNeighbors.Count)
    // flag all unflagged

// Open: если MinesAround == flags -> все оставшиеся = безопасны
if (freeCell.MinesAround == flaggedCount && unflaggedNeighbors.Count > 0)
    // open any unflagged
```

### 4.2. Level 2 — перекрывающиеся Free ячейки (subset reasoning)

```csharp
// Если соседи B — подмножество соседей A:
var diff = neighborsA.Except(neighborsB).ToList();
var minesDiff = a.MinesAround - b.MinesAround - flagsInDiff;

// minesDiff == diff.Count -> все в diff = мины
if (minesDiff == unflaggedDiff.Count) flag all unflagged in diff

// minesDiff == 0 -> все в diff = безопасны
if (minesDiff == 0) open all unflagged in diff
```

### 4.3. Oracle-проверка (Hard)

Hard бот знает `HasMine`, но использует его только для проверки:

```csharp
var allMines = unflaggedNeighbors.All(t => t.HasMine == true);
if (allMines) // Логический вывод подтвержден oracle
    target = unflaggedNeighbors[0].Position;
```

Это означает, что Hard бот никогда не флажит клетку, которую нельзя было бы доказать логически. Для observer это выглядит как "очень умный бот", а не "читер".

---

## 5. Рекомендации по конфигурации

### Для PvE режима (тренировка)

| Уровень | `FlagsPerRound` | `CardsUsePerRound` | `CellsOpenPerRound` | `MinRoundTime` |
|---------|-----------------|--------------------|---------------------|----------------|
| Easy    | 2               | 1                  | 3                   | 10с            |
| Medium  | 5               | 2                  | 2                   | 7с             |
| Hard    | 50              | 2                  | 1                   | 5с             |

### Для матчмейкинга (если бот заменяет игрока)

Бот должен быть **Medium**, чтобы:
- Не выглядеть как читер
- Давать реалистичный опыт
- Не разрушать баланс

---

## 6. Что еще можно улучшить

1. **Level 3 constraint-solving:** SAT/SMT solver для более сложных выводов из перекрывающихся Free ячеек.

2. **Предиктивное использование карт:** ErosionDozer на краю открытой зоны для максимального chain-reveal.

3. **Учет колоды противника:** Если у противника много Trebuchet — прятать мины в углах.

4. **Адаптивная сложность:** Если winrate > 70% — повышать, < 30% — понижать.

5. **Симуляция ходов:** Hard бот мог бы симулировать ходы на 2-3 раунда вперед.
