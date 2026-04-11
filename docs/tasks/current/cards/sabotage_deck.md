## Задача: SabotageDeck — подмешать Dud в колоду противника

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | SabotageDeck |
| Мана | 2 |
| Цель | Opponent |
| Паттерн | -- |
| Категория | Колода |
| Пул | Research |
| Max-вариант | Нет |

### Описание эффекта

Подмешивает карту-пустышку Dud в колоду противника. Dud занимает место в руке и ничего не делает. Нельзя сыграть — только сбросить.

### Сложность: 8

Нужен новый CardType.Dud — карта без эффекта, которую нельзя сыграть (Use возвращает ошибку или карта не дает use). Добавление карты в Deck противника. Dud на клиенте: визуал пустышки + невозможность перетащить.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. CardType: `SabotageDeck = XXXX` + `Dud = XXXX` (новый тип карты-пустышки)
- [ ] 1.2. Config для SabotageDeck: ManaCost=2, Target=Opponent
- [ ] 1.3. Config для Dud: ManaCost=999 (нельзя сыграть), Target=Self
- [ ] 1.4. Snapshot SabotageDeck: Guid TargetPlayer

#### 2. Backend

- [ ] 2.1. `SabotageDeck.cs` — opponent.Deck.AddRandom(CardType.Dud)
- [ ] 2.2. `Dud.cs` — ICard с Use() -> EmptyResponse.Error("Cannot use Dud")
- [ ] 2.3. Нужна возможность добавлять карты в чужую колоду
- [ ] 2.4. CardFactory для обоих + `SabotageDeckStrategy.cs`
- [ ] 2.5. BotServiceExtensions

#### 4. Client

- [ ] 4.1. `CardSabotageDeckAction.cs` — простой drop
- [ ] 4.2. `CardDudAction.cs` — пустой (нельзя использовать)
- [ ] 4.3. Визуал Dud: серая карта, иконка "пусто", tooltip "Dud — useless card"
- [ ] 4.4. cards-info.json для обоих типов

#### 5-7. Стандартные шаги

---

### Особые требования

- Два новых CardType: SabotageDeck + Dud
- Dud: нельзя использовать (ManaCost=999 или спец. проверка в CardUseCommand)
- Dud: можно только сбросить — проверить что Recycler работает с Dud
- Добавление карты в чужую колоду — новая механика

### Зависимости

Recycler (Dud можно сбросить через Recycler).
