## Задача: Отображение текущих модификаторов игрока

### Что я хочу

Сейчас в игре карты накладывают на игроков различные эффекты — бонусные ходы, скидки на карты, щит от урона, связь душ, штрафы на ману и так далее. Но игрок никак не видит, какие модификаторы на него действуют в данный момент. Это создает непонятность: я использовал карту, получил бонус, но через сколько ходов он пропадет? А действует ли он вообще? А что это за эффект? — ответов на эти вопросы нет в интерфейсе.

Я хочу, чтобы в игровом оверлее (`Game_Overlay`) слева вверху появился столбец иконок, показывающий все активные модификаторы текущего игрока. Каждая иконка — это квадратный префаб с изображением эффекта. В правом нижнем углу иконки отображается число — сколько ходов осталось до истечения. Если модификатор бесконечный (например, щит), цифра не показывается.

При наведении мыши на иконку справа от нее должно появляться окно-тултип с текстовым описанием эффекта. Тексты тултипов хранятся в отдельном `ScriptableObject`, который маппит строковый ключ модификатора на локализованное описание. Тот же ключ используется для выбора иконки — так на клиенте по `Key` мы знаем, какую картинку показать и что написать в подсказке.

Чтобы эта система заработала, бэкенд должен перестать хранить модификаторы как голые `float`-значения в `Dictionary<PlayerModifier, float>`. Вместо этого каждый модификатор — это источник (`IModifierSource`) со своим `Guid`, типом, значением, строковым ключом и количеством оставшихся ходов. Несколько источников могут давать один и тот же тип модификатора — тогда их значения суммируются. Публичное API по-прежнему отдает вычисленный `Dictionary<PlayerModifier, float>`, но внутри происходит полный перерасчет при каждом добавлении, обновлении или удалении источника.

При этом синхронизация на клиент меняется: вместо пары "тип + значение" бэкенд отправляет `DurationalModifierOverview` — структуру с типом, значением, ключом и оставшимися ходами. Клиент получает список таких овервью и отрисовывает иконки. Когда ходы заканчиваются, источник удаляется, overview пропадает из синхронизации, и иконка исчезает с экрана.

Текущая система `RoundActionService` работает так: карта планирует `ModifierDisposeAction`, который через N ходов просто откатывает значение. Это не позволяет узнать, сколько ходов осталось, и не дает контроля над источником. Я хочу переделать это на `ModifierRoundAction`, который каждый раунд вызывает `Tick()` у источника. `Tick()` возвращает `bool` — пора ли удалить экшен. Внутри `Tick` источник обновляет свое состояние (уменьшает счетчик ходов), и этот апдейт триггерит перерасчет и отправку overview на клиент. Так клиент всегда видит актуальное количество оставшихся ходов.

Та же модель `Tick()` должна применяться ко всем остальным раундовым экшенам — `BlackoutDisposeAction`, `FogDisposeAction`, `FrostDisposeAction` и остальным. У них тоже меняется интерфейс: вместо `void OnTime()` теперь `bool Tick()`.

### Цель

Реализовать полноценную систему отображения активных модификаторов игрока в игровом оверлее, с переработкой бэкенд-хранилища модификаторов на основе источников (sources) с поддержкой длительности и синхронизацией овервью на клиент.

**Все требования пользователя:**

