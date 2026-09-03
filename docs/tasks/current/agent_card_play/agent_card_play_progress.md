---
task: agent_card_play
updated: 2026-09-03
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| 1 Skill: card phase first | [x] | `.agents/skills/agent-play/SKILL.md` Step 3.2 card phase до сапёра, mana rule дословно, Step 4 `Cards`; `grep "Legal plays not implemented"` по репо пуст | — |
| 2 Observation: описание карт | [x] | `AgentCardCatalogTests` (5 тестов, 3 параметризованных по 74 `CardType`), `AgentObservationBuilderTests.SelfHand_Trebuchet_DescribesOpponentBoardRhombus`, `OpponentHand_OracleFalse_HidesCardType` | — |
| 3 Legal plays для всех карт | [x] | `AgentLegalPlaysBuilderTests.EveryPositionCard_HalfOpenBoards_HasCellsAndNoError` (37 карт с позицией), Trebuchet до/после генерации, OpponentBomb flagged, MinefieldScout ориентации | — |
| 4 Solver: scout / attack targets | [x] | `python3 tools/scripts/board-solver.py --self-test` → `self-test ok` (ромб 4 = 12 клеток как в `RhombusShape`, scout в дальнем квадранте, closed-метрика не берёт доказанные мины) | — |
| 5 Diff после действия | [x] | `game-agent.py diff` офлайн-прогон на двух observation: opened / closed / flagged / unflagged / exploded, resources, hand, events; baseline сдвигается | — |
| 6 Fixture через GameMock | [x] | `GameMock.ReadFixture` из `EditorPrefs` под `#if UNITY_EDITOR`; `game-agent.py start --scenario cross_vs_hard` собирает `{"BotProfile":"Hard","SelfHand":[...]}` и пишет ключ до Play; при активном матче — ошибка `Match already running` | — |

Тесты: `backend/Tools/Tests/bin/Debug/net10.0/Tests --filter-namespace Tests.Game` → 704 passed, 0 failed. Агентские классы (`*AgentLegalPlays*`, `*AgentCardCatalog*`, `*AgentObservationBuilder*`, `*CardUsePayloadFactory*`, `*AgentMatchFixture*`) → 282 passed.

## Заметки

- Контракт: `agent_card_play_info.md`, раздел «Решения при реализации» — отклонения от исходного текста и почему.
- Many matches / bot-vs-bot / pixel QA / обязательный внешний LLM-советник — вне скоупа.
- `dotnet test` на SDK 10 падает с `Testing with VSTest target is no longer supported`; запускать собранный `backend/Tools/Tests/bin/Debug/net10.0/Tests --filter-class ...` после `dotnet build`.

### [2026-09-03] Разбор текущего поведения

Агент открывает клетки руками и не играет карты по шести причинам: skill ставит карты последними и «только если»; skill велит пропускать всё с `Legal plays not implemented` (все карты кроме Bloodhound / Medic / Recycler); observation не описывает карты (`Id` / `Type` / `ManaCost`); правило «мана сгорает в конце хода» нигде не написано; `card-advisor.py` выключен по умолчанию и требует внешний ключ; фикстура руки не доезжает до матча (`GameMock` создаёт матч без fixture после `2d58626a`).

### [2026-09-03] Реализация

