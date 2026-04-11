## Задача: BloodPact — -1 HP, +3 маны, +2 хода

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | BloodPact |
| Мана | 0 |
| Цель | Self |
| Паттерн | -- |
| Категория | Ресурсы |
| Пул | Research |
| Max-вариант | Нет |

### Описание эффекта

Жертва: -1 HP, взамен +3 временной маны и +2 хода. Стоит 0 маны. Единственная карта с нулевой маной. Торгует HP на темп.

### Сложность: 2

Модификация трех чисел (HP, mana, moves), но все прямые. Особенность: 0 маны — проверить что система допускает.

---

### Шаги реализации

#### 1. Shared

- [ ] 1.1. `shared/Domain/CardType.cs` — `BloodPact = XXXX`
- [ ] 1.2. `shared/Configs/CardConfigOptions.cs` — ManaCost=0, HpCost=1, ManaGain=3, MovesGain=2, Target=Self
- [ ] 1.3. `shared/Game/Cards/ICardUsePayload.cs` — простой payload
- [ ] 1.4. `shared/Game/Snapshots/CardActionSnapshotRecord.cs` — snapshot

#### 2. Backend

- [ ] 2.1. `backend/Game/GamePlay/Cards/BloodPact.cs` — owner.DealDamage(1), AddTempMana(3), AddMoves(2)
- [ ] 2.2. `backend/Game/GamePlay/Cards/CardFactory.cs` — case
- [ ] 2.3. `backend/Game/GamePlay/Bot/CardStrategies/BloodPactStrategy.cs` — utility: высокий при HP >= 2 и мало маны
- [ ] 2.4. `backend/Game/GamePlay/Bot/BotServiceExtensions.cs`

#### 3. Console

- [ ] 3.1. Кастомный редактор для HpCost/ManaGain/MovesGain

#### 4. Client

- [ ] 4.1. `CardBloodPactAction.cs` — drop + визуал потери HP
- [ ] 4.2-4.3. Стандартные шаги

#### 5-7. Стандартные шаги

---

### Особые требования

- Проверить что ManaCost=0 корректно обрабатывается в CardUseCommand
- Нужен механизм временной маны (общий с ManaSurge)
- Бот не должен использовать при HP=1

### Зависимости

ManaSurge (общий механизм временной маны).
