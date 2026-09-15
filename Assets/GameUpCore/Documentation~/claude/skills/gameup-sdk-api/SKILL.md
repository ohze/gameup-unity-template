---
name: gameup-sdk-api
description: Tra cứu API GameUp SDK (com.ohze.gameup.sdk) — quảng cáo đa mediation (AdMob/MAX/LevelPlay: banner, interstitial, rewarded, app open, native), luật chặn ads (capping, level, remove ads), analytics (Firebase, AppsFlyer, AppMetrica, GameAnalytics), Firebase Remote Config, consent ATT/GDPR. Dùng trước khi viết bất kỳ code nào hiện quảng cáo, log event, đọc remote config, hoặc khi cần biết SDK có sẵn gì.
---

# GameUp SDK — tra API trước khi viết mới

## Bước 0 — project có SDK không, đọc ở đâu

1. Có `.claude/gameup-sdk/API_INDEX.md` → SDK đang cài. Không có → kiểm `Packages/manifest.json` / `Assets/GameUpSDK/`.
   Có cài mà thiếu index → nhờ người dùng chạy `GameUp → Project → Sync GameUp source for AI`. Không cài → nói rõ, **đừng** viết code gọi `AdsManager`.
2. **Đọc `API_INDEX.md` trước** — chữ ký sinh bằng reflection từ bản đang cài. README của repo có ví dụ cũ (overload không còn tồn tại); index và source thắng README.
3. Grep/Read với `path` tường minh: `.claude/gameup-sdk/src` (cài qua Git UPM) hoặc `Assets/GameUpSDK` (embedded). Không tìm trong `Library/PackageCache`.

Namespace `GameUp.SDK` · asmdef `GameUp.SDK.Runtime` (reference `GameUp.Core.Runtime`). Singleton dùng `MonoSingleton<T>` của Core → `.Instance`.

## SDK có gì

| Nhu cầu | Type | Ghi chú |
|---|---|---|
| Hiện/ẩn quảng cáo | `AdsManager` | `ShowBanner(where)`/`HideBanner(where)`, `ShowInterstitial(where, currentLevel, onSuccess, onFail)`, `ShowRewardedVideo(where[, currentLevel], onSuccess, onFail)`, `ShowAppOpenAds`, `ShowNativeAd`/`HideNativeAd`, `Is…Available(where)`, `LoadAd`, event `OnAdsInitialized` |
| Waterfall nhiều mediation | `AdsManager.Networks`, `IAdNetwork` (`AdmobNetwork`, `MaxNetwork`, `IronsourceNetwork`), `mediationPriority` trong `GameUpAdsConfig` | Không tự gọi network — `AdsManager` chọn provider có ad |
| Luật chặn ads | `IAdCondition` + `AdsManager.AddCondition`: `CappingTimeCondition`, `MinLevelCondition`, `RemoveAdCondition`, `CrossFormatCooldownCondition`, `HideBannerFromRemote`; `AdCappingManager` (`SetCappingLimit`, `IsCappingReady`, `ResetCapping`, `PauseAllCapping`) | Luật riêng của game → implement `IAdCondition` |
| Đã mua Remove Ads | `RemoveAdsSetting.Instance.IsRemoveAllAds` / `IsRemoveInter` (`BooleanVar` của Core) | `AdsManager` tự đọc: remove inter → interstitial trả `onSuccess` ngay; remove all → chặn cả app open, ẩn banner |
| Log event | `GameUpAnalytics` (static) | Level/wave (`LogLevelStart/Fail/Complete`, `LogWave…`), loading, currency (`LogEarn/SpendVirtualCurrency`), `LogPurchase`, `LogTutorialCompletion`, `LogAchievementUnlocked`, `LogButtonClick`, `LogFirebase(Params)` — gửi Firebase/AppsFlyer/GA/AppMetrica cùng lúc |
| Tên event/param | `AnalyticsEvent`, `AdsEvent`, `AppMetricaEvent` | Dùng hằng số, không gõ chuỗi tay |
| Ad revenue | tự động (`AdsTracker`, `AdsEvent.OnImpressionDataReady`) | Không gọi `LogAdImpression` tay trong luồng thường |
| Remote Config | `FirebaseRemoteConfigUtils.Instance` | Field public trùng key (`inter_capping_time`, `inter_start_level`, `enable_banner`…), `IsRemoteConfigReady`, `OnFetchCompleted`, `FetchAndActivate`. Key riêng của game → kế thừa và override `GetRemoteConfigTargets()`/`GetDefaultValues()` |
| Consent ATT + GDPR/UMP | `PrivacyManager.Instance` | `AdsManager` tự chạy `BeginPrivacyFlow` khi init; game chỉ cần nút Settings gọi `ShowPrivacyOptionsForm` khi `PrivacyOptionsRequired` |
| Callback SDK ở thread native | `MainThreadDispatcher` | Mọi thao tác Unity API trong callback SDK bên thứ ba phải đi qua đây |
| Cấu hình ID/key | `GameUpAdsConfig`, `GameUpSdkConfig` (ScriptableObject ở `Assets/_MainProject/Resources/GameUpSDK/`) | Sửa qua `GameUp → SDK → Setup`, không hardcode ID trong code |
| Tên placement | `AdPlacement` (sinh bởi Setup → `Assets/_MainProject/Scripts/SDK/AdPlacement.cs`) | Dùng hằng thay chuỗi `where` |
| Define theo dependency | `GUDefinetion` (`ADMOB_/MAXSDK_/LEVELPLAY_/FIREBASE_/APPSFLYER_/GAMEANALYTICS_/FACEBOOK_/APPMETRICA_DEPENDENCIES_INSTALLED`, `GAMEUP_SDK_DEPS_READY`) | Đồng bộ bằng `GameUp → SDK → Sync Define Symbols` |

