---
name: gameup-sdk-installer-flow
description: Sửa luồng installer/updater của GameUp trong Unity Editor an toàn (kiểm package, define symbol, post-install, reset). Dùng khi đụng GameUpPackageInstaller, cửa sổ setup dependency, hoặc menu action liên quan cài đặt.
---

# GameUp Installer Flow

## Mục tiêu

Sửa code installer mà không phá: lần cài đầu tiên · luồng update · đồng bộ define symbol · đường reset.

## Workflow

1. Xác định luồng đích: **first install** | **update** | **reset**. Ba luồng, ba điều kiện hoàn tất khác nhau.
2. Xác minh entry point kiểm dependency trước khi sửa hành vi.
3. Giữ chống lặp trong một session bằng `SessionState` (mất khi domain reload thì popup sẽ hiện lại — đó là lỗi hay gặp).
4. **Trạng thái phải suy ra từ file thật trong project**, `EditorPrefs` chỉ là cache. Cờ `EditorPrefs` mất khi clone repo / đổi máy / đổi user — dựa vào nó sẽ báo "chưa cài" sai.
5. Package cài qua Git UPM nằm trong `Library/PackageCache`, **không** phải `Packages/<tên>` — kiểm bằng `AssetDatabase` theo path ảo `Packages/<tên>` hoặc `PackageInfo`, đừng kiểm thư mục vật lý.
6. Post-install chỉ chạy **sau khi** mọi dependency đã đủ.
7. Log/dialog phải có cả success path lẫn failure path, kèm cách khôi phục thủ công.

## Checklist

- [ ] Mở project lần đầu: UI setup chỉ hiện khi thật sự thiếu dependency
- [ ] Project đã đủ: không hiện lại UI setup mỗi lần reload
- [ ] Reset xoá được cờ hoàn tất và cho phép chạy lại
- [ ] Define symbol (`DOTween__DEPENDENCIES_INSTALLED`) đồng bộ trên **mọi** build target sau khi cài
- [ ] Chạy installer nhiều lần vẫn idempotent (không tạo trùng thư mục/prefab/root scene)
- [ ] `Application.isBatchMode` được bỏ qua (CI không được bật popup)
- [ ] Không ghi đè file người dùng đã sửa nếu không có xác nhận

## Output

```markdown
## Installer Change Report
- Flow targeted:
- Files changed:
- Success path validated:
- Failure path validated:
- Idempotency đã kiểm:
- Remaining risk:
```