- Shared: `AgentCardCatalog` (Shape / Cells / Summary на все 74 `CardType`, `ResolveSize` из конфига), `AgentCardView` + `Target / Shape / Size / NeedsPosition / Summary`, интерфейсы размера у `Smoke` / `FogOfWar` / `Frost` / `Blackout` / `ChaosFog`.
- Backend: `AgentObservationBuilder.CollectHand` заполняет новые поля; `AgentLegalPlaysBuilder.FillBoardCells` — общий алгоритм по каталогу, строка `Legal plays not implemented` удалена.
- Сверка с `Use`: Trebuchet / MineCluster / CarpetBomb / FortuneBlast / FogOfWar работают по **открытым** клеткам врага, не по закрытым. Контракт это не учитывал; legal plays и `attack_targets` (метрика `open`) исправлены по коду.
- Tools: `board-solver.py` `scout_targets` / `attack_targets` + `--shape` / `--size`; `game-agent.py` `diff`, `stop`, fixture через `EditorPrefs` в `ensure-play` / `start`; `card-advisor.py` читает описания из observation.
- Client: `GameMock.ReadFixture`.
- Skill: Step 3 переписан (mana rule, card phase → minesweeper → второй проход), Step 4 с обязательной секцией `Cards`, секция «Fixture gap» удалена.

### [2026-09-03] Live-прогон 1: матч завис после фикстуры

- `start --scenario cross_vs_hard` доехал до бэкенда: session log `00:30_c574fc3d…` показывает `Profile=Hard` и базовые баффы, потом тишина; клиент висит на «All players connected», observation нет.
- Причина: `bot_deck` в сценарии 4 карты, `HandSize` 6. `RestoreCards` для бота на пятом `DrawCard` бросает `Deck is empty`, раунд умирает без записи в session log.
- Фикс: `AgentMatchFixtureApplier.ReplaceDeck` повторяет список фикстуры по кругу до `Hand.Size` (как `Deck.Init`). Тест `ApplyDecks_ShortBotDeck_CyclesToHandSize_SoRestoreDoesNotThrow`; `SelfHandAndDeck_SkipRestore_*` обновлён под паддинг.
- Побочный вывод: сама цепочка `EditorPrefs → GameMock → CreateGameWithBot → MatchFactory` работает, `GameMock.cs` компилируется.
- Нужен перезапуск кластера (Aspire поднимает `bin/Release`, старые бинарники фикса не содержат).

### [2026-09-03] Live-прогон 2: фикстура и карты работают

- После перезапуска кластера `start --scenario cross_vs_hard` → `Self.Hand == [Trebuchet, OpponentBomb]`, `IsOwnTurn: true`, `Summary` / `Target` / `Shape` заполнены, рука бота скрыта (`Type: "?"`, новые поля пустые).
- Ход 1: обе карты `skip — legal-plays error: Opponent board is not generated yet`; сапёр по солверу: 1 open + 4 chord, 14 флагов, HP 3.
- Ход 2: `legal-plays` отдал Trebuchet 184 клетки, OpponentBomb 132, Smoke 256, ChainReaction 133. `Trebuchet @2,3` (top `attack_targets` c `metric: open`) → `diff`: 12 клеток врага закрыты, мана 5→3, из руки ушёл Trebuchet. `OpponentBomb @3,0` → `diff`: клетка врага открыта, мана 3→1, HP врага без изменений. Затем 5 chord по солверу, `end-turn`.
- Замечено: `wait` может вернуть `IsOwnTurn: true` с `Trigger: opponent_turn` до `turn_start`, и первый `use-card` падает `Not your turn`; повторный `wait` решает. Записано в skill.
- Замечено: `Events` содержат `[Bot] State | ... Hand=[...]` — рука бота видна через session log, хотя observation её скрывает. Не мины, но утечка информации; в этой задаче не трогал.

### [2026-09-03] Live-прогон 3: end-turn после нуля ходов пропускает следующий раунд

- В `LastManStandingTurnBased` раунд закрывается сам, когда `Moves.Left` доходит до 0 (`TurnsCountdown`). `[Turn] Skipped` пишется только из `SkipTurn()`, а `end-turn` клиента — это `SharedGameAction.SkipTurn`.
- Агент после последнего `chord` (Moves 0) ещё слал лишние chord (`Not your turn`), бот успел доиграть, начался новый свой раунд с полной рукой, и `end-turn` его скипнул без карт и клеток. Trebuchet / Smoke сыграны только на следующем раунде.
- Замечено: после `MovesLeft == 0` раунд закрывается не мгновенно. Пока Hard-бот доигрывает свою фазу, мост отдаёт устаревшие кадры `action` с `IsOwnTurn: true` (видно по session-логу). Это ожидаемое поведение; лечится повторным `wait` до `turn_start`.
- Правило записано в skill (3.4 и «Do not»): после последнего move — `wait` до `turn_start`; `end-turn` только при `MovesLeft > 0`. Подсказка команды в `game-agent.py` уточнена.

