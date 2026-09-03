---
task: agent_card_play
status: implemented
phase: verification
created: 2026-09-03
updated: 2026-09-03
total_steps: 6
completed_steps: [1, 2, 3, 4, 5, 6]
blocked_steps: []
---

# Agent Card Play — spec

Authoritative contract. If this file and the code disagree, this file wins. Do not invent API that is not written here.

Repo root: `/projects/mines-leader`

---

## Что я хочу

Agent play (`.agents/skills/agent-play/SKILL.md` + `tools/scripts/game-agent.py`) ходит, но **не думает**: почти каждый ход это `board-solver → open`, карты из руки лежат мёртвым грузом. Для тестирования карт это бесполезно — карта либо не сыграна, либо сыграна случайно.

Нужно, чтобы агент **каждый ход осознанно решал по каждой карте**: играть или нет, куда, и почему. И чтобы для этого у него была информация, а не только имя типа.

Причины текущего поведения (см. разбор ниже) — все шесть чинятся в этой задаче:

1. Skill ставит карты последним пунктом и «только если». Пока солвер даёт safe open, до карт очередь не доходит.
2. Skill велит пропускать карты с `Legal plays not implemented` — это все карты кроме Bloodhound / Medic / Recycler.
3. Observation отдаёт руку как `Id` / `Type` / `ManaCost`. Что делает карта, куда её можно ткнуть, какой формы паттерн — агент не знает.
4. Мана полностью восстанавливается в конце хода (`LastManStandingTurnBasedRound.cs:276-281`), несыгранная карта = выброшенный ресурс. Skill об этом молчит.
5. Единственный источник «стратегии карт» — `card-advisor.py` через внешнюю LLM и ключ OpenCode, выключен по умолчанию.
6. Фикстуру руки/доски нельзя применить: `game_start_vs_bot` принимает `JObject _` и только ждёт матч, который `GameMock` создал по одному `_mode`. Клиентский путь фикстуры выпилен в `2d58626a`. Нельзя сказать «проверь Trebuchet».

**Не делаем:** many matches / bot-vs-bot / batch. Не делаем pixel QA. Не делаем оценку силы карт через внешнюю модель как обязательный шаг. Не делаем «автоигру» скриптом — решения принимает агент, скрипты только дают данные.

---

## Цель

После задачи агент на `/agent-play`:

1. В начале своего хода перечисляет руку с описанием каждой карты и по каждой пишет решение: **сыграть (куда, зачем)** или **пропустить (почему)**. Пропуск «не знаю, что это» невозможен — описание есть в observation.
2. Разведку (`Sonar`, `MinefieldScout`, `Bloodhound`, `Excavator`, `ThermalVision`, `ChaosDiamond`, `ChaosScout`) играет **до** солвера, атаку (`Trebuchet`, `MineCluster`, `CarpetBomb`, `ChainReaction`, `Smoke`, `FogOfWar`, `Frost`, `Blackout`) — по подсказке солвера в плотный закрытый участок врага, ресурсы (`Overclock`, `Adrenaline`, `ManaSurge`, `BloodPact`) — до открытий, `Shield` — перед guess, `Medic` — при `Health < HealthMax`.
3. `game_legal_plays` отдаёт клетки для **любой** карты с позицией и не отдаёт ошибку для карт без позиции.
4. `board-solver.py` подсказывает цели для карт: `scout_targets` на своём поле, `attack_targets` на поле врага.
5. После `use-card` агент видит, что изменилось (`Events` + diff доски), и пишет это в отчёт.
6. `start --hand Trebuchet Smoke --board ... --bot Hard` реально применяется: агент может собрать сценарий под конкретную карту.

Критерий приёмки одной строкой: в отчёте `## Agent play` есть секция `Cards`, в которой за матч из ≥ 5 ходов сыграно ≥ 3 карт разных типов, и по каждой несыгранной карте есть причина.

---

## Контекст

