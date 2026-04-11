## Задача: Adrenaline — +1 ход в текущем ходу

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | Adrenaline |
| Мана | 1 |
| Цель | Self |
| Паттерн | -- |
| Категория | Усиление |
| Пул | Ordinary |
| Max-вариант | Нет |

### Описание эффекта

Добавляет +1 ход в текущем ходу. Эффект действует немедленно. Простейший темповый буст за минимальную цену.

### Сложность: 1

Самая простая карта: модификация одного числа (moves), без доски, без RNG, без временных эффектов.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. `shared/Domain/CardType.cs` — добавить `Adrenaline = XXXX`
- [ ] 1.2. `shared/Configs/CardConfigOptions.cs` — config class с ManaCost=1 + Target=Self
- [ ] 1.3. `shared/Game/Cards/ICardUsePayload.cs` — простой payload без Position
- [ ] 1.4. `shared/Game/Snapshots/CardActionSnapshotRecord.cs` — snapshot с TargetPlayer

#### 2. Backend

- [ ] 2.1. `backend/Game/GamePlay/Cards/Adrenaline.cs` — ICard: owner.AddMoves(1)
- [ ] 2.2. `backend/Game/GamePlay/Cards/CardFactory.cs` — case для Adrenaline
- [ ] 2.3. `backend/Game/GamePlay/Bot/CardStrategies/AdrenalineStrategy.cs` — utility: высокий при 0 ходов, средний иначе
- [ ] 2.4. `backend/Game/GamePlay/Bot/BotServiceExtensions.cs` — регистрация

#### 3. Console

- [ ] 3.1. Стандартный `CardConfigEditor` (только ManaCost) — без изменений в Configs.razor

#### 4. Client

- [ ] 4.1. `client/Assets/GamePlay/Cards/Entities/Actions/CardAdrenalineAction.cs` — простой drop без доски
- [ ] 4.2. `client/Assets/GamePlay/Cards/Entities/States/CardStatesExtensions.cs` — оба switch
- [ ] 4.3. `client/Assets/Resources/cards-info.json` — "Adds +1 move this turn"

#### 5. .csproj

- [ ] 5.1. Backend .csproj — Adrenaline.cs + AdrenalineStrategy.cs
- [ ] 5.2. Client .csproj — CardAdrenalineAction.cs

#### 6. Документация

- [ ] 6.1. Переместить из queue в implemented
- [ ] 6.2. Обновить cards_implemented_all.md
- [ ] 6.3. Обновить GAMEPLAY.md

#### 7. Верификация

- [ ] 7.1. Union ID совпадает в Config/Payload/Snapshot
- [ ] 7.2. CardType value корректный
- [ ] 7.3. Все файлы в .csproj
- [ ] 7.4. Бот-стратегия зарегистрирована

---

### Особые требования

Нет. Прямая модификация счетчика ходов.

### Зависимости

Нет.