1. **Компонент `PlayerModifiersView`** на сцене `Game_Overlay` — столбец иконок модификаторов в левом верхнем углу, использующий `ResponsiveContainer` для вертикального стекирования.
2. **Префаб модификатора** — квадратная иконка со скриптом `PlayerModifierEntryView`, на префабе обязательно `UIElementPointerHandler` для обработки наведения мыши.
3. **Счетчик ходов** — в правом нижнем углу префаба цифра (количество ходов до окончания). Если `-1` — модификатор бесконечный, цифра скрывается.
4. **Тултип при наведении** — справа от иконки всплывает окно с описанием эффекта. Отдельный объект, включается/выключается, позиционируется динамически.
5. **`ScriptableObject`** с `Dictionary<string, string>` — маппинг ключа модификатора на строку тултипа.
6. **Переработка `Modifiers.cs` на бэкенде** — внутреннее хранилище `Dictionary<Guid, IModifierSource>` вместо `Dictionary<PlayerModifier, float>`.
7. **Публичное поле `Modifiers`** на бэкенде — отдает вычисленный `Dictionary<PlayerModifier, float>`.
8. **`IModifierSource`** — метод `Calculate()` возвращает `(PlayerModifier, float)`, метод `GetOverview()` возвращает `IModifierOverview`.
9. **`IModifierOverview` + `DurationalModifierOverview`** в `shared` — `DurationalModifierOverview { int TurnsToEnd, PlayerModifier Type, float Value, string Key }`. По `Key` маппим иконки и тултипы на клиенте.
10. **Методы `Modifiers`** — `Add(source)`, `Update(source)`, `Remove(source)` вместо `Set()`. При каждой операции — перерасчет всех источников.
11. **`ModifierRoundAction`** — замена `ModifierDisposeAction`. Новый `IRoundAction` с методом `bool Tick()`.
12. **`IRoundAction.Tick()`** — возвращает `bool` (удалить ли экшен). Все существующие экшены перерабатываются на эту модель.
13. **Из `Tick` обновляем сурс** — указываем сколько ходов осталось. Сурс отдает overview клиенту, клиент отображает.

### Контекст

Текущая система модификаторов на бэкенде — простой `Dictionary<PlayerModifier, float>`. Карты напрямую инкрементируют/декрементируют значения, а `ModifierDisposeAction` через `RoundActionService` откатывает изменения по таймеру. Это не позволяет:
- Отслеживать источник каждого модификатора
- Показывать игроку сколько ходов осталось до истечения
- Показывать описание эффекта в UI

Новая архитектура: модификаторы хранятся как источники (`IModifierSource`), каждый со своим `Guid`, типом, значением, ключом и длительностью. Вычисленные значения (`Dictionary<PlayerModifier, float>`) получаются суммированием всех источников. При изменении любого источника происходит полный перерасчет и синхронизация overview на клиент.

### Шаги реализации

**1. Shared: интерфейсы overview и обновление snapshot-структур**
  1.1. Создать `IModifierOverview` — `shared/Game/Player/IModifierOverview.cs` [новый файл — добавить в shared.csproj]
  1.2. Создать `DurationalModifierOverview` — `shared/Game/Player/DurationalModifierOverview.cs` [новый файл — добавить в shared.csproj]
  1.3. Обновить `PlayerModifiersState` — `shared/Game/Player/PlayerModifiersState.cs` — изменить Values с `Dictionary<PlayerModifier, float>` на `List<DurationalModifierOverview>`
  1.4. Обновить `PlayerSnapshotRecord.ModifierUpdate` — `shared/Game/Snapshots/PlayerSnapshotRecord.cs` — заменить поля `Modifier`+`Value` на `DurationalModifierOverview Overview`
  1.5. Обновить `GameStateSnapshot.Modifiers` — `shared/Game/Snapshots/GameStateSnapshot.cs` — изменить тип на `List<DurationalModifierOverview>`

**2. Backend: переработка хранилища модификаторов**
  2.1. Создать `IModifierSource` — `backend/Game/GamePlay/Players/IModifierSource.cs` [новый файл — Game.csproj SDK-style, авто-включение]
  2.2. Создать реализацию `DurationModifierSource` — `backend/Game/GamePlay/Players/DurationModifierSource.cs` [новый файл]
  2.3. Переработать `Modifiers` — `backend/Game/GamePlay/Players/Modifiers.cs` — внутреннее `Dictionary<Guid, IModifierSource>`, публичное `Values`, методы `Add/Update/Remove`, перерасчет
  2.4. Обновить `IModifiers` — в том же файле — новый контракт
  2.5. Обновить `PlayerModifiersExtensions` — `Inc/Dec/Reset` должны работать через создание/удаление источников или быть удалены