### [2026-09-03] Баг инструмента: HasError залипал в state после неудачного действия

- Воспроизведение: round 6, sequence 97–106. После `use-card` с `HasError` каждый `game-agent.py state` выходил с кодом 1, `board-solver.py --from-agent` падал до следующего кадра с сервера.
- Причина: серверная ошибка действия приходит как observation с `HasError = true` и оседает в `GameAgentBridge.LastObservation`; `GetState` отдаёт её как есть. Пустой `Error` давали `OpenCellCommand` / `OpenMultipleCellsCommand` / `SetFlagAction` / `RemoveFlagAction`: они возвращали `EmptyResponse.Failed` без сообщения.
- Правка клиента: `GameAgentBridge.SendAction` пропускает результат через `ConsumeError`: вызывающий получает ошибку один раз, а в `LastObservation` остаётся тот же кадр без флага. Клиентские ошибки моста (`Not your turn` и т. п.) в `LastObservation` не попадали и раньше. `GamePlay.csproj` собирается через `dotnet build`; в Unity нужна перекомпиляция.
- Правка backend: те четыре команды теперь возвращают `Cell is already open` / `Chord needs an open cell` / `Cell is already flagged` / `Cell is not flagged`. Тесты `OpenCellCommandTests`, `FlagActionTests`, `BoardCommandTests`, `CardUseCommandTests` зелёные (27). Нужен перезапуск кластера.

### [2026-09-03] Разбор пяти сессий сторонних агентов

Транскрипты: `backend/.telemetry/logs-games/{deepseek-v4-flash,deepseek-v4-pro,glm-5.3-flash,gpt-5.6-luna,kimi-k3}.txt`.

- Общая причина долгих матчей и мусора в контексте: `state` и любое действие печатали полную observation с `Cells` (2000–4000 строк). Все пять моделей после первого дампа писали свой фильтр `python3 -c 'json.load...'` и таскали его в каждой команде. 60–95 вызовов инструментов на матч, по 5–15 с размышления на вызов.
- Вторая причина: двусмысленные ответы. Устаревшие `action`-кадры с `IsOwnTurn: true` после `MovesLeft 0` (петли `wait` у всех), залипший `HasError` (glm потерял 2 HP), `Cells: []` без `Error` для Bloodhound на несгенерированной своей доске (deepseek-v4-pro ушёл читать исходники и не сыграл ни хода), `Card not found in hand` при розыгрыше по id после добора.
- kimi-k3 проиграл с причиной `disconnected`: в 01:09:29 обе сокет-сессии (meta и game) упали «без close handshake», это сторона Unity, таймера хода в коде нет. Причина не найдена, `~/.config/unity3d/Editor.log` не наполняется.
- gpt-5.6-luna остановился сам («несколько ходов достаточно»): в скилле не было явного запрета.

Сделано по итогам:

