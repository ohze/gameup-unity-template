# GameUp SDK — API index (tự sinh)

> **Không sửa tay.** Sinh bởi `GameUp → Project → Sync GameUp source for AI` từ assembly thật của `com.ohze.gameup.sdk` `2.0.0`; lần sync sau sẽ ghi đè.

- Source đọc được: `Assets/GameUpSDK/` — cột **File** bên dưới là đường dẫn tương đối so với thư mục này.
- Đường dẫn trong Unity (dùng cho asmdef/AssetDatabase): `Assets/GameUpSDK/`.
- Chữ ký lấy bằng reflection → khớp bản đang cài. Hành vi, comment, ví dụ → mở file nguồn.
- Chỉ liệt kê member `public`/`protected` và field `[SerializeField]`. `[Obsolete]` = đừng dùng cho code mới.

## Assembly (asmdef cần reference)

| asmdef | Namespace |
|---|---|
| `GameUp.SDK.Runtime` | `GameUp.Core`, `GameUp.SDK` |

## Class nền để kế thừa

Kế thừa những class này thay vì tự viết lại. **abstract** = bắt buộc override; **virtual** = hook tuỳ chọn (nhớ gọi `base.` nếu class nền có logic).

| Type | Namespace | abstract | virtual | File |
|---|---|---|---|---|
| `AdmobAppOpenAd` | `GameUp.SDK` | — | `IsAvailable()`, `RequestAdInternal()`, `Load()` | [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs) |
| `AdmobBannerAd` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs) |
| `AdmobInterstitialAd` | `GameUp.SDK` | — | `IsAvailable()`, `RequestAdInternal()`, `Load()` | [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs) |
| `AdmobNativeBannerBridge` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Admob/AdmobNativeBannerBridge.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobNativeBannerBridge.cs) |
| `AdmobNativeFullscreenAd` | `GameUp.SDK` | — | `IsAvailable()`, `RequestAdInternal()`, `Load()` | [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs) |
| `AdmobRewardedAd` | `GameUp.SDK` | — | `IsAvailable()`, `RequestAdInternal()`, `Load()` | [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs) |
| `BaseAdFormat` | `GameUp.SDK` | `IsAvailable()`, `RequestAdInternal()` | `Load()` | [Scripts/Runtime/Ads/Refactor/Base/BaseAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/BaseAdFormat.cs) |
| `DummyAppOpenAd` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Base/DummyAppOpenAd.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/DummyAppOpenAd.cs) |
| `DummyNativeFullscreenAd` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Base/DummyNativeFullscreenAd.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/DummyNativeFullscreenAd.cs) |
| `FirebaseRemoteConfigUtils` | `GameUp.SDK` | — | `GetDefaultValues()`, `GetRemoteConfigTargets()`, `ShouldIncludeFieldAsDefault()`, `BuildDefaultsFromTargets()` | [Scripts/Runtime/Firebase/FirebaseRemoteConfigUtils.cs](Assets/GameUpSDK/Scripts/Runtime/Firebase/FirebaseRemoteConfigUtils.cs) |
| `IronSourceBannerAd` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs) |
| `IronSourceInterstitialAd` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs) |
| `IronSourceRewardedAd` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs) |
| `MaxAppOpenAd` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs) |
| `MaxBannerAd` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs) |
| `MaxInterstitialAd` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs) |
| `MaxRewardedAd` | `GameUp.SDK` | — | `Load()`, `IsAvailable()`, `RequestAdInternal()` | [Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs) |

## Prefab có sẵn trong package

Không chép sang thư mục source (file YAML lớn). Prefab trong package cài qua UPM là read-only — dùng tool setup của package (hoặc Prefab Variant trong `_MainProject`), không sửa bản gốc.

- `Assets/GameUpSDK/Prefab/AdmobAds.prefab`
- `Assets/GameUpSDK/Prefab/AppmetricaObject.prefab`
- `Assets/GameUpSDK/Prefab/AppsFlyerObject.prefab`
- `Assets/GameUpSDK/Prefab/IronSourceAds.prefab`
- `Assets/GameUpSDK/Prefab/MaxAds.prefab`
- `Assets/GameUpSDK/Prefab/SDK.prefab`

## Scripts/Runtime

### `public class GUDefinetion`

`GameUp.SDK` · [Scripts/Runtime/GUDefinetion.cs](Assets/GameUpSDK/Scripts/Runtime/GUDefinetion.cs)

- `public const string DepsReadyDefine`
- `public const string FirebaseDepsInstalled`
- `public const string AppsFlyerDepsInstalled`
- `public const string GameAnalyticsDepsInstalled`
- `public const string AdMobDepsInstalled`
- `public const string LevelPlayDepsInstalled`
- `public const string FacebookDepsInstalled`
- `public const string MaxDepsInstalled`
- `public const string AppMetricaDepsInstalled`
- `public const string PrimaryMediationLevelPlay`
- `public const string PrimaryMediationAdMob`
- `public const string PrimaryMediationMax`

### `public static class MainThreadDispatcher`

`GameUp.SDK` · [Scripts/Runtime/MainThreadDispatcher.cs](Assets/GameUpSDK/Scripts/Runtime/MainThreadDispatcher.cs)

> Ensures SDK callbacks that may run off the main thread are invoked on the Unity main thread.

- `public static void Enqueue(Action action)`
- `public static void ProcessQueue()`

## Scripts/Runtime/Ads

### `public class RemoveAdsSetting : Singleton<RemoveAdsSetting>`

`GameUp.Core` · [Scripts/Runtime/Ads/RemoveAdsSetting.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/RemoveAdsSetting.cs)

- `public const string RemoveInter`
- `public const string RemoveAllAds`
- `public readonly BooleanVar IsRemoveInter`
- `public readonly BooleanVar IsRemoveAllAds`

### `public static class AdHistoryTracker`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdHistoryTracker.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdHistoryTracker.cs)

- `public static void MarkAdClosed(AdUnitType adType)`
- `public static float GetTimeSinceLastClosed(AdUnitType adType)`

### `public class AdImpressionData`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdsEvent.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdsEvent.cs)

> DTO for ad impression data (ARM). Used when forwarding IronSource/LevelPlay impression data to GameUpAnalytics.

- `public string AdNetwork { get; set; }`
- `public string AdUnit { get; set; }`
- `public string InstanceName { get; set; }`
- `public string AdFormat { get; set; }`
- `public double? Revenue { get; set; }`

### `public static class AdsEvent`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdsEvent.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdsEvent.cs)

