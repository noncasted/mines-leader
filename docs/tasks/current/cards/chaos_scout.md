## Задача: ChaosScout — случайный Minefield Scout

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | ChaosScout |
| Мана | 2 |
| Цель | OwnBoard |
| Паттерн | линия (3-7) |
| Категория | Разведка |
| Пул | Ludic |
| Max-вариант | Нет |

### Описание эффекта

Рандомный Minefield Scout: линия случайной длины от 3 до 7, направление выбирает игрок. Мины флажатся, безопасные открываются.

### Сложность: 4

Minefield Scout + RNG на длину. Направление выбирает игрок (payload включает Direction). Backend: Random длина + стандартная линейная расчистка.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `ChaosScout = XXXX`
- [ ] 1.2. Config: ManaCost=2, MinLength=3, MaxLength=7, Target=OwnBoard
- [ ] 1.3. Payload: IBoardCardUsePayload с Position + Direction
- [ ] 1.4. Snapshot: int ActualLength + стандартные поля

#### 2. Backend

- [ ] 2.1. `ChaosScout.cs` — Random length, далее логика Minefield Scout
- [ ] 2.2-2.4. Стандартные

#### 4. Client

- [ ] 4.1. `CardChaosScoutAction.cs` — board drop с выбором направления
- [ ] 4.2-4.3. Стандартные

#### 5-7. Стандартные шаги

---

### Особые требования

- Payload содержит Direction (направление линии) — проверить как это реализовано в Minefield Scout
- Preview: показать линию мин/макс длины

### Зависимости

Нет. Аналог Minefield Scout.
