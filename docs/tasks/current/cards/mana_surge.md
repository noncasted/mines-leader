## Задача: ManaSurge — +3 временной маны

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | ManaSurge |
| Мана | 2 |
| Цель | Self |
| Паттерн | -- |
| Категория | Ресурсы |
| Пул | Ordinary |
| Max-вариант | Нет |

### Описание эффекта

Добавляет +3 временной маны на текущий ход. Временная мана не переходит в следующий ход. Чистая прибыль +1 мана (2 потрачено, 3 получено).

### Сложность: 1

Прямая модификация числа (temp mana). Без доски, без RNG, без временных эффектов.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. `shared/Domain/CardType.cs` — добавить `ManaSurge = XXXX`
- [ ] 1.2. `shared/Configs/CardConfigOptions.cs` — config: ManaCost=2, ManaGain=3, Target=Self
- [ ] 1.3. `shared/Game/Cards/ICardUsePayload.cs` — простой payload
- [ ] 1.4. `shared/Game/Snapshots/CardActionSnapshotRecord.cs` — snapshot с TargetPlayer

#### 2. Backend

- [ ] 2.1. `backend/Game/GamePlay/Cards/ManaSurge.cs` — ICard: owner.AddTempMana(config.ManaGain)
- [ ] 2.2. `backend/Game/GamePlay/Cards/CardFactory.cs` — case
- [ ] 2.3. `backend/Game/GamePlay/Bot/CardStrategies/ManaSurgeStrategy.cs` — utility: высокий если есть дорогие карты в руке
- [ ] 2.4. `backend/Game/GamePlay/Bot/BotServiceExtensions.cs` — регистрация

#### 3. Console

- [ ] 3.1. Нужен кастомный редактор или расширить CardConfigEditor для поля ManaGain

#### 4. Client

- [ ] 4.1. `client/Assets/GamePlay/Cards/Entities/Actions/CardManaSurgeAction.cs` — простой drop
- [ ] 4.2. `CardStatesExtensions.cs` — оба switch
- [ ] 4.3. `cards-info.json` — "+3 temporary mana this turn"

#### 5. .csproj

- [ ] 5.1. Backend — ManaSurge.cs + ManaSurgeStrategy.cs
- [ ] 5.2. Client — CardManaSurgeAction.cs

#### 6. Документация

- [ ] 6.1-6.3. Стандартный набор (queue -> implemented, таблицы, GAMEPLAY.md)

#### 7. Верификация

- [ ] 7.1-7.4. Стандартный чеклист

---

### Особые требования

Нужен механизм "временной маны" — проверить, есть ли уже. Если нет — потребуется добавить поле TempMana в player state.

### Зависимости

Нет.