> Centralized Firebase event names and parameter names for ads logging. PascalCase for identifiers, snake_case for values.

- `public const string InterStartLoad`
- `public const string InterCompleteLoad`
- `public const string InterLoadFail`
- `public const string InterShow`
- `public const string InterShowComplete`
- `public const string RewardStartLoad`
- `public const string RewardCompleteLoad`
- `public const string RewardLoadFail`
- `public const string RewardShow`
- `public const string RewardShowComplete`
- `public const string AdsRequest`
- `public const string AdsAvailable`
- `public const string AdsShowSuccess`
- `public const string AdsShowFail`
- `public const string AfInterShow`
- `public const string AfInterDisplayed`
- `public const string AfRewardShow`
- `public const string AfRewardDisplayed`
- `public const string ParamWhere`
- `public const string ParamLevel`
- `public const string ParamSource`
- `public const string ParamAdType`
- `public const string ParamPlacement`
- `public const string ParamAfLevel`
- `public const string AdTypeBanner`
- `public const string AdTypeInterstitial`
- `public const string AdTypeRewardedVideo`
- `public const string AdTypeAppOpen`
- `public const string AdTypeNativeAd`
- `public static event Action<AdImpressionData> OnImpressionDataReady`
- `public static event Action<string, string> OnBannerSwap`

### `public class AdsExample : MonoBehaviour`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdsExample.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdsExample.cs)

### `public class Example : MonoBehaviour`

`GameUp.SDK` · [Scripts/Runtime/Ads/Example.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Example.cs)

### `public class PrivacyManager : MonoSingleton<PrivacyManager>`

`GameUp.SDK` · [Scripts/Runtime/Ads/PrivacyManager.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/PrivacyManager.cs)

> Luồng privacy: ATT (iOS 14.5+) rồi tới UMP consent form. ATT và GDPR là hai thứ độc lập — từ chối ATT KHÔNG miễn nghĩa vụ hiện form GDPR, nên UMP luôn được chạy bất kể kết quả ATT.

- `public bool IsCompleted { get; }`
- `public bool CanRequestAds { get; }`
- `public bool TrackingAllowed { get; }`
- `[Obsolete] public bool ConsentGranted { get; }`
- `public PrivacyResult Result { get; }`
- `public bool PrivacyOptionsRequired { get; }`
- `protected override void Awake()`
- `public void BeginPrivacyFlow(Action<PrivacyResult> onCompleted = null)`
- `public void ShowPrivacyOptionsForm(Action<string> onError = null)`
- `public void ResetConsent()`

### `public struct PrivacyResult`

`GameUp.SDK` · [Scripts/Runtime/Ads/PrivacyManager.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/PrivacyManager.cs)

> Kết quả luồng privacy. Hai tín hiệu này ĐỘC LẬP với nhau, đừng gộp làm một: một bên quyết định có được bắn request hay không, bên kia chỉ ảnh hưởng chất lượng ad.

- `public readonly bool CanRequestAds`
- `public readonly bool TrackingAllowed`
- `public PrivacyResult(bool canRequestAds, bool trackingAllowed)`

### `public class ShowAdByKey : IAdCondition`

`GameUp.SDK` · [Scripts/Runtime/Ads/Example.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Example.cs)

- `public ShowAdByKey(AdUnitType adType, Func<bool> funcCheckHideAd)`
- `public bool CanShow(AdUnitType adType, string where, out string reason)`
- `public string GetString()`

## Scripts/Runtime/Ads/AdsRules

### `public class AdCappingManager : MonoSingleton<AdCappingManager>`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdsRules/AdCappingManager.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdsRules/AdCappingManager.cs)

- `[SerializeField] private float defaultCappingTime`
- `public bool IsAnyAdShowing { get; }`
- `protected override void Awake()`
- `public void SetCappingLimit(AdUnitType groupId, float limit, float seconds)`
- `public bool IsCappingReady(AdUnitType groupId = AdUnitType.Interstitial)`
- `public void ResetCapping(AdUnitType adUnitType)`
- `public void PauseAllCapping()`
- `public void ResumeAllCapping()`

### `public class CappingTimeCondition : IAdCondition`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdsRules/CappingTimeCondition.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdsRules/CappingTimeCondition.cs)

- `public CappingTimeCondition(AdUnitType adUnitType = AdUnitType.Interstitial, float cappingTime = 60f, float startCounter = 60f)`
- `public bool CanShow(AdUnitType adType, string where, out string reason)`
- `public string GetString()`

### `public class CrossFormatCooldownCondition : IAdCondition`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdsRules/CappingTimeCondition.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdsRules/CappingTimeCondition.cs)

- `public CrossFormatCooldownCondition(AdUnitType targetAdType, AdUnitType triggerAdType, float cooldownSeconds)`
- `public bool CanShow(AdUnitType adType, string where, out string reason)`
- `public string GetString()`

### `public class HideBannerFromRemote : IAdCondition`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdsRules/CappingTimeCondition.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdsRules/CappingTimeCondition.cs)

- `public HideBannerFromRemote(Func<bool> getHideBannerFunc)`
- `public bool CanShow(AdUnitType adType, string where, out string reason)`
- `public string GetString()`

### `public interface IAdCondition`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdsRules/IAdCondition.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdsRules/IAdCondition.cs)

- `bool CanShow(AdUnitType adType, string where, out string reason)`
- `string GetString()`

### `public class MinLevelCondition : IAdCondition`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdsRules/CappingTimeCondition.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdsRules/CappingTimeCondition.cs)

- `public MinLevelCondition(int minLevelRequired, Func<int> getCurrentLevelFunc)`
- `public bool CanShow(AdUnitType adType, string where, out string reason)`
- `public string GetString()`

### `public class RemoveAdCondition : IAdCondition`

`GameUp.SDK` · [Scripts/Runtime/Ads/AdsRules/CappingTimeCondition.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/AdsRules/CappingTimeCondition.cs)

- `public RemoveAdCondition(List<AdUnitType> removeAdUnits, Func<bool> getRemoveAdFunc)`
- `public bool CanShow(AdUnitType adType, string where, out string reason)`
- `public string GetString()`

## Scripts/Runtime/Ads/Refactor

### `public class AdsManager : MonoSingleton<AdsManager>`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/AdsManager.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/AdsManager.cs)

