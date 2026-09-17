---
name: invent-common
description: >
  Iteratively invent new Common editor tools and runtime primitives from catalogs,
  the generated container, lifetimes, and reactive properties. Each idea is checked
  against the repo so it closes a real need. Use whenever the user runs /invent-common,
  asks to invent tools, придумай инструменты, новые идеи для Common, loop идей,
  or wants another round of Common invention ideas.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /invent-common, invent-common, придумай инструменты, новые идеи для Common, loop идей, invent tools, invent primitives
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Launch the invent-common workflow immediately.

# Invent Common

Launch the project workflow `invent-common`. Do not invent ideas yourself in this session — the workflow is the loop.

Repo root: `/projects/mines-leader`
Workflow: `.grok/workflows/invent-common.rhai`

## Arguments

| User says | Args |
|-----------|------|
| `/invent-common` | `{ "rounds": 4 }` |
| `2 раунда` / `rounds 2` | `{ "rounds": 2 }` (min 2, max 6) |
| `focus: container` / `про каталоги` / `про агентов` | `{ "rounds": 4, "focus": "..." }` |

Default rounds: 4. Stop early after two consecutive dry rounds.

## How to launch

Call the `workflow` tool:

```
source: { type: "name", name: "invent-common" }
args: { rounds: 4 }
agent_budget: 96
```

If `name` is not registered yet, use `source: { type: "script_path", script_path: "/projects/mines-leader/.grok/workflows/invent-common.rhai" }`.

Do not pass `validate_only`. Do not implement any proposed idea unless the user later asks.

## What the loop does

1. **Ground** — two read-only agents: architecture docs + Common source friction.
2. **Invent** — up to N rounds. Each round four lenses in parallel (editor, primitive, agent-loop, health), then one skeptic per idea. Keep only ideas whose need is real, which do not already exist, and whose evidence path is under `client/`, `docs/`, or `.agents/`.
3. **Report** — Russian markdown of survivors.

Watch progress in `/workflows`. When the run completes, open the scratch report (`invent-common.md`) and present it to the user in Russian. Do not pad it with extra ideas.

## Do not

- Re-run the ideation in the parent session.
- Start implementation from the report without an explicit user request.
- Raise `agent_budget` above 128 unless a previous run died on budget.
