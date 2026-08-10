# GameUp Claude Toolkit — template

Thư mục này là **nguồn** cho bộ công cụ Claude Code của dự án. Nó không được Unity import (thư mục `Documentation~`).

Cài vào project bằng **`GameUp → Settings` → thẻ AI Toolkit → Cài / Cập nhật Claude toolkit**
(hoặc menu `GameUp/Project/Install Claude Code toolkit`).

## Cài gì, vào đâu

| Nguồn (`Documentation~/claude/`) | Đích (gốc project) | Ghi đè khi cập nhật? |
|---|---|---|
| `CLAUDE.md` | `CLAUDE.md` | Có (hỏi trước) |
| `agents/*.md` | `.claude/agents/` | Có |
| `skills/*/SKILL.md` | `.claude/skills/` | Có |
| `commands/*.md` | `.claude/commands/` | Có |
| `hooks/*` | `.claude/hooks/` | Có |
| `settings/settings.template.json` | `.claude/settings.json` | Có (backup `.bak` nếu file cũ không do GameUp sinh) |

`.claude/settings.local.json` (quyền cá nhân của từng người) **không bao giờ** bị đụng tới.

## Nội dung

**Agents** — `unity-game-developer`, `gameup-core-architect`, `unity-performance-optimizer`, `unity-qa-engineer`.

**Skills** — `gameup-core-api`, `unity-feature-kickoff`, `unity-design-to-tasks`, `unity-implement-story`,
`unity-refactor-safely`, `unity-test-plan`, `unity-bug-triage`, `unity-perf-audit`,
`unity-release-checklist`, `gameup-sdk-installer-flow`.

**Commands** — `/gu-kickoff` `/gu-tasks` `/gu-story` `/gu-refactor` `/gu-test` `/gu-bug` `/gu-perf`
`/gu-release` `/gu-core` `/gu-review` `/gu-installer`.

**Hooks**
- `gu-shell-guard` (PreToolUse · Bash) — chặn `rm -rf`, `git reset --hard`, `git push --force`, `git clean -f`, xoá `.meta`.
- `gu-csharp-guard` (PostToolUse · Write|Edit) — bắt `UnityEngine.Debug.*` trong `.cs` thuộc
  `Assets/_MainProject` và `Assets/GameUpCore`, trả lỗi để Claude tự đổi sang `GULogger` ngay trong lượt.
  Bỏ qua một file bằng comment `// gu-lint:allow-debug`. Bỏ sẵn `GULogger.cs`, `FullSerializerJson/`,
  `ThirdParty/`, `Plugins/`. Chuỗi và comment được lọc trước khi soi nên `"…Debug.Log…"` trong string không bị báo nhầm.

Hook cố ý **chỉ** thi hành luật cứng kiểm được chính xác. Quy ước cần đọc ngữ cảnh (namespace, naming,
`FindObjectOfType` không cache, alloc mỗi frame…) nằm ở `CLAUDE.md` và `/gu-review` — hook chạy sau *mỗi*
lần ghi file, nhiễu một chút là người dùng tắt luôn.

Installer chọn `.sh` hay `.ps1` theo hệ điều hành lúc cài, và `chmod +x` trên macOS/Linux.

## Luồng làm việc gợi ý

```
/gu-kickoff  → chốt scope & acceptance criteria
/gu-tasks    → chẻ thành task 1-4h
/gu-story    → implement từng increment
/gu-review   → soi convention trước khi commit
/gu-test     → test plan
/gu-release  → Go / No-Go
```

Gặp bug: `/gu-bug`. Game giật/nặng: `/gu-perf`. Không biết Core có sẵn gì: `/gu-core`.

## Sửa template

Sửa file trong thư mục này rồi bấm **Cập nhật** trong `GameUp → Settings`. Sửa thẳng `.claude/` ở gốc
project cũng được, nhưng lần cập nhật sau sẽ ghi đè — đưa thay đổi hay dùng về đây để cả team nhận được.
