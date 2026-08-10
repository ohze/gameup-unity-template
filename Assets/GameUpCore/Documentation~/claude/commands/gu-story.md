---
description: Implement một story Unity theo increment nhỏ, ưu tiên API GameUp Core
argument-hint: [story / task cần làm]
---

Dùng skill `unity-implement-story` để implement: **$ARGUMENTS**

Trước khi sửa file:
1. Grep API Core liên quan (skill `gameup-core-api`) — không viết lại thứ đã có.
2. Liệt kê file sẽ đụng.

Sau mỗi increment, báo cáo theo mẫu `Increment` và tự kiểm:
- không `Debug.*` trong code feature (dùng `GULogger`)
- không `using` thừa, không dead code
- naming theo `CLAUDE.md` §3
- không alloc trong `Update`/`FixedUpdate`