## Mẫu chuẩn

```csharp
using GameUp.SDK;

// Interstitial cuối level — luôn xử lý cả hai nhánh để flow không kẹt
AdsManager.Instance.ShowInterstitial(AdPlacement.EndLevel, currentLevel,
    onSuccess: ContinueToNextLevel,
    onFail: ContinueToNextLevel);

// Rewarded — chỉ thưởng trong onSuccess
AdsManager.Instance.ShowRewardedVideo(AdPlacement.Revive, currentLevel,
    onSuccess: RevivePlayer,
    onFail: ShowNoAdToast);

// Luật riêng: không hiện inter trong tutorial
AdsManager.Instance.AddCondition(new MinLevelCondition(3, () => PlayerSave.Instance.level));

// Analytics
GameUpAnalytics.LogLevelComplete(level, index: 1, timeSeconds: elapsed, score: score);

// Remote config
var rc = FirebaseRemoteConfigUtils.Instance;
if (rc.IsRemoteConfigReady) ApplyBalance(rc.inter_start_level);
else rc.OnFetchCompleted += OnRemoteConfigFetched;
```

*`AdPlacement.*`, `PlayerSave`, các method game ở trên là ví dụ — xác nhận tên thật trong project và chữ ký trong `API_INDEX.md`.*

## Luật

- Code game **không** gọi thẳng `GoogleMobileAds`, `MaxSdk`, `LevelPlay`, `Firebase.Analytics`, `AppsFlyer` — đi qua bảng trên (đã lo main thread, capping, remove ads, tracking revenue).
- Code bắt buộc chạm SDK bên thứ ba phải bọc `#if <X>_DEPENDENCIES_INSTALLED`; không sửa define tay.
- `SDK.prefab` (DontDestroyOnLoad) chỉ có **một** ở scene load đầu (`GameUp → SDK → Setup → Tạo SDK trong Scene`).
- Không sửa code trong package SDK (read-only khi cài qua UPM) và `.claude/gameup-sdk/` (bản chép tự sinh) — mở rộng bằng `IAdCondition`, kế thừa `FirebaseRemoteConfigUtils`, hoặc code trong `Assets/_MainProject/`.
- Test trong Editor: mediation không trả ad thật; luôn có nhánh `onFail` để test flow.
