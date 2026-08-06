# Changelog

Tất cả thay đổi đáng chú ý của **GameUp SDK** (`com.ohze.gameup.sdk`) được ghi ở đây.

Định dạng theo [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Fixed

- **Gỡ dependencies không còn để sót Scripting Define Symbols.** Auto-sync define dựa vào `IsAssemblyLoaded`, nhưng assembly của SDK vừa gỡ vẫn còn trong AppDomain tới lần domain reload kế tiếp — nên `compilationFinished` ngay sau khi xóa file lại set đúng những define vừa clear, kéo theo `GameUp.SDK.Runtime` compile code trong `#if` của SDK đã mất (lỗi compile khiến installer không load được để tự sửa). Nay trạng thái dependency đọc theo asset trên disk (+ asmdef còn trong project), auto-sync bị chặn trong lúc installer đang gỡ, và `ProjectSettings.asset` được lưu ngay sau khi clear define.
- **Gỡ lẻ một package cũng clear define trước khi xóa file** (trước đây chỉ nút "Gỡ toàn bộ" làm việc này), kèm dọn define do chính SDK third-party ghi: `gameanalytics_*` (GameAnalytics), `APPMETRICA_FEATURES_*` (AppMetrica).
- **Gỡ toàn bộ dependencies không còn xóa `Assets/Plugins/Android`.** Danh sách residual từng liệt kê nguyên thư mục này, tức là xóa cả `mainTemplate.gradle`, `settingsTemplate.gradle`, `gradleTemplate.properties` và mọi `.aar` của plugin khác trong project. Nay chỉ xóa đúng file thuộc SDK; thêm chốt chặn `s_neverDeleteExactPaths` cấm xóa các thư mục dùng chung (`Assets/Plugins`, `Assets/Resources`, `Assets/Editor`, …) kể cả khi có entry sai trong danh sách. Cũng bỏ `Assets/SDK` khỏi danh sách vì tên quá chung.
- **"Cài tất cả" không còn chết giữa chừng khi Unity reload domain.** Scope được lưu vào `SessionState`, sau mỗi lần compile/reload phần còn thiếu tự chạy tiếp (tối đa 3 lượt), có nút hủy và log rõ khi dừng.
- **Tự xóa `Assets/FacebookSDK/Examples` giờ mới thực sự chạy.** Trước đây cleanup gọi ngay sau `AssetDatabase.ImportPackage` (bất đồng bộ) nên thư mục chưa tồn tại; nay chạy trong `importPackageCompleted`, và chạy bù sau domain reload nếu callback bị cắt ngang.
- **Lỗi cài không còn bị nuốt.** `RefreshStatus` từng xóa `InstallError` của mọi package mỗi lần chạy (rất thường xuyên) nên HelpBox lỗi hầu như không kịp hiện; nay chỉ xóa khi package đã cài được.
- Trạng thái "đang cài" bám theo `importPackageCompleted` thay vì đánh dấu "đã cài" ngay khi vừa gọi import, kèm timeout 5 phút để không kẹt cờ.
- `HasDefine` so khớp từng symbol thay vì `string.Contains` (tránh khớp nhầm khi một define là chuỗi con của define khác).
- Callback "Cập nhật bản AdMob mới nhất" kiểm tra window đã đóng trước khi thao tác; package không có URL tải không còn treo cờ "đang cài"; package `ScopedRegistry` đi đúng nhánh sửa manifest thay vì bị đẩy vào hàng đợi Git URL.

### Changed

- **Primary Mediation mặc định là AdMob** (trước là IronSource LevelPlay): project chưa có define mediation nào sẽ được set `GAMEUP_PRIMARY_MEDIATION_ADMOB`, bộ pack "Cài tất cả" mặc định gồm Google Mobile Ads + 2 adapter bắt buộc, và "Gỡ toàn bộ dependencies" cũng reset về AdMob. Mặc định `mediationPriority` đổi thành `Admob → Max → IronSource` (asset/prefab đã lưu giữ nguyên giá trị cũ).
- Mục **Công cụ & xử lý sự cố** có thêm "Define symbols của SDK đã gỡ": liệt kê define còn sót của SDK không còn trong project (vd sau khi xóa folder bằng tay) và nút dọn.
- Gỡ lẻ từng package dọn đủ hơn: Firebase kèm `Editor Default Resources/Firebase`, `GeneratedLocalRepo/Firebase`, `Plugins/iOS|tvOS/Firebase`; AdMob kèm `GoogleMobileAdsPlugin.androidlib`, `googlemobileads-unity.aar`, `GADUAdNetworkExtras.h`; GameAnalytics kèm `Resources/GameAnalytics`. EDM4U (dùng chung) vẫn chỉ dọn khi gỡ toàn bộ.
- Mô tả package khớp với cờ `Required` (chỉ Facebook là bắt buộc; Firebase là "khuyến nghị mạnh", AdMob "cần khi Primary Mediation = AdMob").
- Cửa sổ **Setup Dependencies** vẽ lại: toolbar chuyển tab + tiến độ, cột trái là 2 bước có đánh số (chọn mediation → cài), cột phải là danh sách package với vạch trạng thái và bộ lọc "chỉ hiện mục chưa cài", footer cố định dẫn sang cửa sổ cấu hình. Tự chuyển 1 cột khi cửa sổ hẹp.
- Cửa sổ **GameUp SDK Setup** chuyển sang dạng sidebar trái (nhóm Quảng cáo / Analytics & dịch vụ) + panel chi tiết bên phải, thanh nút Lưu/Tạo SDK luôn hiển thị.
- Dọn code chết trong installer: `StartInstall`, `EnqueueGitInstall`, `StartDownloadAndImport`, `GetBundledPackagePath` và các field download không còn dùng.

## [1.3.0] — 2026-08-06

### Summary

Toàn bộ dữ liệu cấu hình chuyển từ **prefab** sang **ScriptableObject** nằm trong project: `GameUpAdsConfig.asset` (ads) và `GameUpSdkConfig.asset` (AppsFlyer, AppMetrica, Remote Config defaults), cùng ở `Assets/_MainProject/Resources/GameUpSDK/`. Package cài qua Git UPM là read-only nên để cấu hình trong prefab của package là bế tắc — nay **không tab Setup nào còn ghi vào prefab**, project chỉ cần mở Setup, điền key, Save là chạy.

### Added

- **`GameUpAdsConfig` (ScriptableObject)** — nguồn cấu hình ads duy nhất, gồm 3 nhánh `admob` / `max` / `ironSource`, mỗi nhánh có `AdUnitConfigSet` (banner, interstitial, rewarded, appOpen, nativeAd). Load runtime qua `Resources.Load("GameUpSDK/GameUpAdsConfig")`, cache trong `GameUpAdsConfig.Instance`.
- **`AdPlacementIds`** — một placement gộp cả 3 tầng eCPM (`idHigh` / `idMedium` / `idAll`) + thiết lập banner, thay cho list phẳng "mỗi floor một dòng" của v1. UI và runtime dùng chung một cấu trúc nên không còn khâu gom/tách nhóm.
- **`configOverride`** trên `AdmobNetwork` / `MaxNetwork` / `IronSourceNetwork`: để trống = dùng asset chung, gán asset khác = bộ ID riêng cho scene/biến thể build.
- **Migrate tool** — menu **GameUp → SDK → Migrate Ads Config (Prefab → ScriptableObject)** và nút migrate trong cửa sổ Setup khi phát hiện ID còn nằm trong prefab. Tự chạy lần đầu khi asset được tạo.
- **Custom Inspector cho `GameUpAdsConfig`** (toolbar AdMob / MAX / IronSource) và inspector rút gọn cho 3 network — sửa ID ở Setup window, ở asset hay ở prefab đều ghi vào cùng một chỗ.
- `AdPlacementGenerator` tách khỏi `GameUpSetupWindow`, đọc placement từ asset thay vì mở từng prefab bằng `LoadPrefabContents`.
- **`GameUpSdkConfig` (ScriptableObject)** — `appsFlyer` (devKey, appIdIOS, isDebug, getConversionData), `appMetrica` (apiKey, enableLogs, enableEventLogging), `remoteConfig` (7 key mặc định + `extraData`). Có `configOverride` trên `AppsFlyerUtils`, `AppMetricaActivator`, `FirebaseRemoteConfigUtils`; custom Inspector cảnh báo khi asset nằm ngoài `Resources`.
- **Migrate SDK Config** — menu **GameUp → SDK → Migrate SDK Config (Prefab → ScriptableObject)**; đọc `AppsFlyerObjectScript` qua reflection nên chạy được cả khi chưa cài AppsFlyer SDK.
- `AdsManager.mediationPriority` và `nativeCtaClickRate` chuyển vào `GameUpAdsConfig`, đọc lúc `Awake`.
- Mục **Nâng cao** trong cửa sổ Setup: clone prefab (giờ là tuỳ chọn) và migrate lại dữ liệu.

### Changed

- **Không tab Setup nào còn yêu cầu clone prefab** (`SetupTabBase.RequiresWritablePrefab = false` cho tất cả). Luồng clone prefab + vá link nested prefab chuyển xuống mục Nâng cao, chỉ dùng khi muốn sửa cấu trúc prefab.
- `AppsFlyerUtils.Awake` ghi devKey / appID / isDebug từ asset sang `AppsFlyerObjectScript` trước khi `Start()` của nó init SDK — không phải sửa prefab hay code của AppsFlyer. Chuỗi rỗng trong asset không ghi đè giá trị đang có trên prefab.
- `FirebaseRemoteConfigUtils` copy default từ asset vào field cùng tên lúc `Awake`, giữ nguyên cơ chế bind Remote Config theo tên field — asset không bị ghi đè lúc runtime.
- Prefab `AppsFlyerObject` trong package: xoá chuỗi rác trong `devKey` (trước đây project mới migrate sẽ nuốt phải giá trị này).
- UI cấu hình ads vẽ trực tiếp trên `SerializedProperty` của asset: bỏ lớp mirror `AdUnitConfigData` / `PlacementGroup` cùng cặp `Load`/`Save` tra field theo chuỗi ⇒ có sẵn Undo, tự đánh dấu dirty, và sai tên field là lỗi biên dịch chứ không im lặng.
- `AdUnitConfig` giữ nguyên API runtime (`GetEntry`, `ResolveUnitId`, `GetAllPlacements`, `WhereByKey`) nên `BaseAdFormat` và các format Admob/MAX/LevelPlay không đổi; chỉ đổi cách lưu trữ bên trong.
- `GetAllWhere()` trả về danh sách đã loại trùng (trước đây trả cả bản trùng theo từng floor).
- Việc so khớp placement không còn phụ thuộc `AdType` của từng entry — trước đây entry sai `AdType` sẽ âm thầm rơi về ID mặc định.

### Fixed

- **ID App Open của MAX chưa bao giờ được lưu/đọc:** setup window tra field `"appOpenConfig"` trong khi `MaxNetwork` khai báo `appOpenAdConfig`, `FindProperty` trả null và bị các guard `if (p != null)` nuốt mất. Tool migrate đọc đúng field cũ nên dữ liệu (nếu có) không mất.

### Migration

Dự án đang dùng bản cũ: mở **GameUp → SDK → Setup** → bấm **Migrate dữ liệu từ Prefab → ScriptableObject** → **Save Configuration**. Field cũ trong prefab được giữ lại (ẩn khỏi Inspector) để migrate; runtime không đọc chúng nữa.

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
