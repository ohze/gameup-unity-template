---
name: unity-bug-triage
description: Triage bug Unity thành báo cáo tái hiện được, giả thuyết root cause và kế hoạch fix tối thiểu. Dùng khi nhận ticket bug, phản hồi QA, hoặc crash/exception report.
---

# Unity Bug Triage

## Ba nhóm nguyên nhân — phân loại trước, đào sau

1. **Data / config** — SO sai giá trị, save cũ chưa `Migrate` (`dataVersion`), AudioID lệch với `AudioDatabase`, Addressables group/label sai.
2. **Script logic** — điều kiện sai, thứ tự init, dùng object đã `Destroy`, quên huỷ đăng ký `Signal`, race giữa coroutine.
3. **Scene / prefab wiring** — reference rơi sau refactor, prefab bị unpack, `.meta`/GUID đổi, component thiếu trên variant.

Mẹo phân biệt nhanh: bug **chỉ xảy ra ở một scene/prefab** → nhóm 3. Bug **chỉ với save cũ / máy đã chơi trước** → nhóm 1. Bug **tái hiện 100% ở project sạch** → nhóm 2.

## Output

```markdown
## Bug Card
- Title:
- Severity: blocker | critical | major | minor
- Repro steps:
- Expected / Actual:
- Frequency: always | intermittent (n/m) | once
- Build / Platform / Device / Unity version:

## Technical Notes
- Nhóm nguyên nhân:
- Giả thuyết root cause (xếp theo khả năng):
- Bằng chứng còn thiếu (log, screenshot, save file, profiler capture):

## Fix Plan
- Fix nhỏ nhất an toàn:
- Regression test (fail trước fix, pass sau fix):
- Rollback plan:
```

## Rules

- Report mơ hồ → **hỏi lại**, không tự bịa bước tái hiện.
- Tách rõ sự thật quan sát được và giả định của bạn.
- Fix nhỏ nhất trước; refactor "nhân tiện" là cách tạo bug mới.
- Nếu có stack trace: đọc đúng frame đầu tiên thuộc code dự án, đừng dừng ở frame của engine.