- Сделано: `docs/tasks/complete/agent_play.md`, `docs/tasks/current/agent_test_extend/` (fixture, legal plays, inspect, wait_visual). Режим `GameMatchType.LastManStandingTurnBased = 31`.
- Skill: `.agents/skills/agent-play/SKILL.md`. Step 3 — цикл хода. Step 4 — отчёт.
- CLI: `tools/scripts/game-agent.py`. `ensure-play` уже ставит `GameMock._mode` через `execute_code` + `SerializedObject` и жмёт Play через `manage_editor`.
- Observation: `shared/Game/Agent/SharedAgentObservation.cs` (`AgentCardView` = Id / Type / ManaCost). Билдер: `backend/Game/GamePlay/Agent/AgentObservationBuilder.cs` (`CollectHand`).
- Legal plays: `backend/Game/GamePlay/Agent/AgentLegalPlaysBuilder.cs`. Сейчас `FillBoardCells` — `switch` только на Bloodhound, всё остальное `Error = "Legal plays not implemented for {type}"`. Карты без позиции (`Medic`) уже возвращаются без ошибки.
- Конфиги карт: `shared/Configs/CardConfigOptions.cs`. `ICardConfig.Target` (`CardTarget.OwnBoard / OpponentBoard / Self / Opponent`), `ICardConfig.ManaCost`. Размеры через интерфейсы `IAreaSizeCardConfig.Size`, `ILengthCardConfig`, `IRandomSizeCardConfig.MinSize/MaxSize`, `IRandomLengthCardConfig`, `IChainCardConfig`, `ISearchRadiusCardConfig`. `CardConfigOptions.All` — словарь `CardType → ICardConfig`.
- Паттерны: `shared/Game/Cards/PatternShapes.cs` — `Rhombus(size)`, `Line(length, horizontal)`, `Cross(size)`. `SelectTaken(board, pos)`.
- Каталог карт с текстом эффекта есть в двух местах и нигде в коде: `tools/scripts/card-advisor.py` (`CARDS` dict, 53 записи) и `docs/obsidian/game/cards/implemented/cards_implemented_all.md`.
- Мана: `LastManStandingTurnBasedRound.EndTurn` — `Mana.SetMax(+1)` до `MaxManaCap`, затем `Mana.Restore`. Ходы — `Moves` тоже восстанавливаются.
- Фикстура на бэкенде рабочая и покрыта тестами: `AgentMatchFixture`, `AgentMatchFixtureApplier`, `MatchFactory.CreateWithBot(…, fixture)`, `client/Assets/Meta/Matchmaking/Matchmaking.cs:16` уже принимает `AgentMatchFixture fixture = null`. Единственный разрыв — `GameMock.Process` вызывает `CreateGameWithBot(scope.Lifetime, _mode)` без фикстуры, а `game_start_vs_bot` не создаёт матч сам.
- `GameMock` — `client/Assets/Common/Flow/Mocks/GameMock.cs`, одно поле `[SerializeField] GameMatchType _mode`.
- Domain reload при входе в Play сбрасывает статики. `_mode` уже передаётся через `SerializedObject` и пачкает сцену (см. `git status` — `Game_Field.unity` modified). Для фикстуры повторять это не хотим.
- Солвер: `tools/scripts/board-solver.py`, `solve(observation, side)` → `safe_opens / chords / flags / guesses / closed / flagged`. Уже умеет `--side opponent`. Есть `--self-test`.

---

## Locked decisions

