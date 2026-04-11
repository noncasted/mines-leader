## Задача: MineCluster — крест мин на поле противника

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | MineCluster |
| Мана | 3 |
| Цель | OpponentBoard |
| Паттерн | крест (2) |
| Категория | Кросс-борд |
| Пул | Ordinary |
| Max-вариант | Да |

### Описание эффекта

Добавляет мины на поле противника в крестообразном паттерне: центральная клетка + 4 стороны (5 клеток). Бюджетный вариант минирования.

### Сложность: 5

Аналог Trebuchet, но крест вместо ромба. Нужен Cross pattern. Бэкенд: добавление мин + пересчет MinesAround (BoardMinesScanner).

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `MineCluster = XXXX`, `MineCluster_Max = XXXX`
- [ ] 1.2. Config: ManaCost=3, Size=2, Target=OpponentBoard
- [ ] 1.3. Payload: IBoardCardUsePayload с Position
- [ ] 1.4. Snapshot: IReadOnlyList<Position> MinedCells

#### 2. Backend

- [ ] 2.1. `MineCluster.cs` — крест на доске противника, для каждой клетки: SetMine + BoardMinesScanner.Rescan
- [ ] 2.2. CardFactory — два case
- [ ] 2.3. `MineClusterStrategy.cs` — utility: как Trebuchet (зависит от прогресса противника)
- [ ] 2.4. BotServiceExtensions

#### 4. Client

- [ ] 4.1. `CardMineClusterAction.cs` — opponent board drop с Cross pattern
- [ ] 4.2-4.3. Стандартные

#### 5-7. Стандартные шаги

---

### Особые требования

- Нужен Cross pattern (общий с Excavator)
- Пересчет MinesAround после добавления мин
- Max-вариант: больший Size

### Зависимости

Excavator (общий Cross pattern). Аналог Trebuchet по backend-логике.
