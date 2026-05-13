---
name: unity-feature-kickoff
description: Defines a Unity feature kickoff brief with goals, constraints, risks, and acceptance criteria. Use when starting a new feature, spike, or gameplay system.
disable-model-invocation: true
---

# Unity Feature Kickoff

## Output Template

Use this structure:

```markdown
## Feature Brief
- Feature:
- Player value:
- In scope:
- Out of scope:

## Constraints
- Unity version:
- Target platform:
- Performance budget:
- Existing systems to reuse:

## Risks
- Risk 1:
- Risk 2:

## Acceptance Criteria
- [ ] Criterion 1
- [ ] Criterion 2
```

## Rules

1. Ask for missing constraints before implementation.
2. Keep acceptance criteria measurable and testable.
3. Flag unknowns that can cause rework.
4. Prefer existing project frameworks (for this repo: GameUp Core APIs) before proposing custom infrastructure.
