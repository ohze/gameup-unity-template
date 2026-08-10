---
description: Audit hiệu năng Unity (FPS, GC, draw call, memory, load time) và đề xuất sửa
argument-hint: [triệu chứng / scene / hệ thống cần audit]
---

Dùng skill `unity-perf-audit` cho: **$ARGUMENTS**

Yêu cầu:
- Đo/tìm bằng chứng trước; thứ nào chưa đo được thì ghi rõ là **giả thuyết**.
- Quét code theo danh mục hay thủng: `Instantiate`/`Destroy` không pool, alloc mỗi frame (LINQ, string, closure), `GetComponent`/`Find` trong `Update`, Canvas rebuild, handle Addressables không release, `Signal` không huỷ đăng ký.
- Xếp đề xuất theo hiệu quả/chi phí, kèm con số kỳ vọng.
- Không đổi `ProjectSettings/` mà không báo trước.