- `[SerializeField] private GameUpAdsConfig configOverride`
- `public List<MediationProvider> mediationPriority`
- `[SerializeField] private int nativeCtaClickRate`
- `public static Action<string> OnBannerLoadedEvent`
- `public bool IsInitialized { get; }`
- `public bool AreAllNetworksInitialized { get; }`
- `public Dictionary<MediationProvider, IAdNetwork> Networks { get; }`
- `public event Action OnAdsInitialized`
- `protected override void Awake()`
- `public void ExportLegacyInto(GameUpAdsConfig target)`
- `public void RetryInitializeAfterConsent()`
- `public void SetConsent(bool isConsent)`
- `public void UpdateNativeCtaClickRate(float clickRate)`
- `public void AddCondition(IAdCondition condition)`
- `public void RefreshBannerVisibility()`
- `public IAdNetwork GetAvailableProvider(AdUnitType adType, string where)`
- `public bool IsRewardedVideoAvailable(string where = null)`
- `public void ShowRewardedVideo(string where, Action onSuccess = null, Action onFail = null)`
- `public void ShowRewardedVideo(string where, int currentLevel, Action onSuccess = null, Action onFail = null)`
- `public bool IsInterstitialAvailable(string where = null)`
- `public void ShowInterstitial(string where, int currentLevel, Action onSuccess = null, Action onFail = null)`
- `public bool IsAppOpenAdAvailable(string where = "default")`
- `public void ShowAppOpenAds(string where = "default", Action onSuccess = null, Action onFail = null)`
- `public bool IsBannerAvailable(string where = null)`
- `public void ShowBanner(string where)`
- `public void HideBanner(string where)`
- `public bool IsNativeAdAvailable(string where = null)`
- `public void ShowNativeAd(string where = "default", Action onSuccess = null, Action onFail = null)`
- `public void HideNativeAd(string where = "default")`
- `public void LoadAd(AdUnitType adType, string where = null)`

### `public class AdsTracker : MonoBehaviour`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/AdsTracker.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/AdsTracker.cs)

- `public void RegisterPlacementLevel(string where, int level)`
- `public void SubscribeToNetwork(IAdNetwork network)`
- `public void LogAdsEventManager(string eventName, string adType, string placement, string failReason = null)`

### `public enum BannerFormatType`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/AdsManager.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/AdsManager.cs)

- `StandardBanner, NativeOverlay`

### `public enum BannerSize`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/AdsManager.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/AdsManager.cs)

- `Banner, Large, Adaptive, MediumRectangle, Leaderboard`

### `public static class NativeAdConfigBridge`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/NativeAdConfigBridge.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/NativeAdConfigBridge.cs)

> Cầu nối cấu hình Native Ads xuống lớp native (Android/iOS). Hiện dùng để set tỉ lệ biến toàn bộ vùng quảng cáo thành CTA (overlay trap).

- `public static void SetGlobalCtaClickRate(int ratePercent)`

### `public class TimerHelper : MonoSingleton<TimerHelper>`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/TimerHelper.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/TimerHelper.cs)

- `public static void Schedule(float time, Action callback)`

## Scripts/Runtime/Ads/Refactor/Admob

### `public class AdmobAppOpenAd : BaseAdFormat, IAppOpenAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs)

- `public AdmobAppOpenAd(AdUnitConfig config)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where, Action onSuccess, Action onFail)`

### `public class AdmobBannerAd : BaseAdFormat, IBannerAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs)

- `public AdmobBannerAd(AdUnitConfig config)`
- `public override void Load(string where = null)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where)`
- `public void Hide(string where)`
- `public void Restore(string where)`

### `public class AdmobBannerDispatcher : IAdFormat, IBannerAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs)

- `public event Action<string> OnAdLoaded`
- `public event Action<string, string> OnAdLoadFailed`
- `public event Action<string> OnAdDisplayed`
- `public event Action<string, string> OnAdDisplayFailed`
- `public event Action<string> OnAdClosed`
- `public AdmobBannerDispatcher(AdUnitConfig config)`
- `public bool IsAvailable(string where = null)`
- `public void Load(string where = "default")`
- `public void LoadAll()`
- `public void Show(string where = "default")`
- `public void Hide(string where = "default")`
- `public void Restore(string where)`

### `public class AdmobInterstitialAd : BaseAdFormat, IInterstitialAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs)

- `public AdmobInterstitialAd(AdUnitConfig config)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where, Action onSuccess, Action onFail)`

### `public class AdmobNativeBannerBridge : BaseAdFormat, IBannerAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Admob/AdmobNativeBannerBridge.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobNativeBannerBridge.cs)

- `public event Action<string> OnCollapsedNativeBanner`
- `public AdmobNativeBannerBridge(AdUnitConfig config)`
- `public override void Load(string where = null)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where)`
- `public void Hide(string where)`
- `public void Restore(string where)`

### `public class AdmobNativeFullscreenAd : BaseAdFormat, INativeFullScreenAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs)

- `public AdmobNativeFullscreenAd(AdUnitConfig config)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where, Action onSuccess, Action onFail)`
- `public void Hide()`

### `public class AdmobNetwork : MonoBehaviour, IAdNetwork`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Admob/AdmobNetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobNetwork.cs)

- `[SerializeField] private GameUpAdsConfig configOverride`
- `[SerializeField] private List<string> testDevices`
- `[SerializeField] private bool showMediationInspector`
- `[SerializeField] private AdUnitConfig interstitialConfig`
- `[SerializeField] private AdUnitConfig rewardedConfig`
- `[SerializeField] private AdUnitConfig appOpenConfig`
- `[SerializeField] private AdUnitConfig bannerConfig`
- `[SerializeField] private AdUnitConfig nativeAdConfig`
- `public bool IsInitialized { get; }`
- `public Action<IAdNetwork> OnInitialized { get; set; }`
- `public MediationProvider MediationProvider { get; set; }`
- `public IInterstitialAd InterstitialAd { get; }`
- `public IRewardedAd RewardedAd { get; }`
- `public IAppOpenAd AppOpenAd { get; }`
- `public IBannerAd BannerAd { get; }`
- `public INativeFullScreenAd NativeFullScreenAd { get; }`
- `public AdmobAdsSettings Settings { get; }`
- `public void Initialize()`
- `public void SetConsent(bool isConsent)`
- `public AdmobAdsSettings ExportLegacySettings()`

### `public class AdmobRewardedAd : BaseAdFormat, IRewardedAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/AdmobAdFormat.cs)

- `public AdmobRewardedAd(AdUnitConfig config)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where, Action onSuccess, Action onFail)`

### `public class FullScreenNativeAdManager : MonoSingleton<FullScreenNativeAdManager>`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Admob/FullScreenNativeAdManager.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/FullScreenNativeAdManager.cs)

