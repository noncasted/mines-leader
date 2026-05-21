# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /workflow, workflow, воркфлоу, запусти воркфлоу, create workflow, task workspace, начни задачу, workflow task
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Workflow Skill

When the user runs `/workflow [description]`, create a structured task workspace with three files and begin implementation.

This is an enhanced version of `/task` that maintains a living workspace throughout the task lifecycle.

---

## Workspace Structure

Active tasks live under `docs/tasks/current/`, completed tasks move to `docs/tasks/complete/`.

```
docs/tasks/
  current/<task_name>/
    <task_name>_info.md         — Requirements (stable) + mutable Plan
    <task_name>_progress.md     — State snapshot + chronology
    <task_name>_result.md       — Living result document
  complete/<task_name>/
    <task_name>.md              — Condensed summary (created by /workflow-complete)
```

`<task_name>` — lowercase, underscores, from the short task name (e.g. `bot_card_strategies`).

---

## Phase 1 — Create Brief (`<task_name>_info.md`)

Follow the same research process as `/task`:

### Step 1 — Extract ALL requirements

Parse the user's message after `/workflow`. **Preserve everything — nothing is noise.**

- Составь полный список ВСЕХ целей, подзадач, ограничений и контекста из сообщения пользователя
- Если пользователь упомянул 7 пунктов — в списке должно быть 7 пунктов. Ничего не отбрасывай
- Extract all mentioned class names, system names, file names, features
- Fix typos, normalize informal names to proper C# identifiers
- Сохрани пользовательские описания поведения рядом с техническим маппингом

### Step 2 — Search the codebase

For every class/system/feature mentioned:
- `Grep` for the class name in `client/Assets/`, `backend/`, or `shared/` to find its file
- `Glob` by pattern if a group of files is involved
- Read key files briefly — enough to understand current implementation
- If new files will be created, find the correct `.csproj`

### Step 3 — Map task to documentation

Include docs that are **relevant** to this task:

| Task touches... | Include |
|-----------------|---------|
| MonoBehaviour, `[Inject]`, `ISceneService`, `IScopeSetup` | `.agents/docs/COMMON_CONTAINER.md` |
| `Advise`, `View`, `Lifetime`, subscriptions, cleanup | `.agents/docs/COMMON_LIFETIMES.md` |
| `EventSource`, `ViewableProperty`, `ViewableList` | `.agents/docs/COMMON_REACTIVE_BASICS.md` |
| `UniTask`, async methods, file I/O, `IReadOnlyList` | `.agents/docs/API_DESIGN_FULL.md` |
| member order, naming, braces, `NoAwait` | `.agents/docs/CODE_STYLE_FULL.md` |
| Grain, State, `[Transaction]`, Orleans backend | `.agents/docs/COMMON_ORLEANS.md` |
| Blazor, razor, `@inject`, console UI | `.agents/docs/BLAZOR.md` |
| game flow, board, cards, bots, matchmaking | `.agents/docs/GAMEPLAY.md` |
| IOrleans, AddressableDictionary, messaging | `.agents/docs/COMMON_ORLEANS.md` |
| "which pattern", architectural choice | `.agents/docs/DECISION_TREES.md` |
| common pitfalls, known mistakes | `.agents/docs/CLAUDE_MISTAKES.md` |
| PrefabBuilder, prefab codegen | `.agents/docs/PREFAB_CODEGEN.md` |
| card effects, card mechanics | `.agents/docs/CARD_EFFECTS.md` |
| terminology, vocabulary | `.agents/docs/VOCABULARY.md` |
| menu UI, UI Toolkit, color palette | `.agents/docs/UI_MENU.md` |
| keyword lookup, documentation finder | `.agents/docs/TRIGGERS.md` |
| testing, xUnit, test logs | `.agents/docs/TESTING.md` |
| telemetry, metrics, logs | `.agents/docs/TELEMETRY.md` |
| error lookup, debugging | `.agents/docs/ERRORS.md` |
| deploy, Coolify, docker-compose, Aspire | `.agents/docs/DEPLOY.md` |
| deploy failure, troubleshooting | `.agents/docs/DEPLOY_TROUBLESHOOTING.md` |
| reactive values, ViewableProperty patterns | `.agents/docs/COMMON_REACTIVE_VALUES.md` |
| reactive collections, ViewableList | `.agents/docs/COMMON_REACTIVE_COLLECTIONS.md` |
| reactive patterns, event wiring | `.agents/docs/COMMON_REACTIVE_PATTERNS.md` |
| lifetime patterns, scoped cleanup | `.agents/docs/COMMON_LIFETIMES_PATTERNS.md` |
| code examples, runnable samples | `.agents/docs/CODE_EXAMPLES.md` |

