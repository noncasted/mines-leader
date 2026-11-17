# Claude Code Instructions

## Overview
Competitive multiplayer minesweeper. Three codebases in one repo:
- **`client/`** — Unity3D (own DI container of the framework with compile-time generation, UniTask, custom reactive/lifetime framework)
- **`backend/`** — .NET Orleans (distributed actor model); subfolders: `Game/`, `Meta/`, `Infrastructure/`, `Orchestration/`
- **`shared/`** — Protocol, domain models, configs — used by both client and backend

## Framework (`tools/framework`)

The shared base of our games is a git submodule at `tools/framework` with its own `CLAUDE.md` (read it before
changing anything there). It holds the forked packages (`client/packages`: Unity packages, UniTask, MemoryPack,
unity-mcp), the client core (`client/core`: assemblies `Internal`, `Internal.Editor`, `Internal.Tests` - DI container
and ContainerGenerator, reactive, scopes, catalogues, editor tools), the base of the game flow (`client/flow`), the
modules of the global scope (`client/modules`: backend, audio, inputs, ui, publish; `client/platforms`: itch, web -
namespaces `Global.*`, registered from `Global/Setup/GlobalScopeExtensions.cs` with the prefabs and options of this
game), the protocol base (`shared`, assembly `Framework.Shared`, namespace `Shared`) and backend `Common`, `Infrastructure`,
`Cluster` and `Console` (infrastructure pages and widgets).
- A change in `tools/framework` affects every game: commit and push it inside the submodule, then commit the
  submodule pointer here.
- The client content of this game is `client/Assets/Common/Internal` (assembly `Internal.Content`, namespace
  `Internal`): generated catalogues, catalogue groups, options, the container asset map, the root scope
  (`InternalScopeExtensions`, it also adds the network: `AddNetwork(NetworkOptions, ProtocolHash.Value)`) and the
  startup preload. The client network is the framework module `network` (`Internal.Network`). Every asmdef that
  references `Internal` references `Internal.Content` too. The layout for the framework editor tools is
  `client/ProjectSettings/Framework.json`.
- The protocol of this game: `shared/Protocol/NetworkContextUnion.cs` declares the `INetworkContext` union (framework
  tags + session tags + game tags, game tags from 1000) and `EntityPayloadUnion` (payload tags next to the payloads),
  `shared/Protocol/ProtocolHash.cs` holds the protocol hash.
- The real-time session is the framework `backend/Session` (namespace `Game.Session`): this game creates its sessions
  in `backend/Game/Global/SessionFactory.cs` (`SessionCreateOptions`: no reconnect window, bots, players built by
  the backend) and keeps its session log in `backend/Game/Session/Logging`.
- The game part of the cluster is `backend/Cluster.Game` (same namespaces as the framework `Cluster`): game configs
  (`AddGameConfigs`, `GameConfigsSetup`), live data of matches and matchmaking, service roles (`GameRoles`), game
  switches (`IGameFlags` over the `IClusterFeatures` game flags).
- The game part of the console is `backend/Console` (assembly `Console.Game`): home page, game pages, benchmarks.
  Game flags and extra infrastructure cards are registered in `ConsoleGateway/Program.cs`.

## Framework docs

The patterns shared by the games on the framework live in `tools/framework/.claude/docs`. Their index with the
keywords and the architecture principles:

@tools/framework/.claude/docs/INDEX.md

## Keyword → Documentation (FAST LOOKUP)

Docs of this game; the shared ones are in the index above. Values of this game for the shared skills:
`.claude/project-skills.md`.

