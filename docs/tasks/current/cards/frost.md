## Задача: Frost — заморозка клеток на 1 раунд

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | Frost |
| Мана | 2 |
| Цель | OpponentBoard |
| Паттерн | ромб (2) |
| Категория | Кросс-борд |
| Пул | Ordinary |
| Max-вариант | Да |

### Описание эффекта

Замораживает клетки в ромбе на поле противника: нельзя открывать и флажить 1 раунд. Заморозка снимается автоматически.

### Сложность: 7

Новое состояние клетки: Frozen. Нужна интеграция с OpenCellCommand и CellFlagAction — блокировка действий на замороженных клетках. Временной эффект (IRoundActionService). Визуал заморозки на клиенте.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `Frost = XXXX`, `Frost_Max = XXXX`
- [ ] 1.2. Config: ManaCost=2, Size=2, Duration=1, Target=OpponentBoard
- [ ] 1.3. Payload: IBoardCardUsePayload с Position
- [ ] 1.4. Snapshot: IReadOnlyList<Position> FrozenCells

#### 2. Backend

- [ ] 2.1. `Frost.cs` — пометить клетки как Frozen, Schedule dispose через 1 раунд
- [ ] 2.2. Новое состояние клетки: добавить IsFrozen флаг в CellState или отдельный tracking
- [ ] 2.3. Интеграция с `OpenCellCommand` — проверка IsFrozen, отклонить если заморожена
- [ ] 2.4. Интеграция с `CellFlagAction` — аналогично
- [ ] 2.5. `FrostDisposeAction : IRoundAction` — снятие заморозки
- [ ] 2.6. CardFactory + `FrostStrategy.cs` + BotServiceExtensions

#### 4. Client

- [ ] 4.1. `CardFrostAction.cs` — opponent board drop с Rhombus pattern
- [ ] 4.2. Визуал: ледяной эффект на клетках (новый CellView state)
- [ ] 4.3. Sync: снятие визуала при unfrost
- [ ] 4.4. Стандартные регистрации

#### 5-7. Стандартные шаги

---

### Особые требования

- Новое состояние клетки (IsFrozen) — изменение в shared модели CellState
- Интеграция с двумя командами (OpenCell + Flag) — блокировка действий
- Временной эффект с автоснятием
- Клиент: новый визуальный эффект заморозки

### Зависимости

Нет. Но IsFrozen может переиспользоваться другими картами.
