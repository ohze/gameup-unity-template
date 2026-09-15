---
name: gameup-iap-api
description: Tra cứu API GameUp IAP (com.ohze.gameup.iap) — Unity IAP v5 bọc sẵn trong MyIAPManager: khai báo sản phẩm, init, mua, giá localize, kiểm chữ ký receipt chống hack, analytics mua hàng. Dùng trước khi viết shop, gói Remove Ads, subscription, hoặc bất kỳ code mua hàng thật nào.
---

# GameUp IAP — tra API trước khi viết mới

## Bước 0 — project có IAP không, đọc ở đâu

1. Có `.claude/gameup-iap/API_INDEX.md` → IAP đang cài. Không có → kiểm `Packages/manifest.json` / `Assets/GameUpIAP/`.
   Có cài mà thiếu index → nhờ người dùng chạy `GameUp → Project → Sync GameUp source for AI`. Không cài → nói rõ, đừng tự dựng Unity IAP riêng khi chưa hỏi.
2. **Đọc `API_INDEX.md`** rồi `Doc/README.md` trong source (hướng dẫn đầy đủ + checklist release).
3. Grep/Read với `path` tường minh: `.claude/gameup-iap/src` (Git UPM) hoặc `Assets/GameUpIAP` (embedded).

Namespace `GameUp.IAP` · asmdef `GameUp.IAP.Runtime` (reference `GameUp.Core.Runtime`, `GameUp.SDK.Runtime`, `Unity.Purchasing`) → **cần GameUp SDK**. Code game dùng IAP phải thêm reference `GameUp.IAP.Runtime` + `Unity.Purchasing` vào asmdef của mình.

## IAP có gì

| Nhu cầu | API |
|---|---|
| Khai báo sản phẩm | `new IAPProductDefinition(id, ProductType.Consumable/NonConsumable/Subscription, localPackCost)` — `[Serializable]`, nhúng được vào SO/config shop |
| Khởi tạo | `MyIAPManager.Instance.Initialize(products)` hoặc `await InitializeAsync(products)`; `SetProducts(...)` rồi `InitializeAsync()` |
| Trạng thái | `IsIAPInitialized`, `Products` |
| Mua | `BuyProduct(productId, onPurchaseComplete: bool => …, level: int?)` |
| Giá hiển thị | `GetLocalizedPrice(productId)` (fallback `localPackCost`), `GetLocalizedPrice(productId, defaultPrice)`, `GetMultipliedLocalizedPrice(productId, defaultPrice, multiplier)` |
| Subscription | `TryGetSubscriptionInfo` — hiện luôn trả `false` trên IAP v5, đừng dựa vào |
| Chống hack receipt | `IAPReceiptValidator` + `ReceiptValidationResult`, bật sẵn bằng `enableReceiptValidation`; cần tangle sinh từ `Services → In-App Purchasing → Receipt Validation Obfuscator` (`IAPTangleProvider` đọc qua reflection) |
| Analytics mua hàng | tự gửi `iap_initialize` / `iap_purchase_start` / `iap_purchase_fail` + purchase qua `GameUpAnalytics` (tắt bằng `enableAnalytics`) |
| Tạo manager | menu `GameUp → IAP → Create MyIAPManager` |

## Mẫu chuẩn

```csharp
using System.Collections.Generic;
using GameUp.IAP;
using GameUp.SDK;
using UnityEngine.Purchasing;

private readonly List<IAPProductDefinition> _products = new()
{
    new IAPProductDefinition("remove_ads", ProductType.NonConsumable, "1.99"),
    new IAPProductDefinition("coin_pack_1", ProductType.Consumable, "0.99"),
};

private async void InitIap()
{
    var ok = await MyIAPManager.Instance.InitializeAsync(_products);
    btnBuyRemoveAds.interactable = ok;
    txtRemoveAdsPrice.text = MyIAPManager.Instance.GetLocalizedPrice("remove_ads");
}

private void BuyRemoveAds()
{
    MyIAPManager.Instance.BuyProduct("remove_ads", OnRemoveAdsPurchased);
}

private void OnRemoveAdsPurchased(bool success)
{
    if (!success) return;
    RemoveAdsSetting.Instance.IsRemoveAllAds.Value = true;   // AdsManager tự ẩn banner, chặn inter/app open
}
```

*Tên button/field là ví dụ — theo naming của project.*

## Luật

- `MyIAPManager` đặt ở **scene loading**, đúng một instance (tự `DontDestroyOnLoad`). Không đặt ở scene gameplay mở sau.
- Chỉ cấp hàng khi callback `BuyProduct` trả `true` — không cấp khi bấm nút, không cấp khi `IsIAPInitialized == false`.
- Không tự dựng `StoreController` / `UnityIAPServices` riêng song song với `MyIAPManager`.
- `testMode` chỉ dùng Editor/dev build (build release sẽ từ chối mua); không tắt `enableReceiptValidation` khi release.
- Product `id` phải khớp tuyệt đối với Google Play / App Store; UI giá luôn lấy từ `GetLocalizedPrice`, không hardcode.
- Không sửa code trong package IAP và `.claude/gameup-iap/` (bản chép tự sinh).