1. Решения по картам принимает **агент** (Claude в skill), не python. Скрипты дают факты: описание, легальные клетки, кандидаты целей, diff.
2. Описание карт живёт **на сервере в observation**, один источник правды. `card-advisor.py` и markdown-каталог не являются источником для skill.
3. `game_legal_plays` для карт с позицией считает клетки **по конфигу** (target board + паттерн), без вызова `Use`. Для карт без позиции — `NeedsPosition=false`, `Error` пустой. Строка `Legal plays not implemented` исчезает из кода.
4. Skill: фаза карт **до** фазы сапёра. Каждая карта в руке — явное решение. Пропуск без причины запрещён текстом skill.
5. Skill: правило «мана сгорает в конце хода» написано явно в Step 3.
6. Фикстура передаётся в `GameMock` через **`EditorPrefs` JSON-ключ**, только под `#if UNITY_EDITOR`, читается один раз и удаляется. Production `GameMock` без ключа ведёт себя как сейчас. Сцена не пачкается.
7. `game_start_vs_bot` по-прежнему **не** входит в Play и не создаёт матч. Фикстуру кладёт `ensure-play` / `start` в `game-agent.py` через `execute_code` **до** `manage_editor play`.
8. Oracle-мины не текут ни в observation, ни в legal plays, ни в подсказки солвера. Солвер работает только по player-visible клеткам.
9. `card-advisor.py` остаётся опциональным. В этой задаче его не удаляем и не делаем обязательным. Можно (не обязательно) переключить его `CARDS` на поля из observation.
10. Никаких изменений в `LastManStandingRound` / `TimeLimitedRound` / боте.

---

## Observation: описание карт

`shared/Game/Agent/SharedAgentObservation.cs`, расширить `AgentCardView`:

```csharp
[MemoryPackable]
public partial class AgentCardView
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public int ManaCost { get; set; }
    public string Target { get; set; } = string.Empty;   // OwnBoard | OpponentBoard | Self | Opponent
    public string Shape { get; set; } = string.Empty;    // Rhombus | Line | Cross | Chain | Single | None
    public int Size { get; set; }                        // 0 когда Shape = None; для Random* — MaxSize
    public bool NeedsPosition { get; set; }
    public string Summary { get; set; } = string.Empty;  // одна строка, английский
}
```

Правила заполнения (`AgentObservationBuilder.CollectHand`):

- `Target` — `config.Target.ToString()`. Если конфига нет — `Self`.
- `NeedsPosition` — `CardUsePayloadFactory.CreateDefault(type) is IBoardCardUsePayload` (тот же критерий, что в `AgentLegalPlaysBuilder`).
- `Shape` / `Size` — из интерфейсов конфига. Таблица соответствия **зашита в новый статический класс** `shared/Game/Agent/AgentCardCatalog.cs`, не выводится из `ICardConfig` эвристикой: там `CardType → (Shape, Summary)`. `Size` берётся из конфига (`IAreaSizeCardConfig.Size`, `ILengthCardConfig.Length`, `IRandomSizeCardConfig.MaxSize`, `IRandomLengthCardConfig.MaxLength`, `IChainCardConfig.SpawnSize`); если ни один интерфейс не реализован — `0`.
- `Summary` — из `AgentCardCatalog`. Текст переносится из `card-advisor.py` `CARDS[*].summary`. `_Max` варианты получают тот же текст, что базовый тип.
- `hideHand: true` (рука противника без oracle) — все новые поля пустые/0, как сейчас `Type = "?"`.

`AgentCardCatalog`:

```csharp
public static class AgentCardCatalog
{
    public static AgentCardInfo Get(CardType type);   // всегда возвращает; unknown → Shape "None", Summary "Unknown card"
    public static IReadOnlyDictionary<CardType, AgentCardInfo> All { get; }
}

public sealed class AgentCardInfo
{
    public string Shape { get; init; } = "None";
    public string Summary { get; init; } = string.Empty;
}
```

Тест: `AgentCardCatalogTests` — для **каждого** значения `CardType` (через `CardTypeExtensions.All`) `Summary` непустой и `Shape` из допустимого набора. Это ловит добавление карты без описания.

---

## Legal plays: все карты

`AgentLegalPlaysBuilder.FillBoardCells` заменить на общий алгоритм:

1. `board = TargetBoard(type)` — как сейчас. Если `null` (доска врага не сгенерирована) — уже обработано выше, ошибка `Opponent board is not generated yet` остаётся.
2. `shape = AgentCardCatalog.Get(type).Shape`, `size` — из конфига по тем же правилам, что observation.
3. Для каждой позиции доски:
   - `Rhombus` → `PatternShapes.Rhombus(size).SelectTaken(board, pos).Count > 0`
   - `Cross` → `PatternShapes.Cross(size).SelectTaken(board, pos).Count > 0`
   - `Line` → `PatternShapes.Line(size, horizontal: true).SelectTaken(...)` **или** вертикальная — легальна, если хотя бы одна ориентация задевает taken. (Если реальная карта выбирает ориентацию иначе — посмотреть `MinefieldScout.Use` и повторить его правило; не придумывать.)
   - `Single` (`OpponentBomb`) → клетка taken и не flagged.
   - `Chain` (`ZipZap`, `ChainReaction`) → клетка taken.
   - `None` при `NeedsPosition == true` → пустые `Cells` и `Error = "No target rule for {type}"` — единственная допустимая ошибка, и тест обязан проверить, что для текущего `CardType` она не возникает ни для одной карты.
4. Карты `Target = Self | Opponent` — `NeedsPosition = false`, `Cells` пустые, `Error` пустой (уже так).

Мины не сериализуются (правило 8).

Тесты `AgentLegalPlaysBuilderTests` дополнить:

1. Для каждого `CardType` с `NeedsPosition` на 8×8 полностью taken доске `Cells.Count > 0` и `Error` пустой (параметризованный тест по `CardTypeExtensions.All`).
2. `Trebuchet` до генерации доски врага → `Opponent board is not generated yet`, после генерации → `Cells` непустые.
3. `OpponentBomb` — flagged клетка не в списке.
4. `Medic` / `Overclock` / `Siphon` — `NeedsPosition false`, `Error` пустой.

Run:

```bash
dotnet test backend/Tools/Tests/Tests.csproj -- --filter-class "*AgentLegalPlays*" --filter-class "*AgentCardCatalog*" --filter-class "*AgentObservationBuilder*"
```

---

## Solver: цели для карт

`tools/scripts/board-solver.py`, в результат `solve()` добавить два списка. Оба считаются **только** по видимым клеткам, детерминированно, без рандома.

```json
"scout_targets": [
  {"x": 5, "y": 7, "shape": "Rhombus", "size": 4, "unknown": 21, "reason": "most unknown closed cells in rhombus 4"}
],
"attack_targets": [
  {"x": 9, "y": 3, "shape": "Rhombus", "size": 4, "closed": 24, "reason": "densest closed pocket on opponent board, no proven opponent mines inside"}
]
```

- `scout_targets` — для `side=self`. Для каждой формы из `{Rhombus 4, Cross 2, Line 5}` одна лучшая клетка: максимум `unknown` (closed, не flagged, не в `mines` и не в `safe` солвера). Отдаём top-3 по каждой форме, отсортировано по `unknown` desc.
- `attack_targets` — для `side=opponent` (солвер прогоняется по `Opponent` из того же observation). Для `{Rhombus 4, Cross 2, Line 5}` максимум `closed` внутри паттерна, **минус** клетки, где сам солвер по доске врага доказал мину (там мина уже есть, сажать бесполезно). Top-3 по форме.
- Формы и размеры — дефолты; CLI `--shape Rhombus --size 3` переопределяет.
- Клетка-центр может быть любой (в том числе открытой), потому что серверный паттерн `SelectTaken` смотрит на клетки вокруг. Финальную легальность подтверждает `legal-plays`, не солвер.

CLI: `board-solver.py --from-agent --json` печатает оба списка. `--side` больше не нужен для attack: `attack_targets` всегда считается по `Opponent`, если у него `BoardAscii` непустой.

`--self-test` дополнить: доска 8×8 с одним открытым углом → `scout_targets[0]` в противоположном углу; доска врага с известными флагами → `attack_targets` не включает клетку, у которой в ромбе все закрытые уже доказанные мины.

---

## Diff после действия

`tools/scripts/game-agent.py`: каждая команда, возвращающая observation (`state`, `open`, `chord`, `flag`, `use-card`, `end-turn`, `wait`), после печати сохраняет её в `$XDG_CACHE_HOME/game-agent/last-state.json` (fallback `~/.cache/game-agent/`). Новая команда:

```bash
python3 tools/scripts/game-agent.py diff
```

Берёт свежий `game_get_state`, сравнивает с сохранённым, печатает JSON:

```json
{
  "self": {"opened": [[3,4],[3,5]], "flagged": [[7,7]], "unflagged": [], "exploded": []},
  "opponent": {"opened": [], "flagged": [], "unflagged": [], "exploded": []},
  "resources": {"Health": [3, 3], "Mana": [5, 2], "MovesLeft": [3, 3]},
  "hand": {"removed": ["Sonar"], "added": ["Excavator"]},
  "events": ["...последние события после сохранённого Sequence..."]
}
```

Если сохранённого состояния нет — `HasError: true`, `Error: "no previous state"`. Не трогает матч.

---

## Fixture через GameMock

`client/Assets/Common/Flow/Mocks/GameMock.cs`:

```csharp
public const string FixturePrefsKey = "MinesLeader.GameMock.Fixture";

public override async UniTaskVoid Process()
{
    ...
    var fixture = ReadFixture();
    var sessionData = await matchmaking.CreateGameWithBot(scope.Lifetime, _mode, fixture);
    ...
}

private static AgentMatchFixture ReadFixture()
{
#if UNITY_EDITOR
    if (UnityEditor.EditorPrefs.HasKey(FixturePrefsKey) == false)
        return null;
    var json = UnityEditor.EditorPrefs.GetString(FixturePrefsKey);
    UnityEditor.EditorPrefs.DeleteKey(FixturePrefsKey);
    return string.IsNullOrEmpty(json) ? null : JsonConvert.DeserializeObject<AgentMatchFixture>(json);
#else
    return null;
#endif
}
```

JSON-форма ключа = поля `AgentMatchFixture` как есть (`SelfBoardLayout`, `SelfHand`, `SelfDeck`, `BotDeck`, `BotProfile`, `HumanGoesFirst`, `Mana`, `Moves`). `CardType` сериализуется строкой (`StringEnumConverter`), чтобы `--hand Trebuchet` не требовал знать числа.

`tools/scripts/game-agent.py`:

- `_ensure_play(client, fixture: dict | None)`: если `fixture` непустой — **до** `manage_editor play` выполнить `execute_code`, который пишет `EditorPrefs.SetString(GameMock.FixturePrefsKey, json)`. Если матч уже активен (`already_active`) и фикстура задана — вернуть ошибку `Match already running; stop play mode to apply a fixture`, не молча игнорировать.
- `start` собирает `params` как сейчас (`_start_params`) и передаёт их в `_ensure_play`. `game_start_vs_bot` вызывается без параметров.
- Маппинг CLI → JSON: `board → SelfBoardLayout`, `hand → SelfHand`, `deck → SelfDeck`, `bot_deck → BotDeck`, `bot → BotProfile`, `human_goes_first → HumanGoesFirst`, `mana → Mana`, `moves → Moves`.
- Убрать секцию «Fixture gap» из skill; сценарии `tools/scripts/agent-scenarios/*.json` становятся рабочими.

`GameAgentMcpTools.StartVsBot` не меняется.

Проверка: `start --hand Trebuchet Smoke --human-first` → `state` показывает `Self.Hand` ровно `[Trebuchet, Smoke]` и `IsOwnTurn: true`. Второй `start` без фикстуры → обычная случайная рука (ключ удалён).

---

## Skill: новый Step 3

Переписать `.agents/skills/agent-play/SKILL.md` Step 3 и Step 4. Обязательные элементы текста:

**Правило маны.** Дословно: «Mana refills at the end of every turn. Unspent mana is lost. A card left in hand with affordable cost is a wasted turn unless you write why it is worse than nothing.»

**Порядок хода:**

1. `wait` → `state`. Прочитать `Self.Hand` (теперь с `Target` / `Shape` / `Summary`), `Mana`, `Health`, `MovesLeft`.
2. `legal-plays` + `board-solver --from-agent --json`.
3. **Card phase.** По каждой карте в руке одна строка решения в формате `Type: play @x,y — reason` или `Type: skip — reason`. Допустимые причины skip: `cost > mana`, `legal-plays error`, `no useful target (solver: … )`, `saving for guess (Shield)`, `HP full (Medic)`. «Не понял карту» — недопустимая причина.
   Приоритет: ресурсы (`Overclock`, `Adrenaline`, `ManaSurge`, `BloodPact`, `ManaFountain`, `Focus`, `PowerSurge`) → разведка своего поля (`scout_targets`) → атака (`attack_targets`) → добор (`Scavenger`, `Salvage`, `Recycler`, `MysticDraw`) → защита (`Shield` если дальше guess, `Medic` если `Health < HealthMax`).
   После каждой `use-card` — `diff`, потом заново `state` + solver, потому что разведка меняет доску.