**3. Backend: переработка IRoundAction и RoundActionService**
  3.1. Обновить `IRoundAction` — `backend/Game/GamePlay/Context/RoundActionService.cs` — заменить `void OnTime()` на `bool Tick()`
  3.2. Обновить `RoundActionService.Tick()` — вызывать `Tick()` у всех экшенов, удалять тех кто вернул `true`
  3.3. Создать `ModifierRoundAction` — `backend/Game/GamePlay/Context/ModifierRoundAction.cs` [новый файл] — тик уменьшает ходы, обновляет сурс, при 0 удаляет сурс
  3.4. Удалить `ModifierDisposeAction` — `backend/Game/GamePlay/Context/ModifierDisposeAction.cs`
  3.5. Обновить все *DisposeAction — `BlackoutDisposeAction`, `ChaosFogDisposeAction`, `FogDisposeAction`, `FrostDisposeAction`, `SmokeDisposeAction`, `SoulLinkDisposeAction`, `MineHighlightDisposeAction` — реализовать `Tick()`

**4. Backend: обновление всех карт и команд**
  4.1. Обновить карты Buff — `Adrenaline`, `CoinToss`, `Focus`, `Lockdown`, `Overclock`, `PowerSurge`, `Shield` — использовать `Add(source)` + `ModifierRoundAction`
  4.2. Обновить карты Resources — `BloodPact`, `DoubleOrNothing`, `Embargo`, `GamblersRuin`, `ManaFountain`, `ManaSurge`, `SoulLink` — аналогично
  4.3. Обновить карты CrossBoard — `Trebuchet`, `TrebuchetAimer` — `Reset/Inc` через новую модель
  4.4. Обновить `CardUseCommand` — `backend/Game/GamePlay/Commands/CardUseCommand.cs` — использует `Get()` для расчета стоимости
  4.5. Обновить `OpenCellCommand` — `backend/Game/GamePlay/Commands/OpenCellCommand.cs` — использует `Get()` и `Set()` для Shield
  4.6. Обновить `Health`, `Mana`, `Moves` — `backend/Game/GamePlay/Players/Health.cs`, `Mana.cs`, `Moves.cs` — используют `_modifiers.Get()`
  4.7. Обновить `GameStateCapture` — `backend/Game/GamePlay/Snapshots/GameStateCapture.cs` — синхронизация overview
  4.8. Обновить `SnapshotDiffGuard` — `backend/Game/GamePlay/Snapshots/SnapshotDiffGuard.cs` — сравнение overview-списков

**5. Client: обновление синхронизации и хранилища модификаторов**
  5.1. Обновить `IPlayerModifiers` — `client/Assets/GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs` — добавить `IViewableList<DurationalModifierOverview> Overviews`
  5.2. Обновить `PlayerModifiers` — хранить и обновлять список overviews
  5.3. Обновить `PlayerModifierSnapshotHandler` — `client/Assets/GamePlay/Sync/PlayerModifierSnapshotHandler.cs` — обработка overview
  5.4. Обновить `GamePlaySyncExtensions` — при необходимости зарегистрировать новый handler

**6. Client: UI компоненты и префабы**
  6.1. Создать `ModifierDescriptionsConfig` — `client/Assets/GamePlay/UI/Overlay/ModifierDescriptionsConfig.cs` [новый файл — добавить в GamePlay.csproj] — ScriptableObject с `Dictionary<string, string>`
  6.2. Создать `PlayerModifierEntryView` — `client/Assets/GamePlay/UI/Overlay/PlayerModifierEntryView.cs` [новый файл — добавить в GamePlay.csproj] — иконка модификатора
  6.3. Создать `PlayerModifierTooltipView` — `client/Assets/GamePlay/UI/Overlay/PlayerModifierTooltipView.cs` [новый файл — добавить в GamePlay.csproj] — тултип
  6.4. Создать `PlayerModifiersView` — `client/Assets/GamePlay/UI/Overlay/PlayerModifiersView.cs` [новый файл — добавить в GamePlay.csproj] — контейнер на сцене
  6.5. Создать `ModifierEntryPrefabDefinition` — `client/Assets/GamePlay/UI/Overlay/ModifierEntryPrefabDefinition.cs` [новый файл — добавить в GamePlay.csproj] — определение префаба иконки
  6.6. Добавить `PlayerModifiersView` на сцену `Game_Overlay.unity` — создать GameObject с компонентом, `ResponsiveContainer`, привязать к Canvas

