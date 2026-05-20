## Синхронизация описаний карт — Рабочие заметки

### Статус: В работе

### Заметки

#### [12:00] Интерфейсы эффектов
- Добавлены `IDurationalCardConfig`, `IDamageCardConfig`, `IHealCardConfig` в `shared/Configs/CardConfigOptions.cs`
- Применены к 7 картам с длительностью, 1 карте урона, 1 карте лечения
- `Smoke.Duration` и `FogOfWar.Duration` были read-only expression-bodied — переделаны в settable
- Исправлены рассинхроны: `Frost_Max.Duration = 2`, `Blackout.Normal.Duration = 1`

#### [12:15] Маркап в JSON
- `{ROUNDS}` — 10 карт
- `{DAMAGE}` — OpponentBomb
- `{HEAL}` — Medic

#### [12:30] CardDescriptionProvider
- Создан `client/Assets/Meta/Cards/CardDescriptionProvider.cs`
- Lazy-build кэша при первом вызове `GetDescription`
- Graceful fallback на raw template если `ICardConfigs.Value` ещё null
- Зарегистрирован в DI (`MetaServicesExtensions.cs`)

#### [12:45] Интеграция с UI
- `CardDataView` — инжектит `ICardDescriptionProvider`, использует в `OnSetup`
- `CardRevealView` — аналогично
- `MenuDeckPoolCard` — инжектит provider, хранит `ResolvedDescription`, использует в `Setup`
- `MenuDeckCard` — использует `_currentCard.ResolvedDescription` в `UpdateDisplay`

#### [13:00] Документация
- Создан `.agents/docs/CARD_EFFECTS.md` с полным описанием интерфейсов, карт, токенов и пайплайна

#### [13:30] Удаление Duration
- Заменено `config.Duration` → `config.TurnsDuration` в 7 backend файлах
- Удалены `Duration` свойства из всех классов карт, оставлен только `TurnsDuration`
- Обновлены initializers `Frost_Max` и `Blackout_Max`
- Тесты проходят (634/634)

#### [14:00] Расширение на все нестандартные поля
- Пользователь запросил систематизацию всех нестандартных полей
- Полный список уникальных полей (кроме Type, ManaCost, Target):
  - Size, SearchRadius, DrainAmount, MaxChain, SpawnSize, ExtraMoves, DrawCount
  - MovesReduction, ManaGain, HpCost, WinMoves, LoseMoves, MinMana, MaxMana
  - Discount, CostIncrease, WinDraw, LoseReturn, WinMana, LoseDiscard
  - Length, MinSize, MaxSize, MinLength, MaxLength, MinMines, MaxMines, PeekCount
- Начинаю добавление интерфейсов и маркап-токенов
#### [17:30] Устранение циклической зависимости
`CardDescriptionProvider` зависел от `ICardsRegistry`, а `CardsRegistry` теперь должен использовать `ICardDescriptionProvider` — цикл.

Решение:
- `CardDescriptionProvider` теперь сам загружает `cards-info` в конструкторе (`LoadRawDescriptions`), хранит `_rawDescriptions`
- Убрана зависимость `ICardsRegistry` из конструктора и всех обращений к `_registry`
- `BuildIfNeeded` берёт базовое описание из `_rawDescriptions` вместо `_registry.Entries`
- `CardsRegistry` принимает `ICardDescriptionProvider`, использует `_descriptionProvider.GetDescription(type)` при создании `CardDefinition`
- DI-регистрация не требует изменений — VContainer разрешает граф автоматически

Изменённые файлы:
- `client/Assets/Meta/Cards/CardDescriptionProvider.cs`
- `client/Assets/Meta/Cards/CardsRegistry.cs`
