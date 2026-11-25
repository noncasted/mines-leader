# 0002. One framework shared by several games

Status: accepted.

## Problem

The team builds several games on the same stack (Unity WebGL client, Orleans backend, real-time sessions). Without a
shared base each game copies the DI container, the reactive primitives, the network, the content loading, the
cluster infrastructure and the forks of Unity packages, and every fix has to be repeated by hand in each copy.

## Decision

- The shared base is a separate repository consumed as a git submodule at `tools/framework`: the client core (DI
  container and its generator, scopes, lifetimes, content), optional modules (network, telemetry, ui, audio, ...),
  platforms, the forks of third-party packages, the protocol base and the backend `Common`, `Infrastructure`,
  `Cluster`, `Console`, `Session`, `ClientTelemetry`, `MatchMetrics`.
- The framework holds no game code: a game plugs in through interfaces, registration extensions and settings assets.
  A game includes only the modules it lists in `client/Packages/manifest.json`.
- A game stands on a release, not on a random commit. Each release is a tag `vX.Y.Z` and a file
  `tools/framework/releases/X.Y.Z.md` that lists what a game must change to update. The skill `release-framework`
  cuts a release (version, notes, a check against the consumer projects, tag); the skill `update-framework` moves the
  pointer of a game and carries out the steps of the notes, then builds and tests.
- `0.MINOR.PATCH` while the API changes freely: `MINOR` means a game has work to do, `PATCH` means only the pointer
  moves.

## Numbers

- 26 releases from 0.1.0 (2026-11-02) to 0.16.0 (2026-11-25); 673 commits in the framework repository.
- Six game projects consume the submodule, on releases from 0.13.1 to 0.16.0.
- Example of a shared feature: `PrefabPool<T>` moved from this game into the core in 0.16.0; the VFX of cards and
  the floating text of this game now take it from there.
- Seven forked packages live in `tools/framework/client/packages` (uGUI, RP Core, URP, Web Stripping Tool, UniTask,
  MemoryPack, MCP for Unity), each with a `FORK.md` of its patches.

## Cost

- Two repositories to commit: a framework change is committed and pushed in the submodule, then the pointer is
  committed in the game.
- A breaking change of the framework costs every game an update; the release notes and `update-framework` exist to
  keep that cheap, but it is still work per game.
- The framework repository is private, so the CI of this repository needs a token to check out the submodule
  (`FRAMEWORK_TOKEN` in `.github/workflows/ci.yml`).