- `public event Action<string> OnAdLoadedEvent`
- `public event Action<string, string> OnAdLoadFailedEvent`
- `public event Action<string, string> OnAdClosedEvent`
- `public event Action<string, string> OnAdDisplayedEvent`
- `public event Action<string, string, double> OnAdPaidEvent`
- `public event Action<string, string> OnAdLogEvent`
- `protected override void Awake()`
- `public void RequestAd(string adUnit)`
- `public bool IsAdReady(string adUnit)`
- `public void ShowFullScreenAd(string adUnit, string where)`
- `public void ForceCloseAd()`

### `public class RuntimeCollapsibleUI : MonoBehaviour`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Admob/RuntimeCollapsibleUI.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Admob/RuntimeCollapsibleUI.cs)

> Tự động sinh giao diện Nút Gập/Mở Native Ad bằng code lúc Runtime. Thiết kế: Nền đen bao quanh, thanh ngang trên đỉnh có Nút bấm vuông góc phải. Font LegacyRuntime.

- `public static RuntimeCollapsibleUI Create(Action onToggle)`
- `public void SetVisible(bool isVisible)`

## Scripts/Runtime/Ads/Refactor/Base

### `public class AdUnitConfig`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/AdUnitConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/AdUnitConfig.cs)

- `public bool enableWaterfallFloor`
- `public bool useMultiAdUnitIds`
- `public string defaultIdAndroid_High`
- `public string defaultIdAndroid_Medium`
- `public string defaultIdAndroid_All`
- `public string defaultIdIOS_High`
- `public string defaultIdIOS_Medium`
- `public string defaultIdIOS_All`
- `public BannerSize defaultBannerSize`
- `public BannerFormatType defaultBannerFormat`
- `public CollapsibleBannerPlacement defaultCollapsible`
- `public List<AdPlacementIds> placementsAndroid`
- `public List<AdPlacementIds> placementsIOS`
- `public List<AdUnitIdEntry> multiIdsAndroid`
- `public List<AdUnitIdEntry> multiIdsIOS`
- `public bool HasLegacyData { get; }`
- `public EcpmFloor[] GetActiveFloors()`
- `public List<AdPlacementIds> GetPlacements()`
- `public List<AdPlacementIds> GetPlacements(bool android)`
- `public AdPlacementIds FindPlacement(string where)`
- `public AdUnitIdEntry GetEntry(AdUnitType type, string where)`
- `public AdUnitIdEntry GetEntry(AdUnitType type, string where, EcpmFloor floor)`
- `public string WhereByKey(AdUnitType type, string key)`
- `public List<string> GetAllPlacements()`
- `public List<string> GetAllWhere()`
- `public string ResolveUnitId(AdUnitType type, string where, EcpmFloor floor = EcpmFloor.All)`
- `public AdUnitConfig CloneMigrated()`
- `public bool MigrateLegacyEntries()`

### `public class AdUnitIdEntry`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/AdUnitIdEntry.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/AdUnitIdEntry.cs)

- `public AdUnitType AdType`
- `public string NameId`
- `public string Id`
- `public int intId`
- `public BannerFormatType BannerFormat`
- `public BannerSize BannerSize`
- `public CollapsibleBannerPlacement CollapsiblePlacement`
- `public EcpmFloor Floor`
- `public bool IsValid()`

### `public enum AdUnitType`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/AdUnitIdEntry.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/AdUnitIdEntry.cs)

- `Banner, Interstitial, RewardedVideo, AppOpen, NativeAd`

### `public abstract class BaseAdFormat : IAdFormat`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/BaseAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/BaseAdFormat.cs)

- `protected readonly AdUnitConfig _config`
- `protected readonly AdUnitType _adType`
- `protected readonly string _networkName`
- `protected const int LoadRetryExponentCap`
- `public event Action<string> OnAdLoaded`
- `public event Action<string, string> OnAdLoadFailed`
- `public event Action<string> OnAdDisplayed`
- `public event Action<string, string> OnAdDisplayFailed`
- `public event Action<string> OnAdClosed`
- `protected BaseAdFormat(AdUnitConfig config, AdUnitType adType, string networkName)`
- `protected void NotifyAdDisplayed(string where)`
- `protected void NotifyAdDisplayFailed(string where, string error)`
- `protected void NotifyAdClosed(string where)`
- `public virtual void Load(string where = null)`
- `public void LoadByFloor(string where, EcpmFloor floor)`
- `public void LoadAll()`
- `public abstract bool IsAvailable(string where = null)`
- `protected abstract void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `protected void HandleLoadFailed(string unitId, string where, EcpmFloor floor, string error)`
- `protected void HandleLoadSuccess(string unitId, string where)`
- `protected void LogTrace(string phase, string unitId, string where, string extra = null)`
- `protected void TrackRevenue(string adUnitId, string placement, string adFormat, double revenue)`
- `protected string WhereByKey(string key)`
- `protected EcpmFloor FloorOf(string unitId)`

### `public enum CollapsibleBannerPlacement`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/AdUnitIdEntry.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/AdUnitIdEntry.cs)

- `None, Top, Bottom`

### `public class DummyAppOpenAd : BaseAdFormat, IAppOpenAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/DummyAppOpenAd.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/DummyAppOpenAd.cs)

- `public DummyAppOpenAd(AdUnitConfig config, AdUnitType adType, string networkName)`
- `public override void Load(string where = null)`
- `public override bool IsAvailable(string where = null)`
- `public void Show(string where, Action onSuccess, Action onFail)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor epmFloor)`

### `public class DummyNativeFullscreenAd : BaseAdFormat, INativeFullScreenAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/DummyNativeFullscreenAd.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/DummyNativeFullscreenAd.cs)

- `public override void Load(string where = null)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where, Action onSuccess, Action onFail)`
- `public void Hide()`

### `public enum EcpmFloor`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/AdUnitIdEntry.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/AdUnitIdEntry.cs)

- `High, Medium, All`

### `public interface IAdFormat`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/INetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/INetwork.cs)

- `event Action<string> OnAdLoaded`
- `event Action<string, string> OnAdLoadFailed`
- `event Action<string> OnAdDisplayed`
- `event Action<string, string> OnAdDisplayFailed`
- `event Action<string> OnAdClosed`
- `void Load(string where = null)`
- `void LoadAll()`
- `bool IsAvailable(string where = null)`

### `public interface IAdNetwork`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/INetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/INetwork.cs)

