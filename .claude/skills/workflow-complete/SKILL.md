# Workflow Complete Skill

When the user runs `/workflow-complete [task_name]`, analyze the completed work, update task files, and propagate changes to project documentation and memory.

If `task_name` is omitted, look for the most recently modified task folder in `docs/tasks/`.

---

## Phase 1 — Identify the Task

1. Find the task folder: `docs/tasks/<task_name>/`
2. Read all three files: `_info.md`, `_in_progress.md`, `_result.md`
3. If `_result.md` has `Статус: Не завершено` — ask the user if the task is actually done before proceeding

---

## Phase 2 — Analyze What Was Actually Done

### Step 1 — Collect real changes

Run `git diff main --name-only` (or `git diff main...HEAD --name-only` if on a feature branch) to get the actual list of changed files. Cross-reference with `_info.md` plan and `_in_progress.md` notes.

### Step 2 — Compare plan vs reality

For each step in `_info.md`:
- Was it done as planned?
- Was it done differently? How?
- Was it skipped? Why?

Identify work that was done but NOT mentioned in the plan (emergent changes, bug fixes discovered along the way).

### Step 3 — Identify problems encountered

Scan `_in_progress.md` and git commit messages for:
- Bugs found and fixed
- Unexpected complications
- Workarounds applied
- Patterns that didn't work as expected
- Compilation errors that required changes

---

## Phase 3 — Update Task Files

### Update `_in_progress.md`

Add any missing entries that should have been recorded during implementation:
- Files that were changed but not listed
- Decisions that were made but not documented
- Problems that were solved but not noted

Keep existing entries. Add new ones under a section:

```markdown
### [Дополнено при завершении]
- [entry]
```

### Update `_result.md`

Fill in or update all sections:

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
[What was done differently from _info.md. Omit if plan followed exactly.]

### Проблемы и решения
[Problems encountered and how they were solved. Omit if none.]

### Нерешенные вопросы
[What remains to be done. Omit if everything is done.]
```

The file list in `_result.md` MUST match the actual git diff, not just what was planned.

---

## Phase 4 — Update Project Documentation

Based on the completed work, check and update relevant documentation in `.claude/`.

### 4.1 — Check each documentation file for relevance

| What changed in the task | Documentation to check/update |
|--------------------------|-------------------------------|
| New MonoBehaviour service patterns | `docs/GAMEPLAY.md`, `rules/MONOBEHAVIOUR.md` |
| New Orleans grains or state types | `docs/COMMON_ORLEANS.md`, `rules/ORLEANS_GRAINS.md`, `rules/ORLEANS_STATE.md` |
| New card types or mechanics | `docs/GAMEPLAY.md` |
| New Blazor pages or editors | `rules/BLAZOR.md` |
| New PrefabBuilder patterns | `docs/PREFAB_CODEGEN.md` |
| New API patterns or async patterns | `rules/API_DESIGN.md` |
| New vocabulary/concepts introduced | `docs/VOCABULARY.md` |
| New error patterns discovered | `docs/ERRORS.md` |
| New decision points for developers | `docs/DECISION_TREES.md` |
| AI mistakes made during task | `docs/CLAUDE_MISTAKES.md`, `rules/COMMON_MISTAKES.md` |
| New key files added to the project | `docs/GAMEPLAY.md` key files section, relevant docs |

### 4.2 — What to update

For each relevant doc:
1. **Read** the current file
2. **Identify** what needs adding/changing based on the task results
3. **Update** in the existing format (match style of surrounding content)
4. **Cross-reference** — add links between related docs if needed

### 4.3 — CLAUDE_MISTAKES.md (CRITICAL)

If ANY mistakes were made during the task (visible in `_in_progress.md`, git history, or known from context):
- Add a new numbered Lesson entry to `docs/CLAUDE_MISTAKES.md`
- Add a condensed rule to `rules/COMMON_MISTAKES.md` if it's a recurring/critical pattern
- Format: wrong code, correct code, one-line rule, link to relevant rules doc

### 4.4 — VOCABULARY.md

If new concepts/terms were introduced:
- Add entries in the existing table format
- Include "do not mix" notes if similar terms exist

---

## Phase 5 — Update Memory

Check if any of the following should be saved to auto-memory:

1. **New critical patterns** discovered during the task that future conversations need
2. **New key files** added that are important for navigation
3. **Architecture changes** that affect how to approach future tasks
4. **Feedback from the user** during the task that should be remembered

Do NOT save:
- File lists (derivable from git)
- Implementation details (in the code)
- Anything already in `_result.md` or `.claude/` docs

---

## Phase 6 — Report

Output a summary to the user:

```markdown
## Workflow Complete: [task name]

### Файлы задачи обновлены
- `_in_progress.md` — [what was added]
- `_result.md` — [filled/updated]

### Документация обновлена
- `docs/GAMEPLAY.md` — [what changed]
- `docs/VOCABULARY.md` — [what added]
- (or "Обновления не требуются")

### Ошибки зафиксированы
- `docs/CLAUDE_MISTAKES.md` — Lesson N: [description]
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