### Step 4 — Decompose into steps

Break the task into ordered concrete steps with two-level decomposition:

1. **First level — by user requirements.** Each requirement becomes a group.
2. **Second level — by files.** Within each group, list concrete file changes.

Rules:
- Name the target file explicitly (real path found in Step 2)
- If a new file must be created, mark it: `[новый файл — добавить в X.csproj]`
- Steps must be sequenced so dependencies come first
- Each step must have an acceptance criterion — a concrete test, command, or observable behavior that proves the step is done

### Step 5 — Verify completeness (CRITICAL)

**Re-read the user's original message.** For every requirement:
- Verify it is covered by a concrete step
- If something is NOT covered — add it now

### Step 5.5 — Gate: Ask or Proceed?

Before writing `_info.md`, check:
1. Are there conflicting requirements with no clear priority?
2. Does the task require choosing between architectural approaches where both are valid but have different risk profiles?
3. Does the task reference external context not present in the codebase (past sprints, verbal agreements, other tasks)?

If ANY checked: output the specific ambiguity, propose 2-3 options with tradeoffs, and ask the user **one concise question**. Wait for answer before proceeding to Step 6.

If NONE checked: proceed to Step 6 without questions.

### Step 6 — Write `<task_name>_info.md`

Save the brief to `docs/tasks/current/<task_name>/<task_name>_info.md`.

**Mandatory YAML front-matter at the top of the file:**

```yaml
---
task: <task_name>
status: pending
phase: brief
created: <YYYY-MM-DD>
updated: <YYYY-MM-DD>
total_steps: 0
completed_steps: []
blocked_steps: []
---
```

**File structure:** Requirements first (stable — do not change without user agreement), then Plan (mutable — update when approach changes).

```markdown
## Задача: [short name in Russian]

### Что я хочу

[Несколько абзацев, подробно передающих суть задачи. Сохрани здесь максимум информации из исходного сообщения пользователя: что нужно сделать, зачем, какие ограничения, примеры, контекст, детали. Не сокращай до одной фразы — распиши развёрнуто, чтобы через неделю можно было восстановить полную картину без перечитывания истории.]

### Цель
[Полное описание цели. Каждый пункт пользователя должен быть представлен.]

### Контекст
[Мотивация, ограничения, предпочтения, связь с другими задачами.]

## План реализации

#### [N] Название шага
- **Статус:** [ ] pending | [/] in_progress | [x] completed | [!] blocked
- **Цель:** [одно предложение — что должно быть true после шага]
- **Как:** [конкретные действия]
- **Проверка:** [конкретный тест, команда, или observable behavior]
- **Файлы:** [список файлов]
- **Зависит от:** [шаги или —]
- **Блокирует:** [шаги или —]

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `path/to/File.cs` | [what and why] |

### Документация к прочтению
- `.agents/docs/COMMON_CONTAINER.md` — [конкретная причина]

### Риски
[Specific gotchas. Omit section if no risks.]
```

### Step 7 — Initialize `<task_name>_progress.md`

Create the file with this template. The snapshot table goes first, then chronology:

