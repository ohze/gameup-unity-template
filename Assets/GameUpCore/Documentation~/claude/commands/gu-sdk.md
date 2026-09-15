---
description: Tra API GameUp SDK (ads, analytics, remote config, consent) cho một nhu cầu trước khi viết code
argument-hint: [nhu cầu, vd: "rewarded hồi sinh", "inter cuối level mỗi 2 phút", "log hoàn thành level"]
---

Dùng skill `gameup-sdk-api` để trả lời: **$ARGUMENTS**

Yêu cầu:
- Xác nhận project có cài GameUp SDK (`.claude/gameup-sdk/API_INDEX.md` hoặc `Assets/GameUpSDK`). Không cài → nói rõ và dừng.
- Mở `API_INDEX.md` trước; tìm tiếp bằng Grep với `path` tường minh (`.claude/gameup-sdk/src` khi cài qua Git UPM, `Assets/GameUpSDK` khi embedded). **Đọc file nguồn** lấy chữ ký thật — README repo có ví dụ đã cũ.
- Chỉ ra class/method SDK đáp ứng nhu cầu kèm đường dẫn file; luật chặn ads thì chọn `IAdCondition` có sẵn hoặc đề xuất implement mới trong `Assets/_MainProject/`.
- Code mẫu ngắn: xử lý cả `onSuccess` lẫn `onFail`, không gọi thẳng SDK bên thứ ba, dùng hằng `AdPlacement`/`AnalyticsEvent` nếu project có.
- Nêu việc cần làm trong Editor (Setup ID, define, `SDK.prefab` ở scene đầu) nếu nhu cầu phụ thuộc cấu hình.
