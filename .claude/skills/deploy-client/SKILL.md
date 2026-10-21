---
name: deploy-client
description: Run all local tests (tools/scripts/run-tests.sh: backend, container generator, Unity EditMode), then build the Unity WebGL client with the web_dev build profile (submodule stripping included) and upload it to Coolify via tools/scripts/deploy-webgl.sh. Use when the user runs /deploy-client or asks to build and deploy the web client, ship the WebGL build, or update the client on Coolify.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /deploy-client, deploy-client, задеплой клиент, залей клиент, залей веб билд, деплой клиента, deploy client, deploy webgl, ship the web build
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Deploy Client

Run all tests locally (step 1b), then build the WebGL client in the open Unity editor with the
optimized web profile, let the Web Stripping Tool strip it, and upload the result to the Coolify host,
where an nginx container serves it at `/`. Web is the only target for now.

| What | Value |
|------|-------|
| Build profile | `Assets/Settings/Build Profiles/web_dev - Desktop - Development.asset` |
| Build output | `client/build` (Unity path: `build`) |
| Stripping settings | `Assets/Settings/DefaultSubmoduleStrippingSettings.asset` (106 submodules) |
| Deploy | `BUILD_DIR=client/build tools/scripts/deploy-webgl.sh` |
| Deploy details | `client/deploy/README.md` |

Invoking this skill is the user's go-ahead for the upload - do not ask again before step 5.
Any failure in steps 1-4 (step 1b - the tests - included) stops the skill: never upload a build that
was not produced by this run, and never build one from a tree whose tests fail.

## Unity MCP specifics of this project

