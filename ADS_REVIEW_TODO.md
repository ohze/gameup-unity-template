# GameUpSDK — Ads Review & Fix Checklist

> Kết quả review `Assets/GameUpSDK/` ngày **2026-08-06**, tập trung vào AdMob + Unity.
> File tạm để theo dõi tiến độ xử lý. Tick `[x]` khi xong, ghi chú commit vào cột cuối.

**Tiến độ:** 9/25 · P0: **8/8 xong** · P1: 1/11 · P2: 0/6
**Đã sửa (chưa compile Unity, chưa test device):** toàn bộ P0 #1–#8, kèm #14 được giải quyết gián tiếp bởi #1.
**Tiếp theo:** Đợt 2 — P1 #9, #10, #17 (rủi ro tuân thủ / store).

---

## Thứ tự xử lý đề xuất

| Đợt | Nội dung | Lý do |
|---|---|---|
| 1 | P0 #1–#8 | Lỗi nhìn thấy được ở production, sửa gọn và độc lập nhau |
| 2 | P1 #9, #10, #17 | Rủi ro tuân thủ GDPR / bị store từ chối |
| 3 | P1 #12, #13, #14, #18 | Ảnh hưởng trực tiếp match rate và eCPM |
| 4 | P2 #20–#25 | Refactor, gộp chung với việc bỏ `MainThreadDispatcher` cho AdMob |

---

# P0 — Lỗi chắc chắn, sửa trước

## [x] 1. Collapsible banner → vòng lặp request vô hạn, banner không bao giờ hiện

> **ĐÃ SỬA.** `AdmobBannerAd.Show()` không còn nhánh "collapsible luôn load mới". Nay: có banner sẵn → hiện luôn; chưa có → phát đúng một request qua cổng `_pendingShow` (key theo `unitId`), cổng được mở lại trong callback load-failed. Cũng thêm `TryGetValue` cho `_banners` thay vì index trực tiếp.

**Vị trí:** `Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs:331-334`
kết hợp `Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/AdsManager.cs:190-200`

**Triệu chứng:** `AdmobBannerAd.Show()` với `CollapsiblePlacement != None` **luôn gọi `Load()`** thay vì show. Chuỗi khép kín:

```
Show(where) → Load(where) → RequestAdInternal → banner.LoadAd()
  → OnBannerAdLoaded → banner.Hide() + HandleLoadSuccess
  → OnAdLoaded → AdsManager.OnBannerLoaded (AdsManager.cs:190)
  → ShowBanner(where) → BannerAd.Show(where) → Show() → Load(where) → ...
```

Mỗi vòng destroy + tạo mới `BannerView`, bắn request AdMob liên tục không giới hạn. Banner bị `Hide()` ngay trong callback nên **không bao giờ hiển thị**. Ngoài burn quota, đây là mẫu hành vi AdMob dễ gắn cờ invalid traffic.

**Hướng sửa:** tách "load mới" khỏi "show"; chỉ load khi chưa có request đang chờ.

```csharp
private readonly HashSet<string> _pendingShow = new HashSet<string>();

public void Show(string where)
{
    MainThreadDispatcher.Enqueue(() =>
    {
        string unitId = _config.ResolveUnitId(_adType, where, EcpmFloor.All);
        if (_isLoaded.TryGetValue(unitId, out bool loaded) && loaded)
        {
            NotifyAdDisplayed(where);
            _banners[unitId].Show();
            _pendingShow.Remove(where);
        }
        else if (_pendingShow.Add(where))   // chỉ load nếu chưa có request đang chờ
        {
            Load(where);
        }
    });
}
```

Collapsible vẫn cần request mới, nhưng chỉ khi user **chủ động** yêu cầu banner (kèm cooldown), không phải mỗi lần callback loaded quay về. Xem thêm #14.

**Kiểm chứng sau khi sửa:** bật collapsible cho 1 placement, chạy device, xác nhận log `request_All` chỉ xuất hiện 1 lần và banner hiện được.

---

## [x] 2. `OnAdLoadFailed` sai thứ tự tham số — toàn bộ log lỗi load vô dụng

> **ĐÃ SỬA.** `BaseAdFormat.cs:87` → `OnAdLoadFailed?.Invoke(where, error);`

**Vị trí:** `Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/BaseAdFormat.cs:85`

```csharp
OnAdLoadFailed?.Invoke(unitId, where);   // đang truyền (unitId, where)
```

Consumer đọc là `(where, error)` — `Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/AdsTracker.cs:28,36`:

```csharp
network.InterstitialAd.OnAdLoadFailed += (where, error) => LogAdsEvent(AdsEvent.InterLoadFail, null, error);
```

