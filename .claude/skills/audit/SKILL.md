---
name: audit
description: "Audit and improve parts of the .claude configuration folder — agents, skills, rules, docs, CLAUDE.md. Researches best practices online for the specific domain being audited, then finds inconsistencies, errors, and improvement opportunities. Use this skill when the user says /audit, asks to review or improve their .claude setup, mentions 'audit agents', 'improve my rules', 'check my skill', or wants to optimize any part of the .claude folder."
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /audit, audit, аудит, проверь конфиг, аудит агентов, аудит скиллов, аудит доков, аудит CLAUDE.md, аудит всего
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Audit Skill

Audits parts of the `.claude` folder by researching domain-specific best practices and then comparing the current state against them.

## Arguments

- `/audit agents` — audit agent definitions in `.claude/agents/`
- `/audit skills` — audit all skills in `.claude/skills/`
- `/audit skills/check` — audit a specific skill
- `/audit docs` — audit docs in `docs/db/docs/`
- `/audit CLAUDE.md` — audit the main CLAUDE.md
- `/audit all` — full audit of everything (takes a while)

If no argument given, ask the user what to audit.

## How It Works

The audit has three phases that must run in order. Don't skip phases or combine them — the research phase directly feeds what you look for in the audit phase.

### Phase 1: Understand the Domain

Before searching anything, read the target files and figure out **what domain** you're actually auditing. This determines what to search for.

The same files can serve different purposes depending on context. For example:

| Target | Domain | What to research |
|--------|--------|-----------------|
| `.claude/agents/` as files | AI agent prompt engineering | How to write effective agent system prompts, tool descriptions, few-shot examples |
| `.claude/agents/` as validators (from `/check`) | Static analysis / linting design | How to build effective code validators, what makes good lint rules, false positive rates |
| `.claude/skills/commit/` | Git commit workflow automation | Conventional commits, commit message best practices, pre-commit hooks |
| `.claude/skills/check/` | Code review automation | Automated code review systems, which checks catch real bugs vs noise |
| `docs/db/docs/COMMON_LIFETIMES.md` | Resource management documentation | How to document ownership/lifetime patterns, common pitfalls in reactive systems |
| `docs/db/docs/COMMON_ORLEANS.md` | Distributed systems documentation | Orleans grain best practices, actor model anti-patterns |
| `docs/db/docs/GAMEPLAY.md` | Game design documentation | How to document game mechanics for developers |
| `.claude/CLAUDE.md` | Claude Code configuration | CLAUDE.md structure, prompt hierarchy, what goes where |

The key insight: **don't audit the container, audit the content**. Agent files are prompts — audit them as prompts. A skill that automates commits — audit the commit workflow it implements. Rules about Orleans — audit whether the Orleans guidance is correct and complete.

To identify the domain:

1. Read all target files
2. For each file, determine: what is this file trying to accomplish? What subject matter expertise would someone need to evaluate whether it does that well?
3. Group files by domain if auditing a folder (some agents might share a domain, others might not)

Output a brief domain summary before proceeding to Phase 2. Example:

```
Domain: AI agent prompt engineering
Files: 13 agent definitions in .claude/agents/
Context: These are system prompts for specialized validation subagents.
Each agent is spawned by the /check skill to validate specific code patterns.
```

### Phase 2: Research Best Practices

Search the internet for best practices, common pitfalls, and expert recommendations **specific to the identified domain**. This is the step that makes the audit valuable — you're bringing external expertise the user might not have.

Use WebSearch to find:

1. **Best practices** for the domain (2-3 searches with different angles)
2. **Common mistakes** people make in this domain
3. **Expert recommendations** from authoritative sources

Search strategy by domain type:

**For prompt/agent engineering:**
- Search for recent prompt engineering guides, system prompt best practices
- Look for agent design patterns, tool-use prompt strategies
- Find common anti-patterns in LLM instructions

**For code workflow automation (commits, PRs, checks):**
- Search for the specific workflow's best practices (e.g., "conventional commits best practices 2025")
- Look for what similar tools do (e.g., how other linters organize rules)
- Find metrics on what actually catches bugs vs what's noise

**For technical documentation (rules, docs):**
- Search for documentation best practices for the specific technology (Orleans, Unity, reactive systems)
- Look for the technology's own official best practices to verify the rules are correct
- Find common misconceptions about the technology

**For CLAUDE.md / configuration:**
- Search for Claude Code CLAUDE.md best practices, project instruction patterns
- Look for prompt hierarchy and context management strategies

After researching, compile a **best practices checklist** specific to the domain. This becomes your audit criteria. Write it down explicitly before proceeding to Phase 3.

Example checklist for agent prompts:
```
Best Practices Found:
1. System prompts should state role, scope, and anti-scope clearly
2. Few-shot examples should cover edge cases, not just happy path
3. Output format should be specified unambiguously
4. Instructions should explain WHY, not just WHAT
5. Negative examples ("don't do X") are weaker than positive reframes
6. Long prompts benefit from headers and progressive disclosure
7. Tool descriptions should include failure modes
...
```

### Phase 3: Audit

Go through each target file systematically, checking against:

1. **The best practices checklist** from Phase 2
2. **Internal consistency** — do the files contradict each other? Do they reference things that don't exist?
3. **Completeness** — are there obvious gaps? Does the coverage match the project's actual needs?
4. **Accuracy** — for technical rules, is the guidance actually correct? (Cross-reference with project code if needed)
5. **Effectiveness** — based on the research, will this actually achieve its goal?

For each finding, categorize:

- **Error** — factually wrong, will cause problems (e.g., a rule references a class that doesn't exist)
- **Gap** — important thing missing (e.g., no agent covers a critical validation area)
- **Improvement** — works but could be better (e.g., agent prompt lacks examples for edge cases)
- **Style** — minor formatting/consistency issues

## Output Format

```
## Audit: [target]
Domain: [identified domain]

### Research Summary
[2-3 key insights from best practices research that are most relevant]

### Findings

#### Errors
> `file.md` — [description]
> Evidence: [what's wrong and why]
> Fix: [specific fix]

#### Gaps
> [what's missing]
> Why it matters: [based on research]
> Suggestion: [how to add it]

#### Improvements
> `file.md` — [what could be better]
> Best practice: [what research says]
> Suggestion: [specific improvement]

#### Style
> [minor issues]

### Summary
Errors: N | Gaps: N | Improvements: N | Style: N

### Top 3 Recommendations
1. [highest impact change]
2. [second highest]
3. [third highest]
```

## Rules

1. **Always research first** — the audit's value comes from external knowledge, not just pattern matching
2. **Domain-specific searches** — "best practices for X" where X is the actual domain, not "best practices for markdown files"
3. **Cite sources** — when a recommendation comes from research, mention where (e.g., "per Orleans documentation..." or "common pattern in prompt engineering...")
4. **Cross-reference with project** — if a rule says "always do X", grep the codebase to see if X is actually done. Rules that don't match reality are either wrong rules or unenforced rules — both are findings
5. **Russian prose** for the report, English for code and technical terms
6. **Don't suggest changes to CLAUDE.md structure that would break the keyword lookup table** — it's load-bearing
7. **Prioritize actionable findings** — "this could be better" is less useful than "change line 42 from X to Y because Z"