- `game-agent.py`: компактный текстовый вывод по умолчанию (шапка, ресурсы, рука, события, обе доски ASCII с линейками), действия печатают одну строку с изменениями, `--json` для сырых данных; `wait` крутится до `turn_start` / game over / своего хода с ходами в запасе; новая команда `turn` = `wait` + state + legal plays с `best`-клетками от солвера + сводка солвера; `end-turn` отказывает при `MovesLeft 0`; каждое действие сдвигает базу для diff.
- `board-solver.py`: `zipzap_targets` (открытая клетка в ромбе 3, максимум закрытых в ромбе поиска 4), `limit` у целей, `format_human` с ограничением списков, `--from-agent` вызывает `--json state`.
- `AgentLegalPlaysBuilder`: до генерации своей доски карты по закрытым клеткам получают все клетки, ZipZap — ошибку `Own board is not generated yet: open a cell first`. Тесты `AgentLegalPlaysBuilderTests` 53 зелёные.
- Скилл: Step 3 переписан под `turn`, запреты: не читать исходники, не фильтровать вывод, карты только по `--type`, карты до последнего move, остановка только по `GameOver`.

- ZipZap больше не помечается `NeedsExtraCard`: поле `CardUsePayload.ZipZap.CardId` — рудимент (сервер читает `context.CardId`, бот кладёт туда id самого ZipZap), а `legal-plays` из-за него отдавал всю руку в `ExtraCardIds`, и агенты думали, что ZipZap сбрасывает карту. Мост по-прежнему подставляет id самой карты. Тест `ZipZap_NeedsNoExtraCard`.

- Долгий старт: сторонний агент тратил шесть вызовов и абзацы рассуждений на ручную проверку `curl` / `ps` / `stat` / `git status`, затем читал файлы сценариев и выбирал фикстуру для обычного `/agent-play`. Теперь `game-agent.py preflight` делает все проверки одной строкой (кластер 200, MCP, файлы `backend/` и `shared/` новее старта Silo), `start` запускает её сам и отказывает при провале (`--ignore-stale`). Скилл: Step 1 = `preflight`, Step 2 = `start` без фикстуры + `turn`; сценарии только по явной просьбе пользователя.

Не проверено вживую (в этой сессии Unity MCP не подключён): вывод `turn` на реальном матче, цикл `wait` с Hard-ботом. Офлайн-проверка рендера, `wait`-цикла и `end-turn`-guard сделана на синтетической observation.

### Что осталось проверить руками

1. `stop`, затем `start` без аргументов → случайная рука (ключ `EditorPrefs` удалён после чтения).
2. `/agent-play` полный матч ≥ 5 ходов, в отчёте `Cards` с ≥ 3 типами сыгранных карт.
3. `turn` на живом матче: первый ход с `--human-first` (все клетки для Bloodhound, ошибка для ZipZap), переход через раунд бота без лишних `wait`.

## Разбор v2-deepseek-v4-flash (2026-09-03)

Матч `02:17_4e8bd459`: 7 своих ходов, победа за 6 минут игрового времени, 4 типа карт, 142k токенов, общее время 10 минут. Прогон шёл по старому скиллу (правка Step 1/2 легла в 07:19, матч начался в 07:17), поэтому старт снова занял 4 минуты ручных проверок.

Что мешало агенту и что сделано:

