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
    <task_name>_info.md              — initial brief (what to do)
    <task_name>_progress.md       — notes during implementation
    <task_name>_result.md            — final result summary
  complete/<task_name>/
    <task_name>.md        — condensed summary (created by /workflow-complete)
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

### Step 4 — Decompose into steps

Break the task into ordered concrete steps with two-level decomposition:

1. **First level — by user requirements.** Each requirement becomes a group.
2. **Second level — by files.** Within each group, list concrete file changes.

Rules:
- Name the target file explicitly (real path found in Step 2)
- If a new file must be created, mark it: `[новый файл — добавить в X.csproj]`
- Steps must be sequenced so dependencies come first

### Step 5 — Verify completeness (CRITICAL)

**Re-read the user's original message.** For every requirement:
- Verify it is covered by a concrete step
- If something is NOT covered — add it now

### Step 6 — Write `<task_name>_info.md`

Save the brief to `docs/tasks/current/<task_name>/<task_name>_info.md` using this format:

```markdown
## Задача: [short name in Russian]

### Цель
[Полное описание цели. Каждый пункт пользователя должен быть представлен.]

### Контекст
[Мотивация, ограничения, предпочтения, связь с другими задачами.
Опустить секцию только если пользователь не дал никакого контекста.]

### Шаги реализации

**1. [Требование пользователя N1]**
  1.1. [Concrete action] — `path/to/File.cs`
  1.2. [Concrete action] — `path/to/Other.cs`

**2. [Требование пользователя N2]**
  2.1. [Создать X] — `path/to/New.cs` [новый файл — добавить в X.csproj]

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

Create the file with this template:

```markdown
## [Task name] — Рабочие заметки

### Статус: В работе

### Заметки
<!-- Сюда записываются находки, решения и полезная информация по ходу реализации -->
```

### Step 8 — Initialize `<task_name>_result.md`

Create the file with this template:

```markdown
## [Task name] — Результат

### Статус: Не завершено
```

### Step 9 — Confirm with user

Output the brief to the user and ask: **"Начинаем реализацию?"**

---

## Phase 2 — During Implementation

While working on the task, **actively maintain `<task_name>_progress.md`**:

### What to write there

- Важные находки при исследовании кода (неочевидные зависимости, edge cases)
- Принятые решения и почему (если был выбор из нескольких вариантов)
- Обнаруженные проблемы и как они были решены
- Изменения в плане относительно `<task_name>_info.md` (если план скорректирован)
- Список реально измененных/созданных файлов

### When to update

- Перед началом каждого крупного шага из плана — записать что начинаем
- После обнаружения чего-то неочевидного — записать находку
- После решения проблемы — записать проблему и решение
- При изменении плана — записать что и почему изменилось

### Format for entries

Каждая запись — с временной меткой:

```markdown
### [HH:MM] Название шага или находки
Содержание заметки.
```

---

## Phase 3 — Completion (`<task_name>_result.md`)

When the task is complete, write `<task_name>_result.md`:

```markdown
## [Task name] — Результат

### Статус: Завершено

### Что сделано
[Краткое описание результата — что реализовано, 3-7 пунктов]

### Измененные файлы

| Файл | Что изменено |
|------|-------------|
| `path/to/File.cs` | [краткое описание изменения] |

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
- `<task_name>_progress.md` обновляется по ходу работы, а не только в конце
- `<task_name>_result.md` заполняется только когда задача завершена
- При повторном вызове `/workflow` на ту же задачу — продолжить работу в существующей папке в `docs/tasks/current/`, не создавать новую
- Все рабочие файлы (`<task_name>_info.md`, `<task_name>_progress.md`, `<task_name>_result.md`) создаются в `docs/tasks/current/<task_name>/`
- Перемещение в `docs/tasks/complete/` выполняет только `/workflow-complete`
