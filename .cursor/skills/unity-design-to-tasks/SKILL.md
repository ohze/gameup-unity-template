---
name: unity-design-to-tasks
description: Breaks a Unity design into implementation-ready tasks and sequencing. Use when converting GDD notes or feature ideas into engineering tasks.
disable-model-invocation: true
---

# Unity Design to Tasks

## Workflow

1. Extract gameplay intent and player-visible behavior.
2. Identify impacted systems (`input`, `state`, `UI`, `audio`, `save`, `addressables`).
3. Split into tasks sized for 1-4 hours each.
4. Mark dependencies and parallelizable tasks.
5. Add test notes to every task.

## Task Format

```markdown
- Task: [short name]
  - Why:
  - Files:
  - Definition of done:
  - Test note:
  - Depends on:
```

## Guardrails

- Avoid giant umbrella tasks.
- Include at least one risk mitigation task when touching a new system.
- Explicitly call out where existing shared framework code should be reused.
