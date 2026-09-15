---
description: Tra API GameUp IAP (mua hàng, giá, receipt) cho một nhu cầu trước khi viết code shop
argument-hint: [nhu cầu, vd: "gói remove ads", "shop coin 3 gói", "hiện giá localize"]
---

Dùng skill `gameup-iap-api` để trả lời: **$ARGUMENTS**

Yêu cầu:
- Xác nhận project có cài GameUp IAP (`.claude/gameup-iap/API_INDEX.md` hoặc `Assets/GameUpIAP`). Không cài → nói rõ và dừng.
- Mở `API_INDEX.md` và `Doc/README.md` trong source (`.claude/gameup-iap/src` khi cài qua Git UPM, `Assets/GameUpIAP` khi embedded); **đọc file nguồn** lấy chữ ký thật.
- Code mẫu: khai báo `IAPProductDefinition`, init trước khi cho mua, cấp hàng **chỉ** trong callback `success == true`, giá lấy từ `GetLocalizedPrice`.
- Gói Remove Ads → nối với `RemoveAdsSetting` của GameUp SDK.
- Nhắc checklist Editor: `MyIAPManager` ở scene loading, tangle cho receipt validation, product id khớp store, asmdef reference `GameUp.IAP.Runtime` + `Unity.Purchasing`.
