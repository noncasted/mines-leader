## Задача: Excavator — крестообразная расчистка

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | Excavator |
| Мана | 3 |
| Цель | OwnBoard |
| Паттерн | крест (3) |
| Категория | Разведка |
| Пул | Ordinary |
| Max-вариант | Да |

### Описание эффекта

Крестообразная расчистка: центральная клетка + 4 луча по 2 клетки в каждом кардинальном направлении (до 9 клеток). Мины флажатся, безопасные открываются.

### Сложность: 5

Аналог Bloodhound, но новый паттерн — крест вместо ромба. Нужен PatternShapes.Cross(). Бэкенд: флаг мин + reveal safe (BoardRevealer). Клиент: ICardDropPattern с крестом.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `Excavator = XXXX`, `Excavator_Max = XXXX`
- [ ] 1.2. Config: ManaCost=3, Size=3, Target=OwnBoard
- [ ] 1.3. Payload: IBoardCardUsePayload с Position
- [ ] 1.4. Snapshot: TargetPlayer + IReadOnlyList<Position> FlaggedCells + IReadOnlyList<Position> OpenedCells

#### 2. Backend

- [ ] 2.1. `Excavator.cs` — собрать крест, для каждой клетки: HasMine -> flag, else -> reveal
- [ ] 2.2. Нужна утилита для крестового паттерна (или использовать существующие)
- [ ] 2.3. CardFactory — два case (Normal + Max)
- [ ] 2.4. `ExcavatorStrategy.cs` — utility как Bloodhound (8 если >50% closed)
- [ ] 2.5. BotServiceExtensions

#### 4. Client

- [ ] 4.1. `CardExcavatorAction.cs` — board drop с Pattern: PatternShapes.Cross(size)
- [ ] 4.2. Возможно нужен PatternShapes.Cross() — проверить наличие
- [ ] 4.3-4.4. Стандартные

#### 5-7. Стандартные шаги

---

### Особые требования

- Новый паттерн Cross — 4 кардинальных направления (не диагональ)
- Проверить что PatternShapes имеет Cross, если нет — добавить
- Max-вариант: больший Size

### Зависимости

Нет. Аналогичен Bloodhound по структуре.