- `Action<IAdNetwork> OnInitialized { get; set; }`
- `MediationProvider MediationProvider { get; set; }`
- `IInterstitialAd InterstitialAd { get; }`
- `IRewardedAd RewardedAd { get; }`
- `IAppOpenAd AppOpenAd { get; }`
- `IBannerAd BannerAd { get; }`
- `INativeFullScreenAd NativeFullScreenAd { get; }`
- `bool IsInitialized { get; }`
- `void Initialize()`
- `void SetConsent(bool isConsent)`

### `public interface IAppOpenAd : IAdFormat`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/INetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/INetwork.cs)

- `void Show(string where, Action onSuccess, Action onFail)`

### `public interface IBannerAd : IAdFormat`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/INetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/INetwork.cs)

- `void Show(string where)`
- `void Hide(string where)`
- `void Restore(string where)`

### `public interface IInterstitialAd : IAdFormat`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/INetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/INetwork.cs)

- `void Show(string where, Action onSuccess, Action onFail)`

### `public interface INativeFullScreenAd : IAdFormat`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/INetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/INetwork.cs)

- `void Show(string where, Action onSuccess, Action onFail)`
- `void Hide()`

### `public interface IRewardedAd : IAdFormat`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/INetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/INetwork.cs)

- `void Show(string where, Action onSuccess, Action onFail)`

### `public enum MediationProvider`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Base/INetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Base/INetwork.cs)

- `None, Admob, Max, IronSource`

## Scripts/Runtime/Ads/Refactor/Ironsource

### `public class IronSourceBannerAd : BaseAdFormat, IBannerAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs)

- `public IronSourceBannerAd(AdUnitConfig config)`
- `public override void Load(string where = null)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where)`
- `public void Hide(string where)`
- `public void Restore(string where)`

### `public class IronSourceInterstitialAd : BaseAdFormat, IInterstitialAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs)

- `public IronSourceInterstitialAd(AdUnitConfig config)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where, Action onSuccess, Action onFail)`

### `public class IronSourceNetwork : MonoBehaviour, IAdNetwork`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceNetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceNetwork.cs)

- `[SerializeField] private GameUpAdsConfig configOverride`
- `[SerializeField] private string levelPlayAppKey`
- `[SerializeField] private AdUnitConfig interstitialConfig`
- `[SerializeField] private AdUnitConfig rewardedConfig`
- `[SerializeField] private AdUnitConfig bannerConfig`
- `public Action<IAdNetwork> OnInitialized { get; set; }`
- `public MediationProvider MediationProvider { get; set; }`
- `public bool IsInitialized { get; }`
- `public IInterstitialAd InterstitialAd { get; }`
- `public IRewardedAd RewardedAd { get; }`
- `public IAppOpenAd AppOpenAd { get; }`
- `public IBannerAd BannerAd { get; }`
- `public INativeFullScreenAd NativeFullScreenAd { get; }`
- `public IronSourceAdsSettings Settings { get; }`
- `public void Initialize()`
- `public void SetConsent(bool isConsent)`
- `public IronSourceAdsSettings ExportLegacySettings()`

### `public class IronSourceRewardedAd : BaseAdFormat, IRewardedAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Ironsource/IronsourceAdFormat.cs)

- `public IronSourceRewardedAd(AdUnitConfig config)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where, Action onSuccess, Action onFail)`

## Scripts/Runtime/Ads/Refactor/Max

### `public class MaxAppOpenAd : BaseAdFormat, IAppOpenAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs)

- `public MaxAppOpenAd(AdUnitConfig config)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where, Action onSuccess, Action onFail)`

### `public class MaxBannerAd : BaseAdFormat, IBannerAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs)

- `public MaxBannerAd(AdUnitConfig config)`
- `public override void Load(string where = null)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where)`
- `public void Hide(string where)`
- `public void Restore(string where)`

### `public class MaxInterstitialAd : BaseAdFormat, IInterstitialAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs)

- `public MaxInterstitialAd(AdUnitConfig config)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where, Action onSuccess, Action onFail)`

### `public class MaxNetwork : MonoBehaviour, IAdNetwork`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Max/MaxNetwork.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Max/MaxNetwork.cs)

- `[SerializeField] private GameUpAdsConfig configOverride`
- `[SerializeField] private string sdkKey`
- `[SerializeField] private bool showMediationDebugger`
- `[SerializeField] private AdUnitConfig rewardedConfig`
- `[SerializeField] private AdUnitConfig interstitialConfig`
- `[SerializeField] private AdUnitConfig bannerConfig`
- `[SerializeField] private AdUnitConfig appOpenAdConfig`
- `[SerializeField] private AdUnitConfig nativeAdConfig`
- `public Action<IAdNetwork> OnInitialized { get; set; }`
- `public MediationProvider MediationProvider { get; set; }`
- `public bool IsInitialized { get; }`
- `public IRewardedAd RewardedAd { get; }`
- `public IInterstitialAd InterstitialAd { get; }`
- `public IBannerAd BannerAd { get; }`
- `public IAppOpenAd AppOpenAd { get; }`
- `public INativeFullScreenAd NativeFullScreenAd { get; }`
- `public MaxAdsSettings Settings { get; }`
- `public void Initialize()`
- `public void SetConsent(bool isConsent)`
- `public MaxAdsSettings ExportLegacySettings()`

### `public class MaxRewardedAd : BaseAdFormat, IRewardedAd`

`GameUp.SDK` · [Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs](Assets/GameUpSDK/Scripts/Runtime/Ads/Refactor/Max/MaxAdFormat.cs)

- `public MaxRewardedAd(AdUnitConfig config)`
- `public override bool IsAvailable(string where = null)`
- `protected override void RequestAdInternal(string unitId, string where, EcpmFloor floor)`
- `public void Show(string where, Action onSuccess, Action onFail)`

## Scripts/Runtime/Analytics

### `public static class AnalyticsEvent`

`GameUp.SDK` · [Scripts/Runtime/Analytics/AnalyticsEvent.cs](Assets/GameUpSDK/Scripts/Runtime/Analytics/AnalyticsEvent.cs)

> Event names and parameter names for game analytics: Firebase, AppsFlyer (MMP); GameAnalytics progression (level/wave) map trong `GameUpAnalytics`. Ad-related events are handled elsewhere.

