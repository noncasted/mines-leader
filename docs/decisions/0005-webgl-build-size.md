# 0005. WebGL size budget: forked packages, stripping, trimmed MemoryPack

Status: accepted. Detailed log of every step (Russian): `docs/obsidian/architecture/client-webgl-build-size.md`.

## Problem

The game is played in a browser tab, often on a phone, and the first load is what a new player waits for. The Unity
linker keeps a whole engine module, with its native dependencies, as soon as one managed reference to it survives, and
Unity packages add such references through `versionDefines` that switch on when a module is in the manifest. The
modules cannot simply be removed from the manifest: editor packages (MCP for Unity, Aseprite Importer) depend on them.

## Decision

- Fork the packages that pull modules in and patch their asmdefs and code: uGUI (no `PACKAGE_UITOOLKIT`, physics and
  animation defines; clipboard without IMGUI), RP Core (no physics), Web Stripping Tool (`wasm-opt` with exception
  handling enabled, otherwise every `try`/`catch` is dropped and any exception halts the player), UniTask (no physics
  support). The forks live in `tools/framework/client/packages`, each with a `FORK.md`.
- Drop what the game does not use: Input System (legacy `Input` with `StandaloneInputModule`), XR, 2D physics (own
  hit-test of cards and cells), URP ([0004](0004-own-2d-srp.md)), Addressables (engine AssetBundles through the
  framework `ContentBundle`).
- Strip 106 wasm submodules that a profiled play session never called (Web Stripping Tool, list in
  `client/Assets/Settings/DefaultSubmoduleStrippingSettings.asset`).
- Ship a MemoryPack.Core built from source without the rarely used formatters: the NuGet build marks almost every
  formatter `[Preserve]` and drags in Immutable collections, Brotli, Numerics, Uri, TimeZoneInfo, CultureInfo and
  Regex. The backend keeps the NuGet package; the wire format is the same and is pinned by golden tests.

## Numbers

First load of the live build, `https://minesleader.xyz/Build`, `Content-Length` of HEAD requests on 2026-11-25 (the
build deployed on 2026-11-17, brotli as served):

| File | Bytes | KiB |
|---|---:|---:|
| `*.wasm.br` | 2,909,243 | 2,841 |
| `*.data.br` | 1,175,780 | 1,148 |
| `*.framework.js.br` | 71,847 | 70 |
| `*.loader.js` | 28,175 | 28 |
| **Total** | **4,185,045** | **4,087 (about 4.0 MiB)** |

The content bundles of `StreamingAssets/Content` that load later (the music among them) are not in this total.

History (brotli, each step measured on the build of its day; the September rows are the sum of the player files, the
later rows `wasm` + `data`):

| Date | Unity | State | First load |
|---|---|---|---:|
| 2026-09-11 | 6000.x | after Newtonsoft -> JsonUtility and managed stripping | 8,020 KB |
| 2026-09-11 | 6000.x | Input System, UI Toolkit, XR, physics, `ClientWebSocket` out of the WebGL build | 6,000 KB |
| 2026-10-21 | 7000.0.0a10 | after the upgrade, still on URP | ~5,780 KB |
| 2026-10-21 | 7000.0.0a10 | own SRP, no 2D physics, 106 submodules, trimmed MemoryPack, no IMGUI | ~3,910 KB |
| 2026-11-17 | 7000.0.0a10 | content on engine AssetBundles, uGUI fork 7.0.0 (TextCore back through TMP) | ~3,990 KB |

Single steps of 2026-10-21: own SRP -483 KB, no 2D physics -296 KB, submodule stripping -426 KB, trimmed MemoryPack
-396 KB (-337 KB wasm and -59 KB data), no IMGUI -177 KB.

For scale: the framework measures an empty project with the same profile per Unity version
(`tools/framework/client/measurements/RESULTS.md`); on 7000.0.0a10 its `base` variant is 3,624 KiB. The rest of this
build over that is TMP and uGUI, the game code and protocol, and the IL2CPP metadata (about 730 KB of the `data.br`).

Not measured:
- **Startup time of the current build.** The startup trace of the client telemetry (`MetaLoop.SendStartupTrace`) is
  off on production (the feature flag of the client telemetry is turned on by hand), and the backend holds no trace
  of the last 7 days. How: turn the flag on in the console, open the game, read it with
  `.claude/skills/client-telemetry/telemetry startups`. The download before Unity starts is not in that trace;
  the `device-lab` stand measures it.

## Cost

- Seven forks to carry across every Unity and package upgrade: re-copy the pristine package, re-apply the patches,
  check for new `versionDefines`, check that the stripping profile still loads (on the 7000 upgrade its path was saved
  empty and the stripping silently stopped: raw wasm 15.6 -> 23.5 MB).
- Silent regressions: a new `GUI` / `OnGUI` call or a new physics reference brings a module back without any error,
  only a bigger build. The checks are by hand (module list in `UnityClassRegistration.cpp`, `wasm-dis` for `(try`).
- Features given up in the player: `PhysicsRaycaster` / `Physics2DRaycaster`, animation transitions of `Selectable`,
  `OnGUI`, local volumes with colliders; a stripped submodule called at runtime stops the player.
- The trimmed MemoryPack is rebuilt by hand on every MemoryPack upgrade (the source needs C# 11, Unity compiles C# 9).
