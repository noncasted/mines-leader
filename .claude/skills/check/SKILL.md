# Check Skill

When the user runs `/check`, launch validation agents in parallel to audit code for correctness.

---

## Arguments

- `/check` — check all git-modified files (staged + unstaged + untracked `.cs`/`.razor`)
- `/check path/to/dir` — check all `.cs`/`.razor` files in the specified directory (recursively)
- `/check path/to/File.cs` — check a single file

---

## Execution Steps

### Step 1 — Determine target files

**If no argument given** — get git-modified files:
```bash
# Staged + unstaged
git diff --name-only HEAD
git diff --name-only --cached
# Untracked
git ls-files --others --exclude-standard
```
Merge results, deduplicate. Filter to `.cs` and `.razor` files only.

**If directory argument given** — use Glob to find all `.cs` and `.razor` files recursively in that directory.

**If file argument given** — use that single file.

If no matching files found — report "Нет файлов для проверки" and stop.

### Step 2 — Categorize files

Split files into domains:

| Domain | Path pattern | Files |
|--------|-------------|-------|
| `client` | `client/**/*.cs` | Unity MonoBehaviours, services, UI |
| `backend` | `backend/**/*.cs` | Orleans grains, services |
| `backend-blazor` | `backend/Console/**/*.razor` | Blazor config editors |
| `shared` | `shared/**/*.cs` | Configs, protocol, models |

### Step 3 — Select agents per domain

Launch ONLY relevant agents. Use this mapping:

**Always launch** (if any `.cs` files present):
- `code-style-checker` — member order, naming, braces, client/backend separation, .csproj
- `public-interface-prettifier` — return types, naming conventions, vocabulary
- `logging-inspector` — log prefixes, silent failures, logger injection
- `error-handling-checker` — try/catch coverage, rethrow violations, resource cleanup

**If `shared` files present:**
- `shared-model-checker` — serialization attributes, enum completeness, ConfigOptions consistency, shared-to-client/backend sync

**If `client` files present:**
- `monobehaviour-checker` — ISceneService/IScopeSetup/Create/OnSetup pattern
- `lifetimes-inspector` — null lifetimes, View vs Advise, item.Lifetime in collections

**If `backend` files present:**
- `state-checker` — [GenerateSerializer], [Id(N)], StatesLookup, AddStates registration
- `transaction-checker` — [Transaction] attribute, InTransaction usage, OnUpdated variants
- `race-condition-checker` — interleaving after await, concurrent mutations

**If `backend` OR `client` files with async/UniTask present:**
- `race-condition-checker` — (also covers client-side UniTask races)

**If `backend-blazor` files present:**
- `blazor-inspector` — model-editor completeness, @bind-Value, routing

**If `client` AND `backend` files present (cross-cutting change):**
- `lifetimes-inspector` — Lifetime is used in both client and backend

**If any significant changes (new features, new enums, new cards):**
- `docs-checker` — check if documentation needs updating

### Step 4 — Launch agents in PARALLEL

Launch all selected agents simultaneously using the Agent tool. Each agent receives:

```
Validate the following files for <agent-specific-scope>:

Files to check:
<list of files with full paths>

Read each file and apply your validation rules. Report findings in your standard output format.
```

CRITICAL: Launch ALL agents in a SINGLE message with multiple Agent tool calls. Do NOT launch them sequentially.

### Step 5 — Collect and summarize results

After all agents complete, produce a unified report.

---

## Output Format

```
## /check Report

### Запущенные агенты
- code-style-checker — <VERDICT>
- lifetimes-inspector — <VERDICT>
- monobehaviour-checker — <VERDICT>
- shared-model-checker — <VERDICT>
- docs-checker — <VERDICT>
- ...

---

### Критические проблемы (CRITICAL)

> Из: lifetimes-inspector
`client/Assets/.../MyPanel.cs:42` — `Advise(null, ...)` memory leak
Fix: передать lifetime параметр из OnSetup

> Из: state-checker
`shared/States/CardState.cs` — не зарегистрирован в AddStates()
Fix: добавить `Add<CardState>(StatesLookup.Card)` в ProjectsSetupExtensions

---

### Ошибки (ERROR)

> Из: monobehaviour-checker
`client/Assets/.../ShopPanel.cs` — IScopeSetup отсутствует
Fix: добавить `IScopeSetup` в объявление класса

> Из: shared-model-checker
`shared/Protocol/MatchResult.cs` — `[Id(N)]` gap: 0, 1, 3
Fix: перенумеровать: 0, 1, 2

---

### Предупреждения (WARNING)

> Из: code-style-checker
`client/Assets/.../Utils.cs:15` — поле `_hp` → `_health`

---

### Документация

> Из: docs-checker
`docs/GAMEPLAY.md` — CardType.ChainReaction не документирован
Action: запустить docs-writer для обновления

---

## Итог
Файлов проверено: N
Агентов запущено: M
CRITICAL: X | ERROR: Y | WARNING: Z

VERDICT: PASS | FAIL
```

---

## Rules

1. **Parallel execution is mandatory** — all agents launch in one message
2. **Only launch relevant agents** — don't run monobehaviour-checker on backend files
3. **Deduplicate findings** — if two agents report the same issue, show it once with both sources
4. **Order by severity** — CRITICAL first, then ERROR, then WARNING, then docs
5. **Include file:line** where agents provide it
6. **VERDICT** — FAIL if any CRITICAL or ERROR exists, PASS if only warnings or clean
7. **Russian prose** for the report, English for code identifiers