- Ход 1 на несгенерированной доске: солвер печатал только `SAFE OPENS (none proven)`, агент долго сомневался, безопасен ли первый клик. `board-solver.py`: `solve` отдаёт `generated`, `format_human` печатает `BOARD NOT GENERATED ... open W/2 H/2` (генератор исключает клетку и соседей).
- Агент 4 хода не понимал, откуда у бота +1 HP: бот играл `Medic`, а в хвост `events` (8 строк) из ~40 строк раунда бота попадали `[Mana] Changed` и маркеры раундов. `game-agent.py`: `_fmt_events` оставляет только `[Card] Used`, `[Bot] Card |`, `[Cell] Opened`, `[Health]`, `[Buff]`, `[Game]`, `[Turn]`, без таймстампа. В скилл добавлен абзац **Match rules**. Первая версия ("копить бурст") заставила агента планировать урон даже без карт урона в руке; переписан: цель прогона отыграть карты, урон не тема, если `OpponentBomb` / `ChainReaction` нет в руке, лечение бота ожидаемо.
- Два Trebuchet подряд в одну область: `best` давал соседние центры 11,9 12,10 13,11, второй ушёл в `ERR No free cells in the pattern`. `_best_cells`: для площадных карт центры не ближе `Size` друг к другу (Manhattan).
- Потерянные ходы: `chord` по открытой клетке без закрытых соседей списывал ход и не логировался (агент потерял 2 хода в раунде 11 на `chord 11,2` / `chord 12,6` из устаревшего списка после каскада 27 клеток). `OpenMultipleCellsCommand` теперь отвечает `Fail("Chord reveals nothing: ...")` и `Fail("Chord needs N flag(s) around, M placed")` до списания хода; клиентский UI на ответ не смотрит (`Request` без обработки). Тест `OpenMultipleCellsCommandTests` (3 кейса), полный прогон 973 passed. Тот же механизм, скорее всего, объясняет `moves 5->4` на Trebuchet в раунде 5: `CardMovesCost` 0 во всех режимах, seq прыгнул 34→36 без записей в логе, похоже на клик по открытой клетке в окне Unity.
- В скилл 3.3 добавлено: после каскада список `CHORDS` / `SAFE OPENS` устаревает, пересчитывать перед следующим chord.

Не проверено вживую: новый вывод `events`, `BOARD NOT GENERATED` в реальном `turn`, отказ пустого chord через мост (нужен рестарт кластера).

## Разбор матча 02:35_0e12e691 (2026-09-03, серверный лог)

Случайная рука без фикстуры, 6 своих раундов по 56–93 с, матч 8,5 минуты, победа по флагам (`All opponent mines flagged`). Отыграно 7 типов карт: GamblersRuin x4, Bloodhound x5, ZipZap x4, Trebuchet x5, DimensionRift, Blackout_Max x3. Первый open в центр (8,8) по подсказке `BOARD NOT GENERATED`, каскад 91 клетка. Аккорд один за матч, остальное одиночные `SAFE OPENS`.

Найденная дыра: в раунде 1 агент отправил 4 `open` одной строкой при 3 оставшихся ходах. Четвёртый (`(2,8)`, 02:37:32.704) ушёл уже в раунд бота: сервер не проверял ни очередь хода, ни остаток ходов, `OpenCellCommand` записал `[Cell] Opened` в лог, затем `Moves.OnUsed` бросил исключение (`Turns cannot be less than zero`), и клиент получил `An error occurred while processing the command`. Клетка не открылась (нет `Revealed`, открыта заново в раунде 3). Тот же путь позволял сыграть карту во время раунда бота, если мост держал устаревший кадр `IsOwnTurn: true`.

Сделано:

- `GameCommand.RequireOwnTurn` (`Not your turn`, только когда `CurrentPlayer` задан, в реальном времени он null) и `RequireMove` (`No moves left`). `OpenCellCommand` / `OpenMultipleCellsCommand` вызывают `RequireMove` до `EnsureGenerated`, `CardUseCommand` вызывает `RequireOwnTurn`. Тесты: `OpenCellCommandTests` (+2), `OpenMultipleCellsCommandTests` (+1), `CardUseCommandTests` и `OpenCellCommandTests` теперь задают `CurrentPlayer` через `RoundOf(player)`.
- `game-agent.py`: `_action` отказывает `open` / `chord` / `use-card`, если сохранённое наблюдение показывает свой ход и `MovesLeft` 0 (`MovesLeft is 0: the round is closing, run \`turn\``).
- Скилл 3.3: не ставить в одну строку больше ходов, чем `moves` в последней строке результата.

Не проверено вживую (нужен рестарт кластера).

## Отзыв агента после матча 02:35: результат карты нечитаем

Агент: `opp: closed 12` не говорит, что сделал Trebuchet (уплотнение поля вывел по `mines 40 → 54`), Blackout_Max дал `self: no change`, DimensionRift выглядел криптично, `All opponent mines flagged` при `Winner=Human` двусмысленно.

