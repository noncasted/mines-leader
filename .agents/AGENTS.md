# Claude Code Instructions

## Overview
Competitive multiplayer minesweeper. Three codebases in one repo:
- **`client/`** — Unity3D (VContainer DI, UniTask, custom reactive/lifetime framework)
- **`backend/`** — .NET Orleans (distributed actor model); subfolders: `Game/`, `Meta/`, `Infrastructure/`, `Orchestration/`
- **`shared/`** — Protocol, domain models, configs — used by both client and backend

## Keyword → Documentation (FAST LOOKUP)

| Keywords | Go to |
|----------|-------|
| MonoBehaviour, ISceneService, IScopeSetup, Create(), OnSetup(), [Inject] | .agents/docs/COMMON_CONTAINER.md |
| Lifetime, Advise, View, Terminate, subscription, cleanup | .agents/docs/COMMON_LIFETIMES.md |
| EventSource, ViewableProperty, ViewableList, reactive, observable, event | .agents/docs/COMMON_REACTIVE_BASICS.md |
| UniTask, async, IReadOnlyList, file I/O, callback wrapping | .agents/docs/API_DESIGN_FULL.md |
| member order, _camelCase, GC.KeepAlive, NoAwait, braces | .agents/docs/CODE_STYLE_FULL.md |
| Grain, IGrainWithGuidKey, [Transaction], constructor injection | .agents/docs/COMMON_ORLEANS.md |
| State<T>, IStateValue, [GenerateSerializer], [Id(N)], StatesLookup, StateCollection | .agents/docs/COMMON_ORLEANS.md |
| DeployId, IDeployManagement, IDeployContext, IDeployAware, DeployIdPipe, DeployIdentity, DeployLifetime, LiveState, cluster restart | /docs/obsidian/architecture/deploy-epoch.md |
| Blazor, razor, @inject, UiComponent, early return, console UI | .agents/docs/BLAZOR.md |
| which pattern to use, decision | .agents/docs/DECISION_TREES.md |
| error lookup, why X fails, memory leak, NullRef | .agents/docs/ERRORS.md |
| game flow, board, cell, mine, flag, card, CardType, ICard, snapshot, bot, matchmaking, BoardParser, game tests | .agents/docs/GAMEPLAY.md |
| menu UI, UI Toolkit, .uss, color palette, MenuTheme, pixel art | .agents/docs/UI_MENU.md |
| jsonb, GrainStateStorage, PostgresJsonbConverter, OrleansStorage, PostgreSQL | .agents/docs/COMMON_ORLEANS.md |
| new card, card idea, card design, card validation, fail reasons, why card rejected | /docs/obsidian/game/cards/fail/fail_reasons.md |
| IOrleans, GetGrain, AddressableDictionary, AddressableDictionaryView, messaging, ListenQueue | .agents/docs/COMMON_ORLEANS.md |
| trigger keywords, documentation finder, reading order | .agents/docs/TRIGGERS.md |
| code examples, Docs_*.cs, working examples | .agents/docs/CODE_EXAMPLES.md |
| common mistakes, top errors, checklist failures | .agents/docs/CLAUDE_MISTAKES.md |
| full examples, Lifetime details, reactive details | .agents/docs/COMMON_*.md |
| PrefabBuilder, prefab codegen, [PrefabDefinition], Prefabs.cs, convert prefab | .agents/docs/PREFAB_CODEGEN.md |
| telemetry, metrics, logs, backend/.telemetry, file logging, session logs | .agents/docs/TELEMETRY.md |
| test logs, xUnit v3, UTF-16LE, get-test-log, TestResults, filter-class, ITestOutputHelper, dotnet test | .agents/docs/TESTING.md |

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
- `.agents/docs/COMMON_CONTAINER.md` — VContainer DI, MonoBehaviour service pattern, lifecycle phases
- `.agents/docs/COMMON_LIFETIMES.md` + `.agents/docs/COMMON_LIFETIMES_PATTERNS.md` — Lifetime usage, scoped subscriptions
- `.agents/docs/COMMON_REACTIVE_BASICS.md` / `.agents/docs/COMMON_REACTIVE_VALUES.md` / `.agents/docs/COMMON_REACTIVE_COLLECTIONS.md` / `.agents/docs/COMMON_REACTIVE_PATTERNS.md` — EventSource, ViewableProperty, ViewableList
- `.agents/docs/API_DESIGN_FULL.md` — UniTask, return types, error handling
- `.agents/docs/CODE_STYLE_FULL.md` — member order, naming, braces, NoAwait
- `.agents/docs/COMMON_ORLEANS.md` — Grains, State<T>, IStateValue, StateCollection, IOrleans, messaging
- `.agents/docs/BLAZOR.md` — Blazor console UI: early returns, injection, UiComponent
- `.agents/docs/UI_MENU.md` — Menu UI Toolkit: color palette, reusable panel classes

**Reference & lookup:**
- `.agents/docs/DECISION_TREES.md` — which pattern to use (8 decision trees)
- `.agents/docs/ERRORS.md` — error lookup table with causes & fixes
- `.agents/docs/GAMEPLAY.md` — game flow, board, cards, snapshot sync, bots, matchmaking
- `.agents/docs/PREFAB_CODEGEN.md` — PrefabBuilder API, converting prefabs to code, codegen workflow
- `.agents/docs/VOCABULARY.md` — consistent terminology
- `.agents/docs/CLAUDE_MISTAKES.md` — history of AI mistakes and lessons
- `.agents/docs/TRIGGERS.md` — keyword-based documentation finder, reading orders
- `.agents/docs/CODE_EXAMPLES.md` — index of runnable code examples
- `.agents/docs/TELEMETRY.md` — backend/.telemetry directory: metrics, logs, game session logs
- `client/Assets/Common/Docs/Claude/*.cs` — runnable code examples for all patterns
