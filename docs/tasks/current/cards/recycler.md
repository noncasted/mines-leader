## Задача: Recycler — сброс 1 карты, добор 2

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | Recycler |
| Мана | 1 |
| Цель | Self |
| Паттерн | -- |
| Категория | Колода |
| Пул | Ordinary |
| Max-вариант | Нет |

### Описание эффекта

Сбрасывает 1 карту по выбору игрока из руки и добирает 2 карты из колоды. Чистая фильтрация руки (+1 карта).

### Сложность: 5

Требует UI для выбора карты из руки (новый interaction pattern на клиенте). Backend: работа с Hand и Deck (Discard + Draw). Payload должен содержать ID сбрасываемой карты.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. `shared/Domain/CardType.cs` — `Recycler = XXXX`
- [ ] 1.2. Config: ManaCost=1, DiscardCount=1, DrawCount=2, Target=Self
- [ ] 1.3. Payload: Guid DiscardCardId — ID карты для сброса
- [ ] 1.4. Snapshot: Guid DiscardedCardId + IReadOnlyList<CardType> DrawnCards

#### 2. Backend

- [ ] 2.1. `Recycler.cs` — hand.Discard(payload.DiscardCardId), deck.Draw(2), добавить в hand
- [ ] 2.2. CardFactory
- [ ] 2.3. `RecyclerStrategy.cs` — бот выбирает карту с наименьшей utility для сброса
- [ ] 2.4. BotServiceExtensions

#### 3. Console — стандартный

#### 4. Client

- [ ] 4.1. `CardRecyclerAction.cs` — UI для выбора карты из руки + confirmation
- [ ] 4.2-4.3. Стандартные

#### 5-7. Стандартные шаги

---

### Особые требования

- Нужен UI-паттерн "выбор карты из руки" (новый для проекта) — может переиспользоваться Salvage, Card Thief
- Бот-стратегия должна уметь оценивать utility каждой карты в руке для выбора сброса
- Валидация: нельзя сбросить саму себя (Recycler)

### Зависимости

Нет.
