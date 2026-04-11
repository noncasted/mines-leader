## Задача: FortuneBlast — случайный Trebuchet

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | FortuneBlast |
| Мана | 2 |
| Цель | OpponentBoard |
| Паттерн | ромб (1-4) |
| Категория | Кросс-борд |
| Пул | Ludic |
| Max-вариант | Нет |

### Описание эффекта

Рандомный Trebuchet: добавляет мины на поле противника ромбом случайного размера от 1 до 4.

### Сложность: 4

Trebuchet + RNG. Backend: Random.Range(1,5), далее стандартная ромбовая минировка.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `FortuneBlast = XXXX`
- [ ] 1.2. Config: ManaCost=2, MinSize=1, MaxSize=4, Target=OpponentBoard
- [ ] 1.3. Payload: IBoardCardUsePayload с Position
- [ ] 1.4. Snapshot: int ActualSize + IReadOnlyList<Position> MinedCells

#### 2. Backend

- [ ] 2.1. `FortuneBlast.cs` — Random size, далее логика Trebuchet
- [ ] 2.2-2.4. Стандартные

#### 4. Client

- [ ] 4.1. `CardFortuneBlastAction.cs` — opponent board drop
- [ ] 4.2-4.3. Стандартные

#### 5-7. Стандартные шаги

---

### Особые требования

- Preview: мин/макс зона на доске противника

### Зависимости

Нет. Аналог Trebuchet.
