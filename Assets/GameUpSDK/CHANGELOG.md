# Changelog

Tất cả thay đổi đáng chú ý của **GameUp SDK** (`com.ohze.gameup.sdk`) được ghi ở đây.

Định dạng theo [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.2.7] — 2026-07-29

### Summary

Đồng bộ `sdk-gameup` từ `7bbc1ba` → `65c47c5` (8 commit, 2026-07-21 → 2026-07-22): CTA click-bait rate cho Native Ads (overlay trap), kênh log từ native về Unity Console, delay đóng ad sau khi click CTA.

### Added

- **Native CTA click rate** (`NativeAdConfigBridge`): đẩy tỉ lệ `ctaClickRate` (0–100%) xuống Android (`NativeBannerManager.setCtaClickRate`, `UnityNativeFullScreen.setCtaClickRate`) và iOS (`NativeBanner_SetCtaRate`, `_iosSetNativeFullScreenCtaRate`). Theo quy ước template: đặt trong namespace `GameUp.SDK`, log qua `GULogger`.
- `AdsManager.nativeCtaClickRate` (`[Range(0,100)]`, mặc định 30) — áp dụng sau khi privacy flow xong; `AdsManager.UpdateNativeCtaClickRate(float)` để chỉnh lúc runtime (nhận giá trị 0..1).
- `FirebaseRemoteConfigUtils.native_cta_click_rate` (mặc định `0.3f`, đơn vị 0..1) — key Remote Config tương ứng. Khác upstream: template tự đẩy giá trị này xuống native trong `OnRemoteConfigFetched` nên Remote Config điều khiển được tỉ lệ CTA thật sự (fetch lỗi → giữ default 0.3).
- **Overlay trap trong native layout** (Android/iOS): theo tỉ lệ `ctaClickRate`, phủ một view trong suốt full-size làm `CallToActionView` (vô hiệu nút close / blur background); ngược lại giữ setup thường (CTA là nút CTA, close/blur đóng ad).
- **Kênh log native → Unity**: `AdCallback.onLog(String)` (Android), callback `onLog` trong `NativeBanner_SetCallbacks` / `_iosLoadNativeAd` (iOS); C# in ra Console với prefix `[GameUp-NativeBanner]` / `[GameUp-FullScreenNative]`. `FullScreenNativeAdManager.OnAdLogEvent` cho phép UI lắng nghe.
- `[Preserve]` (`UnityEngine.Scripting`) trên các `AndroidJavaProxy` và method callback — chống IL2CPP/managed stripping làm mất callback native.

### Changed

- Native Banner: sau khi Google SDK báo `onAdClicked`, delay 1.5s rồi mới đóng layout (nhường tài nguyên cho hiệu ứng mở Store) và bắn `onClicked` + `onClosed`.
- Di chuyển `FullScreenNativeAdManager.cs` từ `Scripts/Runtime/Ads/` sang `Scripts/Runtime/Ads/Refactor/Admob/` (theo upstream, giữ nguyên GUID).

### Fixed

- **Thứ tự init ads: chờ consent xong mới initialize network** (khôi phục hành vi upstream `3177b03`). Trước đây `AdsManager.Start()` gọi `BeginPrivacyFlow(SetConsent)` (coroutine ATT → UMP, chạy nhiều frame) rồi `InitializeAll()` **ngay dòng sau** → network init xong trước khi có consent. Nay `InitializeAll` + `SetGlobalCtaClickRate` nằm trong callback của `BeginPrivacyFlow`, đúng thứ tự **ATT (iOS) → UMP → SetConsent → Initialize**. Quan trọng vì `MaxSdk.SetHasUserConsent` và `LevelPlay.SetConsent` phải được gọi *trước* khi init SDK; init trước rồi set sau làm mất tín hiệu personalized ads (giảm eCPM) và sai luồng GDPR/ATT.
- Bỏ block `AdsManager.Instance.SetConsent(...)` trong `PrivacyManager.RunPrivacyFlowCoroutine` — thừa (callback `_onCompleted` đã gọi `SetConsent`) và do `MonoSingleton.Instance` tự tạo GameObject nên có thể sinh ra một `AdsManager (Singleton)` rỗng nếu scene không có AdsManager. `PrivacyManager` giờ khớp upstream.

### Notes

- Không port `NativeAdTest.cs` của upstream: file này gọi `setLogListener` / interface `NativeBannerManager$LogListener` và `NativeBanner_SetLogCallback` — không tồn tại trong plugin Android/iOS, chạy sẽ ném exception. Kênh log đã được thay bằng `onLog` ở trên.
- Giá trị `native_cta_click_rate` trên Firebase console phải theo đơn vị **0..1** (`0.3` = 30%), không phải phần trăm — `UpdateNativeCtaClickRate` nhân 100 rồi clamp 0–100.

