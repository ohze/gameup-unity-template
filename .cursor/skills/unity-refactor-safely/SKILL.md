---
name: unity-refactor-safely
description: Refactors Unity C# code with behavior preservation and rollback safety. Use when improving architecture, readability, or performance without changing feature behavior.
disable-model-invocation: true
---

# Unity Refactor Safely

## Steps

1. Define unchanged behavior explicitly.
2. Identify seams for extraction or simplification.
3. Refactor in tiny checkpoints.
4. Run or design regression checks after each checkpoint.
5. Document what was intentionally not changed.

## Checklist

- [ ] Public behavior preserved
- [ ] Scene/prefab references still valid
- [ ] No new allocations in hot paths
- [ ] Tests or manual regression notes updated

## Guardrails

- Prefer rename/extract/move over broad rewrites.
- Stop if refactor becomes feature work.
- Keep file moves and namespace changes explicit to avoid broken Unity references.
