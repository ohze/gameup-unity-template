---
name: unity-implement-story
description: Implement một story/task Unity C# theo từng increment nhỏ có kiểm chứng. Dùng khi bắt tay code một task cụ thể trong Assets/_MainProject.
---

# Unity Implement Story

## Vòng lặp

1. Diễn giải acceptance criteria thành điều kiện ở mức code.
2. **Trước khi sửa**: liệt kê file sẽ đụng và grep API Core liên quan (`CLAUDE.md` §4).
3. Implement increment nhỏ nhất có ích.
4. Kiểm chứng: `using` đủ/không thừa · namespace đúng · asmdef có reference · không alloc trong hot path · không `Debug.*`.
5. Lặp tới khi đủ criteria.

## Báo cáo mỗi increment

```markdown
## Increment
- Goal:
- Files changed:
- Core API dùng lại:
- Validation:
- Remaining:
```

## Guardrails

- Không trộn refactor không liên quan vào feature work.
- Đổi public API phải nêu rõ và có lý do.
- Yêu cầu mơ hồ → dừng và hỏi, đừng đoán rồi làm lại.
- Ưu tiên `GULogger`, `Signal`, `UIScreen`/`UIPopup`, `GUPool`, `BaseDataSave` thay vì tự viết.
- Không đặt type mới của game vào namespace `GameUp.Core*`.
