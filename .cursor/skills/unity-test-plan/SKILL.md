---
name: unity-test-plan
description: Produces pragmatic Unity test plans across edit mode, play mode, and manual checks. Use when preparing validation for a feature, bug fix, or release candidate.
disable-model-invocation: true
---

# Unity Test Plan

## Plan Sections

```markdown
## Scope
- Feature/bug:
- Risk level:

## Automated Tests
- EditMode:
- PlayMode:

## Manual Scenarios
- Scenario 1:
- Scenario 2:

## Non-Functional Checks
- Performance:
- Memory/GC:
- Platform-specific:
```

## Rules

- Tie each acceptance criterion to at least one test.
- Include one negative case and one edge case.
- Keep manual scenarios reproducible.
- If tests are deferred, explicitly record why and when they will be added.
