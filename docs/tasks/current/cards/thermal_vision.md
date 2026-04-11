## Задача: ThermalVision — подсветка мин в ромбе

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | ThermalVision |
| Мана | 3 |
| Цель | OwnBoard |
| Паттерн | ромб (3) |
| Категория | Разведка |
| Пул | Ordinary |
| Max-вариант | Да |

### Описание эффекта

Временно подсвечивает все мины в области ромба на текущий ход: визуальный glow-эффект без флагов и без открытия клеток. Эффект исчезает в конце хода.

### Сложность: 5

Аналог Sonar по таргетингу, но без флагов — только визуал. Нужна система временной подсветки мин (новая клиентская механика). Бэкенд отдает позиции мин в snapshot.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `ThermalVision = XXXX`, `ThermalVision_Max = XXXX`
- [ ] 1.2. Config: ManaCost=3, Size=3, Target=OwnBoard
- [ ] 1.3. Payload: IBoardCardUsePayload с Position
- [ ] 1.4. Snapshot: IReadOnlyList<Position> HighlightedMines

#### 2. Backend

- [ ] 2.1. `ThermalVision.cs` — собрать ромб, найти клетки с HasMine, передать позиции в snapshot (БЕЗ флагов)
- [ ] 2.2. CardFactory — два case
- [ ] 2.3. `ThermalVisionStrategy.cs` — utility как Bloodhound
- [ ] 2.4. BotServiceExtensions

#### 4. Client

- [ ] 4.1. `CardThermalVisionAction.cs` — board drop + Snapshot: применить glow-эффект
- [ ] 4.2. Нужна новая клиентская механика: временная подсветка клеток (glow на CellView, снимается в конце хода)
- [ ] 4.3. Стандартные

#### 5-7. Стандартные шаги

---

### Особые требования

- Новая клиентская механика: временный glow-эффект на клетках
- Эффект исчезает в конце хода — нужен hook на конец хода
- Общая с FortuneCookie система подсветки

### Зависимости

Нет. Но FortuneCookie использует ту же систему подсветки.
