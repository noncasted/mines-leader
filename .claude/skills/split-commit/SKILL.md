# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /split-commit, split-commit, раздели коммит, split commit, разбей на коммиты, организуй коммиты, commit everything in parts, too many changes for one commit
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Split Commit Skill

When the user runs `/split-commit`, analyze all current changes and split them into logical groups, committing each group separately. The goal is to produce a clean, readable git history instead of one massive commit with unrelated changes mixed together.

Use this skill whenever the user has a large set of uncommitted changes and wants to organize them into multiple focused commits. Also use when the user says things like "commit everything in parts", "split this into commits", "too many changes for one commit", or "organize my changes".

## Commit Message Rules

Follow the EXACT same format as `/commit`:

### Title Format
- Extract the ticket ID from the current branch name (e.g., `ATS-123`)
- If ticket ID exists: `[ATS-123] Brief description`
- If no ticket ID: `[Scope] Brief description` or `[Scope1] [Scope2] Brief description`
  - Use multiple tags when a commit spans several areas (e.g., `[Shared] [Console]`, `[Infra] [Tests]`)
  - Scope is determined by the area of work (e.g., [Visual], [Timeline], [Objects], [Editor], [Core], [UI], [Network], [Client], [Shared], [Console], [Infra], [Tests], [Cards], [Claude], [Docs])
- Keep the title SHORT and descriptive

### Description Format
- Use bullet points, each starting with `-`
- Each bullet must start with one of these tags:
  - `add:` - for new features or additions
  - `fix:` - for bug fixes
  - `refactor:` - for code refactoring
  - `remove:` - for deletions

NEVER NEVER NEVER ADD CLAUDE TO CO-AUTHORS
NEVER NEVER NEVER ADD CLAUDE TO CO-AUTHORS

### Language
- ALL text must be in ENGLISH (titles, scopes, descriptions, tags)

## Execution Steps

### Phase 1: Gather Context

1. Run `git status` to see all changed/untracked files
2. Run `git diff` and `git diff --cached` to understand what changed
3. Run `git log -40 --oneline` to see recent commits and understand naming style
4. Get current branch name with `git rev-parse --abbrev-ref HEAD` (for ticket ID extraction)

### Phase 2: Plan the Split

Analyze all changes and group them by logical purpose. The key principle: each commit should represent ONE coherent idea that makes sense on its own.

Good grouping strategies:
- **By feature/scope**: all files related to one feature go together (e.g., "add Lockdown card" = shared model + backend grain + client UI + config + artwork)
- **By layer when independent**: if changes in different layers are unrelated, split them (e.g., backend infra refactor separate from client UI fix)
- **Tests with their code**: test files go in the same commit as the code they test
- **Config/docs separate**: pure config changes or documentation updates can be their own commit
- **Renames/moves separate**: bulk renames or file moves are cleaner as their own commit

Bad grouping (avoid):
- One commit per file (too granular, loses context)
- Grouping by file type regardless of purpose (all .cs together, all .razor together)
- Mixing unrelated features just because they touch the same directory

Present the plan to the user as a numbered list:
```
Proposed split:
1. [Cards] Add Lockdown card (5 files)
   - shared/Configs/CardConfigOptions.cs
   - backend/Game/Cards/LockdownCard.cs
   - ...
2. [Infrastructure] Refactor task scheduler (4 files)
   - ...
3. [Tests] Add task balancer tests (3 files)
   - ...
```

Ask the user to confirm or adjust before committing.

### Phase 3: Execute Commits

For each group, in order:

1. Stage ONLY the files in that group: `git add <file1> <file2> ...`
   - NEVER use `git add .` or `git add -A` — stage specific files only
   - For renamed/moved files, stage both old and new paths
2. Verify staging is correct: `git diff --cached --stat`
3. Commit with the formatted message
4. Report: commit hash + summary

After all commits, show a final summary:
```
Done! Created N commits:
  abc1234 [Cards] Add Lockdown card
  def5678 [Infrastructure] Refactor task scheduler
  ghi9012 [Tests] Add task balancer tests
```

## Edge Cases

- **Partial file changes**: if one file contains changes for two different groups, mention this to the user and suggest which group to put it in (don't try to split hunks with `git add -p` — keep it simple)
- **Dependencies between groups**: commit the dependency first (e.g., shared model before backend code that uses it)
- **Very few changes**: if changes naturally form just 1 group, say so and suggest using `/commit` instead
- **Binary files** (images, assets): group with the feature they belong to

## Important Notes

- ALWAYS ask the user to confirm the split plan before committing
- Use `git add` with explicit file paths, never `-A` or `.`
- Commits are created locally; push to remote is handled separately
- If the user disagrees with the grouping, adjust and re-present