**Hệ quả:** Firebase nhận `source = <tên placement>` thay vì mã lỗi. Biến `error` (đã có sẵn trong scope của `HandleLoadFailed`) bị vứt bỏ hoàn toàn → không chẩn đoán được no-fill / lỗi adapter.

**Sửa:** `OnAdLoadFailed?.Invoke(where, error);`

---

## [x] 3. Ad fullscreen không bao giờ `Destroy()` — rò native object mỗi impression

> **ĐÃ SỬA.** Cả 3 format (Interstitial / Rewarded / AppOpen) thêm local function `Release()` có cờ `released` chống double-destroy, gọi trong cả `OnAdFullScreenContentClosed` lẫn `OnAdFullScreenContentFailed`. Destroy chạy trong `MainThreadDispatcher` nên không nằm trong stack của native callback.

**Vị trí:** `AdmobAdFormat.cs:63` (Interstitial), `:141` (Rewarded), `:229` (AppOpen)

Khi show, ad bị `_ads.Remove(unitId)` nên tham chiếu biến mất; `oldAd.Destroy()` trong `RequestAdInternal` không bao giờ chạm tới nó nữa. Google yêu cầu gọi `Destroy()` sau khi ad đóng.

**Sửa:** gọi `ad.Destroy()` trong `OnAdFullScreenContentClosed` và `OnAdFullScreenContentFailed`, trước khi `LoadByFloor(...)`.

---

## [x] 4. `HideNativeAd()` ném NullReferenceException

> **ĐÃ SỬA.** Thêm `?.` cho `NativeFullScreenAd`.

**Vị trí:** `Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/AdsManager.cs:536-542`

```csharp
network.Value.NativeFullScreenAd.Hide();   // thiếu null-check
```

Trước khi network init xong, mọi property format đều `null`. Gọi `HideNativeAd()` sớm (hoặc network init fail) là crash. Mọi hàm `LoadAd` khác đều dùng `?.` — chỗ này bị sót.

**Sửa:** `network.Value.NativeFullScreenAd?.Hide();`

---

## [x] 5. `ShowInterstitial` thoát sớm mà không gọi `onFail` → treo game

> **ĐÃ SỬA.** Nhánh `IsAnyAdShowing` nay log + gọi `onFail` như các nhánh return khác.

**Vị trí:** `AdsManager.cs:405`

```csharp
if (AdCappingManager.Instance.IsAnyAdShowing) return;   // onFail không được gọi
```

Game gọi `ShowInterstitial(..., onFail: ContinueLevel)` sẽ treo vĩnh viễn ở màn chờ. Mọi nhánh return khác trong hàm đều gọi `onFail`.

**Sửa:** `onFail?.Invoke(); return;`

---

## [x] 6. MAX: `OnAdRevenuePaidEvent` đăng ký mỗi lần load, không gỡ → doanh thu bị thổi phồng

> **ĐÃ SỬA.** `MaxInterstitialAd` / `MaxRewardedAd` đăng ký `OnRevenuePaid` **một lần** trong constructor. Để giữ nhãn `Interstitial_{floor}` khi không còn closure theo từng lần load, `BaseAdFormat` ghi `_floorByUnitId` trong `LoadByFloor` và expose `protected EcpmFloor FloorOf(string unitId)`.
>
> **Còn tồn:** `MaxAppOpenAd` và `MaxBannerAd` **không** đăng ký revenue → doanh thu 2 format này của MAX hiện không được track. Đây là lỗ hổng riêng, chưa xử lý trong đợt này.

**Vị trí:** `Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs:38` (và bản Rewarded tương ứng)

`Unsubscribe()` chỉ gỡ `OnAdLoadedEvent` + `OnAdLoadFailedEvent`; `onRevenue` tích lũy sau mỗi lần load. Sau N lần load, một impression bắn N sự kiện revenue → số liệu Firebase/AppsFlyer sai theo cấp số.

**Sửa:** đăng ký `OnAdRevenuePaidEvent` **một lần** trong constructor, hoặc thêm nó vào `Unsubscribe()`.

---

## [x] 7. Retry đóng băng khi `Time.timeScale = 0`

> **ĐÃ SỬA.** `TimerHelper.IESchedule` dùng `WaitForSecondsRealtime`.

**Vị trí:** `Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/TimerHelper.cs:17`

```csharp
yield return new WaitForSeconds(time);   // chạy theo scaled time
```

Hầu hết game set `timeScale = 0` khi pause hoặc khi ads đang hiển thị. Retry backoff đứng im; `_isLoadingByUnitId` đã reset `false` nhưng không ai load lại → ad không bao giờ được nạp cho tới khi game unpause.