Сделано:

- `game-agent.py`: `_board_diff` добавляет `counters` (`mines`, `flags` из счётчиков игрока) и `effects` (число клеток под Fog / Blackout / Frost / Smoke / MineHighlight из `Cells[].Effects`); строка результата печатает `mines 40->44`, `blackout cells 0->3`. После `use-card` вторая строка `Type: Summary` из каталога карты, которая ушла из руки (по `--type` или по diff руки).
- WinReason при победе по флагам во всех трёх раундах и в `AgentObservationPublisher`: `Player {id} flagged every mine on own board` вместо `All opponent mines flagged` (`GetFlagWinner` возвращает владельца полностью размеченной доски). Обновлён `docs/obsidian/game/modes/time-limited.md`.
- Скилл 3.2 описывает новые счётчики и строку `Type: summary`, Step 4 поясняет WinReason.

## Отзыв агента 2: таймаут вместо ошибки и мелочи

- `open` после обнуления ходов давал `ERR Timed out waiting for observation`: `GameCommand` при исключении внутри команды возвращал `Fail` до `Publish`, мост ждал кадр 10 с. Теперь catch публикует кадр с `HasError` и текстом ошибки (`GameCommandTests`). Вместе с `RequireMove` этот путь для ходов вне очереди больше не срабатывает, но любое другое исключение тоже дойдёт до агента текстом.
- Bloodhound на несгенерированной доске числился легальным без пояснения: `legal-plays` печатает `own board not generated: the card generates it around the click, nothing to scan yet; open a cell first` для карт `OwnBoard`.
- Заголовок руки из `turn` устаревал после draw-карт: после действия, добавившего карты, печатается строка `hand now:` с текущей рукой.
- Мана после `GamblersRuin` (`MaxMana` +2) была неочевидна: строка результата показывает `mana 6->6/6->8` (текущая/максимум с изменениями). Скилл 3.2 поясняет.

## Матч 03:07_f552beca и отзыв агента 3

Первый прогон на перезапущенном кластере: WinReason уже новый (`flagged every mine on own board`), агент получил `ERR Chord needs N flag(s)` вместо молчаливого `ok`. 6 своих раундов за 7,3 минуты, 15 карт, HP 3/3 весь матч. Блокеров агент не назвал; тормозила интерпретация карт с ветвлением.

Сделано по его списку:

- `hand:` после карты: `played GamblersRuin drew A B C` / `discarded X Y` / `no draw, no discard`. Ветка монетки видна без `state`.
- Строка `own mines 40->39 removed without damage (hp 3)` (или `with damage (hp 3->2)`). Различить подрыв и обезвреживание по наблюдению нельзя: `Cell.Explode()` только шлёт событие, клетка факт взрыва не хранит, статус `exploded` в `AgentObservationBuilder` мёртвый. Для решения важен только урон.
- После карты, изменившей свою доску (Bloodhound, ZipZap, DimensionRift): строка `own board changed by the card: LEGAL PLAYS best and SOLVER are stale, re-run ...`.
- `SCOUT TARGETS` в солвере группируются по центру: `1,1  rhombus 3: 5 unknown, cross 3: 5 unknown`.
- Скилл: причина `deferred: mana went to <Type> this turn`; пояснение `deck` / `stash` в заголовке; описание новых строк результата.

Не сделано: полные ASCII-доски каждый ход оставлены (агент сам назвал баланс спорным).

## Отзыв агента 4 (v5-deepseek-v4-flash)

Вход за три команды, блокеров нет, ошибки действий понятны. Главная находка: агент семь ходов пропускал GamblersRuin с выдуманным условием «нет чужих эффектов на своей доске» (перепутал с Purge). Причина в инструменте: компактный `turn` печатал руку как `Type(cost Target Shape)`, а summary карт из наблюдения не показывал вовсе, хотя скилл называл summary справочником. Агент не мог знать, что делает карта, и придумал.