```markdown
---
task: <task_name>
updated: <YYYY-MM-DD>
---

## Snapshot

| Шаг | Статус | Evidence | Блокер |
|-----|--------|----------|--------|
| [N] | [ ] / [x] | [test name or command] | [or —] |

## Заметки
<!-- Сюда записываются находки, решения и полезная информация по ходу реализации -->
```

### Step 8 — Initialize `<task_name>_result.md`

Create the file with this template. It is a living document — fill it gradually as steps complete, not only at the end:

```markdown
## [Task name] — Результат

### Статус: В работе

### Что сделано
<!-- Populated as steps complete -->

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|

### Отличия от плана
<!-- Recorded immediately when approach changes -->

### Нерешенные вопросы
<!-- Populated as open questions arise -->
```

### Step 9 — Confirm with user

Output the brief to the user and ask: **"Начинаем реализацию?"**

---

## Phase 2 — During Implementation

While working on the task, **actively maintain `<task_name>_progress.md`** and **gradually populate `<task_name>_result.md`**.

### Update priorities

1. **Update step status in `_info.md`** — `[ ]` → `[/]` → `[x]`.
2. **Update `_progress.md` snapshot** — mark completed steps with evidence.
3. **If the plan changes** — update `_info.md` (Plan section) and record the diff in `_progress.md`.
4. **Populate `_result.md`** — add rows to the files table as changes are made.

### What to write in `_progress.md`

- Important findings during code investigation (non-obvious dependencies, edge cases)
- Decisions made and why (if there was a choice between options)
- Problems discovered and how they were solved
- Changes to the plan relative to `<task_name>_info.md` (when and why)
- List of actually changed/created files

### When to update

- Before starting each major step — record what we are starting
- After discovering something non-obvious — record the finding
- After solving a problem — record the problem and solution
- When the plan changes — record what and why changed

### Format for entries

Each entry — with a timestamp:

```markdown
### [YYYY-MM-DD HH:MM] Step title or finding title
Content.
```

---

## Phase 3 — Completion

When the task is complete, finalize `<task_name>_result.md`:

1. Change `### Статус:` to `Завершено`.
2. Ensure `### Что сделано` lists all completed items.
3. Ensure `### Измененные файлы` table is complete with Evidence column.
4. Fill `### Отличия от плана` if any.
5. Fill `### Нерешенные вопросы` if any.

```markdown
## [Task name] — Результат

### Статус: Завершено

### Что сделано
[Краткое описание результата — что реализовано, 3-7 пунктов]

### Измененные файлы

| Файл | Что изменено | Шаг | Evidence |
|------|-------------|-----|----------|
| `path/to/File.cs` | [краткое описание изменения] | [N] | [test name or command] |

### Отличия от плана
[Что было сделано иначе, чем описано в <task_name>_info.md. Omit if plan was followed exactly.]

### Нерешенные вопросы
[Что осталось сделать или требует внимания. Omit if everything is done.]
```

---

## Rules

- Code identifiers and file paths — English
- Prose labels — Russian
- Steps must name real files found via search, never guessed paths
- Do not lose user requirements during transformation
- **Plan (in `_info.md`) is the source of truth. When the approach changes, update the Plan section in `_info.md` and record the diff in `_progress.md` and `_result.md`.**
- `<task_name>_progress.md` — snapshot first, chronology second; updated continuously
- `<task_name>_result.md` — living document, populated gradually as steps complete
- При повторном вызове `/workflow` на ту же задачу — читать `_result.md` как primary source, `_info.md` как актуальный план
- При повторном вызове `/workflow` на ту же задачу — продолжить работу в существующей папке в `docs/tasks/current/`, не создавать новую
- Все рабочие файлы (`<task_name>_info.md`, `<task_name>_progress.md`, `<task_name>_result.md`) создаются в `docs/tasks/current/<task_name>/`
- Перемещение в `docs/tasks/complete/` выполняет только `/workflow-complete`
