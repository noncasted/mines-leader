## Задача: ChaosFog — случайный Smoke

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | ChaosFog |
| Мана | 2 |
| Цель | OpponentBoard |
| Паттерн | ромб (1-4) |
| Категория | Кросс-борд |
| Пул | Ludic |
| Max-вариант | Нет |

### Описание эффекта

Рандомный Smoke: накрывает поле противника дымом ромбом случайного размера от 1 до 4 на 3 раунда.

### Сложность: 5

Smoke + RNG. Временной эффект (3 раунда) + случайный размер. Нужен IRoundActionService для dispose.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `ChaosFog = XXXX`
- [ ] 1.2. Config: ManaCost=2, MinSize=1, MaxSize=4, Duration=3, Target=OpponentBoard
- [ ] 1.3. Payload: IBoardCardUsePayload с Position
- [ ] 1.4. Snapshot: int ActualSize + IReadOnlyList<Position> AffectedCells

#### 2. Backend

- [ ] 2.1. `ChaosFog.cs` — Random size, далее логика Smoke + Schedule dispose
- [ ] 2.2-2.4. Стандартные

#### 4-7. Стандартные шаги

---

### Особые требования

- Временной эффект (IRoundActionService) — 3 раунда
- Preview: мин/макс зона дыма

### Зависимости

Smoke (существующая карта — переиспользовать логику дыма).