Сделано:

- `turn` / `state`: блок `cards:` с summary по одному на тип карты в руке.
- Подкоманда `solve [--section all|flags|opens]`: солвер по текущему состоянию из `game-agent.py`; `--section flags` печатает `FLAGS: none` или список одной строкой (агент трижды гонял полный дамп ради этого вопроса). Из скилла убраны ссылки на `board-solver.py --from-agent` как на рабочую команду (агент дважды звал его как подкоманду `game-agent.py`).
- Секции `SCOUT / ZIPZAP / ATTACK TARGETS` печатаются только для типов карт в руке (`_solve_for_hand`).
- Заметки после карты: `opponent board changed by the card: ... stale` (второй Trebuchet в ту же область), `own flags 44->43: the card opened flagged cells that held no mine`.
- Скилл: карта с `no position` играется сразу, условий кроме `Fails if ...` в summary не существует; правило `cost > mana` только при `mana < cost` в момент решения, иначе `deferred`; победа по флагам в Match rules; формулировка про end-turn; таблица команд с `solve`.

Предложение агента добавить причину «no enemy effects to purge» для GamblersRuin не принято: условие выдумано.

## Баг игры: карта играется без маны

`CardUseCommand` не проверял `Mana.Current >= cost`, а `Mana.Use` обрезает остаток до нуля: карта за 4 играется при 2 манах. UI клиента такую карту прячет, мост агента нет. Сделано:

- `CardManaCost.Resolve(player, config)`: одна формула стоимости со скидками (`NextCardDiscount`, `AllCardsDiscount`) и штрафом (`ManaCostPenalty`) для розыгрыша, legal plays и наблюдения. `CardUseCommand` отвечает `Not enough mana: N needed, M left` до розыгрыша и до сброса `NextCardDiscount` (раньше Focus сгорал даже при отказе карты).
- `AgentLegalPlaysBuilder`: `ManaCost` теперь эффективная стоимость, при нехватке маны `Error` с тем же текстом. `AgentObservationBuilder`: `ManaCost` в руке тоже эффективная.
- `game-agent.py`: `use-card` отказывает локально по сохранённому наблюдению с тем же текстом.
- Тесты: `CardUseCommandTests` (+2: отказ без побочных эффектов, скидка Focus учитывается), `AgentLegalPlaysBuilderTests` (+1; тестовому игроку дано 99 маны, чтобы проверки клеток не упирались в бюджет). Полный прогон 980 passed.

Нужен рестарт кластера.

## Разбор «неоптимальных ходов» и что из него взято

Агент разобрал свои ходы как игрок на победу, хотя цель прогона отыграть карты; большая часть претензий (Blackout без пользы, поздний GamblersRuin, DimensionRift ради эффекта) это работа по назначению. Предложенные планировщик карт с EV, симуляция ходов, режимы `optimal`/`safe`, `solve-and-move` и оценочные причины пропуска не взяты: они превращают агента в обёртку вокруг скрипта и возвращают широкий список причин, под который агенты раньше пропускали карты.

Взято два пункта, оба в `game-agent.py`:

- Локальная проверка цели перед `open` / `chord` / `flag` / `unflag` по наблюдению, сохранённому после предыдущего действия (только на своём ходу, когда бот не может менять доску): `stale: 5,6 has no closed neighbours left (state seq 83); run solve for a fresh list`, `chord 7,5 needs 2 flag(s) around, 1 placed`, `stale: 0,0 is already open`, `3,3 is flagged: unflag it first`. Хвост батча по устаревшему списку теперь отсекается без похода на сервер.
- `best` для DimensionRift считается по обеим доскам: `x,y(opp open 4, own open 0, own flags 0)`, ранг = чужие открытые минус 2 x свои открытые минус 3 x свои флаги, центры разнесены на Size. Ход `@1,3`, снявший свой флаг, первым предложен бы не был.

