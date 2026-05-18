# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /workflow-complete, workflow-complete, заверши воркфлоу, complete workflow, finalize task, task complete, закрой задачу, workflow complete
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Workflow Complete Skill

When the user runs `/workflow-complete [task_name]`, finalize the task, create a condensed summary in `docs/tasks/complete/`, and clean up the working folder from `docs/tasks/current/`.

If `task_name` is omitted, look for the most recently modified task folder in `docs/tasks/current/`.

---

## Phase 1 — Identify the Task

1. Find the task folder: `docs/tasks/current/<task_name>/`
2. Read all three files: `<task_name>_info.md`, `<task_name>_progress.md`, `<task_name>_result.md`
3. If `<task_name>_result.md` has `Статус: Не завершено` — ask the user if the task is actually done before proceeding

---

## Phase 2 — Analyze What Was Actually Done

### Step 1 — Collect real changes

Run `git diff main --name-only` (or `git diff main...HEAD --name-only` if on a feature branch) to get the actual list of changed files. Cross-reference with `<task_name>_info.md` plan and `<task_name>_progress.md` notes.

### Step 2 — Compare plan vs reality

For each step in `<task_name>_info.md`:
- Was it done as planned?
- Was it done differently? How?
- Was it skipped? Why?

Identify work that was done but NOT mentioned in the plan (emergent changes, bug fixes discovered along the way).

### Step 3 — Identify problems encountered

Scan `<task_name>_progress.md` and git commit messages for:
- Bugs found and fixed
- Unexpected complications
- Workarounds applied
- Patterns that didn't work as expected
- Compilation errors that required changes

---

## Phase 3 — Update `<task_name>_result.md` in current/

Fill in or update all sections of `docs/tasks/current/<task_name>/<task_name>_result.md`:

```markdown
## [Task name] — Результат

### Статус: Завершено

### Что сделано
[3-7 bullet points of what was implemented]

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `path/to/File.cs` | [description] |

### Отличия от плана
[What was done differently from <task_name>_info.md. Omit if plan followed exactly.]

### Проблемы и решения
[Problems encountered and how they were solved. Omit if none.]

### Нерешенные вопросы
[What remains to be done. Omit if everything is done.]
```

The file list in `<task_name>_result.md` MUST match the actual git diff, not just what was planned.

---

## Phase 4 — Create Condensed Summary in complete/

Create `docs/tasks/complete/<task_name>.md` — a short, self-contained summary extracted from `<task_name>_result.md`.

Format:

```markdown
## [Task name]

### Что сделано
[3-5 bullet points — the essence of what was implemented, no filler]

### Ключевые файлы
[Only the most important files — 3-7 max, not the full change list]

### Заметки
[Non-obvious decisions, gotchas, or things to know for future work. Omit if nothing noteworthy.]
```

Rules for the condensed summary:
- **Maximum ~40 lines** — this is a quick reference, not a full report
- No "Измененные файлы" table — only key files that matter for understanding
- No "Отличия от плана" — irrelevant after completion
- No "Нерешенные вопросы" unless they are blockers for future work
- Write for someone who needs to quickly understand what was done and where to look

---

## Phase 5 — Clean Up current/

Delete the working folder `docs/tasks/current/<task_name>/` (all three files: `<task_name>_info.md`, `<task_name>_progress.md`, `<task_name>_result.md`).

The full `<task_name>_result.md` is preserved in git history if anyone needs the detailed version later.

---

## Phase 6 — Update Project Documentation

Based on the completed work, check and update relevant documentation in `.agents/`.

### 6.1 — Check each documentation file for relevance

| What changed in the task | Documentation to check/update |
|--------------------------|-------------------------------|
| New MonoBehaviour service patterns | `docs/db/docs/GAMEPLAY.md`, `docs/db/docs/COMMON_CONTAINER.md` |
| New Orleans grains or state types | `docs/db/docs/COMMON_ORLEANS.md` |
| New card types or mechanics | `docs/db/docs/GAMEPLAY.md` |
| New Blazor pages or editors | `docs/db/docs/BLAZOR.md` |
| New PrefabBuilder patterns | `docs/db/docs/PREFAB_CODEGEN.md` |
| New API patterns or async patterns | `docs/db/docs/API_DESIGN_FULL.md` |
| New vocabulary/concepts introduced | `docs/db/docs/VOCABULARY.md` |
| New error patterns discovered | `docs/db/docs/ERRORS.md` |
| New decision points for developers | `docs/db/docs/DECISION_TREES.md` |
| AI mistakes made during task | `docs/db/docs/CLAUDE_MISTAKES.md` |
| New key files added to the project | `docs/db/docs/GAMEPLAY.md` key files section, relevant docs |

### 6.2 — What to update

For each relevant doc:
1. **Read** the current file
2. **Identify** what needs adding/changing based on the task results
3. **Update** in the existing format (match style of surrounding content)
4. **Cross-reference** — add links between related docs if needed

### 6.3 — CLAUDE_MISTAKES.md (CRITICAL)

If ANY mistakes were made during the task (visible in `<task_name>_progress.md`, git history, or known from context):
- Add a new numbered Lesson entry to `docs/db/docs/CLAUDE_MISTAKES.md`
- Format: wrong code, correct code, one-line rule, link to relevant docs page

### 6.4 — VOCABULARY.md

If new concepts/terms were introduced:
- Add entries in the existing table format
- Include "do not mix" notes if similar terms exist

---

## Phase 7 — Update Memory

Check if any of the following should be saved to auto-memory:

1. **New critical patterns** discovered during the task that future conversations need
2. **New key files** added that are important for navigation
3. **Architecture changes** that affect how to approach future tasks
4. **Feedback from the user** during the task that should be remembered

Do NOT save:
- File lists (derivable from git)
- Implementation details (in the code)
- Anything already in `<task_name>_result.md` or `.agents/` docs

---

## Phase 8 — Report

Output a summary to the user:

```markdown
## Workflow Complete: [task name]

### Задача завершена
- `docs/tasks/complete/<task_name>.md` — сводка создана
- `docs/tasks/current/<task_name>/` — рабочие файлы удалены

### Документация обновлена
- `docs/db/docs/GAMEPLAY.md` — [what changed]
- `docs/db/docs/VOCABULARY.md` — [what added]
- (or "Обновления не требуются")

### Ошибки зафиксированы
- `docs/db/docs/CLAUDE_MISTAKES.md` — Lesson N: [description]
- (or "Новых ошибок не обнаружено")

### Memory обновлена
- [what was saved]
- (or "Обновления не требуются")
```

---

## Rules

- Code identifiers and file paths — English
- Prose — Russian
- NEVER guess file paths — verify with Glob/Grep
- NEVER add documentation for things that didn't actually change
- Match existing format in every doc you update
- If a doc file doesn't exist, do NOT create it — only update existing files
- Git diff is the source of truth for changed files, not the plan
- Be conservative with CLAUDE_MISTAKES.md — only add genuinely new lessons, not variations of existing ones
- When updating memory, check MEMORY.md first for duplicates
- The condensed summary in `complete/` is a quick reference — keep it short and useful
- The full detailed `<task_name>_result.md` is preserved in git history, no need to duplicate everything in the summary
