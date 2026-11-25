# Mines Leader

**[Play in the browser](https://minesleader.xyz/)**

Two-player competitive minesweeper with cards. Each player clears a 16×16 field with 40 mines. A turn gives you 5 actions: open a cell or play a card. Cards cost mana. Some clear or scan your own field, others drop mines, smoke, or frost on the opponent's field, and a third group works on hands, decks, and mana. The game has 51 cards.

You win when the opponent loses 3 HP to mines, runs out of time, or when you flag every mine on your field. PvP comes in two timed modes: a personal 120 s time bank, or 30 s per turn. A server-side bot covers practice matches. The client ships as a Unity WebGL build and runs in the browser.

![Match layout](docs/media/layout.webp)

Your field sits next to your opponent's. You both dig at the same time, and you see every move they make.

![Bloodhound](docs/media/bloodhound.webp)

Cards replace guesswork. Drop Bloodhound on a spot you can't read, and it clears the whole diamond, mines included.

![Enemy field](docs/media/enemy-field.webp)

Watch your opponent aim and track how close they are to finishing. Pick your moment to bury their field in mines or smoke.

![Your field](docs/media/player-field.webp)

Stack cards into one turn. Zip Zap defuses a chain of mines, and one good click cascades open a corner of the board.

## Stack

| Layer | Tech |
|---|---|
| Client | Unity 7000.0.0a10, C#, own 2D SRP, UniTask, engine AssetBundles (framework `ContentBundle`), WebGL |
| Backend | .NET 10, Orleans 10.4, PostgreSQL (Npgsql, ADO.NET, Marten for event states) |
| Shared | C# project compiled by both sides, MemoryPack |
| Framework | Shared base of our games, git submodule `tools/framework` (v0.16.0): DI container, reactive and lifetimes, content, network, real-time session, cluster infrastructure, forked Unity packages |
| Orchestration | .NET Aspire 13.6, Docker Compose, nginx, Coolify |
| Admin | Blazor console (BlazorBlueprint) |

## Technical decisions

Records in [`docs/decisions/`](docs/decisions/), each as problem, decision, numbers and cost:

- [0001](docs/decisions/0001-server-authoritative-sessions.md) Server-authoritative sessions and one shared protocol.
- [0002](docs/decisions/0002-shared-framework.md) One framework shared by several games, updated by releases.
- [0003](docs/decisions/0003-generated-di-container.md) Generated DI container instead of VContainer / Zenject.
- [0004](docs/decisions/0004-own-2d-srp.md) Own 2D render pipeline instead of URP.
- [0005](docs/decisions/0005-webgl-build-size.md) WebGL size budget: forked packages, stripping, trimmed MemoryPack.
- [0006](docs/decisions/0006-unity-7000-alpha.md) Unity 7000.0.0a10 (alpha) as the editor.
- [0007](docs/decisions/0007-unity-tests-local-gate.md) Unity tests: a local gate before every deploy instead of CI.
- [0008](docs/decisions/0008-no-git-lfs.md) No Git LFS.
- [0009](docs/decisions/0009-static-bridges-for-editor-tools.md) One static field per feature for the editor and MCP tools.
- [0010](docs/decisions/0010-board-pointer-by-coordinates.md) Board under the pointer by coordinates, not by a scan of the cells.

**Server-authoritative gameplay.** The client sends actions (open, flag, play card) over WebSocket. The game session on the server validates them, runs the board and card effects, checks win conditions, and pushes property updates back. The bot plays through the same session code as a regular user with a deck.

**One shared codebase.** `shared/` holds the protocol, board and card models, and configs. Unity imports it as a local package, and the backend references it as a csproj. MemoryPack union types serialize every message.

**Two connections per client.** A meta socket lives for the whole session and carries auth, matchmaking, and user projections (profile, rating, progression). A game socket opens for each match.

**Custom transactions on Orleans.** `ITransactions` collects state changes from every grain touched inside a call, writes them to PostgreSQL in one DB transaction, then calls `OnSuccess` or `OnFailure` on each grain. States live as versioned JSONB with migrations on read. Match end updates 7 states atomically: the match, plus rating, progression, and history for both players. Side effects commit with the states and a worker runs them afterwards (outbox).

**Grain-based messaging.** Three primitives cover service communication:
- `DurableQueue`: at-least-once delivery, syncs in-memory state collections.
- `RuntimePipe`: request/response with a timeout. Meta asks Game to create a match session through it.
- `RuntimeChannel`: pub/sub for user projections and live config updates.

**Deploy epochs.** Each coordinator start generates a `DeployId`, and all ephemeral cluster state keys off it. Services fetch the id through a pipe before they start, and resubscribe when it changes. A restart gives you a clean cluster without wiping the database.

**Generated DI container.** The client uses its own container. A Roslyn generator emits a sealed container class per scope at compile time, with no reflection at runtime, and reports a broken graph as a compile error. Scopes chain Internal → Global → Meta → GameLoop / Menu / GamePlay, with entity scopes per card and player below. Against VContainer on the same synthetic graph it built and resolved 4.8x faster with 5.8x fewer bytes ([0003](docs/decisions/0003-generated-di-container.md)); VContainer itself left the repository in September 2026.

**Small WebGL build.** The framework forks uGUI, RP Core, the Web Stripping Tool and UniTask and patches them so the linker drops UI Toolkit, IMGUI, Physics and XR; the game draws with its own 2D render pipeline, ships a trimmed MemoryPack and strips 106 unused wasm submodules. The first load of the live build is about 4.0 MiB brotli (wasm 2,841 KiB, data 1,148 KiB), down from 8.0 MB in September 2026 ([0005](docs/decisions/0005-webgl-build-size.md)).

## Layout

```
backend/          game part of the backend: Game (sessions, gameplay), Meta (users, matches, bots),
                  Cluster.Game, Console, Orchestration (silo, gateways, coordinator, Aspire host),
                  Tools (tests, benchmarks, generators)
client/           Unity project: Common (internal content, global scope, flow, rendering), Meta, Menu, GamePlay, Art
shared/           protocol, game models, configs
tools/framework/  framework submodule: client core, modules and forked packages, shared protocol base,
                  backend infrastructure, cluster, session and console
tools/scripts/    cluster, tests, deploy and agent scripts
docs/             design notes (Obsidian, Russian) and media
```

## Tests

GitHub CI (`.github/workflows/ci.yml`) builds and tests the backend on every push that touches `backend/`, `shared/` or
the framework. The client has about 850 Unity EditMode tests (the gameplay ones in `client/Assets/GamePlay/Tests`: board
geometry, card shapes, board snapshots and the snapshot queue). They are not in CI and run locally with
`tools/scripts/run-tests.sh`, together with the backend tests and the container generator; `/deploy-client` runs that
script before every WebGL build, so the tests gate each deploy. The run writes a short report per assembly to
`report.md` next to its logs.