## [1.2.5] — 2026-07-20

### Summary

Đồng bộ toàn bộ logic + tài nguyên từ `sdk-gameup` v1.2.5 (≈90 commit sau v1.2.4): kiến trúc AdsManager mới (waterfall đa network), Native Ads bridge (Android/iOS), capping rules, ECPM floor, native banner/fullscreen.

### Added

- **Bật/tắt eCPM Waterfall Floor** (đồng bộ `sdk-gameup@06c76fe`): `AdUnitConfig.enableWaterfallFloor` + `GetActiveFloors()`. Tắt (mặc định) → chỉ dùng 1 ad unit ID (`_All`); bật → chạy 3 tầng High → Medium → All. Mọi `IsAvailable`/`Show`/`Load` của Admob/IronSource/MAX đọc theo `GetActiveFloors()` thay vì luôn duyệt hết enum `EcpmFloor`.
- **Custom Inspector cho các Network** (`Editor/Networks/`): `AdmobNetworkEditor`, `IronSourceNetworkEditor`, `MaxNetworkEditor` + `NetworkEditorUI` — cấu hình ad unit ID ngay trên prefab network, dùng chung UI với cửa sổ SDK Setup (`SetupTabBase.DrawConfigDataUI` ủy quyền sang `NetworkEditorUI`). UI tự ẩn ô High/Medium khi tắt waterfall; Banner luôn tắt waterfall.
- `AdmobNetwork.showMediationInspector` — mở AdMob Ad Inspector ngay sau khi init để debug mediation.
- `BaseAdFormat.WhereByKey(key)` — helper tra placement theo key.

- **Kiến trúc ads mới** `Scripts/Runtime/Ads/Refactor/`: `AdsManager` (waterfall theo `mediationPriority`), `IAdNetwork`/`INetwork`, `AdmobNetwork` + `AdmobAdFormat` (ECPM floor, native banner/collapsible, native fullscreen), `IronsourceNetwork`, `MaxNetwork`, `AdsTracker`, `AdUnitConfig`/`AdUnitIdEntry` (ad unit theo platform), `TimerHelper`, Dummy AOA/native.
- **AdsRules**: `AdCappingManager`, `CappingTimeCondition`, `IAdCondition` — điều kiện hiển thị + capping time theo loại ad.
- **Native Ads bridge**: `Plugins/Android/GameUpNativeAds.androidlib` (layout/drawable/values), `NativeBannerManager.java`, `UnityNativeFullScreen.java`; iOS: `NativeBannerManager.mm`, `UnityiOSNativeFullScreen.mm`; C#: `AdmobNativeBannerBridge`, `FullScreenNativeAdManager`, `RuntimeCollapsibleUI`.
- **AdHistoryTracker** — theo dõi thời điểm đóng ad để bỏ qua AOA ngay sau ad khác.
- **Analytics**: `AppMetricaEvent`, `AppMetricaUtils`, `VideoAdsAppMetricaTracker` (track video ads qua AppMetrica).
- **Editor**: `Editor/Setup/` (SetupTabBase + tab Admob/Max/IronSource/AppsFlyer/AppMetrica/FirebaseRC/Facebook/GameAnalytics, generate `AdPlacement`), `GameUpAndroidBuildProcessor` (tự thêm proguard rules cho native ads).
- `GameUtils.cs` (runtime helpers).

### Changed

- `PrivacyManager`: chạy UMP trước khi initialize networks (`BeginPrivacyFlow` từ `AdsManager.Start`).
- `AdsEvent`, `AdsExample`, `AppMetricaActivator`, `GameUpAnalytics`: đồng bộ theo v1.2.5.
- Installer `GameUpDependenciesWindow`: chuyển sang enum `MediationProvider` (thay `AdsManager.PrimaryMediation`); giữ policy template — AdMob primary tự cài adapter Unity Ads + IronSource, hỗ trợ "Cài tất cả" với MAX, URL tải từ `ohze/gameup-unity-template` releases.
- Prefabs `SDK`, `AdmobAds`, `IronSourceAds`, `MaxAds`, `AppmetricaObject`: cấu trúc mới theo network refactor.
- `GameUp.SDK.Runtime.asmdef`: thêm reference `UnityEngine.UI` (RuntimeCollapsibleUI).
- Giữ quy ước template: namespace `GameUp.SDK`, `GULogger`/`MonoSingleton` từ GameUp Core, tích hợp `RemoveAdsSetting` vào AdsManager mới (chặn inter/banner/AOA/native khi mua Remove Ads; Rewarded vẫn cho phép).

### Removed

