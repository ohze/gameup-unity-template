---
name: unity-release-checklist
description: Runs a lightweight Unity release readiness checklist with quality and operational gates. Use before merging major features, cutting release branches, or publishing builds.
disable-model-invocation: true
---

# Unity Release Checklist

## Checklist

- [ ] Build succeeds for target platforms.
- [ ] High and critical bugs triaged or resolved.
- [ ] Smoke scenarios pass on candidate build.
- [ ] Performance budget checked on key scenes.
- [ ] Save/load and upgrade path verified.
- [ ] Changelog and known issues drafted.
- [ ] Rollback or hotfix plan documented.

## Output Format

```markdown
## Release Readiness
- Version:
- Status: Go / No-Go
- Blocking issues:
- Risks accepted:
- Next action:
```

## Guardrails

- If any blocking gate fails, return `No-Go`.
- Separate must-fix items from post-release follow-ups.
- Call out platform-specific risks explicitly (mobile, PC, console) if relevant.