4. **Minesweeper phase.** Как сейчас: flags → chords → safe opens → один guess при `MovesLeft > 0`. Перед guess — если в руке `Shield` и хватает маны, сыграть его.
5. Если после сапёра осталась мана и карта с легальной целью — вернуться в п.3 один раз.
6. `end-turn`.

**Отчёт (Step 4).** Добавить обязательную секцию:

```
Cards:
- turn 1: Sonar @5,7 → flagged 3 (events: …) | skipped Trebuchet (opponent board not generated)
- turn 2: Trebuchet @9,3 → planted; skipped Medic (HP full)
- unplayed at game over: [Shield]
```

Убрать из skill: «Play a card only when…», «Skip types whose legal-plays error is `Legal plays not implemented`», секцию «Fixture gap», упоминание `card-advisor.py` как единственного пути к картам (оставить одной строкой как optional).

---

## План реализации

#### 1 Skill: card phase first
- **Статус:** [x] done
- **Цель:** Агент каждый ход перечисляет карты и принимает явное решение по каждой; мана-правило написано; отчёт содержит `Cards`.
- **Как:** Переписать Step 3 / Step 4 по разделу «Skill: новый Step 3». Пока нет описаний в observation (шаг 2), временно разрешить skill читать `Summary` из `tools/scripts/card-advisor.py` `CARDS` — убрать эту строку в шаге 2.
- **Проверка:** Прогнать `/agent-play` один матч. В отчёте есть `Cards`, ни одного skip без причины.
- **Файлы:** `.agents/skills/agent-play/SKILL.md`
- **Зависит от:** —
- **Блокирует:** —

#### 2 Observation: описание карт
- **Статус:** [x] done
- **Цель:** `Self.Hand[*]` содержит `Target / Shape / Size / NeedsPosition / Summary`; каждый `CardType` покрыт каталогом.
- **Как:** `AgentCardCatalog` в shared, расширить `AgentCardView`, заполнить в `CollectHand`. Перенести тексты из `card-advisor.py`. Тест на полноту каталога.
- **Проверка:** `AgentCardCatalogTests` зелёный; `state` в Unity показывает `Summary` у карт; рука противника без oracle — пустые поля.
- **Файлы:** `shared/Game/Agent/SharedAgentObservation.cs`, `shared/Game/Agent/AgentCardCatalog.cs` [новый], `backend/Game/GamePlay/Agent/AgentObservationBuilder.cs`, `backend/Tools/Tests/Game/AgentCardCatalogTests.cs` [новый], `backend/Tools/Tests/Game/AgentObservationBuilderTests.cs`, `.agents/skills/agent-play/SKILL.md` (убрать временный fallback)
- **Зависит от:** —
- **Блокирует:** 3

#### 3 Legal plays для всех карт
- **Статус:** [x] done
- **Цель:** `game_legal_plays` отдаёт клетки для любой карты с позицией; ошибка `Legal plays not implemented` удалена.
- **Как:** Общий алгоритм по `Shape` из каталога + размер из конфига. Параметризованный тест по всем `CardType`.
- **Проверка:** filter-class `*AgentLegalPlays*`. `grep -rn "Legal plays not implemented" backend` пусто.
- **Файлы:** `backend/Game/GamePlay/Agent/AgentLegalPlaysBuilder.cs`, `backend/Tools/Tests/Game/AgentLegalPlaysBuilderTests.cs`
- **Зависит от:** 2
- **Блокирует:** —

