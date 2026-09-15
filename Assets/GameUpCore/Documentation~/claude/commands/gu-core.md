---
description: Tra API GameUp Core có sẵn cho một nhu cầu, trước khi viết class mới
argument-hint: [nhu cầu, vd: "lưu tiến độ người chơi", "popup shop", "spawn đạn"]
---

Dùng skill `gameup-core-api` để trả lời: **$ARGUMENTS**

Yêu cầu:
- Mở `.claude/gameup-core/API_INDEX.md` trước; tìm tiếp bằng Grep với `path` tường minh (`.claude/gameup-core/src` khi cài qua Git UPM, `Assets/GameUpCore` khi embedded).
- Chỉ ra type/namespace của Core đáp ứng nhu cầu, kèm đường dẫn file nguồn thật; nếu là class để kế thừa thì liệt kê member abstract/virtual cần override.
- **Đọc file nguồn** để lấy chữ ký hàm chính xác, không đoán.
- Không có `.claude/gameup-core/` → nói người dùng chạy `GameUp → Project → Sync GameUp source for AI`, đừng trả lời theo trí nhớ.
- Nếu Core không có, nói rõ và đề xuất nơi đặt code mới trong `Assets/_MainProject/` (kèm namespace riêng).
- Kèm đoạn code mẫu ngắn dùng đúng API.
