---
task: agent_test_extend
updated: 2026-08-21
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| 1 Fixture apply + tests | [x] | `AgentMatchFixtureApplierTests` 4 passed | — |
| 2 Transport + round hook | [x] | `LastManStandingTurnBasedRoundTests` 5 passed; `MatchmakingTests` 6 passed | — |
| 3 MCP start fixture + use_card by type | [x] | `game_start_vs_bot` / `game_use_card` + `game-agent.py` | — |
| 4 Legal plays | [x] | `AgentLegalPlaysBuilderTests` 5 passed | — |
| 5 Inspect cell + wait_visual | [x] | `CellView.Inspect` / `WaitVisual`; Unity Editor was not running | — |

## Заметки

- Контракт: `agent_test_extend_info.md`.
- Many matches / bot-vs-bot / pixel QA — явно вне скоупа.
- Не зависит от `prefab_catalog`.
- Шаги 4 и 5 не зависят друг от друга; 5 не зависит от 1–3.

### [2026-08-21] Deck/bot scenarios

Named JSON scenarios + per-match `BotProfile` / `SelfDeck` / `BotDeck` on the fixture. Cluster `CurrentProfile` is not mutated. ApplyDecks runs before RestoreCards; a set SelfHand skips human RestoreCards so the draw pile stays intact.

### [2026-08-21] All steps implemented

- Orleans pipe cannot serialize unmarked Shared MemoryPack types: `RequestWithBot.[Id(2)]` is `FixturePayload` mapped to `AgentMatchFixture`.
- Round loop plays `First(p != current)`, so HumanGoesFirst sets the *previous* player (bot when true/null, human when false).
- `game_legal_plays` is a typed RPC (`SharedAgentLegalPlaysRequest`/`Response`), not an observation wait.
- Unity Editor was not connected; client scripts were not compiler-checked in Editor.

### [2026-08-21] Start step 1

`LastManStandingTurnBasedRound` ставит `_currentPlayer` на бота *до* цикла, а первый `ProcessRound` берёт `players.First(t => t != current)` — то есть человек уже ходит первым. `HumanGoesFirst` реализуем по смыслу флага: `true`/null fixture оставляет previous=bot; `false` ставит previous=human, чтобы первым ходил бот.

Orleans pipe `RequestWithBot` не может нести unmarked `AgentMatchFixture` из Shared — на пайпе будет `[GenerateSerializer]`-копия с `[Id(2)]`.