- `public const string EarnVirtualCurrency`
- `public const string SpendVirtualCurrency`
- `public const string StartLoading`
- `public const string CompleteLoading`
- `public const string LevelStart`
- `public const string LevelFail`
- `public const string LevelComplete`
- `public const string ButtonClick`
- `public const string WaveStart`
- `public const string WaveFail`
- `public const string WaveComplete`
- `public const string StartLevel1`
- `public const string CompleteLevel1`
- `public const string AfCompleteRegistration`
- `public const string AfLevelAchieved`
- `public const string AfPurchase`
- `public const string AfTutorialCompletion`
- `public const string AfAchievementUnlocked`
- `public const string ParamVirtualCurrencyName`
- `public const string ParamValue`
- `public const string ParamAmount`
- `public const string ParamItemName`
- `public const string ParamSource`
- `public const string ParamLevel`
- `public const string ParamIndex`
- `public const string ParamTime`
- `public const string ParamWave`
- `public const string ParamAfLevel`
- `public const string ParamAfScore`
- `public const string ParamAfRegistrationMethod`
- `public const string ParamAfCustomerUserId`
- `public const string ParamAfCurrencyCode`
- `public const string ParamAfQuantity`
- `public const string ParamAfContentId`
- `public const string ParamAfPurchasePrice`
- `public const string ParamAfOrderId`
- `public const string ParamAfSuccess`
- `public const string ParamAfTutorialId`
- `public const string ParamContentId`

### `public class AppMetricaActivator : MonoSingleton<AppMetricaActivator>`

`GameUp.SDK` · [Scripts/Runtime/Analytics/AppMetricaActivator.cs](Assets/GameUpSDK/Scripts/Runtime/Analytics/AppMetricaActivator.cs)

- `[SerializeField] private GameUpSdkConfig configOverride`
- `[SerializeField] private string apiKey`
- `[SerializeField] private bool enableLogs`
- `[SerializeField] private bool enableEventLogging`
- `public static bool EnableEventLogging { get; }`
- `public static bool IsUtilsDebugLogEnabled { get; }`
- `public AppMetricaSettings ExportLegacySettings()`

### `public static class AppMetricaEvent`

`GameUp.SDK` · [Scripts/Runtime/Analytics/AppMetricaEvent.cs](Assets/GameUpSDK/Scripts/Runtime/Analytics/AppMetricaEvent.cs)

> Event names và params gửi lên AppMetrica (Product Analytics / IDLE spec).

- `public const string LevelStart`
- `public const string LevelFinish`
- `public const string VideoAdsAvailable`
- `public const string VideoAdsStarted`
- `public const string VideoAdsWatch`
- `public const string AfAdRevenue`
- `public const string AfPurchase`
- `public const string ParamPlacement`
- `public const string ParamAdType`
- `public const string ParamResult`
- `public const string ParamConnection`
- `public const string AdTypeRewarded`
- `public const string AdTypeInterstitial`
- `public const string ResultSuccess`
- `public const string ResultNotAvailable`
- `public const string ResultStart`
- `public const string ResultFailed`
- `public const string ResultWatched`
- `public const string ResultCanceled`
- `public const string ParamAfRevenue`
- `public const string ParamAfCurrency`
- `public const string ParamMonetizationNetwork`
- `public const string ParamAdUnit`

### `public static class AppMetricaUtils`

`GameUp.SDK` · [Scripts/Runtime/Analytics/AppMetricaUtils.cs](Assets/GameUpSDK/Scripts/Runtime/Analytics/AppMetricaUtils.cs)

> Gửi custom events / ad revenue / IAP revenue lên AppMetrica. Bật/tắt qua `AppMetricaActivator.EnableEventLogging` (GameUp SDK Setup). Debug xác nhận gửi: bật `AppMetricaActivator.IsUtilsDebugLogEnabled` (SDK debug logs).

- `public static bool IsEventLoggingEnabled { get; }`
- `public static void LogEvent(string eventName, Dictionary<string, string> parameters = null)`
- `public static void LogAdRevenue(AdImpressionData data)`
- `public static void LogPurchaseRevenue(string currencyCode, int quantity, string contentId, string purchasePrice, string orderId, int? level = null)`
- `public static void LogAfPurchaseEvent(Dictionary<string, string> afParams)`
- `public static void SendEventsBuffer()`

### `public static class FacebookSdkBootstrap`

`GameUp.SDK` · [Scripts/Runtime/Analytics/FacebookSdkBootstrap.cs](Assets/GameUpSDK/Scripts/Runtime/Analytics/FacebookSdkBootstrap.cs)

> Gá»i FB.Init sá»›m trÃªn Android/iOS Ä‘á»ƒ Facebook SDK báº­t vÃ gá»­i app events (ká»ƒ cáº£ App Launch khi cáº¥u hÃ¬nh Android Ä‘Ãºng: Bundle ID, key hash, class name trÃªn Meta Developer Console; FacebookSettings trong Unity). Auto log app events láº¥y tá»« FacebookSettings (máº·c Ä‘á»‹nh báº­t). Chá»‰ compile khi FACEBOOK_DEPENDENCIES_INSTALLED.

- `public static bool IsInitialized { get; }`
- `public static void TryInitialize()`

### `public enum GaProgressionStatus`

`GameUp.SDK` · [Scripts/Runtime/Analytics/GaProgressionStatus.cs](Assets/GameUpSDK/Scripts/Runtime/Analytics/GaProgressionStatus.cs)

> Tráº¡ng thÃ¡i progression gá»­i lÃªn GameAnalytics; giÃ¡ trá»‹ sá»‘ khá»›p GameAnalyticsSDK.GAProgressionStatus (Start=1, Complete=2, Fail=3).

- `Start, Complete, Fail`

### `public static class GameAnalyticsSdkBootstrap`

`GameUp.SDK` · [Scripts/Runtime/Analytics/GameAnalyticsSdkBootstrap.cs](Assets/GameUpSDK/Scripts/Runtime/Analytics/GameAnalyticsSdkBootstrap.cs)

> Gá»i GameAnalytics.Initialize() theo bÆ°á»›c 2.5 tÃ i liá»‡u GA Unity â€” SDK khÃ´ng tá»± init trong Awake. DÃ¹ng `RuntimeInitializeLoadType.AfterSceneLoad` Ä‘á»ƒ cháº¡y sau Awake cá»§a GameObject GameAnalytics (náº¿u cÃ³ trong scene). Váº«n cáº§n Resources/GameAnalytics/Settings (game key / secret) vÃ nÃªn cÃ³ má»™t GameObject GameAnalytics (menu Window â†’ GameAnalytics).

- `public static void TryInitialize()`

### `public static class GameUpAnalytics`

`GameUp.SDK` · [Scripts/Runtime/Analytics/GameUpAnalytics.cs](Assets/GameUpSDK/Scripts/Runtime/Analytics/GameUpAnalytics.cs)