| Keywords | Go to |
|----------|-------|
| DeployId, IDeployManagement, IDeployContext, IDeployAware, DeployIdPipe, DeployIdentity, DeployLifetime, LiveState, cluster restart | /docs/obsidian/architecture/deploy-epoch.md |
| error lookup, why X fails, memory leak, NullRef | .claude/docs/ERRORS.md |
| game flow, board, cell, mine, flag, card, CardType, ICard, snapshot, bot, matchmaking, BoardParser, game tests | .claude/docs/GAMEPLAY.md |
| new card, card idea, card design, card validation, fail reasons, why card rejected | /docs/obsidian/game/cards/fail/fail_reasons.md |
| trigger keywords, documentation finder, reading order | .claude/docs/TRIGGERS.md |
| common mistakes, top errors, checklist failures | .claude/docs/CLAUDE_MISTAKES.md |
| PrefabBuilder, prefab codegen, [PrefabDefinition], Prefabs.cs, convert prefab | .claude/docs/PREFAB_CODEGEN.md |
| deploy client, WebGL deploy, deploy-webgl.sh, nginx.conf, Coolify web, rollback client | client/deploy/README.md, .claude/skills/deploy-client/SKILL.md, .claude/project-skills.md (deploy-client) |
| telemetry, metrics, logs, backend/.telemetry, file logging, session logs, client telemetry, startup trace, frame timing, match metrics, MatchGameMetrics, SessionMetrics, IMatchClock, match history, IMatch record, Grafana, /api/client-telemetry, /api/match-metrics, /api/match-history, CONSOLE_TOKEN | .claude/docs/TELEMETRY.md, skills `client-telemetry`, `match-history` |
| test logs, xUnit v3, UTF-16LE, get-test-log, TestResults, filter-class, ITestOutputHelper, dotnet test, run-tests.sh, run all tests | .claude/docs/TESTING.md |

## Architecture

**Client (Unity3D):**
- MonoBehaviour services: ISceneService + IScopeSetup + Create() + OnSetup()
- DI: the framework container (generated at compile time), MonoBehaviour dependencies via an `[Inject]` method, registration in Create()
- Reactive: EventSource (events) / ViewableProperty (state) / ViewableList (collections)
- Lifetime: every Advise/View/ListenClick needs Lifetime or memory leak

**Backend (.NET Orleans):**
- State: `State<T>` for all grain state — inject via `[State]` in constructor
- Collections: `StateCollection<TKey, TValue>` — in-memory dict, auto-syncs from DB via messaging
- Messaging: IMessaging for pushing updates to subscribers (uses Lifetime for subscriptions)

**Shared:**
- Lifetime is used in both client and backend — it is not client-only

## MOST IMPORTANT RULE

**Fail fast, always.** If the code does not explicitly expect an error, require a fallback, or alter control flow on failure — **we CRASH with an error.**

Especially in Unity:
- **NEVER** silently swallow failures with `if (something == null) return;` or empty `try/catch` blocks unless the user explicitly asked for graceful degradation.
- **NEVER** add defensive null-checks "just in case."
- When an unexpected condition occurs, **throw an exception** or call `Debug.LogError` and let it fail loudly. This makes bugs visible immediately instead of hiding them.

If the user wants error handling, they will say so. Until then — **fail fast, fail loud.**

## Critical Rules

1. **Never look inside `/bin` or `/obj` folders** — only read source code
2. **Answer prose in Russian, code identifiers/comments in English**
3. **No emojis in code or docs**
4. **UTF-8 encoding** only
5. **Update knowledge base** when you learn new mistakes — never repeat!
6. **New .cs files must be added to .csproj manually** — Unity (client) and backend both require manual .csproj entry
7. **When compacting context:** always preserve — MonoBehaviour full pattern, Lifetime rules, list of modified files, any in-progress task state
8. **Don't mix client/backend patterns** — container/MonoBehaviour services only in client, Orleans grain pattern only in backend

## Full Documentation

**Patterns & rules:** container, lifetimes, reactive, API design, code style, Orleans, Blazor, decision trees - the
framework docs (index above, `tools/framework/.claude/docs`).

**Reference & lookup:**
- `.claude/docs/ERRORS.md` — error lookup table with causes & fixes
- `.claude/docs/GAMEPLAY.md` — game flow, board, cards, snapshot sync, bots, matchmaking
- `.claude/docs/PREFAB_CODEGEN.md` — PrefabBuilder API, converting prefabs to code, codegen workflow
- `.claude/docs/VOCABULARY.md` — consistent terminology
- `.claude/docs/CLAUDE_MISTAKES.md` — history of AI mistakes and lessons
- `.claude/docs/TRIGGERS.md` — keyword-based documentation finder, reading orders
- `.claude/docs/TELEMETRY.md` — backend/.telemetry directory: metrics, logs, game session logs; client telemetry, match metrics, match history, Grafana dev, the read APIs and the skills
