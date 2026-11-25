# Claude Code Instructions

## Overview
Competitive multiplayer minesweeper. Three codebases in one repo:
- **`client/`** — Unity3D (own DI container of the framework with compile-time generation, UniTask, custom reactive/lifetime framework)
- **`backend/`** — .NET Orleans (distributed actor model); subfolders: `Game/`, `Meta/`, `Cluster.Game/`, `Console/`, `Orchestration/`, `Tools/`
- **`shared/`** — Protocol, domain models, configs — used by both client and backend

## Build and test

- `tools/scripts/run-tests.sh` is the only full run (GitHub CI builds and tests only the backend): backend (needs Docker), the container generator
  and Unity EditMode, all in a snapshot of the working tree, so the open editor and a running cluster stay untouched;
  `--skip-unity`, `--skip-backend`. One class and the test logs: `.claude/docs/TESTING.md`.
- Backend build: `dotnet build backend/backend.slnx`.

## Framework (`tools/framework`)

The shared base of our games is a git submodule at `tools/framework` with its own `CLAUDE.md` (read it before
changing anything there): the forked packages, the client core, flow, modules and platforms (the ones of this game are
listed in `client/Packages/manifest.json`), the protocol base and the backend `Common`, `Infrastructure`, `Cluster`,
`Console` and `Session`.
- A change in `tools/framework` affects every game: commit and push it inside the submodule, then commit the
  submodule pointer here.
- Client: the content of this game is `client/Assets/Common/Internal` (assembly `Internal.Content`, namespace
  `Internal`): generated catalogues, options, the container asset map and the root scope (`InternalScopeExtensions`,
  it also adds the network). Every asmdef that references `Internal` references `Internal.Content` too. The modules
  of the global scope are registered in `client/Assets/Common/Global/Setup/GlobalScopeExtensions.cs` with the prefabs
  and options of this game. The layout for the framework editor tools is `client/ProjectSettings/Framework.json`.
- Protocol: `shared/Protocol/NetworkContextUnion.cs` declares the `INetworkContext` union (framework and session
  tags, game tags from 1000) and `EntityPayloadUnion`; `shared/Protocol/ProtocolHash.cs` holds the protocol hash.
- Sessions: the real-time session is the framework `backend/Session` (namespace `Game.Session`); this game creates
  its sessions in `backend/Game/Global/SessionFactory.cs` (no reconnect window, bots, players built by the backend)
  and keeps its session log in `backend/Game/Session/Logging`.
- Cluster: the game part is `backend/Cluster.Game` (same namespaces as the framework `Cluster`): game configs, live
  data of matches and matchmaking, service roles (`GameRoles`), game switches (`IGameFlags`).
- Console: the game part is `backend/Console` (assembly `Console.Game`): home page, game pages, benchmarks. Game
  flags and extra infrastructure cards are registered in `backend/Orchestration/ConsoleGateway/Program.cs`.

## Framework docs

The patterns shared by the games on the framework live in `tools/framework/.claude/docs`. Their index with the
keywords and the architecture principles:

@tools/framework/.claude/docs/INDEX.md

## Keyword -> Documentation

Docs of this game; the shared ones are in the index above. Values of this game for the shared skills:
`.claude/project-skills.md`.

| Keywords | Go to |
|----------|-------|
| DeployId, IDeployManagement, IDeployContext, IDeployAware, DeployIdPipe, DeployIdentity, DeployLifetime, cluster restart | docs/obsidian/architecture/deploy-epoch.md |
| error lookup, why X fails, memory leak, NullRef | .claude/docs/ERRORS.md |
| game flow, board, cell, mine, flag, card, CardType, ICard, snapshot, bot, matchmaking, BoardParser, game tests | .claude/docs/GAMEPLAY.md |
| card effect values, card description tokens, CardConfigOptions, CardDescriptionProvider, `_Normal` / `_Max` config | .claude/docs/CARD_EFFECTS.md |
| new card, card idea, card design, card validation, fail reasons, why card rejected | docs/obsidian/game/cards/fail/fail_reasons.md |
| common mistakes, top errors, checklist failures | .claude/docs/CLAUDE_MISTAKES.md |
| terminology, which term to use | .claude/docs/VOCABULARY.md |
| deploy client, WebGL deploy, deploy-webgl.sh, nginx.conf, Coolify web, rollback client | client/deploy/README.md, .claude/skills/deploy-client/SKILL.md, .claude/project-skills.md (deploy-client) |
| backend deploy, Coolify, docker-compose, Dockerfile, deploy branch, deploy failure | .claude/docs/DEPLOY.md, .claude/docs/DEPLOY_TROUBLESHOOTING.md, backend/Tools/deploy/COOLIFY.md |
| telemetry, metrics, logs, backend/.telemetry, file logging, session logs, client telemetry, startup trace, frame timing, match metrics, MatchGameMetrics, SessionMetrics, IMatchClock, match history, IMatch record, Grafana, /api/client-telemetry, /api/match-metrics, /api/match-history, CONSOLE_TOKEN | .claude/docs/TELEMETRY.md, skills `client-telemetry`, `match-history` |
| test logs, xUnit v3, UTF-16LE, get-test-log, TestResults, filter-class, ITestOutputHelper, dotnet test, run-tests.sh, run all tests | .claude/docs/TESTING.md |

## MOST IMPORTANT RULE

**Fail fast, always.** If the code does not explicitly expect an error, require a fallback, or alter control flow on
failure, we crash with the error. In Unity above all:
- No silently swallowed failures: no `if (something == null) return;`, no empty `try/catch`.
- No defensive null checks "just in case".
- An unexpected condition throws, or logs an error through `ILog<T>`, and fails loudly: a bug is visible at once
  instead of hidden.

Error handling and graceful degradation are added only when the user explicitly asks for them.

## Critical Rules

1. **Never look inside `/bin` or `/obj` folders** — only read source code
2. **Answer prose in Russian, code identifiers/comments in English**
3. **No emojis in code or docs**
4. **UTF-8 encoding** only
5. **Update knowledge base** (`.claude/docs/CLAUDE_MISTAKES.md`, the doc of the area) when you learn new mistakes, so they are not repeated
6. **Every `Advise` / `View` / `ListenClick` needs a `Lifetime`** or it is a memory leak; `Lifetime` is used in both client and backend, it is not client-only
7. **When compacting context:** always preserve — MonoBehaviour full pattern, Lifetime rules, list of modified files, any in-progress task state
8. **Don't mix client/backend patterns** — container/MonoBehaviour services (`ISceneService` + `IScopeSetup` + `Create()` + `OnSetup()`) only in client, Orleans grain pattern (grain state is `State<T>`, injected via `[State]` in the constructor) only in backend
