---
name: unity-refactor-safely
description: Refactor C# Unity mà không đổi hành vi và không làm hỏng reference scene/prefab. Dùng khi cải thiện kiến trúc, khả năng đọc hoặc hiệu năng của code đã chạy.
---

# Unity Refactor Safely

Rủi ro đặc thù Unity: đổi tên/di chuyển file làm **rơi reference** trong scene/prefab, mất `[SerializeField]` value, hỏng GUID.

## Các bước

1. Ghi rõ **hành vi phải giữ nguyên**.
2. Tìm seam để tách/đơn giản hoá; ưu tiên rename/extract/move hơn viết lại.
3. Refactor theo checkpoint tí một, mỗi checkpoint biên dịch được.
4. Sau mỗi checkpoint: nêu regression check (test hoặc bước manual).
5. Ghi lại thứ cố ý **không** đổi.

## Checklist Unity

- [ ] Đổi tên class → **đổi tên file cùng lúc**; Unity mất reference nếu lệch
- [ ] Xoá/đổi tên `[SerializeField]` field → giá trị Inspector mất; cần `[FormerlySerializedAs]` nếu muốn giữ
- [ ] Di chuyển file kèm `.meta` (dùng `git mv`, không copy-delete)
- [ ] Đổi namespace không làm mất script reference trên prefab (GUID không đổi khi chỉ đổi namespace)
- [ ] Prefab variant / nested prefab vẫn nguyên override
- [ ] Không phát sinh alloc mới ở hot path
- [ ] Test hoặc ghi chú regression manual đã cập nhật

## Guardrails

- Dừng lại nếu refactor đang biến thành làm feature.
- Không refactor lớn trong `Packages/com.ohze.gameup.core/` — đó là bản restore.
- Nêu tường minh mọi file move / namespace change để người review kiểm reference.