#### 4 Solver: scout / attack targets
- **Статус:** [x] done
- **Цель:** `board-solver.py --json` отдаёт `scout_targets` и `attack_targets`.
- **Как:** По разделу «Solver: цели для карт». Расширить `--self-test`.
- **Проверка:** `python3 tools/scripts/board-solver.py --self-test` зелёный; на живом матче `attack_targets` непустой после первого хода бота.
- **Файлы:** `tools/scripts/board-solver.py`
- **Зависит от:** —
- **Блокирует:** —

#### 5 Diff после действия
- **Статус:** [x] done
- **Цель:** `game-agent.py diff` показывает, что изменила карта.
- **Как:** Сохранять последнюю observation в кэш, новая команда `diff`.
- **Проверка:** `use-card --type Sonar --x 5 --y 7` → `diff` показывает новые `flagged` и `Mana` до/после.
- **Файлы:** `tools/scripts/game-agent.py`
- **Зависит от:** —
- **Блокирует:** —

#### 6 Fixture через GameMock
- **Статус:** [x] done
- **Цель:** `start --hand … --board … --bot …` применяется; сценарии из `agent-scenarios/` работают.
- **Как:** `EditorPrefs` ключ, `GameMock.ReadFixture`, `_ensure_play` пишет ключ до Play. Убрать «Fixture gap» из skill.
- **Проверка:** `start --scenario cross_vs_easy` → `Self.Hand == [Trebuchet, OpponentBomb]`. Повторный `start` без аргументов → случайная рука. Production билд не трогает `EditorPrefs`.
- **Файлы:** `client/Assets/Common/Flow/Mocks/GameMock.cs`, `tools/scripts/game-agent.py`, `.agents/skills/agent-play/SKILL.md`
- **Зависит от:** —
- **Блокирует:** —

Порядок: 1 и 2 дают основной эффект и не зависят друг от друга. 3 после 2. 4, 5, 6 независимы, можно параллельно.

---

## Решения при реализации (отклонения от контракта выше)

Контракт написан до сверки с `Use` карт. Где `Use` расходится с текстом контракта, реализация повторяет `Use`, иначе legal plays врали бы агенту.

1. **Клетки паттерна зависят от карты, не только от формы.** `Trebuchet`, `MineCluster`, `CarpetBomb`, `FortuneBlast`, `FogOfWar` берут `SelectFree` (закрывают открытые клетки врага и сажают мины); `Smoke`, `Frost`, `Blackout`, `ChaosFog`, `DimensionRift` берут `SelectAll`; разведка и флаг-карты берут `SelectTaken`. Поэтому `AgentCardInfo` получил третье поле `Cells` (`Taken | Free | Any`), а legal plays фильтруют по нему. В observation это поле не выведено: `Summary` явно пишет «OPEN cells» / «closed cells» / «any cells».
2. **`ZipZap`** — `Rhombus` / `Free` (в `Use`: `SelectFree` ромба `Size`, потом поиск мин в радиусе 4), не `Chain`. **`ErosionDozer`** — `Chain` / `Taken` (`GetClosedShape` от клика), в контракте карта не упоминалась.
3. **Тест «все карты с позицией на 8×8»** идёт на полу-открытой доске (левая половина открыта), иначе Free-карты не имели бы клеток. Отдельный тест фиксирует, что `Trebuchet` на полностью закрытой доске врага легальных клеток не имеет.
4. **Конфиги**: `Smoke`, `FogOfWar`, `Frost`, `Blackout` теперь реализуют `IAreaSizeCardConfig`, `ChaosFog` — `IRandomSizeCardConfig` (поля `Size` / `MinSize` / `MaxSize` у них уже были, интерфейсов не было; `CARD_EFFECTS.md` их и так перечислял). Побочный эффект: у `ChaosFog` в `cards-info.json` наконец подставляются `{MIN_SIZE}`/`{MAX_SIZE}`.
5. **`attack_targets`** отдаёт две метрики: `metric: "closed"` (как в контракте, минус доказанные мины) и `metric: "open"` (для карт, которым нужны открытые клетки). Top-3 на форму и метрику. `scout_targets` без изменений. `Line` оценивается по лучшей ориентации, как в `MinefieldScout.Use`.
6. **`diff` baseline** сохраняют только `state`, `wait` и сам `diff`. Если бы `use-card` тоже сохранял свою observation (как написано в контракте), `use-card → diff` всегда был бы пустым. Добавлено поле `closed` (Trebuchet закрывает клетки), `opponent_resources` и `ManaMax`.
7. **`stop`** — новая CLI-команда (`manage_editor stop`): без неё агент не может выполнить требование «stop play mode to apply a fixture». `ensure-play` принимает те же fixture-аргументы, что и `start`.
8. **`OpponentBomb`** по контракту исключает флагнутые клетки. По коду `Use` флагнутая клетка тоже валидна и, если под флагом мина, наносит урон. Оставлено как в контракте; при желании снять ограничение — одна строка в `AgentLegalPlaysBuilder.IsLegal`.
9. `card-advisor.py` (опционально по п. 9 locked decisions) теперь читает `Summary` / `Target` / `Shape` / `Size` / `NeedsPosition` из observation, `CARDS` остался как fallback для старых билдов.

