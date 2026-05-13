---
name: unity-bug-triage
description: Triage Unity bugs into reproducible reports, root-cause hypotheses, and fix plans. Use when receiving bug tickets, QA findings, or crash and exception reports.
disable-model-invocation: true
---

# Unity Bug Triage

## Required Output

```markdown
## Bug Card
- Title:
- Severity:
- Repro steps:
- Expected:
- Actual:
- Frequency:

## Technical Notes
- Suspected area:
- Root-cause hypotheses:
- Evidence needed:

## Fix Plan
- Minimal fix:
- Regression tests:
- Rollback plan:
```

## Rules

- Reject vague reports; ask for missing repro details.
- Separate facts from assumptions.
- Prefer the smallest safe fix first.
- Identify whether the issue is data/config, script logic, or scene/prefab wiring.
