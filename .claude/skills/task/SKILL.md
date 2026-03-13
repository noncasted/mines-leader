# Task Skill

When the user runs `/task [description]`, transform the raw task description into a structured implementation brief.

**Do NOT start implementing.** Output the brief and ask for confirmation.

---

## Execution Steps

### Step 1 — Extract the task

Parse the user's message after `/task`:
- Identify the core goal: what must exist or change when the task is done
- Extract all mentioned class names, system names, file names, features
- Fix typos, normalize informal names to proper C# identifiers (e.g. "тайтл скин" → `TitleSkin`)

### Step 2 — Search the codebase

For every class/system/feature mentioned:
- `Grep` for the class name in `Assets/` to find its file
- `Glob` by pattern if a group of files is involved (e.g. `*Timeline*.cs`, `*Dialogue*.cs`)
- Read key files briefly — enough to understand current implementation, existing interfaces, what the new code must integrate with
- If new files will be created, find the correct `.csproj` using `grep -l "SimilarFile.cs" *.csproj`

### Step 3 — Map task to documentation

Only include docs that are **directly needed** for this specific task. Use this map:

| Task touches... | Include |
|-----------------|---------|
| MonoBehaviour, `[Inject]`, `ISceneService`, `IScopeSetup` | `rules/MONOBEHAVIOUR.md` |
| `Advise`, `View`, `Lifetime`, subscriptions, cleanup | `rules/LIFETIMES.md` |
| `EventSource`, `ViewableProperty`, `ViewableList` | `rules/REACTIVE.md` |
| `UniTask`, async methods, file I/O, `IReadOnlyList` | `rules/API_DESIGN.md` |
| member order, naming, braces, `NoAwait` | `rules/CODE_STYLE.md` |
| Timeline, Dialogue, `ObjectAnimationScheme`, `ParseTracks`, anchor, audio | `docs/GAMEPLAY.md` |
| "which pattern", architectural choice | `docs/DECISION_TREES.md` |
| common pitfalls, known mistakes | `rules/COMMON_MISTAKES.md` |

If unsure whether a doc is needed — omit it.

### Step 4 — Decompose into steps

Break the task into ordered concrete steps:
- Each step = one file or one distinct change
- Name the target file explicitly (real path found in Step 2)
- If a new file must be created, mark it: `[новый файл — добавить в X.csproj]`
- Steps must be sequenced so dependencies come first

### Step 5 — Output the brief

Use the format below.

### Step 6 — Save the brief to a file

After outputting the brief to the user:
- Convert the task short name (from `## Задача: ...`) to a filename: lowercase, spaces → underscores, remove special characters
- Write the full brief (exact same text shown to user) to `Docs/Tasks/<task_name>.md`
- Confirm to the user: `Задача сохранена: Assets/Docs/Tasks/<task_name>.md`
- Then ask **"Начинаем реализацию?"**

---

## Output Format

```
## Задача: [short name in Russian]

### Цель
[One sentence: what the system should do after this task is complete that it can't do now.]

### Шаги реализации
1. [Concrete action] — `path/to/File.cs`
2. [Concrete action] — `path/to/Other.cs`
3. [Создать X] — `Assets/path/to/New.cs` [новый файл — добавить в GamePlay.csproj]

### Ключевые файлы

| Файл | Роль в задаче |
|------|---------------|
| `path/to/File.cs` | [what this file is and why it's relevant] |
| `path/to/Other.cs` | [what this file is and why it's relevant] |

### Документация к прочтению
- `rules/MONOBEHAVIOUR.md` — [конкретная причина: например, создаём новый сервис]
- `docs/GAMEPLAY.md` — [конкретная причина: например, работаем с DialogueTextTrack]

### Риски
[Specific gotchas for this task: e.g. "DialogueOptionTrack может быть null если трек не добавлен в схему"]
[Omit section entirely if no risks identified]
```

After outputting: save to file (Step 6), then ask **"Начинаем реализацию?"**

---

## Rules

- Code identifiers and file paths — English
- Prose labels in the brief — Russian
- Steps must name real files found via search, never guessed paths
- Docs list must be minimal — 1-4 items max
- Do not restate the user's words — rewrite the goal in precise technical terms