**7. Тесты**
  7.1. Обновить все тесты карт — `backend/Tools/Tests/Game/` — заменить проверки `ModifierDisposeAction` на `ModifierRoundAction`, проверки `Set()` на `Add()`/`Remove()`
  7.2. Обновить `RoundActionServiceTests` — `backend/Tools/Tests/Game/PlayerMechanicsTests.cs` — проверить новую модель `Tick()`
  7.3. Обновить `SnapshotDiffGuardTests` — `backend/Tools/Tests/Game/SnapshotDiffGuardTests.cs` — новый формат модификаторов

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `backend/Game/GamePlay/Players/Modifiers.cs` | Ядро переработки — новое хранилище источников и перерасчет |
| `backend/Game/GamePlay/Context/RoundActionService.cs` | Новая модель `Tick()` вместо `OnTime()` |
| `shared/Game/Player/PlayerModifiersState.cs` | Сериализация состояния — список overview вместо словаря значений |
| `shared/Game/Snapshots/PlayerSnapshotRecord.cs` | Snapshot-записи — overview вместо пары Modifier+Value |
| `client/Assets/GamePlay/Players/Entity/Modifiers/PlayerModifiers.cs` | Клиентское хранилище — прием overview |
| `client/Assets/GamePlay/UI/Overlay/PlayerModifiersView.cs` | Главный UI компонент оверлея |
| `client/Assets/GamePlay/UI/Overlay/PlayerModifierEntryView.cs` | Иконка одного модификатора |
| `client/Assets/GamePlay/UI/Overlay/PlayerModifierTooltipView.cs` | Всплывающий тултип |
| `client/Assets/GamePlay/UI/Overlay/ModifierDescriptionsConfig.cs` | ScriptableObject с текстами тултипов |
| `client/Assets/GamePlay/Scenes/Game_Overlay.unity` | Сцена оверлея — добавить контейнер модификаторов |

### Документация к прочтению
- `.agents/docs/COMMON_CONTAINER.md` — паттерн `ISceneService` + `IScopeSetup` + `Create()` для клиентских компонентов
- `.agents/docs/COMMON_LIFETIMES.md` — Lifetime для подписок, без них утечки памяти
- `.agents/docs/COMMON_REACTIVE_COLLECTIONS.md` — `ViewableList`, `ViewableDictionary` для динамических UI-списков
- `.agents/docs/COMMON_REACTIVE_BASICS.md` — `EventSource`, `ViewableProperty` для событий
- `.agents/docs/PREFAB_CODEGEN.md` — `PrefabDefinition`, `PrefabBuilder` для генерации префабов
- `.agents/docs/GAMEPLAY.md` — поток игры, snapshot-синхронизация, карты
- `.agents/docs/COMMON_ORLEANS.md` — сериализация `State<T>`, `MemoryPack` для shared-моделей
- `.agents/docs/CODE_STYLE_FULL.md` — порядок членов, `_camelCase`, `NoAwait`

### Риски

1. **Shield и TrebuchetBoost** — `Shield` добавляется без длительности (бесконечный, `TurnsToEnd = -1`). `TrebuchetBoost` добавляется одной картой (`TrebuchetAimer`) и сбрасывается другой (`Trebuchet`). Эти сценарии не используют `ModifierDisposeAction` и требуют особой обработки в новой модели.
2. **Snapshot совместимость** — изменение `PlayerModifiersState` и `PlayerSnapshotRecord.ModifierUpdate` ломает сериализацию. Нужно обновить и клиент, и бэкенд одновременно.
3. **Множественные источники одного типа** — если две карты дают `AdditionalMoves`, перерасчет должен суммировать оба источника. При удалении одного — уменьшать, но не обнулять.
4. **Тесты** — очень много тестов завязано на `ModifierDisposeAction` и `Set()`. Все нужно переписать.
5. **SoulLinkDisposeAction** — не использует `ModifierDisposeAction`, а напрямую `Dec`. Нужно перевести на `ModifierRoundAction`.
6. **Cross-board экшены** — `BlackoutDisposeAction` и др. работают с `IBoard`/`ICell`, не с модификаторами. Их `Tick()` должен корректно удалять эффекты с клеток.
7. **Префаб Builder** — новый префаб иконки нужно сгенерировать через PrefabBuilder, затем запустить генерацию `Prefabs.cs`.