- Kiến trúc ads cũ: `AdmobAds`, `IronSourceAds`, `MaxAds`, `UnityAds`, `IAds`/`IShowAds`/`IRequestAds`/`IInitialAds`/`ICheckValidAds`, `AdsRules.cs`, `AdsManager` cũ, `AdsTester`, `AdMobMediationConsentBridge` (upstream bỏ forward consent tới adapter), prefab `UnityAds`.
- `Editor/GameUpSetupWindow.cs` cũ (thay bằng `Editor/Setup/GameUpSetupWindow.cs`).

## [1.2.4] — 2026-05-19

### Added

- **AppLovin MAX** (`MaxAds`, `GAMEUP_PRIMARY_MEDIATION_MAX`, `MAXSDK_DEPENDENCIES_INSTALLED`) — mediation thứ ba, cài qua Setup Dependencies.
- **AppMetrica** (`AppMetricaActivator`, `APPMETRICA_DEPENDENCIES_INSTALLED`) — analytics tùy chọn.
- **RemoteExtraData** ScriptableObject — cấu hình remote bổ sung (vd `wave_start_show_inters`).
- Prefab `MaxAds`, `AppmetricaObject`.

### Changed

- Đồng bộ logic từ `sdk-gameup` v1.2.4: installer, Setup window, ads/analytics runtime; giữ namespace `GameUp.SDK`, `GULogger`, `MonoSingleton` từ GameUp Core và `RemoveAdsSetting` của template.

## [1.1.3] — 2026-04-02

### Summary

- **GameAnalytics**: luồng cài đặt, asmdef runtime (`Ensure GameAnalytics runtime asmdef`), define `GAMEANALYTICS_DEPENDENCIES_INSTALLED`, Setup / scene SDK và analytics level–wave được coi là **hoàn thiện** cho consumer.
- **Facebook SDK**: tích hợp trong installer & Setup, define `FACEBOOK_DEPENDENCIES_INSTALLED`, bootstrap/analytics phía GameUp — **hoàn thiện** cùng bản này.

### Changed

- `package.json`: phiên bản **1.1.3**; mô tả & keywords cập nhật (Facebook).

## [1.1.1] — 2026-04-01

### Changed

- **GameAnalytics**: `GameUpAnalytics` gửi **progression events** (Start / Complete / Fail) theo [GA Unity — Progression](https://docs.gameanalytics.com/event-tracking-and-integrations/sdks-and-collection-api/game-engine-sdks/unity/event-tracking); hierarchy cố định `main` → số level → wave (`w{n}`).
- **`GameAnalyticsUtils`**: gọi trực tiếp API `GameAnalyticsSDK`; thêm assembly definition `GameAnalyticsSDK` (`Assets/GameAnalytics/Plugins`) và reference từ `GameUpSDK.Runtime` (không dùng reflection).
- `package.json`: phiên bản **1.1.1** (consumer cập nhật qua Package Manager / Git).

## [1.1.0] — 2026-04-01

### Added

- Tích hợp **GameAnalytics** (tùy chọn): cài qua **GameUp SDK → Setup Dependencies**, define `GAMEANALYTICS_DEPENDENCIES_INSTALLED`, mirror tiến trình **level / wave** qua design events (`gameup:`) trong `GameUpAnalytics`.
- `GameAnalyticsUtils` — gọi GameAnalytics (assembly `GameAnalyticsSDK`); khi chưa bật define GA thì no-op.
- Phát hiện GameAnalytics khi dùng **.unitypackage** cổ điển (type trong `Assembly-CSharp`), không chỉ assembly `GameAnalyticsSDK` (UPM).

### Changed

- `GameUpDependenciesWindow`: thêm package GameAnalytics (hosted `GA_SDK_UNITY.unitypackage`), đưa vào batch cài theo Primary Mediation; `IsGameAnalyticsSdkPresent()` cho define & UI.
- `GameUpDefineSymbolsAutoSync`: đồng bộ `GAMEANALYTICS_DEPENDENCIES_INSTALLED`.
- `GUDefinetion`: `GameAnalyticsDepsInstalled`.

### Fixed

- Trạng thái “chưa cài” GameAnalytics dù đã import `Assets/GameAnalytics` (sai tên assembly so với UPM).

## [1.0.1] — trước đó

- Bản ổn định trước GameAnalytics / cập nhật installer trên.

## [1.0.0]

- Phát hành ban đầu GameUp SDK (Ads + Firebase/AppsFlyer, Setup Dependencies).

[1.1.3]: https://github.com/DuyOhze119/sdk-gameup/compare/v1.1.2...v1.1.3
[1.1.1]: https://github.com/DuyOhze119/sdk-gameup/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/DuyOhze119/sdk-gameup/compare/v1.0.1...v1.1.0
[1.0.1]: https://github.com/DuyOhze119/sdk-gameup/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/DuyOhze119/sdk-gameup/releases/tag/v1.0.0