- MCP `execute_code` does NOT work on this editor (CoreCLR: "Operation is not supported on this
  platform"; Roslyn is not installed). The editor steps are menu items of `Internal.WebGlDeploy`
  (`client/Assets/Common/Internal/Editor/Tools/WebGlDeploy.cs`), run with `execute_menu_item`.
  `execute_menu_item` always answers success - the result is the one line each item writes to
  `client/Logs/WebGlDeploy.txt` (`READY ...`, `ACTIVATING ...`, `RESTORED ...` or `ERROR ...`).
  Before each item delete that file, so a stale line is never read as this run's result.
- Several editors may be connected (all projects are named `client`). Read `mcpforunity://instances`
  and pass `unity_instance` = the `id` whose `path` is `/projects/mines-leader/client/Assets` on every
  Unity call (or `set_active_instance` once).
- Readiness after a reload: `mcpforunity://editor/state`, `data.advice.ready_for_tools`.
- Current platform: `manage_build action=platform`.

## Arguments

- none - tests, build, deploy
- `skip-tests` - skip step 1b. Only when the user typed it in this request - never on your own, not
  even to save time or after a failing test run.
- `--list` - only run `tools/scripts/deploy-webgl.sh --list` and report
- `--rollback <tag>` - only run `tools/scripts/deploy-webgl.sh --rollback <tag>` and report

`--list` and `--rollback` never run the tests or the build.

## Step 1 - Preflight

Run in parallel (from the repo root):

```bash
git status --short
cat client/build/build-guid.txt
tools/scripts/deploy-webgl.sh --list
```

- Remember the current `build-guid.txt` (a missing file is fine) - step 4 compares against it.
- `--list` proves the SSH credentials work before spending minutes on a build. If it fails,
  stop and report (`~/.config/mines-leader/deploy.env`, `tools/scripts/.deploy-password`).
- Remember the live tag - the report gives it as the rollback target.
- Remember the `git status` output - the build reserializes assets, step 6 reports the difference.
- `client/Build` (capital B) must not hold a build next to `client/build`: if it does, report it -
  the deploy script refuses to guess between them.
- The editor is shared between Claude sessions: call `ListAgents`, and if another session is
  running, hand over with `SendMessage` before starting the build - it blocks the editor for minutes.

## Step 1b - Tests

Skipped only with the `skip-tests` argument. Run from the repo root, in the background
(`run_in_background`; a full run takes several minutes), and wait for it to finish:

```bash
tools/scripts/run-tests.sh
```

The script runs the backend tests, the container generator Verify and the Unity EditMode tests in a
reflink snapshot of the working tree (Unity in batchmode there - the open editor is not touched; see
`.claude/docs/TESTING.md`, "Running everything locally"). It ends with a summary table (step, PASS/FAIL,
seconds, totals) and `ALL TESTS PASSED` or `TESTS FAILED`; the exit code is non-zero on any failure.

- Exit code 0 and `ALL TESTS PASSED` - go on to step 2.
- Anything else - STOP, do not build or deploy. Report in Russian: the summary table, the failed test
  names the script printed under each failing step, and the log folder (`Logs: ...`). Do not fix the
  tests, do not rerun with `skip-tests`, do not retry on your own - the user decides.
- The Unity step refuses to start with less than 8 GB of available memory (`low memory`), the backend
  step without Docker - report them as failures too; the user may fix it and rerun.

The shared editor is not needed for this step - the step 1 handover can wait until the tests pass.

## Step 2 - Prepare the editor

Addressables builds its content inside the player build for the editor's ACTIVE build target. A WebGL
player built while the editor is on Linux ships a catalog pointing at `StandaloneLinux64/*.bundle`, and
the deployed client fails with `Failed to decompress data for the AssetBundle` (nginx answers the
missing bundle with `index.html` and 200). The Web Stripping Tool runs only with active settings and
`StripAutomaticallyAfterBuild` on; both live in editor state that a domain reload resets, and on Unity
7000 the saved settings path was lost once and stripping silently stopped - so they are set explicitly
before every build instead of trusting `ProjectSettings/Packages/com.unity.web.stripping-tool/Settings.json`.

1. `rm -f client/Logs/WebGlDeploy.txt`, then
   `execute_menu_item menu_path="Tools/Build/Deploy/1. Activate WebGL Profile"`.
   It refuses in Play mode, while compiling or building, remembers the current profile / platform
   (for step 4b), sets the stripping settings and activates PROFILE. The status line must start
   with `ACTIVATING`; `ERROR ...` - stop and report it (leave Play mode only if the user asked).
2. If the editor was not on the profile yet, the switch reimports and reloads the domain - wait
   until `data.advice.ready_for_tools` is true.
3. `rm -f client/Logs/WebGlDeploy.txt`, then
   `execute_menu_item menu_path="Tools/Build/Deploy/2. Check Ready"`.
   It checks the editor is on WebGL with PROFILE active, re-applies the stripping settings and runs
   `SharedAddressablesSync.Sync()`. The status line must be `READY strip=<n>` with `n` > 0.
   Anything else: stop and report (still do step 4b).

## Step 3 - Build

```
mcp__unityMCP__manage_build:
  action: build
  target: webgl
  profile: Assets/Settings/Build Profiles/web_dev - Desktop - Development.asset
  output_path: build
```

The call returns a `job_id` immediately; the build itself blocks the editor main thread, so status
calls only answer once it is over. Poll until the state is no longer pending/building (a full build
with stripping takes several minutes):

```
mcp__unityMCP__manage_build:
  action: status
  job_id: <job_id>
```

If the job failed, or MCP stops answering:

```bash
pgrep -f "Editor/Unity|unityhub-unity-editor" >/dev/null && echo EDITOR_ALIVE || echo EDITOR_DEAD
grep -n -E "emcc: error|Tundra build failed|BuildFailedException|Submodule stripping failed|Caught fatal signal" client/Logs/Editor.log | tail -20
```

Report the failing lines and stop. Do not retry the build on your own and do not relaunch the editor -
a linker crash (`wasm-ld ... SIGSEGV`) has taken the whole editor down before (in peace-data, same
editor version). A `wasm-ld` heap-corruption abort in LTO is random there: say so in the report and
let the user decide on a rerun.

## Step 4 - Verify the build

```bash
cat client/build/build-guid.txt
ls -la --time-style=long-iso client/build/Build
ls -dt client/Library/com.unity.web.stripping-tool/*/ | head -3
strings client/build/StreamingAssets/aa/catalog.bin | grep -c Standalone
grep -n -E "Submodule stripping failed|Can't strip submodules|No submodules|Using SymbolFile" client/Logs/Editor.log | tail -5
```

All of these must hold, otherwise stop:

- `build-guid.txt` differs from the value remembered in step 1
- `client/build/Build` holds exactly one fresh `*.wasm.br`, `*.data.br`, `*.framework.js.br` and
  `*.loader.js` (names are hashes: the profile has "Name Files As Hashes" on)
- the job status reported success with 0 errors
- Addressables were built for WebGL: the `Standalone` count in `catalog.bin` is `0`
- the stripping pass ran: `client/Logs/Editor.log` has none of the stripping errors after this
  build started (`Using SymbolFile: ...` is logged by the pass when the build has debug symbols - the
  profile has them), and the tool wrote a NEW backup
  folder `client/Library/com.unity.web.stripping-tool/build-<guid>`, where `<guid>` is the new
  `build-guid.txt`. Without stripping the raw wasm is ~8 MB bigger - never upload such a build.

### Step 4b - Restore the editor

Once, after the build (also when it failed or step 2 stopped, as long as the editor is alive):

1. `rm -f client/Logs/WebGlDeploy.txt`, then
   `execute_menu_item menu_path="Tools/Build/Deploy/3. Restore Editor Target"`.
2. The status line starts with `RESTORED` (`nothing to restore` when the editor was already on
   the profile before step 2). If it switched, wait for `ready_for_tools` and check
   `manage_build action=platform` shows the restored platform.

## Step 5 - Deploy

```bash
BUILD_DIR=client/build tools/scripts/deploy-webgl.sh
```

Run from the repo root. `BUILD_DIR` is passed explicitly so the script never has to guess between
`client/build` and `client/Build`. The script refuses:

- a build whose `build-guid.txt` is already live - step 3 did not produce a new build; do not work
  around it with `FORCE=1` unless the user asked for a redeploy of the same build;
- a `catalog.bin` that references Standalone bundles (wrong Addressables target);
- an `index.html` that references a `Build/*` payload that is not on disk (Unity's incremental build
  deletes the outputs of a previous build into another folder).

Never bypass these - rebuild. Then confirm:

```bash
tools/scripts/deploy-webgl.sh --list
```

`current` must point at the tag the deploy printed in `Deployed <tag>`.

## Step 6 - Report

Tell the user, in Russian:

- the result of step 1b (passed with totals per suite, or "skipped by `skip-tests`")
- the deployed tag and the size that was uploaded
- the link: the script prints `URL: ...` when `PUBLIC_URL` is set in `deploy.env`; otherwise say so
- rollback command: `tools/scripts/deploy-webgl.sh --rollback <previous tag>` (the live tag
  remembered in step 1; none for a first deploy)
- the number of stripped submodules (`READY strip=<n>`)
- whether the script printed `nginx.conf changed` - then the web service needs a restart in Coolify
- files the build changed in the working tree compared with step 1 (Unity reserializes assets during a
  build) - list them, do not revert or commit them
