---
name: unity-feature-kickoff
description: Chốt phạm vi và acceptance criteria cho một feature/hệ thống Unity trước khi code. Dùng khi bắt đầu tính năng mới, spike kỹ thuật, hoặc khi yêu cầu còn mơ hồ.
---

# Unity Feature Kickoff

Mục tiêu: biến một ý tưởng mơ hồ thành brief có thể implement mà không phải làm lại.

## Cách làm

1. Hỏi (không đoán) các ràng buộc còn thiếu: platform đích, thiết bị thấp nhất, ngân sách hiệu năng, deadline.
2. Grep xem hệ thống nào trong GameUp Core / `_MainProject` đã giải quyết một phần bài toán — ghi vào "Existing systems to reuse".
3. Viết acceptance criteria **đo được** (có số, có điều kiện quan sát được), không viết "chạy mượt", "đẹp hơn".
4. Nêu rõ out-of-scope — đây là thứ cứu dự án khỏi scope creep.

## Output

```markdown
## Feature Brief
- Feature:
- Player value:
- In scope:
- Out of scope:

## Constraints
- Unity version / render pipeline:
- Target platform + thiết bị thấp nhất:
- Performance budget (FPS, memory, load time):
- Hệ thống Core sẽ tái dùng:

## Risks
- Risk 1 — tác động — cách giảm:
- Risk 2 — …

## Acceptance Criteria
- [ ] Criterion 1 (đo được)
- [ ] Criterion 2
```

## Guardrails

- Thiếu ràng buộc → hỏi, không tự chọn thay người dùng.
- Luôn ưu tiên API GameUp Core trước khi đề xuất hạ tầng mới; nếu đề xuất mới, phải nói rõ Core thiếu gì.
- Đánh dấu mọi unknown có thể gây làm lại.
