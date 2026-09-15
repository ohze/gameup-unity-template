# GameUp IAP — API index (tự sinh)

> **Không sửa tay.** Sinh bởi `GameUp → Project → Sync GameUp source for AI` từ assembly thật của `com.ohze.gameup.iap` `2.0.0`; lần sync sau sẽ ghi đè.

- Source đọc được: `Assets/GameUpIAP/` — cột **File** bên dưới là đường dẫn tương đối so với thư mục này.
- Đường dẫn trong Unity (dùng cho asmdef/AssetDatabase): `Assets/GameUpIAP/`.
- Chữ ký lấy bằng reflection → khớp bản đang cài. Hành vi, comment, ví dụ → mở file nguồn.
- Chỉ liệt kê member `public`/`protected` và field `[SerializeField]`. `[Obsolete]` = đừng dùng cho code mới.

## Assembly (asmdef cần reference)

| asmdef | Namespace |
|---|---|
| `GameUp.IAP.Runtime` | `GameUp.IAP` |

## Class nền để kế thừa

Kế thừa những class này thay vì tự viết lại. **abstract** = bắt buộc override; **virtual** = hook tuỳ chọn (nhớ gọi `base.` nếu class nền có logic).

| Type | Namespace | abstract | virtual | File |
|---|---|---|---|---|

## Runtime

### `public sealed class IAPProductDefinition`

`GameUp.IAP` · [Runtime/MyIAPManager.cs](Assets/GameUpIAP/Runtime/MyIAPManager.cs)

- `[SerializeField] private string id`
- `[SerializeField] private ProductType type`
- `[SerializeField] private string localPackCost`
- `public string Id { get; }`
- `public ProductType Type { get; }`
- `public string LocalPackCost { get; }`
- `public IAPProductDefinition(string productId, ProductType productType = ProductType.NonConsumable, string productLocalPackCost = "0.99")`

### `public sealed class IAPReceiptValidator`

`GameUp.IAP` · [Runtime/IAPReceiptValidator.cs](Assets/GameUpIAP/Runtime/IAPReceiptValidator.cs)

> Bọc `CrossPlatformValidator` của Unity IAP để kiểm tra chữ ký receipt ngay trên máy trước khi cấp hàng. Chặn được các bản hack billing kiểu Lucky Patcher / fake store vì chúng không ký được receipt bằng khoá của store.

- `public bool IsAvailable { get; }`
- `public void Initialize()`
- `public ReceiptValidationResult Validate(string receipt, out IPurchaseReceipt[] parsedReceipts)`
- `public void LogParsedReceipts(IPurchaseReceipt[] parsedReceipts)`

### `public static class IAPTangleProvider`

`GameUp.IAP` · [Runtime/IAPTangleProvider.cs](Assets/GameUpIAP/Runtime/IAPTangleProvider.cs)

> Lấy tangle data (public key Google Play / root certificate Apple) do cửa sổ Services → In-App Purchasing → Receipt Validation Obfuscator sinh ra. File sinh ra nằm ở Assets/Scripts/UnityPurchasing/generated nên thuộc Assembly-CSharp; asmdef GameUp.IAP.Runtime không tham chiếu trực tiếp được, vì vậy phải đọc qua reflection.

- `public static byte[] GetGooglePlayPublicKey()`
- `public static byte[] GetAppleRootCertificate()`

### `public class MyIAPManager : MonoSingleton<MyIAPManager>`

`GameUp.IAP` · [Runtime/MyIAPManager.cs](Assets/GameUpIAP/Runtime/MyIAPManager.cs)

- `[SerializeField] private bool testMode`
- `[SerializeField] private bool enableAnalytics`
- `[SerializeField] private bool enableReceiptValidation`
- `[SerializeField] private bool blockPurchaseWhenValidatorUnavailable`
- `public bool IsIAPInitialized { get; }`
- `public IReadOnlyList<IAPProductDefinition> Products { get; }`
- `protected override void Awake()`
- `public void Initialize(IEnumerable<IAPProductDefinition> externalProducts)`
- `public Task<bool> InitializeAsync(IEnumerable<IAPProductDefinition> externalProducts)`
- `public void SetProducts(IEnumerable<IAPProductDefinition> externalProducts)`
- `public void SetProducts(IEnumerable<string> productIds, ProductType defaultType = ProductType.NonConsumable)`
- `public Task<bool> InitializeAsync()`
- `public void BuyProduct(string productId, Action<bool> onPurchaseComplete = null, int? level = null)`
- `public string GetLocalizedPrice(string productId, string defaultPrice)`
- `public string GetLocalizedPrice(string productId)`
- `public string GetMultipliedLocalizedPrice(string productId, string defaultPrice, int multiplier)`
- `public bool TryGetSubscriptionInfo(string productId, out SubscriptionInfo subscriptionInfo)`

### `public enum ReceiptValidationResult`

`GameUp.IAP` · [Runtime/ReceiptValidationResult.cs](Assets/GameUpIAP/Runtime/ReceiptValidationResult.cs)

> Kết quả kiểm tra receipt cục bộ của một order.

- `Valid, Invalid, Unsupported, ValidatorUnavailable`

