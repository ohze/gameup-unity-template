---
name: unity-implement-story
description: Implements a Unity C# story incrementally with checkpoints and validation. Use when coding a specific story or task from backlog.
disable-model-invocation: true
---

# Unity Implement Story

## Implementation Loop

1. Restate acceptance criteria in code-level terms.
2. Propose minimal file changes before editing.
3. Implement the smallest useful increment.
4. Validate build/test impact.
5. Repeat until criteria are complete.

## Reporting Format

```markdown
## Increment
- Goal:
- Files changed:
- Validation:
- Remaining:
```

## Guardrails

- Do not mix unrelated refactors into feature work.
- Keep public API changes explicit and justified.
- If a requirement is ambiguous, pause and ask.
- For this GameUp project, prefer Core utilities (`GULogger`, `Signal`, `UIScreen`/`UIPopup`, `GUPool`) over reinvention.
