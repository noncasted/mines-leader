---
name: task
description: Transform a raw task description into a structured implementation brief and save it to docs/tasks/. Use when the user runs /task; do not implement.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /task, task, задача, создай задачу, task brief, implementation brief, structured task, опиши задачу
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Task Skill

When the user runs `/task [description]`, transform the raw task description into a structured implementation brief.

**Do NOT start implementing.** Output the brief and ask for confirmation.

---

## Execution Steps

### Step 1 — Extract ALL requirements

Parse the user's message after `/task`. **Preserve everything — nothing is noise.**

- Составь полный список ВСЕХ целей, подзадач, ограничений и контекста из сообщения пользователя
- Если пользователь упомянул 7 пунктов — в списке должно быть 7 пунктов. Ничего не отбрасывай
- Extract all mentioned class names, system names, file names, features
- Fix typos, normalize informal names to proper C# identifiers
- Сохрани пользовательские описания поведения рядом с техническим маппингом — не заменяй одно другим

### Step 2 — Search the codebase

For every class/system/feature mentioned:
- `Grep` for the class name in `client/Assets/`, `backend/`, or `shared/` to find its file
- `Glob` by pattern if a group of files is involved
- Read key files briefly — enough to understand current implementation, existing interfaces, what the new code must integrate with
- If new files will be created, find the correct `.csproj`

### Step 3 — Map task to documentation

Include docs that are **relevant** to this task. Use this map:

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

If unsure whether a doc is needed — include it. An extra doc reference is cheap; a missing one causes mistakes.

### Step 4 — Decompose into steps

Break the task into ordered concrete steps. Use **two-level decomposition**:

1. **First level — by user requirements.** Each requirement/sub-task from Step 1 becomes a group.
2. **Second level — by files.** Within each group, list concrete file changes.

Rules:
- Name the target file explicitly (real path found in Step 2)
- If a new file must be created, mark it: `[новый файл — добавить в X.csproj]`
- Steps must be sequenced so dependencies come first

### Step 5 — Verify completeness (CRITICAL)

**Re-read the user's original message.** For every requirement, sub-task, constraint, and piece of context the user gave:
- Verify it is covered by a concrete step in the brief
- If something is NOT covered — add it now
- If a user constraint/preference has no matching step — add it to "Контекст" section

This step exists because information loss during transformation is the #1 failure mode. Do not skip it.

### Step 6 — Output the brief

Use the format below.

### Step 7 — Save the brief to a file

After outputting the brief to the user:
- Convert the task short name (from `## Задача: ...`) to a filename: lowercase, spaces → underscores, remove special characters
- **Save path: `docs/tasks/<task_name>.md`** (relative to repo root)
- Confirm to the user: `Задача сохранена: docs/tasks/<task_name>.md`
- Then ask **"Начинаем реализацию?"**

---

## Output Format

```
## Задача: [short name in Russian]

### Цель
[Полное описание цели. Может быть списком из нескольких пунктов если задача многосоставная.
Каждый пункт пользователя должен быть представлен здесь. Не сжимай до одного предложения.]

### Контекст
[Информация от пользователя, которая не является целью или шагом, но важна для реализации:
мотивация задачи, бизнес-ограничения, предпочтения по реализации, связь с другими задачами.
Если пользователь объяснил "почему" — запиши здесь.
Опустить секцию только если пользователь не дал никакого контекста.]

### Шаги реализации

**1. [Требование пользователя N1]**
  1.1. [Concrete action] — `path/to/File.cs`
  1.2. [Concrete action] — `path/to/Other.cs`

**2. [Требование пользователя N2]**
  2.1. [Создать X] — `path/to/New.cs` [новый файл — добавить в X.csproj]
  2.2. [Concrete action] — `path/to/Existing.cs`

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `path/to/File.cs` | [what this file is and why it's relevant] |
| `path/to/Other.cs` | [what this file is and why it's relevant] |

### Документация к прочтению
- `.agents/docs/COMMON_CONTAINER.md` — [конкретная причина: например, создаём новый сервис]
- `.agents/docs/GAMEPLAY.md` — [конкретная причина: например, работаем с картами]

### Риски
[Specific gotchas for this task.]
[Omit section entirely if no risks identified]
```

After outputting: save to file (Step 7), then ask **"Начинаем реализацию?"**

---

## Rules

- Code identifiers and file paths — English
- Prose labels in the brief — Russian
- Steps must name real files found via search, never guessed paths
- Do not lose user requirements during transformation — if the user said 5 things, all 5 must appear in the brief
- Переформулируй цели в технических терминах, но сохрани все детали из оригинала