**Sửa:** `WaitForSecondsRealtime`.

---

## [x] 8. `mediationPriority` trùng lặp → ArgumentException lúc Awake, chết cả SDK

> **ĐÃ SỬA.** Thêm `SanitizeMediationPriority()` chạy ngay sau `ApplyConfig()`: loại entry `None` và entry trùng, log warning khi có dọn. `_networkDict.Add` đổi sang `TryAdd` làm lớp chặn thứ hai. Lợi ích kèm theo: các vòng lặp khác trên `mediationPriority` (`GetAvailableProvider`, `TemporarilyHideBanners`, `RestoreBanners`, `InitializeAll`) không còn xử lý lặp cùng một network.

**Vị trí:** `AdsManager.cs:79`

```csharp
_networkDict.Add(provider, network);   // ném nếu provider trùng
```

`mediationPriority` là `List<MediationProvider>` do người dùng sửa trong Inspector; một entry trùng là đủ để chết ngay `Awake`.

**Sửa:** dùng `TryAdd`, hoặc dedupe list trong `ApplyConfig()`.

---

# P1 — Thiết lập ad request & tuân thủ chính sách

## [ ] 9. Consent bị từ chối nhưng vẫn request ads (rủi ro GDPR)

**Vị trí:** `AdsManager.cs:114-119` + `AdmobNetwork.cs:100`

`SetConsent(grantConsent)` rồi `InitializeAll()` chạy **bất kể** `grantConsent`. Với AdMob, `SetConsent` là hàm rỗng → khi UMP trả `CanRequestAds() == false`, SDK vẫn init và bắn request bình thường. Theo UMP, `CanRequestAds() == false` nghĩa là **không được phép request**.

```csharp
PrivacyManager.Instance.BeginPrivacyFlow(grantConsent =>
{
    SetConsent(grantConsent);
    if (!grantConsent)
    {
        GULogger.Warning("GameUp", "UMP: CanRequestAds=false — bỏ qua init ads.");
        return;   // hoặc chỉ init các network không cần consent
    }
    InitializeAll();
    NativeAdConfigBridge.SetGlobalCtaClickRate(nativeCtaClickRate);
});
```

---

## [ ] 10. Thiếu privacy options entry point (bắt buộc cho EEA)

**Vị trí:** `Assets/GameUpSDK/Scripts/Runtime/Ads/PrivacyManager.cs`

Không expose `ConsentInformation.PrivacyOptionsRequirementStatus`, không có API mở lại form. Google yêu cầu app phải có nút "Quản lý tuỳ chọn quyền riêng tư" khi status là `Required`.

```csharp
public bool PrivacyOptionsRequired =>
    ConsentInformation.PrivacyOptionsRequirementStatus == PrivacyOptionsRequirementStatus.Required;

public void ShowPrivacyOptionsForm(Action<string> onError) =>
    ConsentForm.ShowPrivacyOptionsForm(err => onError?.Invoke(err?.Message));
```

---

## [ ] 11. Thiếu `MobileAds.RaiseAdEventsOnUnityMainThread = true`

**Vị trí:** `AdmobNetwork.cs:36-97`; `Assets/GameUpSDK/Scripts/Runtime/MainThreadDispatcher.cs`

Plugin Google Mobile Ads có cờ này để mọi callback được raise trên Unity main thread. Code hiện tự bù bằng `MainThreadDispatcher` rải rác, mà `ProcessQueue()` **chỉ được drain trong `AdsManager.Update()`** (`AdsManager.cs:143-146`) — `AdsManager` bị disable là mọi callback kẹt trong queue vĩnh viễn. Cách làm cũng không nhất quán: `AdmobNetwork` lại dùng `MobileAdsEventExecutor.ExecuteInUpdate`.

**Sửa:** set `MobileAds.RaiseAdEventsOnUnityMainThread = true;` **trước** `MobileAds.Initialize()`, rồi bỏ các lớp `MainThreadDispatcher.Enqueue` bọc callback AdMob.

---

## [ ] 12. `NotifyAdDisplayed` gọi trước `ad.Show()` → impression & capping sai

**Vị trí:** `AdmobAdFormat.cs:62` (Inter), `:140` (Rewarded), `:228` (AppOpen)

Nếu present thất bại, hệ thống đã log impression + `PauseAllCapping()` rồi mới nhận `OnAdFullScreenContentFailed` → vừa có `AdsShowSuccess` vừa có `AdsShowFail` cho cùng một lần, capping bị reset sai.

