## Задача: Blackout — замена чисел на "?" на 2 раунда

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | Blackout |
| Мана | 2 |
| Цель | OpponentBoard |
| Паттерн | ромб (2) |
| Категория | Кросс-борд |
| Пул | Ordinary |
| Max-вариант | Да |

### Описание эффекта

Заменяет числа на открытых клетках противника на "?" на 2 раунда. Лишает противника информации об уже открытой области.

### Сложность: 7

Нужен механизм скрытия MinesAround на клиенте (данные есть на сервере, но клиент показывает "?"). Временной эффект 2 раунда. Интеграция с BoardSnapshotHandler.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `Blackout = XXXX`, `Blackout_Max = XXXX`
- [ ] 1.2. Config: ManaCost=2, Size=2, Duration=2, Target=OpponentBoard
- [ ] 1.3. Payload: IBoardCardUsePayload с Position
- [ ] 1.4. Snapshot: IReadOnlyList<Position> AffectedCells

#### 2. Backend

- [ ] 2.1. `Blackout.cs` — пометить клетки как Blacked Out, Schedule dispose через 2 раунда
- [ ] 2.2. Нужен механизм: при отправке snapshot противнику — заменять MinesAround на -1 (или спец. значение) для blacked out клеток
- [ ] 2.3. `BlackoutDisposeAction : IRoundAction` — снятие эффекта + отправка обновления клиенту
- [ ] 2.4. CardFactory + `BlackoutStrategy.cs` + BotServiceExtensions

#### 4. Client

- [ ] 4.1. `CardBlackoutAction.cs` — opponent board drop
- [ ] 4.2. CellView: отображение "?" вместо числа для blacked out клеток
- [ ] 4.3. Sync: восстановление чисел при снятии эффекта

#### 5-7. Стандартные шаги

---

### Особые требования

- Сервер хранит реальные MinesAround, клиент видит "?" — нужен фильтр в snapshot sync
- При снятии эффекта: клиент должен получить актуальные числа
- Нужно решить: blackout влияет на уже открытые клетки — как это взаимодействует с flood-fill?
- Бот: utility высокий после минирования области (Mine Cluster + Blackout комбо)

### Зависимости

Нет. Но хорошо комбинируется с Mine Cluster и Carpet Bomb.
