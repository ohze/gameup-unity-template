---
description: Refactor C# Unity giữ nguyên hành vi, không làm rơi reference scene/prefab
argument-hint: [file / class / hệ thống cần refactor]
---

Dùng skill `unity-refactor-safely` cho: **$ARGUMENTS**

Bắt buộc:
- Ghi rõ hành vi phải giữ nguyên **trước khi** sửa.
- Refactor theo checkpoint nhỏ, mỗi checkpoint biên dịch được.
- Nêu tường minh mọi file move / rename / đổi namespace để review kiểm reference.
- Đổi tên `[SerializeField]` field thì cảnh báo mất giá trị Inspector và đề xuất `[FormerlySerializedAs]`.
- Dừng lại nếu refactor đang biến thành làm feature.
