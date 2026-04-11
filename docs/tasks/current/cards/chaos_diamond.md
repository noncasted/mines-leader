## Задача: ChaosDiamond — случайный Bloodhound

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | ChaosDiamond |
| Мана | 2 |
| Цель | OwnBoard |
| Паттерн | ромб (2-5) |
| Категория | Разведка |
| Пул | Ludic |
| Max-вариант | Нет |

### Описание эффекта

Рандомный Bloodhound: расчистка ромбом случайного размера от 2 до 5. Мины флажатся, безопасные открываются.

### Сложность: 4

Bloodhound + RNG на размер. Backend: Random.Range(2,6) для размера, далее стандартная ромбовая расчистка. Snapshot содержит ActualSize.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `ChaosDiamond = XXXX`
- [ ] 1.2. Config: ManaCost=2, MinSize=2, MaxSize=5, Target=OwnBoard
- [ ] 1.3. Payload: IBoardCardUsePayload с Position
- [ ] 1.4. Snapshot: int ActualSize + стандартные поля Bloodhound

#### 2. Backend

- [ ] 2.1. `ChaosDiamond.cs` — Random size, далее логика Bloodhound
- [ ] 2.2-2.4. Стандартные

#### 4. Client

- [ ] 4.1. `CardChaosDiamondAction.cs` — board drop, preview с мин/макс размером
- [ ] 4.2-4.3. Стандартные

#### 5-7. Стандартные шаги

---

### Особые требования

- Preview на клиенте: показать зону мин и макс размера (полупрозрачный ромб?)
- Клиент: анимация "рулетки" размера

### Зависимости

Нет. Использует ту же логику что Bloodhound.
