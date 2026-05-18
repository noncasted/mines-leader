---
name: improve-prompt
description: "Rewrite and improve a Claude Code prompt for maximum effectiveness. Use this skill whenever the user asks to improve, rewrite, optimize, or enhance a prompt — or when they paste a draft prompt and say something like 'make this better', 'polish this', 'how should I phrase this', 'fix my prompt'. Also trigger when the user says /improve-prompt or mentions prompt quality."
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /improve-prompt, improve-prompt, улучши промпт, оптимизируй промпт, rewrite prompt, polish prompt, fix my prompt, make this better, how should I phrase this, улучши запрос
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Improve Prompt

You receive a raw prompt from the user. Your job: return a significantly better version that will produce more accurate, focused results from Claude Code.

## Output format

Return ONLY the improved prompt inside a fenced code block. No preamble, no explanation, no "here's what I changed". The user will copy it and go.

If the prompt is already strong and you'd only make cosmetic changes, say so in one line instead of rewriting.

## How to improve

Read the original prompt carefully. Then apply these techniques in order of impact:

### 1. Add specificity where it's vague

Vague prompts are the #1 source of bad results. Turn implicit expectations into explicit instructions.

- Bad: "fix the login bug"
- Good: "the login form in `client/Assets/Scripts/Auth/LoginPanel.cs` throws NullRef when email is empty. Add a null check before `_authService.Login()` and show an error message via `_errorText`"

If the user's prompt references something in the project but doesn't specify where — add likely file paths, class names, or grep hints based on what you know about the codebase.

### 2. Scope the task boundaries

Claude tends to overengineer when scope is unclear. A good prompt draws a box around what should change and what shouldn't.

Add scope boundaries when missing:
- Which files/classes to touch
- Which files NOT to touch
- Whether tests are expected
- Whether to commit or just edit

### 3. Point to existing patterns

Instead of describing desired code structure from scratch, point Claude to an existing example in the codebase. This is dramatically more effective than verbal description.

- Bad: "create a new MonoBehaviour service for the shop"
- Good: "create ShopPanel following the same pattern as `client/Assets/Scripts/Lobby/LobbyPanel.cs` — ISceneService + IScopeSetup + Create() + OnSetup()"

### 4. Add verification criteria

The single highest-leverage addition. Tell Claude how to check its own work.

- "run the tests after implementing"
- "verify it compiles with `dotnet build backend/`"
- "grep for any remaining references to the old name"

### 5. Structure with clear sections

For complex prompts, break into logical sections. Use line breaks and short headers. Claude parses structure well — a wall of text is harder to follow than separated concerns.

### 6. State the WHY when it matters

When the desired approach isn't obvious, explain motivation. Claude generalizes better from understanding than from commands.

- Bad: "don't use async void"
- Good: "use UniTask instead of async void — async void swallows exceptions and we can't track lifetime"

### 7. Prefer positive instructions

Tell Claude what TO do, not what NOT to do. Negations are weaker signals.

- Bad: "don't create new files"
- Good: "edit only existing files"

### 8. Remove noise

Strip filler words, politeness padding, and redundant context that Claude already knows from AGENTS.md. Every token of noise dilutes the signal.

## Project-specific improvements

You know this codebase. When improving prompts, leverage that knowledge:

- Reference the correct AGENTS.md rules when relevant (MonoBehaviour pattern, Lifetime rules, Orleans grain checklist)
- Add the right file paths — you can grep or glob to find them
- Mention the right base classes, interfaces, and patterns from the project
- Add build/test commands specific to the project

## What NOT to do

- Don't add instructions that duplicate what's already in AGENTS.md — Claude loads those automatically
- Don't add generic "write clean code" filler
- Don't make the prompt 10x longer than necessary — brevity with precision beats verbose with padding
- Don't change the user's intent — improve HOW they ask, not WHAT they ask