**Sửa:** dùng đúng callback của plugin:

```csharp
ad.OnAdFullScreenContentOpened += () => NotifyAdDisplayed(where);
ad.OnAdImpressionRecorded      += () => /* impression thật */;
```

---

## [ ] 13. Waterfall 3 tầng load **song song** — hại match rate / eCPM

**Vị trí:** `BaseAdFormat.cs:38-44`, `AdmobNetwork.cs:78-83`

`Load()` bắn đồng thời High + Medium + All; `LoadAll()` lúc init nhân với số placement → burst request rất lớn khi khởi động. 3 ad fill nhưng chỉ 1 được show, 2 cái còn lại hết hạn (interstitial ~1h). AdMob theo dõi **match rate** và **show rate**; tỉ lệ thấp kéo eCPM và ưu tiên phân phối xuống.

**Sửa:** waterfall **tuần tự** — request High, no-fill mới xuống Medium, rồi All.

```csharp
public virtual void Load(string where = null)
{
    var floors = _config.GetActiveFloors();
    LoadSequential(where, floors, 0);
}
// trong HandleLoadFailed: còn floor kế tiếp thì thử ngay floor đó;
// hết floor mới áp dụng exponential backoff.
```

Đồng thời `LoadAll()` lúc init chỉ nên preload placement `default` (+ format cần ngay); placement khác load lazy khi lần đầu được yêu cầu.

---

## [x] 14. `RestoreBanners()` sau mỗi lần đóng fullscreen → request collapsible mới

> **ĐÃ SỬA gián tiếp bởi #1.** `Restore(where) => Show(where)`, mà `Show()` nay chỉ gọi `banner.Show()` trên view đang có thay vì load lại. Cần xác nhận lại khi test device.

**Vị trí:** `AdsManager.cs:218-233` → `AdmobAdFormat.cs:356` (`Restore(where) => Show(where)`)

Mỗi interstitial/rewarded đóng lại sinh một request collapsible banner mới.

**Sửa:** thêm cooldown, hoặc `Restore()` chỉ gọi `banner.Show()` trên view đang có thay vì load lại. Làm cùng #1.

---

## [ ] 15. `RequestConfiguration` thiếu field compliance

**Vị trí:** `AdmobNetwork.cs:50-51` — chỉ set `TestDeviceIds`.

Thiếu:
- `TagForChildDirectedTreatment` — bắt buộc nếu app hướng trẻ em (COPPA)
- `TagForUnderAgeOfConsent` — GDPR
- `MaxAdContentRating` — nhiều nhà mạng dùng để lọc creative

**Sửa:** đưa 3 field vào `AdmobAdsSettings` (`Config/GameUpAdsConfig.cs`) và set trong `Initialize()`.

---

## [ ] 16. UMP không có `ConsentDebugSettings` → không test được consent form

**Vị trí:** `PrivacyManager.cs:109` — `new ConsentRequestParameters()` trần, không giả lập được geography EEA.

```csharp
var request = new ConsentRequestParameters
{
    ConsentDebugSettings = new ConsentDebugSettings
    {
        DebugGeography = DebugGeography.EEA,
        TestDeviceHashedIds = settings.testDevices
    }
};
```
(chỉ bật ở build debug)

---

## [ ] 17. Chuỗi `NSUserTrackingUsageDescription` hỏng encoding — rủi ro bị Apple từ chối

**Vị trí:** `Assets/GameUpSDK/Editor/GameUpPostProcess.cs:14`

Hex dump xác nhận chuỗi chứa ký tự thay thế U+FFFD (`ef bf bd`):

> `Dữ li<?>?u này giúp hi<?>fn th<?>< quảng cáo phù hợp hơn v<?>>i bạn.`

Chuỗi này đi thẳng vào `Info.plist` và **hiện trong hộp thoại ATT mà người dùng nhìn thấy**.

**Sửa:** gõ lại chuỗi bằng UTF-8 sạch; tốt nhất đưa vào `GameUpSdkConfig` để mỗi project tự chỉnh.

---

## [ ] 18. Logic chặn AppOpen nằm ở code mẫu, không nằm trong SDK

**Vị trí:** `Assets/GameUpSDK/Scripts/Runtime/Ads/Example.cs:43-53` vs `AdsManager.cs:434`

`Example.cs` là nơi **duy nhất** chặn AOA khi có ad khác đang hiển thị. `AdsManager.ShowAppOpenAds` không tự kiểm tra `IsAnyAdShowing` (khác với `ShowInterstitial`). Project tự viết `OnApplicationPause` sẽ show AOA đè lên interstitial khi user quay lại từ một cú click ads — vi phạm chính sách AdMob.

