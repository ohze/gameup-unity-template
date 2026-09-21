---
description: Dựng prefab UI từ ảnh demo + art bằng GameUp UI Builder (định vị sprite bằng máy, AI bổ sung text/button/anchor)
argument-hint: [prompt copy từ cửa sổ UI Builder, hoặc: ảnh demo + thư mục art + tên UI]
---

Dùng skill `gameup-ui-builder` để làm việc sau: **$ARGUMENTS**

Yêu cầu:
- Tọa độ sprite lấy từ `UIBuilder/<Tên>/locate.json`, không ước lượng lại. Chỉ ước lượng text, glow, art thiếu.
- Dựng và đối chiếu bằng Unity MCP `eval`: `return GameUp.UIBuilder.Editor.UIBuilderApi.BuildAndCompare("<Tên>");` rồi Read `compare.png`, sửa spec tới khi khớp (tối đa 3 vòng).
- Không có Unity MCP → viết xong `spec.json`, nhắc người dùng bấm *Dựng prefab từ spec* trong `GameUp → UI → UI Builder`.
- Kết thúc bằng báo cáo: node đã dựng, phần ước lượng, art thiếu, sprite cần set border 9-slice, phần designer để sót đã bỏ.
