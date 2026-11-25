# 0009. One static field per feature for the editor and MCP tools

Status: accepted (2026-11-25, commit `26e2e5cf`).

## Problem

Everything in the game lives in container scopes ([0003](0003-generated-di-container.md)), and a static with match
state is a finding under the architecture rules. Two development tools still need the live match from outside every
container:
- the agent tools (`client/Assets/GamePlay/Editor/Agent/Tools`, 13 tools: open a cell, play a card, read the state)
  run through MCP for Unity, which calls only a `public static HandleCommand(JObject)` found by reflection, with
  `method.Invoke(null, ...)`: no instance, no factory, no container;
- the cheats window (`GameCheatsEditorWindow`) is an `EditorWindow`, created by the editor, also outside the container.

## Decision

- State and logic are ordinary services of the match scope (`GameAgent`, `AgentObservations`, `AgentHandCards`,
  `AgentBoardView`; `GameCheats`), built and injected by the container like the rest of the game.
- The only static is one property per feature: `GameAgentAccess.Current` (`IGameAgent`) and
  `GameCheatsAccess.Current` (`IGameCheats`). The match scope sets it in `OnSetup` and clears it on the lifetime of the
  scope; `Require()` throws with a message when no match is attached, and a second match while one is attached throws
  in `Attach`.
- All of it is compiled only under `UNITY_EDITOR || DEVELOPMENT_BUILD`. The registration extensions
  (`AddGameAgent`, `AddGameCheats`) always compile, with their bodies under the same `#if`.

Rejected:
- Reaching the live container through `ContainerRegistryDebug`: it holds diagnostics only (names, children, registration
  tables), no instances and no `Resolve`, and it is empty in a release build. Making it work would add a
  framework API for an editor tool and a fragile search of the match scope by type name.
- Keeping the previous static bridges: two static classes of 100-500 lines with match state, a public `IsActive` setter
  and a null check in every method.

## Numbers

- Static surface: 2 properties, each with `Require` and `Attach`.
- Release build: the `GamePlay` assembly built without `UNITY_EDITOR` / `DEVELOPMENT_BUILD` contains none of
  `GameAgent`, `GameAgentAccess`, `AgentObservations`, `GameCheats`, `GameCheatsAccess`, `CellInspect` (checked with
  `strings` on the dll), only the empty `AddGameAgent` / `AddGameCheats`.
- Live check (2026-11-25): three Play sessions in a row with the agent tools and the cheat buttons, no exception from
  `Attach`, no error in `Editor.log`.

## Cost

- Still a global: two matches in one process cannot both be driven by the tools (the second `Attach` throws).
- The tools depend on a convention MCP for Unity imposes (static `HandleCommand`); a version of the package that
  changes it changes every tool.
- Code under `#if` is compiled in fewer configurations: a release-only break shows only in a release build.