**Sửa:** đưa guard `IsAnyAdShowing` vào `ShowAppOpenAds`, kèm chặn AOA ở lần cold start đầu tiên.

---

## [ ] 19. Nhóm sửa nhỏ (gom chung 1 commit)

| Vấn đề | Vị trí |
|---|---|
| `DateTime.Now` cho expire AppOpen → sai khi đổi timezone/DST; dùng `UtcNow` | `AdmobAdFormat.cs:211`, `:190`, `:226` |
| `GULogger.Error` dùng cho log info lúc init | `AdmobNetwork.cs:49,55` |
| `GULogger.Error` trong `IsCappingReady` — spam error log mỗi lần check điều kiện | `AdsRules/AdCappingManager.cs:45` |
| Collapsible chỉ hợp lệ với anchored adaptive banner; không validate khi `bannerSize = MediumRectangle` | `AdmobAdFormat.cs:313-317` |
| Retry backoff không giới hạn số lần — no-fill kéo dài retry mãi (64s/lần) | `BaseAdFormat.cs:84` |
| `PushAdmobAppIdsToGoogleSettings` không gọi `AssetDatabase.SaveAssets()` | `Editor/Setup/GameUpAdsConfigAsset.cs:182` |

---

# P2 — Thiết kế

## [ ] 20. Format object chỉ tồn tại sau khi init xong → mất lệnh gọi ở vài giây đầu

**Vị trí:** `AdmobNetwork.cs:71-76` (và tương tự ở `MaxNetwork`, `IronsourceNetwork`)

`InterstitialAd`, `BannerAd`… được `new` bên trong callback `MobileAds.Initialize`. Trong khoảng 1–3 giây đầu, mọi lệnh gọi từ game trả về null/false lặng lẽ và **bị mất luôn** — không có hàng đợi. Đây là nguyên nhân chính khiến banner/AOA "thỉnh thoảng không lên" ở màn hình đầu.

**Sửa:** tạo format object ngay trong `Awake` (chúng chỉ giữ config, chưa gọi SDK), cho `Load` xếp hàng tới khi init xong.

## [ ] 21. `AdsManager.IsInitialized` = true khi **một** network bất kỳ init xong

**Vị trí:** `AdsManager.cs:178` — tên gây hiểu nhầm là "toàn bộ đã sẵn sàng".

## [ ] 22. Không gỡ event → nhân bản handler khi tắt domain reload

**Vị trí:** `AdmobAdFormat.cs:441-445` (5 event của `FullScreenNativeAdManager`, không bao giờ gỡ); `AdsManager.OnDestroy` cũng không gỡ handler đã gắn vào network.

Với Enter Play Mode Options (tắt domain reload) hoặc scene reload, các handler này tích lũy.

## [ ] 23. `AdmobNativeBannerBridge` dùng static state cho callback iOS

**Vị trí:** `Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobNativeBannerBridge.cs:160-200`

`_instance` + `_currentActiveUnitId` chỉ đúng khi mỗi lần chỉ có một unit đang xử lý. Hai placement native banner load song song sẽ ghi đè state của nhau.

**Sửa:** truyền `adUnitId` qua bridge iOS như phía Android đã làm.

## [ ] 24. `IronSourceNetwork` không xử lý init thất bại

**Vị trí:** `Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceNetwork.cs:43`

Chỉ đăng ký `LevelPlay.OnInitSuccess`; không có `OnInitFailed` → init hỏng thì im lặng, không retry, không log.

## [ ] 25. `MainThreadDispatcher.ProcessQueue` cấp phát `List` mới mỗi frame có việc

**Vị trí:** `MainThreadDispatcher.cs:27-35` — dùng double-buffer swap thay vì `new List<Action>(Pending)`. Sẽ tự hết nếu làm #11.

---

## Ghi chú kiểm chứng

Sau mỗi đợt, test trên **device thật** (không phải Editor — phần lớn code AdMob nằm trong `#if !UNITY_EDITOR`):

- [ ] Bật `showMediationInspector` → Ad Inspector xác nhận adapter init OK, không có "no fill" bất thường
- [ ] Lọc logcat theo `[GameUp]` → đếm số dòng `request_All` cho 1 placement (phải là 1, không phải vòng lặp)
- [ ] Test collapsible banner riêng — đây là đường dễ hỏng nhất
- [ ] Test luồng UMP với `DebugGeography.EEA` sau khi làm #16
- [ ] Kiểm tra `Info.plist` sau build iOS: `NSUserTrackingUsageDescription` phải đọc được tiếng Việt
