---
name: gameup-sdk-installer-flow
description: Handles GameUp SDK installer and updater workflow in Unity Editor with safe package checks and post-install setup. Use when editing GameUpPackageInstaller, dependency windows, or installer-related menu actions.
disable-model-invocation: true
---

# GameUp SDK Installer Flow

## Goal

Update installer code without breaking first-time setup, package update flow, or define-symbol synchronization.

## Workflow

1. Confirm target flow: first install, update, or reset.
2. Verify dependency check entry points before editing installer behavior.
3. Keep one-session safeguards (`SessionState`) to prevent repeated popups.
4. Apply post-install steps only after all required packages are installed.
5. Add or update user-facing logs/dialogs for both success and failure paths.

## Validation Checklist

- [ ] First open after import shows setup UI only when dependencies are missing.
- [ ] Already-complete projects do not show setup UI repeatedly.
- [ ] Reset action clears completion state and can re-trigger setup.
- [ ] Define symbols or readiness flags are synchronized after successful install.
- [ ] Update path remains idempotent when installer runs multiple times.

## Output Format

```markdown
## Installer Change Report
- Flow targeted:
- Files changed:
- Success path validated:
- Failure path validated:
- Remaining risk:
```