> Game analytics: Firebase, AppsFlyer (MMP), AppMetrica (tùy chọn), GameAnalytics progression (Start / Complete / Fail) theo GA Unity — Progression events (world main → level → wave). Cần init GameAnalytics + keys trong scene.

- `public static void LogFirebase(string eventName, string paramName = null, string paramValue = null)`
- `public static void LogFirebaseParams(string eventName, Dictionary<string, string> param)`
- `public static void LogVideoAdsAvailable(string adType, string placement, string result, bool hasConnection)`
- `public static void LogVideoAdsStarted(string adType, string placement, string result, bool hasConnection)`
- `public static void LogVideoAdsWatch(string adType, string placement, string result, bool hasConnection)`
- `public static void LogStartLevel1()`
- `public static void LogCompleteLevel1()`
- `public static void LogEarnVirtualCurrency(string virtualCurrencyName, double value, string source)`
- `public static void LogEarnVirtualCurrency(string virtualCurrencyName, string value, string source)`
- `public static void LogSpendVirtualCurrency(string virtualCurrencyName, double value, string source, string itemName = null)`
- `public static void LogSpendVirtualCurrency(string virtualCurrencyName, string value, string source, string itemName = null)`
- `public static void LogStartLoading()`
- `public static void LogCompleteLoading()`
- `public static void LogLevelStart(int level, int index)`
- `public static void LogLevelFail(int level, int index, float timeSeconds)`
- `public static void LogLevelComplete(int level, int index, float timeSeconds, int? score = null)`
- `public static void LogButtonClick(string source)`
- `public static void LogWaveStart(int level, int wave)`
- `public static void LogWaveFail(int level, int wave)`
- `public static void LogWaveComplete(int level, int wave)`
- `public static void LogCompleteRegistration(string registrationMethod)`
- `public static void LogPurchase(string currencyCode, int quantity, string contentId, string purchasePrice, string orderId, string registrationMethod = null, string customerUserId = null, int? level = null)`
- `public static void SetCustomerUserId(string userId)`
- `public static void LogTutorialCompletion(bool success, string tutorialId = null)`
- `public static void LogAchievementUnlocked(string contentId, int? level = null)`
- `public static void LogAdImpression(AdImpressionData data)`

## Scripts/Runtime/AppsFlyerCheck

### `public class AppsFlyerUtils : MonoSingleton<AppsFlyerUtils>, IAppsFlyerPurchaseRevenueDataSource, IAppsFlyerPurchaseRevenueDataSourceStoreKit2, IAppsFlyerPurchaseValidation`

`GameUp.SDK` · [Scripts/Runtime/AppsFlyerCheck/AppsFlyerUtils.cs](Assets/GameUpSDK/Scripts/Runtime/AppsFlyerCheck/AppsFlyerUtils.cs)

> Gá»i event / ad revenue AppsFlyer. SDK Ä‘Æ°á»£c khá»Ÿi táº¡o bá»Ÿi AppsFlyerObject (AppsFlyerObjectScript) â€” devKey vÃ appID cáº¥u hÃ¬nh trÃªn object Ä‘Ã³.

- `[SerializeField] private GameUpSdkConfig configOverride`
- `protected override void Awake()`
- `public static bool ShouldSkipManualPurchaseRevenueEvent()`
- `public static void SetCustomerUserId(string userId)`
- `public static void LogAdRevenue(AFAdRevenueData adRevenueData, Dictionary<string, string> additionalParameters = null)`
- `public static void LogAdRevenue(string monetizationNetwork, MediationNetwork mediationNetwork, double eventRevenue, string revenueCurrency, Dictionary<string, string> additionalParameters = null)`
- `public static void LogEvents(string eventName, Dictionary<string, string> eventValues = null)`
- `public void didReceivePurchaseRevenueValidationInfo(string validationInfo)`
- `public void didReceivePurchaseRevenueError(string error)`
- `public Dictionary<string, object> PurchaseRevenueAdditionalParametersForProducts(HashSet<object> products, HashSet<object> transactions)`
- `public Dictionary<string, object> PurchaseRevenueAdditionalParametersStoreKit2ForProducts(HashSet<object> products, HashSet<object> transactions)`

## Scripts/Runtime/Config

### `public enum AdContentRating`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpAdsConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpAdsConfig.cs)

> Bản sao của MaxAdContentRating để config vẫn compile khi chưa cài AdMob.

- `Unspecified, G, PG, T, MA`

### `public class AdPlacementIds`

`GameUp.SDK` · [Scripts/Runtime/Config/AdPlacementIds.cs](Assets/GameUpSDK/Scripts/Runtime/Config/AdPlacementIds.cs)

> Một placement ("where") kèm toàn bộ ID theo từng tầng eCPM. Đây là đơn vị dữ liệu duy nhất mà cả Inspector lẫn runtime dùng chung: UI không cần gom/tách list phẳng nữa, nên không còn nguy cơ lệch nhóm.

- `public string where`
- `public string idHigh`
- `public string idMedium`
- `public string idAll`
- `public BannerFormatType bannerFormat`
- `public BannerSize bannerSize`
- `public CollapsibleBannerPlacement collapsible`
- `public string GetId(EcpmFloor floor)`
- `public void SetId(EcpmFloor floor, string value)`
- `public bool HasId(EcpmFloor floor)`
- `public bool HasAnyId()`
- `public bool Matches(string placementName)`

### `public class AdUnitConfigSet`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpAdsConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpAdsConfig.cs)

> Bộ 5 config cho 1 mediation network.

- `public AdUnitConfig banner`
- `public AdUnitConfig interstitial`
- `public AdUnitConfig rewarded`
- `public AdUnitConfig appOpen`
- `public AdUnitConfig nativeAd`
- `public AdUnitConfig Get(AdUnitType type)`
- `public IEnumerable<AdUnitConfig> All()`
- `public bool MigrateLegacyEntries()`

### `public class AdmobAdsSettings`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpAdsConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpAdsConfig.cs)

- `public string appIdAndroid`
- `public string appIdIOS`
- `public List<string> testDevices`
- `public bool showMediationInspector`
- `public ChildDirectedTreatment tagForChildDirectedTreatment`
- `public UnderAgeOfConsent tagForUnderAgeOfConsent`
- `public AdContentRating maxAdContentRating`
- `public bool umpDebugForceEea`
- `public List<string> umpTestDeviceHashedIds`
- `public AdUnitConfigSet units`

