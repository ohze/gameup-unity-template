---
description: Triage bug Unity thành bug card + root cause + fix tối thiểu
argument-hint: [mô tả bug / stack trace / log]
---

Dùng skill `unity-bug-triage` cho: **$ARGUMENTS**

Yêu cầu:
- Phân loại nguyên nhân: data/config · script logic · scene/prefab wiring.
- Xếp giả thuyết root cause theo khả năng, tách rõ sự thật và giả định.
- Nếu thiếu bước tái hiện, thiết bị, hoặc version → hỏi, đừng bịa.
- Đề xuất fix nhỏ nhất an toàn + regression test fail-trước/pass-sau.
