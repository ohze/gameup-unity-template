# GameUp IAP

`GameUpIAP` cung cấp lớp `MyIAPManager` để:
- Kết nối Unity IAP (IAP v5) và tải danh sách sản phẩm.
- Mua sản phẩm theo `productId`.
- Lấy giá localize để hiển thị UI.
- Gửi analytics cho init/mua hàng (có thể tắt bằng `enableAnalytics`).

---

## 1) Setup trên Scene (bắt buộc ở scene loading)

`MyIAPManager` phải được tạo ở **scene loading/scene đầu tiên** để manager khởi tạo sớm và sống xuyên scene (`DontDestroyOnLoad`).

### Cách tạo nhanh
1. Mở scene loading.
2. Chọn menu: `GameUp/IAP/Create MyIAPManager`.
3. Đảm bảo scene chỉ có **1** object `MyIAPManager`.

### Lưu ý quan trọng
- Không đặt `MyIAPManager` ở scene gameplay mở sau, vì lúc đó UI shop có thể gọi mua trước khi IAP sẵn sàng.
- Khi đã đặt ở scene loading, manager sẽ tự giữ lại qua các scene khác.

---

## 2) Khởi tạo đúng cách: tạo danh sách `_products` rồi mới Init

Yêu cầu bắt buộc: chuẩn bị `List<IAPProductDefinition> _products = new();` trước, sau đó mới gọi init.

```csharp
using System.Collections.Generic;
using GameUp.IAP;
using UnityEngine.Purchasing;

public class IapBootstrap
{
    private readonly List<IAPProductDefinition> _products = new();

    public void InitIap()
    {
        _products.Clear();
        _products.Add(new IAPProductDefinition("remove_ads", ProductType.NonConsumable, "1.99"));
        _products.Add(new IAPProductDefinition("coin_pack_1", ProductType.Consumable, "0.99"));
        _products.Add(new IAPProductDefinition("vip_monthly", ProductType.Subscription, "4.99"));

        MyIAPManager.Instance.Initialize(_products);
    }
}
```

### Luồng khuyến nghị
1. Tạo list product.
2. Gọi `Initialize(...)` hoặc `InitializeAsync(...)`.
3. Chỉ cho phép bấm mua khi `MyIAPManager.Instance.IsIAPInitialized == true`.

---

## 3) IAPProductDefinition bắt buộc khi tạo product

Trong `GameUpIAP`, mỗi product cần được mô tả bằng `IAPProductDefinition`.
Nói ngắn gọn: **muốn add product vào hệ thống IAP thì bắt buộc tạo `IAPProductDefinition`**.

### Thông tin bắt buộc của `IAPProductDefinition`
- `id`: mã product trên Store (Google Play / App Store), bắt buộc đúng tuyệt đối.
- `type`: loại product (`Consumable`, `NonConsumable`, `Subscription`).
- `localPackCost`: giá fallback để hiển thị khi chưa fetch được metadata từ store.

Nếu `id` rỗng hoặc null thì product đó sẽ bị bỏ qua khi `SetProducts(...)`.

### Các loại `ProductType`
- `ProductType.Consumable`: vật phẩm tiêu hao, mua lại nhiều lần (ví dụ coin pack, energy pack).
- `ProductType.NonConsumable`: mua 1 lần dùng vĩnh viễn (ví dụ remove ads, premium upgrade).
- `ProductType.Subscription`: gói thuê bao theo kỳ hạn (ví dụ monthly VIP).

### Ví dụ tạo product bắt buộc có `IAPProductDefinition`
```csharp
using System;
using GameUp.IAP;
using UnityEngine.Purchasing;

[Serializable]
public sealed class ShopPack
{
    public IAPProductDefinition iapProduct;
    public int valueReward;
}

// Ví dụ khởi tạo data
var removeAdsPack = new ShopPack
{
    iapProduct = new IAPProductDefinition("remove_ads", ProductType.NonConsumable, "1.99"),
    valueReward = 0
};

var coinPack = new ShopPack
{
    iapProduct = new IAPProductDefinition("coin_pack_1", ProductType.Consumable, "0.99"),
    valueReward = 1000
};
```

Sau đó đưa các `iapProduct` vào list để init:

```csharp
var products = new List<IAPProductDefinition>
{
    removeAdsPack.iapProduct,
    coinPack.iapProduct
};

MyIAPManager.Instance.Initialize(products);
```

---

## 4) API hiện có + ví dụ sử dụng

## Trạng thái

