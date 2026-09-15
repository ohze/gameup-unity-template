---
name: unity-design-to-tasks
description: Chẻ một design/GDD/feature Unity thành task 1-4h có thứ tự, dependency và test note. Dùng khi cần chuyển ý tưởng hoặc brief thành backlog implement được.
---

# Unity Design to Tasks

## Cách làm

1. Rút ra hành vi **người chơi nhìn thấy** — đó là đơn vị chia task, không phải "tạo class X".
2. Xác định hệ thống bị ảnh hưởng: input · state · UI (`UIScreen`/`UIPopup`) · audio (`AudioManager`) · save (`BaseDataSave`) · addressables · pool · analytics.
3. Chẻ thành task **1–4 giờ**. Task > 4h là chưa hiểu bài toán, chẻ tiếp.
4. Đánh dấu dependency và task chạy song song được.
5. Mỗi task có test note. Task đụng hệ thống mới → thêm 1 task giảm rủi ro (spike/prototype).

## Định dạng task

```markdown
- Task: [tên ngắn]
  - Why:
  - Files dự kiến:
  - Core API tái dùng:
  - Definition of done:
  - Test note:
  - Depends on:
```

## Guardrails

- Không có task cỡ "làm toàn bộ hệ thống inventory".
- Task đụng scene/prefab phải tách khỏi task code — hai người không sửa chung file scene.
- Nói rõ chỗ nào **bắt buộc** dùng lại framework thay vì viết mới.
