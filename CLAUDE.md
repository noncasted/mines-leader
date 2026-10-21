# Claude Code Instructions

## Overview
Competitive multiplayer minesweeper. Three codebases in one repo:
- **`client/`** — Unity3D (VContainer DI, UniTask, custom reactive/lifetime framework)
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
  (`InternalScopeExtensions`), the startup preload and the client network (`Tools/Network`). Every asmdef that
  references `Internal` references `Internal.Content` too. The layout for the framework editor tools is
  `client/ProjectSettings/Framework.json`.
- The protocol of this game: `shared/Protocol/NetworkContextUnion.cs` declares the `INetworkContext` union (framework
  tags + game tags, game tags from 1000), `shared/Protocol/ProtocolHash.cs` holds the protocol hash.
- The game part of the cluster is `backend/Cluster.Game` (same namespaces as the framework `Cluster`): game configs
  (`AddGameConfigs`, `GameConfigsSetup`), live data of matches and matchmaking, service roles (`GameRoles`), game
  switches (`IGameFlags` over the `IClusterFeatures` game flags).
- The game part of the console is `backend/Console` (assembly `Console.Game`): home page, game pages, benchmarks.
  Game flags and extra infrastructure cards are registered in `ConsoleGateway/Program.cs`.

## Keyword → Documentation (FAST LOOKUP)

| Keywords | Go to |
|----------|-------|
| MonoBehaviour, ISceneService, IScopeSetup, Create(), OnSetup(), [Inject] | .claude/docs/COMMON_CONTAINER.md |
| Lifetime, Advise, View, Terminate, subscription, cleanup | .claude/docs/COMMON_LIFETIMES.md |
| EventSource, ViewableProperty, ViewableList, reactive, observable, event | .claude/docs/COMMON_REACTIVE_BASICS.md |
| UniTask, async, IReadOnlyList, file I/O, callback wrapping | .claude/docs/API_DESIGN_FULL.md |
| member order, _camelCase, GC.KeepAlive, NoAwait, braces | .claude/docs/CODE_STYLE_FULL.md |
| Grain, IGrainWithGuidKey, [Transaction], constructor injection | .claude/docs/COMMON_ORLEANS.md |
| State<T>, IStateValue, [GenerateSerializer], [Id(N)], StatesLookup, StateCollection | .claude/docs/COMMON_ORLEANS.md |
| DeployId, IDeployManagement, IDeployContext, IDeployAware, DeployIdPipe, DeployIdentity, DeployLifetime, LiveState, cluster restart | /docs/obsidian/architecture/deploy-epoch.md |
| Blazor, razor, @inject, UiComponent, early return, console UI | .claude/docs/BLAZOR.md |
| which pattern to use, decision | .claude/docs/DECISION_TREES.md |
| error lookup, why X fails, memory leak, NullRef | .claude/docs/ERRORS.md |
| game flow, board, cell, mine, flag, card, CardType, ICard, snapshot, bot, matchmaking, BoardParser, game tests | .claude/docs/GAMEPLAY.md |
| menu UI, UI Toolkit, .uss, color palette, MenuTheme, pixel art | .claude/docs/UI_MENU.md |
| jsonb, GrainStateStorage, PostgresJsonbConverter, OrleansStorage, PostgreSQL | .claude/docs/COMMON_ORLEANS.md |
| new card, card idea, card design, card validation, fail reasons, why card rejected | /docs/obsidian/game/cards/fail/fail_reasons.md |
| IOrleans, GetGrain, AddressableDictionary, AddressableDictionaryView, messaging, ListenQueue | .claude/docs/COMMON_ORLEANS.md |
| trigger keywords, documentation finder, reading order | .claude/docs/TRIGGERS.md |
| code examples, Docs_*.cs, working examples | .claude/docs/CODE_EXAMPLES.md |
| common mistakes, top errors, checklist failures | .claude/docs/CLAUDE_MISTAKES.md |
| full examples, Lifetime details, reactive details | .claude/docs/COMMON_*.md |
| PrefabBuilder, prefab codegen, [PrefabDefinition], Prefabs.cs, convert prefab | .claude/docs/PREFAB_CODEGEN.md |
| deploy client, WebGL deploy, deploy-webgl.sh, nginx.conf, Coolify web, rollback client | client/deploy/README.md, .claude/skills/deploy-client/SKILL.md |
| telemetry, metrics, logs, backend/.telemetry, file logging, session logs | .claude/docs/TELEMETRY.md |
| test logs, xUnit v3, UTF-16LE, get-test-log, TestResults, filter-class, ITestOutputHelper, dotnet test, run-tests.sh, run all tests | .claude/docs/TESTING.md |

## Architecture

**Client (Unity3D):**
- MonoBehaviour services: ISceneService + IScopeSetup + Create() + OnSetup()
- DI: VContainer, field injection via [Inject], registration in Create()
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
8. **Don't mix client/backend patterns** — VContainer/MonoBehaviour only in client, Orleans grain pattern only in backend

## Full Documentation

**Patterns & rules:**
- `.claude/docs/COMMON_CONTAINER.md` — VContainer DI, MonoBehaviour service pattern, lifecycle phases
- `.claude/docs/COMMON_LIFETIMES.md` + `.claude/docs/COMMON_LIFETIMES_PATTERNS.md` — Lifetime usage, scoped subscriptions
- `.claude/docs/COMMON_REACTIVE_BASICS.md` / `.claude/docs/COMMON_REACTIVE_VALUES.md` / `.claude/docs/COMMON_REACTIVE_COLLECTIONS.md` / `.claude/docs/COMMON_REACTIVE_PATTERNS.md` — EventSource, ViewableProperty, ViewableList
- `.claude/docs/API_DESIGN_FULL.md` — UniTask, return types, error handling
- `.claude/docs/CODE_STYLE_FULL.md` — member order, naming, braces, NoAwait
- `.claude/docs/COMMON_ORLEANS.md` — Grains, State<T>, IStateValue, StateCollection, IOrleans, messaging
- `.claude/docs/BLAZOR.md` — Blazor console UI: early returns, injection, UiComponent
- `.claude/docs/UI_MENU.md` — Menu UI Toolkit: color palette, reusable panel classes

**Reference & lookup:**
- `.claude/docs/DECISION_TREES.md` — which pattern to use (8 decision trees)
- `.claude/docs/ERRORS.md` — error lookup table with causes & fixes
- `.claude/docs/GAMEPLAY.md` — game flow, board, cards, snapshot sync, bots, matchmaking
- `.claude/docs/PREFAB_CODEGEN.md` — PrefabBuilder API, converting prefabs to code, codegen workflow
- `.claude/docs/VOCABULARY.md` — consistent terminology
- `.claude/docs/CLAUDE_MISTAKES.md` — history of AI mistakes and lessons
- `.claude/docs/TRIGGERS.md` — keyword-based documentation finder, reading orders
- `.claude/docs/CODE_EXAMPLES.md` — index of runnable code examples
- `.claude/docs/TELEMETRY.md` — backend/.telemetry directory: metrics, logs, game session logs
- `client/Assets/Common/Docs/Claude/*.cs` — runnable code examples for all patterns