### `bool IsIAPInitialized`
Kiểm tra IAP đã sẵn sàng hay chưa.

```csharp
if (!MyIAPManager.Instance.IsIAPInitialized)
{
    return;
}
```

### `IReadOnlyList<IAPProductDefinition> Products`
Lấy danh sách product đã cấu hình.

```csharp
var configuredProducts = MyIAPManager.Instance.Products;
```

## Khởi tạo cấu hình product

### `void SetProducts(IEnumerable<IAPProductDefinition> externalProducts)`
Gán danh sách product đầy đủ (id + type + localPackCost).

```csharp
MyIAPManager.Instance.SetProducts(_products);
```

### `void SetProducts(IEnumerable<string> productIds, ProductType defaultType = ProductType.NonConsumable)`
Gán nhanh từ list id, dùng chung 1 `ProductType`.

```csharp
MyIAPManager.Instance.SetProducts(
    new[] { "remove_ads", "premium_upgrade" },
    ProductType.NonConsumable);
```

### `void Initialize(IEnumerable<IAPProductDefinition> externalProducts)`
Set products và chạy init (fire-and-forget).

```csharp
MyIAPManager.Instance.Initialize(_products);
```

### `Task<bool> InitializeAsync(IEnumerable<IAPProductDefinition> externalProducts)`
Set products và await kết quả init.

```csharp
var success = await MyIAPManager.Instance.InitializeAsync(_products);
```

### `Task<bool> InitializeAsync()`
Init bằng danh sách đã set trước đó.

```csharp
MyIAPManager.Instance.SetProducts(_products);
var success = await MyIAPManager.Instance.InitializeAsync();
```

## Mua hàng

### `void BuyProduct(string productId, Action<bool> onPurchaseComplete = null)`
Thực hiện mua theo `productId`, callback `true/false` theo kết quả.

```csharp
MyIAPManager.Instance.BuyProduct("remove_ads", success =>
{
    if (success)
    {
        // unlock remove ads
    }
    else
    {
        // show failed message
    }
});
```

## Giá hiển thị

### `string GetLocalizedPrice(string productId, string defaultPrice)`
Lấy giá localize, fallback về `defaultPrice` nếu chưa có metadata.

```csharp
var price = MyIAPManager.Instance.GetLocalizedPrice("coin_pack_1", "$0.99");
```

### `string GetLocalizedPrice(string productId)`
Lấy giá localize, fallback về `LocalPackCost` đã cấu hình trong `IAPProductDefinition`.

```csharp
var price = MyIAPManager.Instance.GetLocalizedPrice("coin_pack_1");
```

### `string GetMultipliedLocalizedPrice(string productId, string defaultPrice, int multiplier)`
Nhân giá localize theo số lượng (ví dụ gói x3).

```csharp
var bundlePrice = MyIAPManager.Instance.GetMultipliedLocalizedPrice("coin_pack_1", "$2.97", 3);
```

## Subscription

### `bool TryGetSubscriptionInfo(string productId, out SubscriptionInfo subscriptionInfo)`
Hiện tại API giữ lại để tương thích, chưa parse subscription info trong IAP v5 nên đang trả `false`.

```csharp
if (MyIAPManager.Instance.TryGetSubscriptionInfo("vip_monthly", out var info))
{
    // currently not expected with current implementation
}
```

---

## 5) Mẫu tích hợp nhanh cho UI Shop

```csharp
public async void OnOpenShop()
{
    if (!MyIAPManager.Instance.IsIAPInitialized)
    {
        var _products = new List<IAPProductDefinition>
        {
            new IAPProductDefinition("remove_ads", ProductType.NonConsumable, "1.99"),
            new IAPProductDefinition("coin_pack_1", ProductType.Consumable, "0.99")
        };

        MyIAPManager.Instance.SetProducts(_products);
        var ok = await MyIAPManager.Instance.InitializeAsync();
        if (!ok)
        {
            return;
        }
    }

    var removeAdsPrice = MyIAPManager.Instance.GetLocalizedPrice("remove_ads");
    // Bind removeAdsPrice lên UI
}
```

---

## 6) Checklist trước khi release

- Đặt `MyIAPManager` ở scene loading.
- Danh sách `_products` đầy đủ, id khớp với store.
- Mỗi product phải có `IAPProductDefinition` (id + type + localPackCost).
- Khởi tạo xong mới cho mua (`IsIAPInitialized`).
- Nút mua gọi đúng `productId`.
- UI giá dùng `GetLocalizedPrice(...)`.