### `public class AppMetricaSettings`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpSdkConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpSdkConfig.cs)

- `public string apiKey`
- `public bool enableLogs`
- `public bool enableEventLogging`

### `public class AppsFlyerSettings`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpSdkConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpSdkConfig.cs)

- `public string devKey`
- `public string appIdIOS`
- `public bool isDebug`
- `public bool getConversionData`

### `public enum ChildDirectedTreatment`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpAdsConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpAdsConfig.cs)

> Bản sao của enum GoogleMobileAds để config vẫn compile khi chưa cài AdMob.

- `Unspecified, False, True`

### `public class GameUpAdsConfig : ScriptableObject`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpAdsConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpAdsConfig.cs)

> Nguồn sự thật duy nhất cho toàn bộ ID & thông số quảng cáo của dự án. Asset nằm trong project (Resources), KHÔNG nằm trong package, nên project nào cài SDK qua UPM cũng chỉ cần mở GameUp/SDK/Setup, điền ID rồi Save là chạy.

- `public const string ResourceFolder`
- `public const string AssetName`
- `public const string ResourcePath`
- `public List<MediationProvider> mediationPriority`
- `public int nativeCtaClickRate`
- `public bool appOpenOnColdStart`
- `public AdmobAdsSettings admob`
- `public MaxAdsSettings max`
- `public IronSourceAdsSettings ironSource`
- `public static GameUpAdsConfig Instance { get; }`
- `public static GameUpAdsConfig Resolve(GameUpAdsConfig overrideAsset)`
- `public static void ClearCache()`
- `public bool MigrateLegacyEntries()`

### `public class GameUpSdkConfig : ScriptableObject`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpSdkConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpSdkConfig.cs)

> Cấu hình analytics + Remote Config của project. Cùng với `GameUpAdsConfig`, đây là toàn bộ dữ liệu trước kia nằm rải rác trong các prefab của package.

- `public const string AssetName`
- `public const string ResourcePath`
- `public string trackingUsageDescription`
- `public AppsFlyerSettings appsFlyer`
- `public AppMetricaSettings appMetrica`
- `public RemoteConfigDefaults remoteConfig`
- `public static GameUpSdkConfig Instance { get; }`
- `public static GameUpSdkConfig Resolve(GameUpSdkConfig overrideAsset)`
- `public static void ClearCache()`

### `public class IronSourceAdsSettings`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpAdsConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpAdsConfig.cs)

- `public string appKey`
- `public AdUnitConfigSet units`

### `public class MaxAdsSettings`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpAdsConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpAdsConfig.cs)

- `public string sdkKey`
- `public bool showMediationDebugger`
- `public AdUnitConfigSet units`

### `public class RemoteConfigDefaults`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpSdkConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpSdkConfig.cs)

> Giá trị mặc định cho Firebase Remote Config (dùng cho SetDefaults và khi fetch lỗi). Tên field khớp key trên Firebase Console — `FirebaseRemoteConfigUtils` copy sang field cùng tên của mình lúc Awake, rồi Remote Config ghi đè sau khi fetch.

- `public int inter_capping_time`
- `public int inter_start_level`
- `public bool enable_rate_app`
- `public int level_start_show_rate_app`
- `public bool no_internet_popup_enable`
- `public bool enable_banner`
- `public float native_cta_click_rate`
- `public ScriptableObject extraData`

### `public enum UnderAgeOfConsent`

`GameUp.SDK` · [Scripts/Runtime/Config/GameUpAdsConfig.cs](Assets/GameUpSDK/Scripts/Runtime/Config/GameUpAdsConfig.cs)

> Bản sao của enum GoogleMobileAds để config vẫn compile khi chưa cài AdMob.

- `Unspecified, False, True`

## Scripts/Runtime/Firebase

### `public class FirebaseRemoteConfigUtils : MonoSingleton<FirebaseRemoteConfigUtils>`

`GameUp.SDK` · [Scripts/Runtime/Firebase/FirebaseRemoteConfigUtils.cs](Assets/GameUpSDK/Scripts/Runtime/Firebase/FirebaseRemoteConfigUtils.cs)

> Firebase Remote Config: tÃªn biáº¿n trÃ¹ng vá»›i key trÃªn Remote Ä‘á»ƒ tá»± Ä‘á»™ng map (reflection). Number â†’ int, Boolean â†’ bool.

- `public int inter_capping_time`
- `public int inter_start_level`
- `public bool enable_rate_app`
- `public int level_start_show_rate_app`
- `public bool no_internet_popup_enable`
- `public bool enable_banner`
- `public float native_cta_click_rate`
- `protected ScriptableObject remoteConfigExtraData`
- `[SerializeField] private GameUpSdkConfig configOverride`
- `public Action<bool> OnFetchCompleted`
- `public bool IsRemoteConfigReady { get; }`
- `protected override void Awake()`
- `public RemoteConfigDefaults ExportLegacyDefaults()`
- `protected void ApplyDefaultValues()`
- `protected virtual Dictionary<string, object> GetDefaultValues()`
- `protected virtual IEnumerable<object> GetRemoteConfigTargets()`
- `protected virtual bool ShouldIncludeFieldAsDefault(FieldInfo field)`
- `protected virtual Dictionary<string, object> BuildDefaultsFromTargets()`
- `protected void BindingFieldsFromDefaults(KeyValuePair<string, object> kv, object o)`
- `protected void UpdateKeysFromRemote()`
- `protected void BindingFields(string key, object o)`
- `public void FetchAndActivate(Action<bool> onDone = null)`

### `public class FirebaseUtils : MonoSingleton<FirebaseUtils>`

`GameUp.SDK` · [Scripts/Runtime/Firebase/FirebaseUtils.cs](Assets/GameUpSDK/Scripts/Runtime/Firebase/FirebaseUtils.cs)

- `public Action<bool> onInitialized`
- `public bool IsInitialized { get; }`
- `protected override void Awake()`
- `public static void LogEventsAPI(string eventId, Dictionary<object, object> param = null)`
- `public static void LogEvent(string eventName, string paramName, string paramValue)`
- `public static void LogEvent(string eventName, Parameter[] parameters)`
- `public void LogError(string error)`
- `public void LogException(Exception e)`

### `public class RemoteExtraData : ScriptableObject`

`GameUp.SDK` · [Scripts/Runtime/Firebase/RemoteExtraData.cs](Assets/GameUpSDK/Scripts/Runtime/Firebase/RemoteExtraData.cs)

- `public int wave_start_show_inters`

