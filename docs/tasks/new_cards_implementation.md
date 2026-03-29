## Задача: Реализация 11 новых карт из card-ideas.md

### Цель
Добавить 11 новых карт (Medic, Minefield Scout, Mirror Wall, Siphon, Landslide, Decoy, Chain Reaction, Overclock, Fog of War, Scavenger, Hand Scramble), расширив shared-протокол, backend-логику, бот-стратегии и клиентские действия.

---

### Точки регистрации (на каждую карту)

Каждая новая карта требует изменений в **8-10 файлах**. Вот полный чеклист:

| # | Файл | Что добавить |
|---|------|-------------|
| 1 | `shared/Domain/CardType.cs` | Enum entry (+ Max variant если нужна) |
| 2 | `shared/Game/Cards/ICardUsePayload.cs` | `[MemoryPackUnion(N, ...)]` + payload class |
| 3 | `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | `[MemoryPackUnion(N, ...)]` + snapshot class |
| 4 | `shared/Configs/CardConfigOptions.cs` | `[MemoryPackUnion(N, ...)]` на ICardConfig (+ ICardSizeConfig), config class, properties, All dict |
| 5 | `backend/Game/GamePlay/Cards/NewCard.cs` | Реализация ICard [новый файл] |
| 6 | `backend/Game/GamePlay/Cards/CardFactory.cs` | Switch case в Create() |
| 7 | `backend/Game/GamePlay/Bot/CardStrategies/NewStrategy.cs` | Реализация IBotCardStrategy [новый файл] |
| 8 | `backend/Game/GamePlay/Bot/BotServiceExtensions.cs` | DI-регистрация стратегии |
| 9 | `client/Assets/GamePlay/Cards/Entities/Actions/CardNewAction.cs` | Реализация ICardAction [новый файл] |
| 10 | `client/Assets/GamePlay/Cards/Entities/States/CardStatesExtensions.cs` | AddCardAction + AddCardActionSync |

Дополнительно для отдельных карт:
- `shared/Game/Cells.cs` — новые `CellEffectType` (Shield, Decoy, Fog)
- `shared/Game/Cards/PatternShapes.cs` — `LineShape` для Minefield Scout
- `client/Assets/GamePlay/Cards/Entities/Root/CardContext.cs` — SelectTargetBoard для новых типов

---

### Шаги реализации

#### Фаза 1: Shared-инфраструктура (enums, конфиги, протокол)

**1.** Добавить 11 новых CardType в enum — `shared/Domain/CardType.cs`
```
Medic = 1100
MinefieldScout = 1200, MinefieldScout_Max = 1210
MirrorWall = 1300, MirrorWall_Max = 1310
Siphon = 1400
Landslide = 1500
Decoy = 1600
ChainReaction = 1700
Overclock = 1800
FogOfWar = 1900, FogOfWar_Max = 1910
Scavenger = 2000
HandScramble = 2100
```

**2.** Добавить 3 новых CellEffectType — `shared/Game/Cells.cs`
```
Shield = 200
Decoy = 300
Fog = 400
```

**3.** Добавить 11 payload-классов — `shared/Game/Cards/ICardUsePayload.cs`
- Board-targeted (IBoardCardUsePayload): MinefieldScout, MirrorWall, Landslide, Decoy, ChainReaction, FogOfWar
- Self/Opponent (ICardUsePayload): Medic, Siphon, Overclock, Scavenger, HandScramble

**4.** Добавить 11 snapshot-классов — `shared/Game/Snapshots/CardActionSnapshotRecord.cs`
- Все с `Guid TargetPlayer`
- ChainReaction: + `IReadOnlyList<Position> Explosions`
- MinefieldScout: + `IReadOnlyList<Position> RevealedCells`

**5.** Добавить 11 config-классов — `shared/Configs/CardConfigOptions.cs`
- Medic: ManaCost=4, Target=Self
- MinefieldScout: ManaCost=3, Target=OwnBoard, Size=5 (line length)
- MirrorWall: ManaCost=3, Target=OwnBoard, Size=3, Duration=2
- Siphon: ManaCost=2, Target=Opponent
- Landslide: ManaCost=5, Target=OpponentBoard, Size=5
- Decoy: ManaCost=2, Target=OwnBoard
- ChainReaction: ManaCost=4, Target=OpponentBoard, MaxChain=3
- Overclock: ManaCost=3, Target=Self, ExtraMoves=2
- FogOfWar: ManaCost=3, Target=OpponentBoard, Size=4, Duration=2
- Scavenger: ManaCost=2, Target=Self, DrawCount=2
- HandScramble: ManaCost=3, Target=Opponent

**6.** Добавить LineShape — `shared/Game/Cards/PatternShapes.cs`
- Горизонтальная/вертикальная линия от позиции (для Minefield Scout)

#### Фаза 2: Backend — реализация карт

**7.** Medic — `backend/Game/GamePlay/Cards/Medic.cs` [новый файл]
- `owner.Health.Heal(1)` — простейшая карта

**8.** MinefieldScout — `backend/Game/GamePlay/Cards/MinefieldScout.cs` [новый файл]
- Использует LineShape, открывает клетки линией; мины получают флаги вместо взрыва

**9.** MirrorWall — `backend/Game/GamePlay/Cards/MirrorWall.cs` [новый файл]
- Rhombus pattern, AddEffect(ShieldEffect) на каждую клетку
- ShieldDisposeAction через IRoundActionService (как Smoke)

**10.** Siphon — `backend/Game/GamePlay/Cards/Siphon.cs` [новый файл]
- `opponent.Mana.Use(1)`, `owner.Mana.SetCurrent(owner.Mana.Current + 1)`

**11.** Landslide — `backend/Game/GamePlay/Cards/Landslide.cs` [новый файл]
- Rhombus, собирает мины в области, перемещает в случайные закрытые клетки вне области

**12.** Decoy — `backend/Game/GamePlay/Cards/Decoy.cs` [новый файл]
- AddEffect(DecoyEffect) на одну клетку

**13.** ChainReaction — `backend/Game/GamePlay/Cards/ChainReaction.cs` [новый файл]
- Как OpponentBomb, но при мине проверяет соседей, цепная реакция до MaxChain

**14.** Overclock — `backend/Game/GamePlay/Cards/Overclock.cs` [новый файл]
- `owner.Moves.SetCurrent(owner.Moves.Left + config.ExtraMoves)`

**15.** FogOfWar — `backend/Game/GamePlay/Cards/FogOfWar.cs` [новый файл]
- Rhombus, SelectFree, AddEffect(FogEffect) + FogDisposeAction (как Smoke)

**16.** Scavenger — `backend/Game/GamePlay/Cards/Scavenger.cs` [новый файл]
- `owner.Deck.DrawCard()` x DrawCount, `owner.Hand.Add()` каждую

**17.** HandScramble — `backend/Game/GamePlay/Cards/HandScramble.cs` [новый файл]
- Сохранить `opponent.Hand.Entries.Count`, вернуть все карты в колоду, перетасовать, добрать то же количество

**18.** Обновить CardFactory — `backend/Game/GamePlay/Cards/CardFactory.cs`
- 11 новых switch cases (+ Max-варианты где есть)

#### Фаза 3: Backend — бот-стратегии

**19-29.** 11 новых файлов `backend/Game/GamePlay/Bot/CardStrategies/*Strategy.cs`:
- MedicStrategy, MinefieldScoutStrategy, MirrorWallStrategy, SiphonStrategy, LandslideStrategy, DecoyStrategy, ChainReactionStrategy, OverclockStrategy, FogOfWarStrategy, ScavengerStrategy, HandScrambleStrategy

**30.** Зарегистрировать все стратегии — `backend/Game/GamePlay/Bot/BotServiceExtensions.cs`

#### Фаза 4: Client — карточные действия

**31-41.** 11 новых файлов `client/Assets/GamePlay/Cards/Entities/Actions/Card*Action.cs`:
- Board-targeted: DropArea + Pattern (MinefieldScout, MirrorWall, Landslide, Decoy, ChainReaction, FogOfWar)
- Self/Opponent: DropDetector only (Medic, Siphon, Overclock, Scavenger, HandScramble)

**42.** Обновить CardStatesExtensions — `client/Assets/GamePlay/Cards/Entities/States/CardStatesExtensions.cs`
- AddCardAction: 11 новых switch cases
- AddCardActionSync: 11 новых switch cases

**43.** Обновить SelectTargetBoard в CardContext — `client/Assets/GamePlay/Cards/Entities/Root/CardContext.cs`

---

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `shared/Domain/CardType.cs` | Enum всех типов карт — добавить 11+ entries |
| `shared/Game/Cells.cs` | CellEffectType enum — добавить Shield, Decoy, Fog |
| `shared/Game/Cards/ICardUsePayload.cs` | Payload-ы для сетевого протокола — 11 новых классов |
| `shared/Game/Snapshots/CardActionSnapshotRecord.cs` | Snapshot-ы для синхронизации — 11 новых классов |
| `shared/Configs/CardConfigOptions.cs` | Конфиги баланса — 11 новых конфигов |
| `shared/Game/Cards/PatternShapes.cs` | Паттерны выбора — добавить LineShape |
| `backend/Game/GamePlay/Cards/CardFactory.cs` | Фабрика карт — 11+ новых switch cases |
| `backend/Game/GamePlay/Cards/Smoke.cs` | Образец для карт с CellEffect + Duration |
| `backend/Game/GamePlay/Cards/OpponentBomb.cs` | Образец для карт с уроном |
| `backend/Game/GamePlay/Cards/GraveDigger.cs` | Образец для Self-карт без доски |
| `backend/Game/GamePlay/Bot/BotServiceExtensions.cs` | DI-регистрация бот-стратегий |
| `backend/Game/GamePlay/Bot/CardStrategies/BotCardStrategies.cs` | Интерфейс IBotCardStrategy |
| `client/Assets/GamePlay/Cards/Entities/States/CardStatesExtensions.cs` | Клиентская регистрация действий |
| `client/Assets/GamePlay/Cards/Entities/Root/CardContext.cs` | Маппинг CardType -> TargetBoard |

### Документация к прочтению
- `rules/ORLEANS_GRAINS.md` — card implementations используют IGameContext, IPlayer
- `rules/CODE_STYLE.md` — стиль кода для 22+ новых файлов
- `docs/GAMEPLAY.md` — полное описание card system, board, snapshot sync

### Риски
- **MemoryPackUnion индексы** — union indices должны быть уникальные и последовательные. Текущие карты занимают 0-9. Новые должны начинаться с 10. Три разных union-интерфейса (ICardUsePayload, ICardActionData, ICardConfig) — индексы в каждом независимые
- **ICardSizeConfig union** — имеет свой отдельный набор MemoryPackUnion индексов (0-7 заняты), новые size-карты должны продолжать с 8
- **CellEffect взаимодействие** — Shield должен блокировать Trebuchet/FlagErase/Smoke. Нужно добавить проверки в существующие карты (Trebuchet.cs, OpponentFlagErase.cs, Smoke.cs и т.д.)
- **Decoy взаимодействие** — OpponentBomb должен проверять DecoyEffect. Нужно модифицировать OpponentBomb.cs
- **Hand overflow** — Scavenger добавляет 2 карты сверх лимита руки. Hand.Add() не проверяет Size, но клиент может не ожидать > 5 карт
- **Stash для HandScramble** — карты из руки противника идут в колоду, не в сброс. Stash.Collect() возвращает все, но нам нужен другой путь: Hand -> Deck напрямую
- **Deck shuffle** — для HandScramble нужен метод Shuffle() в Deck, которого сейчас нет (Init() делает shuffle при инициализации, но отдельного метода нет)
- **Max-варианты** — не все карты имеют Max-версию. Нужно решить: какие из 11 карт получат Max (MinefieldScout, MirrorWall, FogOfWar — да; остальные — под вопросом)
