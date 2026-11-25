# 0004. Own 2D render pipeline instead of URP

Status: accepted (2026-10-21, together with the upgrade to Unity 7000, see [0006](0006-unity-7000-alpha.md)).

## Problem

The game draws sprites, TextMeshPro and uGUI on a 2D camera: no lights, no 3D, no shadows. URP brought all of its
renderer features into the WebGL build anyway, and an embedded copy of URP had to be patched to keep the UI Toolkit
module out of the build. On the upgrade to Unity 7000 the embedded URP 17.7 broke outright: it read the sprite flip as
floats while 7000 packs it as bits, so every `SpriteRenderer` collapsed and the boards and cards disappeared.

## Decision

- An own scriptable render pipeline in `client/Assets/Common/Rendering` (`GamePipeline`, `GamePipelineAsset`,
  `ShaderLibrary/Core.hlsl`, `Sprite.shader`), asset `client/Assets/Settings/GamePipeline.asset`: sorted sprites and UI,
  nothing else.
- The fork of RP Core in the framework stays only for its `ShaderLibrary`. URP is removed from the manifest.
- Post-processing is planned (bloom first); it goes into this pipeline, between drawing and the camera target, rather
  than back to URP.

## Numbers

- Measured on 2026-10-21, Unity 7000.0.0a10, with submodule stripping on, `client/build`, brotli: URP 4.31 MB wasm +
  1.47 MB data against 4.08 MB + 1.22 MB with the own pipeline, about **-483 KB** of the first load (5.78 -> 5.30 MB at
  that point). Source: `docs/obsidian/architecture/client-webgl-build-size.md`. Not re-measured since: URP is gone from
  the project, and a fresh A/B would need it back on a branch. The current total is in [0005](0005-webgl-build-size.md).
- The pipeline is 4 files, about 350 lines of C# and HLSL.

## Cost

- Every Unity upgrade needs a check by hand: diff the RP Core fork against the built-in package of the new editor and
  compare `Core.hlsl` with the `Core2D.hlsl` / `UnityInput.hlsl` of that version's URP. The sprite flip break above is
  the kind of change it catches; nothing does it automatically.
- Features URP gives for free (2D lights, post-processing, the Volume framework) are written by the team when the
  game needs them.
- Third-party shaders and assets written for URP do not work without porting.
