---
description: Sửa luồng installer/setup của GameUp trong Unity Editor an toàn
argument-hint: [first install | update | reset] + mô tả thay đổi
---

Dùng skill `gameup-sdk-installer-flow` cho: **$ARGUMENTS**

Bắt buộc giữ:
- Trạng thái suy ra từ **file thật** trong project, `EditorPrefs` chỉ là cache.
- Package Git UPM nằm trong `Library/PackageCache` — kiểm qua `AssetDatabase`/`PackageInfo` theo path ảo `Packages/<tên>`, không kiểm thư mục vật lý.
- Chống popup lặp trong một session bằng `SessionState`; bỏ qua khi `Application.isBatchMode`.
- Post-install chỉ chạy sau khi dependency đủ; define symbol đồng bộ trên mọi build target.
- Chạy nhiều lần vẫn idempotent; không ghi đè file người dùng nếu không xác nhận.

Trả `Installer Change Report` ở cuối.
