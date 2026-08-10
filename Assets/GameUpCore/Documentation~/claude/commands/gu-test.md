---
description: Lập test plan EditMode/PlayMode/manual cho feature hoặc bug fix
argument-hint: [feature / bug cần test]
---

Dùng skill `unity-test-plan` cho: **$ARGUMENTS**

Yêu cầu:
- Map từng acceptance criterion tới ít nhất một test.
- Bắt buộc có 1 negative case và 1 edge case.
- Nói rõ test nào EditMode (`Tests/Editor`), test nào PlayMode (`Tests/Runtime`).
- Kịch bản manual phải tái hiện được: scene → setup → thao tác → kết quả mong đợi → platform.
- Nếu hoãn test nào, ghi lý do và thời điểm bổ sung.
