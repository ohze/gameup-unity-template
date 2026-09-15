---
name: unity-test-plan
description: Lập test plan thực dụng cho Unity gồm EditMode, PlayMode và kịch bản manual tái hiện được. Dùng khi chuẩn bị validate một feature, một bug fix, hoặc một build ứng viên.
---

# Unity Test Plan

## Chọn tầng test

| Thứ cần kiểm | Tầng |
|---|---|
| Tính toán, save/`Migrate`, serializer, utils, editor tooling | EditMode (`Tests/Editor`) |
| Pool, audio, UI flow, coroutine, bootstrap, thứ cần `Awake`/frame | PlayMode (`Tests/Runtime`) |
| Cảm giác chơi, VFX, thiết bị thật, IAP/Ads/store | Manual |

## Output

```markdown
## Scope
- Feature/bug: · Risk level: low|medium|high

## Automated Tests
- EditMode:
- PlayMode:

## Manual Scenarios
- Scenario 1: scene → setup → thao tác → kết quả mong đợi → platform

## Non-Functional Checks
- Performance (FPS, frame spike):
- Memory / GC alloc:
- Platform-specific:

## Deferred
- Test hoãn — lý do — khi nào bổ sung:
```

## Rules

- Mỗi acceptance criterion ↔ ít nhất một test.
- Bắt buộc có 1 negative case và 1 edge case.
- Kịch bản manual phải tái hiện được bởi người khác (nêu scene, build, thiết bị).
- Test phải dọn state (`PlayerPrefs`/save) trong `SetUp`/`TearDown` — save của Core mã hoá và lưu trong PlayerPrefs, rất dễ rò giữa các test.
- Không assert theo thời gian thực; PlayMode đếm frame hoặc `yield` tường minh.
