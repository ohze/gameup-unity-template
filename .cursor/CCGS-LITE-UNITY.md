# CCGS-lite for Cursor (Unity C#)

This workspace includes a lightweight "studio workflow" inspired by CCGS and adapted for Unity C# with GameUp conventions.

## Included

- Rules in `.cursor/rules/`
- Skills in `.cursor/skills/`
- Basic shell safety hook in `.cursor/hooks.json`

## Core Skills

- `unity-feature-kickoff`
- `unity-design-to-tasks`
- `unity-implement-story`
- `unity-refactor-safely`
- `unity-test-plan`
- `unity-bug-triage`
- `unity-release-checklist`
- `gameup-sdk-installer-flow`

## Suggested Daily Flow

1. Run `unity-feature-kickoff` to lock scope and acceptance criteria.
2. Run `unity-design-to-tasks` to split implementation work.
3. Use `unity-implement-story` for incremental coding.
4. Use `unity-test-plan` before merge or QA handoff.
5. Use `unity-bug-triage` when issues are reported.
6. Use `unity-release-checklist` before release branch or build publish.

## Daily 1-Line Prompts (Quick Copy)

- `unity-feature-kickoff`: `Use unity-feature-kickoff for [feature], define player value, scope, constraints, risks, and measurable acceptance criteria.`
- `unity-design-to-tasks`: `Use unity-design-to-tasks to split [feature/design] into 1-4h tasks with dependencies and test notes for each task.`
- `unity-implement-story`: `Use unity-implement-story to implement [story] in small increments with minimal file changes and per-increment validation.`
- `unity-refactor-safely`: `Use unity-refactor-safely to refactor [target] without behavior change, keep Unity refs safe, and provide regression checks.`
- `unity-test-plan`: `Use unity-test-plan for [feature/bug], map criteria to EditMode/PlayMode/manual tests plus edge and negative cases.`
- `unity-bug-triage`: `Use unity-bug-triage for [bug], produce repro-ready bug card, root-cause hypotheses, minimal fix, and regression test plan.`
- `unity-release-checklist`: `Use unity-release-checklist for [version], return Go/No-Go with blocking issues, accepted risks, and next action.`

## Installer Flow (GameUp SDK)

Use `gameup-sdk-installer-flow` when working with:

- `Assets/GameUpSDK/Editor/Installer/GameUpPackageInstaller.cs`
- dependency setup windows and installer-related menu actions
- package install/update/reset behavior in Unity Editor

### Team Prompt Template

Use this prompt format in chat:

```text
Use `gameup-sdk-installer-flow`.
Target flow: [first install | update | reset].
Scope: update installer behavior in `Assets/GameUpSDK/Editor/Installer/GameUpPackageInstaller.cs`.
Constraints:
- Keep startup idempotent (no repeated popup in same session).
- Run post-install actions only after all required packages are installed.
- Keep define-symbol readiness synchronized after successful install.
Deliver:
- Minimal code changes
- Validation for success path and failure path
- Installer Change Report
```

### 1-Line Prompt

`Use gameup-sdk-installer-flow: update [first install|update|reset] in Assets/GameUpSDK/Editor/Installer/GameUpPackageInstaller.cs, keep idempotent popup behavior, run post-install only after deps are complete, sync define readiness, and return Installer Change Report with success/failure validation.`

## Notes

- Existing GameUp rules stay in place and remain the source of truth for framework usage.
- The shell hook denies obviously dangerous commands such as `git push --force` and `git reset --hard`.
