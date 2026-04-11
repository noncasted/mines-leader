## Задача: GamblersRuin — монетка: +3к+2м / -2к

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | GamblersRuin |
| Мана | 2 |
| Цель | Self |
| Паттерн | -- |
| Категория | Ресурсы |
| Пул | Research |
| Max-вариант | Нет |

### Описание эффекта

Бросок монеты: орел — +3 карты в руку и +2 временной маны, решка — -2 карты из руки (сбрасываются случайно).

### Сложность: 4

RNG с двойным эффектом (карты + мана при орле). Комбинация MysticDraw и ManaFountain логики.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. `shared/Domain/CardType.cs` — `GamblersRuin = XXXX`
- [ ] 1.2. Config: ManaCost=2, WinDraw=3, WinMana=2, LoseDiscard=2, Target=Self
- [ ] 1.3-1.4. Snapshot: bool IsHeads, IReadOnlyList<CardType> AffectedCards, int ManaGained

#### 2. Backend

- [ ] 2.1. `GamblersRuin.cs` — flip, орел: Draw(3)+AddTempMana(2), решка: DiscardRandom(2)
- [ ] 2.2-2.4. Стандартные
- [ ] Bot: utility средний при руке 1-2 карты (решка менее болезненна)

#### 3-7. Стандартные шаги

---

### Особые требования

- Нужен механизм временной маны
- При решке: случайный сброс 2 карт из руки

### Зависимости

ManaSurge (temp mana), MysticDraw (паттерн случайного сброса).
