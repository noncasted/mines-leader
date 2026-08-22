## Agent Test Extend — Результат

### Статус: Завершено

### Что сделано
- `BoardLayoutParser.Apply` пишет DSL на существующий `IBoard` (паддинг `t`, oversized → `ArgumentException`); `x` трактуется как `t`.
- `AgentMatchFixture` + `AgentMatchFixtureApplier` задают доску/руку/ману/ходы человека после `RestoreCards` и пишут board snapshot в init `MoveSnapshot`.
- CreateWithBot протаскивает фикстуру Shared → Meta → Orleans pipe (`[Id(2)] FixturePayload`) → `MatchCreateOptions` → `LastManStandingTurnBasedRound`.
- `HumanGoesFirst` работает по смыслу флага: цикл ходит `First(p != current)`, поэтому previous=bot ⇒ человек ходит первым (как без фикстуры).
- MCP: `game_start_vs_bot` с board/hand/mana/moves; `game_use_card type=Bloodhound`; `game_legal_plays`; `game_inspect_cell`; `game_wait_visual`. CLI: `tools/scripts/game-agent.py`.
- Legal plays на сервере: Medic без клеток, Bloodhound по `SelectTaken` ромба, Recycler extra ids. Закрытые мины не сериализуются.
- Inspect читает live `CellView` (не сеть). `wait_visual` поллит `IsAnimating` на своём поле.
- Сценарии колод: `--bot Easy|Medium|Hard`, `--deck`, `--bot-deck`, `--scenario name` из `tools/scripts/agent-scenarios/`. Профиль бота только на этот матч.

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `backend/Game/GamePlay/Boards/BoardLayoutParser.cs` | `Apply` на существующий board | 1 | `AgentMatchFixtureApplierTests` |
| `shared/Game/Agent/AgentMatchFixture.cs` | MemoryPack DTO | 1 | compile |
| `backend/Game/GamePlay/Agent/AgentMatchFixtureApplier.cs` | board/hand/mana/moves + snapshot | 1 | `ApplyHand_AfterRestoreCards_ReplacesWithBloodhound` |
| `backend/Tools/Tests/Game/AgentMatchFixtureApplierTests.cs` | 4 теста фикстуры | 1 | 4 passed |
| `shared/Backend/SharedMatchmaking.cs` | `CreateWithBot.Fixture` | 2 | compile |
| `backend/Meta/Matches/MatchPayloads.cs` | `[Id(2)] FixturePayload` | 2 | compile |
| `backend/Meta/Matches/MatchFactory.cs` | прокидывает fixture | 2 | `MatchmakingTests` |
| `backend/Orchestration/MetaGateway/Matchmaking/Matchmaking.cs` | optional fixture arg | 2 | `MatchmakingTests` 6 passed |
| `backend/Orchestration/MetaGateway/Matchmaking/MatchmakingCommands.cs` | request.Fixture | 2 | compile |
| `backend/Game/Session/Root/Session.cs` | `MatchCreateOptions.Fixture` | 2 | compile |
| `backend/Game/Global/SessionEndpoints.cs` | `ToShared()` | 2 | compile |
| `backend/Game/GamePlay/Context/Rounds/LastManStandingTurnBasedRound.cs` | apply + previous player | 2 | `Process_AppliesFixtureAfterRestoreCards` |
| `client/Assets/Meta/Connection/BackendEndpoints.cs` | fixture param | 3 | — |
| `client/Assets/Meta/Matchmaking/Matchmaking.cs` | fixture param | 3 | — |
| `client/Assets/GamePlay/Editor/Agent/GameAgentMcpTools.cs` | start fixture, type, new tools | 3–5 | — |
| `client/Assets/GamePlay/Agent/GameAgentBridge.cs` | type resolve, legal plays, inspect, wait | 3–5 | — |
| `tools/scripts/game-agent.py` | `--board --hand --type inspect wait-visual` | 3–5 | `python3 -m py_compile` |
| `shared/Game/Agent/SharedAgentLegalPlays.cs` | request/response DTO | 4 | compile |
| `shared/Game/Agent/CardUsePayloadFactory.cs` | `CreateDefault` | 4 | `CardUsePayloadFactoryTests` 6 passed |
| `backend/Game/GamePlay/Agent/AgentLegalPlaysBuilder.cs` | server rules | 4 | `AgentLegalPlaysBuilderTests` 5 passed |
| `backend/Game/GamePlay/Commands/RequestAgentLegalPlaysCommand.cs` | typed RPC | 4 | compile |
| `backend/Tools/Tests/Game/AgentLegalPlaysBuilderTests.cs` | Medic/Bloodhound/Recycler | 4 | 5 passed |
| `client/Assets/GamePlay/Boards/Cells/CellView.cs` | `Inspect` / `IsAnimating` | 5 | — |
| `client/Assets/GamePlay/Boards/Cells/CellAnimator.cs` | `IsPlaying` / `PlayingKind` | 5 | — |
| `client/Assets/GamePlay/Boards/Cells/Taken/FlagAnimator.cs` | `IsPlaying` | 5 | — |
| `client/Assets/GamePlay/Boards/Cells/CellInspect.cs` | inspect DTO | 5 | — |

### Отличия от плана
- Orleans pipe несёт `FixturePayload` (`[GenerateSerializer]`), не Shared `AgentMatchFixture` напрямую — Shared без Orleans SDK.
- `HumanGoesFirst` ставит previous player, не «current = human»: иначе цикл сделал бы первым бота.
- `game_legal_plays` — RPC `Request<SharedAgentLegalPlaysResponse>`, не one-way observation.

### Нерешенные вопросы
- Unity Editor не был запущен: клиентские скрипты не проходили compile в Editor.
- Legal plays для карт кроме Medic/Bloodhound/Recycler помечаются `Error = Legal plays not implemented` (v1 lock).
- Сценарии колод/бота добавлены после первого прохода: JSON в `tools/scripts/agent-scenarios/` + `--bot/--deck/--bot-deck`. Агент по-прежнему сам ходит MCP-инструментами — это матч-сетап, не скрипт ходов.
