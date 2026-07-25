---
name: workflow-split
description: Save the current session state into the task's info/progress/result files so a fresh session can continue without context loss. Use when the user runs /workflow-split or says split workflow, save session, or continue later.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /workflow-split, workflow-split, раздели воркфлоу, split workflow, save session, context split, продолжи позже, раздели сессию, workflow split
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Workflow Split Skill

When the user runs `/workflow-split [task_name]`, save all current session context into the task's `_info.md`, `_progress.md`, and `_result.md` files so that the next Claude Code session can seamlessly continue.

Use this when context is running low mid-workflow and the work needs to carry over to a fresh session.

If `task_name` is omitted, find the most recently modified task folder in `docs/tasks/current/`.

---

## Phase 1 — Identify the Task

1. Find the task folder: `docs/tasks/current/<task_name>/`
2. Read all existing files: `<task_name>_info.md`, `<task_name>_progress.md`, `<task_name>_result.md`
3. If no task folder is found — tell the user and stop

---

## Phase 2 — Collect Current State

### Step 1 — Gather what was done

- Run `git diff main --name-only` to get all changed files
- Run `git diff --name-only` (unstaged) and `git diff --cached --name-only` (staged) for uncommitted work
- Run `git log main..HEAD --oneline` to see commits on the branch (if any)
- Review conversation context: what steps were completed, what was being worked on

### Step 2 — Determine what remains

- Compare completed work against the plan in `<task_name>_info.md`
- For each step in the Plan: mark as `[x]` completed, `[/]` in-progress, or `[ ]` not started
- Identify the exact point where work stopped — what was being done right now
- Update `completed_steps` and `blocked_steps` arrays in `_info.md` front-matter

### Step 3 — Collect non-obvious findings

From the current session, gather:
- Discovered edge cases or complications
- Decisions made and why
- Workarounds applied
- Files that turned out to be important but weren't in the original plan
- Any failed approaches and why they failed

---

## Phase 3 — Update `<task_name>_info.md`

Update the Plan section (under `## План реализации`). Do NOT modify Requirements (`### Что я хочу`, `### Цель`, `### Контекст`).

What to update:
- Step statuses: `[ ]` → `[/]` → `[x]` based on what was completed
- Add newly discovered steps if necessary
- Remove steps that turned out to be unnecessary
- Add new key files discovered during work
- Update front-matter: `status`, `phase`, `updated`, `completed_steps`, `blocked_steps`

What NOT to update:
- `### Что я хочу` — Requirements are stable, do not change without user agreement
- `### Цель` and `### Контекст` — same rule

If the plan is still accurate — only update step statuses and front-matter.

---

## Phase 4 — Update `<task_name>_progress.md`

This is the CRITICAL file. It must contain everything the next session needs to continue.

Preserve all existing notes. Restructure to snapshot-first format:

```markdown
---
task: <task_name>
updated: <YYYY-MM-DD>
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| [N] | [x] / [ ] | [test name or command] | [or —] |

## Заметки

### [YYYY-MM-DD HH:MM] Разделение сессии
**Текущий момент остановки:** [Exact description: which file was being edited,
what line, what exactly was being done. Enough detail for a cold start
without loss of context.]

**Важные находки:**
[All non-obvious discoveries from this session: specific files, patterns,
edge cases, decisions, workarounds, failed approaches.]

**Измененные файлы (на момент разделения):**
| Файл | Статус | Что изменено |
|------|--------|-------------|
| `path/to/File.cs` | committed / uncommitted | [description] |

[Previous notes from old _progress.md preserved below]
```

Rules for `_progress.md`:
- **Preserve all existing notes** — append, don't replace
- The snapshot table must reflect current state accurately
- The "Текущий момент остановки" section must be detailed enough for a cold start
- Include file paths, line numbers, and specific context where relevant
- If there are uncommitted changes — note exactly what they are

---

## Phase 5 — Update `<task_name>_result.md`

Result.md is a living document and the primary source for the next session.
Update it with current progress:

1. In `### Что сделано` — add completed items from this session
2. In `### Измененные файлы` — add rows for files changed in this session:
   ```markdown
   | Файл | Что изменено | Шаг | Evidence |
   |------|-------------|-----|----------|
   | `path/to/File.cs` | [description] | [N] | [test or command] |
   ```
3. In `### Отличия от плана` — add any deviations discovered
4. In `### Нерешенные вопросы` — add open questions from this session
5. Update front-matter: `updated` field

---

## Phase 6 — Output Continuation Prompt

After updating the files, output ONLY the following text for the user to paste into the next session.

**CRITICAL: This is text FOR THE USER to copy-paste into a new session. Do NOT execute it yourself. Do NOT read the files mentioned in it. Just output the text verbatim and stop.**

```
[COPY-PASTE THIS INTO THE NEXT SESSION]

В рамках /workflow продолжаем работу над docs/tasks/current/<task_name>/. Прочитай <task_name>_result.md (primary source), <task_name>_info.md (актуальный план) и <task_name>_progress.md (snapshot + находки) и продолжай с момента где остановились.
```

Nothing else. No summaries, no explanations, no execution. Just the prompt string.

---

## Rules

- Code identifiers and file paths — English
- Prose — Russian
- NEVER guess file paths — verify with Glob/Grep
- Git diff is the source of truth for changed files
- Do NOT create commits — only update documentation files
- Do NOT modify any source code files
- Preserve all existing content in `_progress.md` notes — only append and restructure to snapshot format
- The goal is zero information loss between sessions
- Keep "Текущий момент остановки" as specific as possible — vague descriptions defeat the purpose
- If the task has no meaningful progress yet — say so and skip the update
- **Front-matter must be updated in ALL three files: `_info.md`, `_progress.md`, `_result.md`**
- **`_result.md` is the primary source for continuation — it must be kept up to date**
- **When updating `_info.md` — change only the Plan section, never Requirements**
