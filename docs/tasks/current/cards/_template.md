## Задача: [CardName] — [Краткое описание]

### Статус: Не начато

### Карта

| Поле | Значение |
|------|----------|
| Имя | [CardName] |
| Мана | [N] |
| Цель | [Self / OwnBoard / OpponentBoard / Opponent] |
| Паттерн | [-- / ромб (N) / крест (N) / линия (N)] |
| Категория | [Разведка / Колода / Усиление / Кросс-борд / Ресурсы] |
| Пул | [Ordinary / Ludic / Research] |
| Max-вариант | [Да / Нет] |

### Описание эффекта

[Полное описание механики карты из docs/obsidian/game/cards/queue/]

### Сложность: [1-10]

[Обоснование оценки сложности]

---

### Шаги реализации

#### 1. Shared (shared/)

- [ ] 1.1. `shared/Domain/CardType.cs` — добавить enum value(s)
- [ ] 1.2. `shared/Configs/CardConfigOptions.cs` — union attribute + config class + properties + All dict
- [ ] 1.3. `shared/Game/Cards/ICardUsePayload.cs` — union attribute + payload class
- [ ] 1.4. `shared/Game/Snapshots/CardActionSnapshotRecord.cs` — union attribute + snapshot class

#### 2. Backend (backend/)

- [ ] 2.1. `backend/Game/GamePlay/Cards/[CardName].cs` — реализация ICard (новый файл)
- [ ] 2.2. `backend/Game/GamePlay/Cards/CardFactory.cs` — добавить case в Create()
- [ ] 2.3. `backend/Game/GamePlay/Bot/CardStrategies/[CardName]Strategy.cs` — бот-стратегия (новый файл)
- [ ] 2.4. `backend/Game/GamePlay/Bot/BotServiceExtensions.cs` — регистрация стратегии

#### 3. Console (backend/Console/)

- [ ] 3.1. Определить тип редактора: CardConfigEditor / CardSizeConfigEditor / новый
- [ ] 3.2. При необходимости — `backend/Console/Pages/Configs/Configs.razor` — добавить кастомный редактор

#### 4. Client (client/)

- [ ] 4.1. `client/Assets/GamePlay/Cards/Entities/Actions/Card[CardName]Action.cs` — ICardAction (новый файл)
- [ ] 4.2. `client/Assets/GamePlay/Cards/Entities/States/CardStatesExtensions.cs` — AddCardAction() + AddCardActionSync()
- [ ] 4.3. `client/Assets/Resources/cards-info.json` — name, description, icon

#### 5. .csproj

- [ ] 5.1. Backend .csproj — добавить новые .cs файлы
- [ ] 5.2. Client .csproj — добавить новые .cs файлы (если используется)

#### 6. Документация

- [ ] 6.1. Переместить карту из `docs/obsidian/game/cards/queue/` в `implemented/`
- [ ] 6.2. Обновить `docs/obsidian/game/cards/implemented/cards_implemented_all.md`
- [ ] 6.3. Обновить `.claude/docs/GAMEPLAY.md` — добавить в таблицу CardType

#### 7. Верификация

- [ ] 7.1. MemoryPack union ID одинаковый во всех трех файлах (Config, Payload, Snapshot)
- [ ] 7.2. CardType enum value кратен 100, Max = base + 10
- [ ] 7.3. Все новые файлы добавлены в .csproj
- [ ] 7.4. Бот-стратегия зарегистрирована в BotServiceExtensions

---

### Особые требования

[Специфика карты: RNG-механики, временные эффекты, нестандартные зависимости, новые паттерны]

### Зависимости

[Карты или системы, которые должны быть реализованы до этой карты]
