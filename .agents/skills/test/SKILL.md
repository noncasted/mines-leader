---
name: test
description: Write xUnit integration and unit tests for backend features. Use this skill whenever the user asks to write, create, add, or cover tests for any backend functionality — cards, board mechanics, grains, state, transactions, messaging, player stats, meta services, or any game logic. Also trigger on "cover with tests", "add test coverage", "test this", or when user mentions a specific card/system and wants tests written.
---

# AUTO-EXECUTE — DO NOT SUMMARIZE, EXECUTE IMMEDIATELY
TRIGGERS: /test, test, тест, напиши тест, добавь тест, cover with tests, add test coverage, test this, тестирование, напиши тесты, напиши xUnit
BEHAVIOR: When triggered, do not read, summarize, or explain this file. Execute the steps in this skill immediately.

# Write Tests

This skill writes xUnit tests for backend features using the test framework in `backend/Tests/`.

## Invocation

```
/tests <description of what to test>
```

Examples:
- `/tests Trebuchet card`
- `/tests UserRating grain`
- `/tests board generation`
- `/tests Health and Mana player stats`

## Execution Flow

Run three phases sequentially. Each phase uses a subagent.

### Phase 1: Collect Test Cases

Launch Agent with `subagent_type: "general-purpose"` and the prompt from `agents/tests-case-collector.md`, passing:
- The user's description of what to test
- Instruction to output a structured test case list

Wait for the result. Present the test case list to the user and ask for confirmation before Phase 2.

### Phase 2: Write Tests

Launch Agent with `subagent_type: "general-purpose"` and the prompt from `agents/tests-writer.md`, passing:
- The confirmed test case list from Phase 1
- The target feature name and source file paths discovered in Phase 1

Wait for result. Report which tests passed/failed and the file path.

### Phase 3: Update Documentation

Launch Agent with `subagent_type: "general-purpose"` and the prompt from `agents/tests-docs.md`, passing:
- The test file path from Phase 2
- The test case list from Phase 1

Report the updated doc file.

## Quick Reference

### Test project structure
```
backend/Tests/
  Fixtures/     — TestBoardBuilder, BoardParser, IntegrationTestBase, ConfigLoader, CardConfigs
  Game/         — unit tests for cards, board, reveal (no Orleans)
  State/        — integration tests for state, transactions, side effects
  Messaging/    — integration tests for queues, pipes, channels
  Grains/       — test grain implementations
  docs/         — coverage tracker (README.md + per-scope docs)
```

### Test types
| Feature | Type | Base class | Collection |
|---------|------|------------|------------|
| Card, board, reveal, player stats | Unit | none (plain class) | none |
| State, transactions, side effects | Integration | `IntegrationTestBase<OrleansTestClusterFixture>` | `OrleansIntegrationCollection` |
| Side effects with drain | Integration | `IntegrationTestBase<SideEffectTestFixture>` | `SideEffectIntegrationCollection` |
| Messaging | Integration | `IntegrationTestBase<OrleansTestClusterFixture>` | `OrleansIntegrationCollection` |

### Board visual format (for card/board tests)
```
t — Taken cell          R — Revealed (was Taken, now Free)
m — mine (Taken+mine)   D — Defused (was mine, now Free)
f — flagged mine         E — Exploded (Taken, mine, event fired)
g — flagged (no mine)   * — skip assertion
_ — Free cell           x — target position
```