## Ключевые файлы

| Файл | Роль |
|------|------|
| `.agents/skills/agent-play/SKILL.md` | Инструкция агенту; Step 3/4 |
| `tools/scripts/game-agent.py` | CLI, `ensure-play`, `start`, новый `diff` |
| `tools/scripts/board-solver.py` | Солвер; новые `scout_targets` / `attack_targets` |
| `tools/scripts/card-advisor.py` | Источник текстов `Summary` для переноса; остаётся optional |
| `shared/Game/Agent/SharedAgentObservation.cs` | `AgentCardView` |
| `shared/Game/Agent/AgentCardCatalog.cs` | [новый] Shape + Cells + Summary по `CardType`, `ResolveSize` |
| `shared/Configs/CardConfigOptions.cs` | `Target`, размеры |
| `shared/Game/Cards/PatternShapes.cs` | `Rhombus / Line / Cross` |
| `backend/Game/GamePlay/Agent/AgentObservationBuilder.cs` | `CollectHand` |
| `backend/Game/GamePlay/Agent/AgentLegalPlaysBuilder.cs` | Общий `FillBoardCells` |
| `client/Assets/Common/Flow/Mocks/GameMock.cs` | `ReadFixture` из `EditorPrefs` |
| `client/Assets/Meta/Matchmaking/Matchmaking.cs` | Уже принимает fixture |
| `client/Assets/GamePlay/Editor/Agent/GameAgentMcpTools.cs` | Не меняется |
| `backend/Tools/Tests/Game/AgentCardCatalogTests.cs` | [новый] полнота каталога по `CardTypeExtensions.All` |

## Документация к прочтению

- `.agents/docs/GAMEPLAY.md` — round, cards
- `.agents/docs/CARD_EFFECTS.md` — паттерны и targeting карт
- `.agents/docs/TESTING.md` — xUnit
- `.agents/docs/CODE_STYLE_FULL.md`, `.agents/docs/API_DESIGN_FULL.md`
- `docs/tasks/current/agent_test_extend/agent_test_extend_info.md` — контракт fixture / legal plays
- `docs/obsidian/game/cards/implemented/cards_implemented_all.md` — сверка текстов

## Риски

- `MemoryPack` DTO: новые поля `AgentCardView` меняют wire-формат observation; клиент и сервер должны пересобираться вместе (как и раньше при любом изменении Shared).
- `Line` в legal plays: если реальная карта выбирает ориентацию по правилу, которого нет в конфиге, солвер и legal plays разойдутся с `Use`. Сверить с `MinefieldScout.Use` перед реализацией.
- `EditorPrefs` — глобальный для машины, не для проекта. Ключ содержит `MinesLeader.`; удаляется после чтения, чтобы не протечь в следующий Play.
- Skill стал длиннее: следить, чтобы агент не начал «играть в отчёт» вместо игры. Формат решения по карте — одна строка.
- `attack_targets` считает плотность закрытых, а не вероятность мины: это подсказка, не оценка. Агент обязан сверять с `legal-plays`.
