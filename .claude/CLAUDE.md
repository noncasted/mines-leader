# Claude Code Instructions

## Overview
Competitive multiplayer minesweeper. Three codebases in one repo:
- **`client/`** — Unity3D (VContainer DI, UniTask, custom reactive/lifetime framework)
- **`backend/`** — .NET Orleans (distributed actor model); subfolders: `Game/`, `Meta/`, `Infrastructure/`, `Orchestration/`
- **`shared/`** — Protocol, domain models, configs — used by both client and backend

## Keyword → Documentation (FAST LOOKUP)

| Keywords | Go to |
|----------|-------|
| MonoBehaviour, ISceneService, IScopeSetup, Create(), OnSetup(), [Inject] | rules/MONOBEHAVIOUR.md |
| Lifetime, Advise, View, Terminate, subscription, cleanup | rules/LIFETIMES.md |
| EventSource, ViewableProperty, ViewableList, reactive, observable, event | rules/REACTIVE.md |
| UniTask, async, IReadOnlyList, file I/O, callback wrapping | rules/API_DESIGN.md |
| member order, _camelCase, GC.KeepAlive, NoAwait, braces | rules/CODE_STYLE.md |
| Grain, IGrainWithGuidKey, [Transaction], constructor injection | rules/ORLEANS_GRAINS.md |
| State<T>, IStateValue, [GenerateSerializer], [Id(N)], StatesLookup, StateCollection | rules/ORLEANS_STATE.md |
| Blazor, razor, @inject, UiComponent, early return, console UI | rules/BLAZOR.md |
| which pattern to use, decision | docs/DECISION_TREES.md |
| error lookup, why X fails, memory leak, NullRef | docs/ERRORS.md |
| game flow, board, cell, mine, flag, card, CardType, ICard, snapshot, bot, matchmaking | docs/GAMEPLAY.md |
| new card, card idea, card design, card validation, fail reasons, why card rejected | /docs/obsidian/game/cards/fail/fail_reasons.md |
| IOrleans, GetGrain, AddressableDictionary, AddressableDictionaryView, messaging, ListenQueue | docs/COMMON_ORLEANS.md |
| trigger keywords, documentation finder, reading order | docs/TRIGGERS.md |
| code examples, Docs_*.cs, working examples | docs/CODE_EXAMPLES.md |
| common mistakes, top errors, checklist failures | rules/COMMON_MISTAKES.md |
| full examples, Lifetime details, reactive details | docs/COMMON_*.md |
| PrefabBuilder, prefab codegen, [PrefabDefinition], Prefabs.cs, convert prefab | docs/PREFAB_CODEGEN.md |

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

**Rules (critical checklists):**
- `rules/MONOBEHAVIOUR.md` — MonoBehaviour service pattern checklist
- `rules/LIFETIMES.md` — Lifetime usage, which lifetime to use
- `rules/REACTIVE.md` — EventSource, ViewableProperty, ViewableList quick API
- `rules/API_DESIGN.md` — UniTask, return types, error handling (full: `docs/API_DESIGN_FULL.md`)
- `rules/CODE_STYLE.md` — member order, naming, braces, NoAwait (full: `docs/CODE_STYLE_FULL.md`)
- `rules/COMMON_MISTAKES.md` — top mistakes checklist
- `rules/ORLEANS_GRAINS.md` — Orleans grain pattern checklist
- `rules/ORLEANS_STATE.md` — state types, adding new state
- `rules/BLAZOR.md` — Blazor console UI: early returns, injection, UiComponent

**Docs (full reference):**
- `docs/DECISION_TREES.md` — which pattern to use (8 decision trees)
- `docs/ERRORS.md` — error lookup table with causes & fixes
- `docs/GAMEPLAY.md` — game flow, board, cards, snapshot sync, bots, matchmaking
- `docs/PREFAB_CODEGEN.md` — PrefabBuilder API, converting prefabs to code, codegen workflow
- `docs/COMMON_ORLEANS.md` — IOrleans, AddressableDictionary, messaging, grain lifecycle
- `docs/COMMON_CONTAINER.md` — VContainer DI full details, lifecycle phases, gotchas
- `docs/COMMON_LIFETIMES.md` + `COMMON_LIFETIMES_PATTERNS.md` — Lifetime details
- `docs/COMMON_REACTIVE_*.md` — EventSource, ViewableProperty, ViewableList details
- `docs/VOCABULARY.md` — consistent terminology
- `docs/CLAUDE_MISTAKES.md` — history of AI mistakes and lessons
- `docs/TRIGGERS.md` — keyword-based documentation finder, reading orders
- `docs/CODE_EXAMPLES.md` — index of runnable code examples
- `client/Assets/Common/Docs/Claude/*.cs` — runnable code examples for all patterns
