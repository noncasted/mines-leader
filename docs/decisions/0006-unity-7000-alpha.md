# 0006. Unity 7000.0.0a10 (alpha) as the editor

Status: accepted (2026-10-21, commit `0a9619c0`; before that 6000.7.0b1). Pinned to a10 on purpose.

## Problem

The game is size-bound on the web ([0005](0005-webgl-build-size.md)) and the project has always followed new editor
versions closely (6000.7 alphas and betas before this). An alpha is a risk for a shipped game: packages break and there
is no LTS support.

## Decision

- Move to Unity 7000.0.0a10 and stay on it until a later version is measured to be no worse.
- Keep the engine-facing risk in forks that the team controls (uGUI, RP Core, Web Stripping Tool in
  `tools/framework/client/packages`) and in the own render pipeline ([0004](0004-own-2d-srp.md)), not in packages of
  the registry.
- Measure every candidate version on the same template project before moving: `tools/framework/client/measurements`
  builds it per Unity version and stripping variant and runs it in headless Chromium.

The motivation of the move itself was not written down at the time; the numbers below were measured after it (on
2026-11-06) and are what keeps the project on a10 now.

## Numbers

WebGL size of the framework's template project, `base` variant, brotli as served, KiB
(`tools/framework/client/measurements/RESULTS.md` and `ANALYSIS.md`):

| Unity | Download | Against 7000.0.0a10 |
|---|---:|---:|
| 6000.6.8f1 | 4,013 | +389 |
| 6000.7.0b8 | 3,771 | +147 |
| **7000.0.0a10** | **3,624** | - |
| 7000.0.0a11 | 3,875 | +251 (`UnityEngine.EntitiesModule` is linked in, the scene uses none of it) |
| 7000.0.0a12 | 3,972 | +348 |
| 7000.0.0a16 | 5,362 | +1,738 (the .NET 10 BCL: larger CoreLib, ICU data, more IL2CPP metadata) |
| 7000.0.0a20 | 5,409 | +1,785 |

The 7000 editor runs on CoreCLR: `GC.GetAllocatedBytesForCurrentThread` is byte-exact there (it returned 0 on the Mono
editor of 6000), which the allocation tests and benchmarks of the framework rely on
([0003](0003-generated-di-container.md)).

## Problems met on the upgrade

- Addressables 4.1.1, the latest in the registry, did not compile (`CS0619` on a removed API); it was embedded and
  patched, and later replaced by the framework's `ContentBundle` (framework 0.8).
- The embedded URP read the sprite flip in the old format: every `SpriteRenderer` collapsed. Solved by the own pipeline.
- The Web Stripping Tool saved an empty path to its profile on the first load under 7000, and the submodule stripping
  silently stopped (raw wasm 15.6 -> 23.5 MB). The fork retries the load.
- The API updater inverted a lightmapper check in RP Core; the block was removed from the fork.
- The `execute_code` tool of MCP for Unity does not work on the CoreCLR editor; editor actions go through temporary
  editor scripts with a menu item.

## Rollback

Not rehearsed. The project on 6000.7.0b1 is the parent of `0a9619c0` in git, and the framework forks keep a pristine
upstream commit per package version, so the old package versions can be restored from history. The framework's
measurement project builds on 6000.6.8f1 and 6000.7.0b8, but the game itself has not been built on 6000 since the move;
the work since then (own pipeline, uGUI 7.0.0 fork, content on AssetBundles) would have to be checked on the old
editor first.

## Cost

- No LTS: fixes come with new alphas, and the new alphas are larger, so the project carries its own patches instead.
- Package incompatibilities show up first here, with no upstream fix yet (Addressables above).
- Tooling gaps of the alpha editor (MCP `execute_code`, no GameCI images for it, see [0007](0007-unity-tests-local-gate.md)).
