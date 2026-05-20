## Задача: Переделать страницу конфигов карт в консоли

### Цель
1. Сделать грид из карт: иконка сверху, название во всю ширину карточки, ниже — параметры карты (Mana, Size, Damage и т.д.)
2. Разделить карты на гриды по категориям из `docs/obsidian/game/cards/implemented/` (Scout, Cross-Board, Improve, Resources, Hand)
3. Добавить поиск по имени карты во вкладке Cards
4. Подключить иконки карт из `docs/obsidian/game/cards/icons/`

### Контекст
Сейчас в `Configs.razor` карты разбиты на 5 категорий (Scout, Cross-Board, Buff, Resources, Hand), но для каждой карты используется отдельный специализированный редактор (`CardConfigEditor`, `CardSizeConfigEditor`, `CardRangeConfigEditor`, `CardChainReactionConfigEditor`, `CardLockdownConfigEditor`, `CardSiphonConfigEditor`). Это создаёт много дублирующегося кода и неудобное отображение.

Категории из доков:
- **Scout** (Разведка): Bloodhound, ErosionDozer, ZipZap, MinefieldScout, Sonar, ThermalVision, ChaosDiamond, ChaosScout, Excavator, FortuneCookie
- **Cross-Board**: Trebuchet, ChainReaction, OpponentBomb, OpponentFlagErase, OpponentFlagReshuffle, Smoke, FogOfWar, MineCluster, CarpetBomb, FortuneBlast, ChaosFog, Frost, Blackout, DimensionRift
- **Improve** (Усиление): TrebuchetAimer, Overclock, Medic, Purge, Adrenaline, CoinToss, Focus, Shield, PowerSurge
- **Resources**: Siphon, Lockdown, BloodPact, ManaSurge, DoubleOrNothing, Embargo, GamblersRuin, ManaFountain, SoulLink
- **Hand** (Колода): Gravedigger, Scavenger, HandScramble, MirrorMatch, MysticDraw, Recycler, SabotageDeck, Salvage, CardThief, Dud

Иконки лежат в `docs/obsidian/game/cards/icons/{BaseName}.png` (BaseName = CardType без суффикса `_Max`).

### Шаги реализации

**1. Подключить иконки карт к серверу**
  1.1. Настроить `UseStaticFiles` в `backend/Orchestration/ConsoleGateway/Program.cs` для отдачи иконок из `docs/obsidian/game/cards/icons/` по пути `/card-icons/`.

**2. Создать универсальный компонент карточки карты**
  2.1. Создать `backend/Console/Game/Configs/CardGridItem.razor` [новый файл — добавить в Console.csproj если нужно].
  2.2. Компонент принимает `ICardConfig` и `CardType`, показывает иконку (`/card-icons/{BaseName}.png`), название, бейдж Max.
  2.3. Через рефлексию определяет все editable int-свойства (кроме Type, Target) и рендерит input для каждого с читаемым label.

**3. Обновить страницу Configs.razor**
  3.1. Заменить 5 секций карт на гриды с использованием `CardGridItem`.
  3.2. Синхронизировать категории карт с документацией (Lockdown и SoulLink переходят из Buff в Resources).
  3.3. Добавить поле поиска (`_searchQuery`) и фильтрацию карт по имени.
  3.4. Удалить использование старых специализированных редакторов карт в секции Cards.

**4. Удалить/оставить старые редакторы**
  4.1. Старые редакторы (`CardConfigEditor.razor`, `CardSizeConfigEditor.razor`, `CardRangeConfigEditor.razor`, `CardChainReactionConfigEditor.razor`, `CardLockdownConfigEditor.razor`, `CardSiphonConfigEditor.razor`) больше не используются в Configs.razor. Оставить файлы на месте (могут использоваться где-то ещё).

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Console/Game/Configs/Configs.razor` | Главная страница — переделать секцию Cards |
| `backend/Console/Game/Configs/CardGridItem.razor` | Новый универсальный компонент карточки |
| `backend/Orchestration/ConsoleGateway/Program.cs` | Настройка static files для иконок |
| `shared/Configs/CardConfigOptions.cs` | Модели данных карт |
| `docs/obsidian/game/cards/icons/*.png` | Иконки карт |

### Документация к прочтению
- `.agents/docs/BLAZOR.md` — правила Blazor UI (early return, inject, UiComponent)

### Риски
- Рефлексия в Blazor может быть медленной, но для ~50 карточек это незаметно.
- Иконки лежат вне проекта — нужен корректный `PhysicalFileProvider` с абсолютным путём.
- `_Max` карты должны показывать ту же иконку, что и базовая версия.
- Двусторонний биндинг через `@onchange` вместо `@bind` для динамических свойств.
