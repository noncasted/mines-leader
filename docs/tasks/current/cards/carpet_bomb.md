## Задача: CarpetBomb — линия мин на поле противника

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | CarpetBomb |
| Мана | 5 |
| Цель | OpponentBoard |
| Паттерн | линия (5) |
| Категория | Кросс-борд |
| Пул | Ordinary |
| Max-вариант | Да |

### Описание эффекта

Добавляет мины на поле противника в линии из 5 клеток. Самая дорогая карта минирования с максимальным покрытием.

### Сложность: 5

Аналог Trebuchet, но линейный паттерн. Нужны Line pattern + Direction в payload. Мана 5 — поздняя игра.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `CarpetBomb = XXXX`, `CarpetBomb_Max = XXXX`
- [ ] 1.2. Config: ManaCost=5, Length=5, Target=OpponentBoard
- [ ] 1.3. Payload: IBoardCardUsePayload с Position + Direction
- [ ] 1.4. Snapshot: IReadOnlyList<Position> MinedCells

#### 2. Backend

- [ ] 2.1. `CarpetBomb.cs` — линия мин, аналог MineCluster но линейный
- [ ] 2.2-2.4. Стандартные
- [ ] Bot: utility высокий только при мане 5+ (поздняя игра)

#### 4. Client

- [ ] 4.1. `CardCarpetBombAction.cs` — opponent board drop с Line pattern + Direction
- [ ] 4.2-4.3. Стандартные

#### 5-7. Стандартные шаги

---

### Особые требования

- Нужен Line pattern с направлением (общий с ChaosScout на клиенте)
- Самая дорогая карта (5 маны) — бот использует только поздно

### Зависимости

ChaosScout (общий Line pattern с Direction).
