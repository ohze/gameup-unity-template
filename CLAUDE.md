# CLAUDE.md — Unity + GameUp Core

File này do **GameUp Core** cài (`GameUp → Settings → AI Toolkit`). Sửa thoải mái; muốn lấy lại bản gốc thì bấm **Cập nhật** trong cửa sổ Settings (có xác nhận trước khi ghi đè).

---

## 1. Dự án này là gì

Dự án game Unity dùng framework **GameUp Core** (`com.ohze.gameup.core`).

| Thứ | Ở đâu |
|---|---|
| Code game (feature) | `Assets/_MainProject/Scripts/` |
| Framework Core (embedded) | `Assets/GameUpCore/` |
| Framework Core (UPM) | Unity thấy ở `Packages/com.ohze.gameup.core/`, nhưng cài qua Git thì file thật nằm trong `Library/PackageCache/` (bị chặn đọc) → **đọc bản chép `.claude/gameup-core/src/`** |
| GameUp SDK — ads, analytics, remote config (nếu cài) | `Assets/GameUpSDK/` hoặc UPM `com.ohze.gameup.sdk` → đọc `.claude/gameup-sdk/src/` |
| GameUp IAP — mua hàng (nếu cài) | `Assets/GameUpIAP/` hoặc UPM `com.ohze.gameup.iap` → đọc `.claude/gameup-iap/src/` |
| Bảng tra API tự sinh | `.claude/gameup-core/API_INDEX.md`, `.claude/gameup-sdk/API_INDEX.md`, `.claude/gameup-iap/API_INDEX.md` |
| Prefab / SO game | `Assets/_MainProject/Prefabs/`, `Assets/_MainProject/ScriptableObjects/` |
| Thư viện ngoài | `Assets/Plugins/`, `Assets/ThirdParty/` |

**Chỉ tồn tại MỘT nguồn Core** — embedded *hoặc* UPM, không cả hai (trùng asmdef).

---

## 2. Luật cứng (vi phạm là sai, không phải "tuỳ khẩu vị")

1. **Không `UnityEngine.Debug`** trong code game/feature. Dùng `GameUp.Core.GULogger`:
   `GULogger.Log/Verbose/Warning/Error/Exception`, có overload kèm `tag`.
   Chỉ `GULogger.cs` (và logger nội bộ của Core) được wrap `Debug`.
2. **Không tự chế lại thứ Core đã có** — singleton thủ công, pool tay, event bus riêng, JSON save tự viết. Xem bảng API ở §4 trước khi viết class mới.
3. **Không thêm code game vào `Packages/com.ohze.gameup.core/`**. Package restore từ registry/Git là read-only; mở rộng ở `Assets/_MainProject/`. Tương tự với package SDK/IAP. `.claude/gameup-core|sdk|iap/` cũng read-only — là bản chép tự sinh, sửa ở đó không có tác dụng gì.
4. **Không để `using` thừa.** Sau mỗi lần sửa file C#, xoá mọi import không dùng. Không thêm `using` "cho chắc".
5. **Không dead code.** Refactor xong mà class/field/method không còn ai gọi thì xoá luôn.
6. **Không hàm lồng trong hàm** (local function) — tách thành `private` method cùng class.
7. **Một public type = một file**, tên file trùng tên type.
8. **Không sửa `.meta`** thủ công và không xoá `.meta` của asset đang tồn tại — mất reference toàn project.

---

## 3. Naming & style C#

| Thành phần | Quy ước | Ví dụ |
|---|---|---|
| Class / Struct | PascalCase, **danh từ** (không bắt đầu bằng động từ) | `PlayerManager`, `WeaponConfig` |
| Method | PascalCase, **bắt đầu bằng động từ** | `CalculateDamage()`, `SpawnEnemy()` |
| `private` field | `_camelCase` | `_playerScore` |
| `[SerializeField] private` | camelCase, **không** `_` (Inspector cho Designer đọc) | `maxHealth`, `bulletPrefab` |
| Property | PascalCase | `CurrentScore`, `IsDead` |
| Component ref (Button/TMP/Image…) | prefix **hoặc** suffix — chọn 1 kiểu cho cả project, không trộn | `btnPlay`, `txtScore` |
| ScriptableObject class | prefix `SO` + hậu tố `Data`/`Config`/`Settings` | `SO_EnemyConfig` |

- Chuỗi: luôn interpolation `$"..."`, không nối `+` cho log/text động.
- `[CreateAssetMenu]` bắt buộc cho SO, `menuName` phân cấp rõ: `"GameUp/Entity Data/Enemy"`.
- Namespace type mới của game: **không** dùng `GameUp.Core` / `GameUp.Core.UI` — dùng namespace riêng (`GameUp.Game`, `YourStudio.Game.UI`).

---

## 4. Bản đồ API GameUp Core — tra trước khi viết mới

### Tra API GameUp (Core · SDK · IAP) ở đâu — làm theo thứ tự, không đoán chữ ký

Mỗi package GameUp đang cài có một thư mục `.claude/gameup-<core|sdk|iap>/` tự sinh (không có thư mục = package chưa cài).

1. **`API_INDEX.md`** của package — sinh bằng reflection từ đúng bản đang cài: mọi type public, chữ ký member, bảng *class nền để kế thừa* (abstract/virtual cần override), prefab có sẵn, asmdef cần reference. Đọc mục liên quan trước khi viết class hạ tầng, screen/popup, helper UI, code ads/analytics/IAP.
2. **Grep/Glob với `path` tường minh** — `.claude/` là thư mục ẩn, tìm từ gốc project có thể bỏ sót:
   - Cài qua Git UPM → `path: .claude/gameup-core/src` (hoặc `gameup-sdk/src`, `gameup-iap/src`)
   - Embedded → `path: Assets/GameUpCore` / `Assets/GameUpSDK` / `Assets/GameUpIAP`
   - Không chắc chế độ nào → dòng đầu `API_INDEX.md` ghi thư mục source đọc được.
3. **Đọc file nguồn** theo cột *File* trong index để lấy comment, hành vi, cách gọi `base.`. Index/source thắng README khi mâu thuẫn.

**Không** tìm trong `Library/PackageCache/` — bị chặn và tên thư mục đổi theo commit.
Package có trong `Packages/manifest.json` mà thiếu thư mục `.claude/gameup-*`, hoặc index ghi version khác `Packages/packages-lock.json` → dừng lại, bảo người dùng chạy **`GameUp → Project → Sync GameUp source for AI`** (hoặc nút *Đồng bộ* trong `GameUp → Settings`), rồi mới viết code dựa trên package đó.

Skill/lệnh: Core → `gameup-core-api` · `/gu-core`; SDK → `gameup-sdk-api` · `/gu-sdk`; IAP → `gameup-iap-api` · `/gu-iap`.

### Bảng tóm tắt

Asmdef: `GameUp.Core.Runtime` (`GameUp.Core`, `GameUp.Core.Serializer`) · `GameUp.UI.Runtime` (`GameUp.Core.UI`, cần DOTween + define `DOTween__DEPENDENCIES_INSTALLED`).

| Cần gì | Dùng cái này |
|---|---|
| Log | `GULogger` |
| Singleton MonoBehaviour | `MonoSingleton<T>` (`IsPersistent` để sống xuyên scene) |
| Singleton C# / SO | `Singleton<T>`, `ScriptableObjectSingleton<T>`, `ResourcesSingleton` |
| Event type-safe | `Signal`, `BaseSignal`, `IBaseSignal` |
| Object pool | `GUPool`, `GUPoolers`, `IPoolable` (`OnSpawn`/`OnDespawn`), `GUPool.Prewarm` |
| Audio | `AudioManager`, `AudioIdentity(Reference)`, `AudioDatabase`, `AudioSetting`, `AudioCategory`, `AudioHandle` |
| Save local / JSON / mã hoá | `BaseDataSave<T>` (có `dataVersion` + `Migrate`), `LocalStorageUtils`, `JsonHelper`, `EncryptUtils`, `FileStorageUtils` |
| Giá trị đơn có persist | `SettingVar` (`BooleanVar`/`IntVar`/`FloatVar`/`LongVar`) |
| Addressables | `ComponentReference<T>`, `DataReference`, `AddressableDataHolder`, `AddressableLoad.WhenReady` |
| Coroutine không cần MonoBehaviour | `CoroutineRunner`, `CoroutineExtension` |
| Thời gian | `TimeManager`, `TimeUtils`, `ConvertTimeExtension` |
| Khởi động game | `GUBootstrap` (step + timeout + progress) |
| Load scene | `GUSceneLoader` (async, `minDuration`, kiểm soát activate) |
| UI màn hình / popup | `UIScreen`, `UIPopup` (kế thừa `UIBaseView`); data `ScreenData`, `PopupData` |
| UI animation | `UIBaseAnimation`, `UIDefaultAnimation`, `TransitionUtils` |
| Loading / Toast | `Loading`, `LoadingOverlayBase`, `Toast`, `ToastItem` |
| Notch / đa độ phân giải | `SafeArea`, `MultiResolution` |
| Tìm object trong scene theo ID | `ObjectFinder` |
| Tiện ích | `GameUtils`, `StringUtils`, `UIExtension`, `MonoExtension`, `EnumExtension`, `ListCollectionExtension`, `[Button]`, `[ReadOnlyInInspector]` |
| Tracking level local | `LocalLevelTracking`, `ILevelTracking` |

**Chưa thấy trong repo thì đừng giả định có** — `package.json` ghi từ khoá rộng (EventBus/FSM) nhưng thực tế chỉ có `Signal`; không viết code dựa trên FSM tưởng tượng.

### GameUp SDK (`com.ohze.gameup.sdk`) — chỉ khi project có cài

Namespace `GameUp.SDK` · asmdef `GameUp.SDK.Runtime` (reference `GameUp.Core.Runtime`). Quảng cáo đa mediation (AdMob / AppLovin MAX / IronSource LevelPlay, waterfall), analytics (Firebase, AppsFlyer, AppMetrica, GameAnalytics, Facebook), Firebase Remote Config, consent ATT + GDPR.

| Cần gì | Dùng cái này |
|---|---|
| Banner / Interstitial / Rewarded / App Open / Native | `AdsManager.Instance` — `ShowBanner`/`HideBanner`, `ShowInterstitial`, `ShowRewardedVideo`, `ShowAppOpenAds`, `ShowNativeAd`, `Is…Available` |
| Luật chặn ads (capping, level tối thiểu, cooldown chéo, tắt banner từ remote) | `AdsManager.AddCondition(IAdCondition)` + `CappingTimeCondition`, `MinLevelCondition`, `RemoveAdCondition`, `CrossFormatCooldownCondition`, `HideBannerFromRemote`; `AdCappingManager` |
| Trạng thái đã mua Remove Ads | `RemoveAdsSetting.Instance.IsRemoveAllAds` / `IsRemoveInter` (`BooleanVar`) — `AdsManager` tự tôn trọng |
| Log event (level, wave, currency, purchase, tutorial…) | `GameUpAnalytics` (static), tên event trong `AnalyticsEvent` |
| Remote Config | `FirebaseRemoteConfigUtils.Instance` (field trùng key, `IsRemoteConfigReady`, `OnFetchCompleted`); kế thừa để thêm key riêng |
| Consent ATT / GDPR | `PrivacyManager.Instance` (`AdsManager` tự chạy flow; game chỉ gọi `ShowPrivacyOptionsForm`) |
| Callback SDK ngoài main thread | `MainThreadDispatcher` |
| ID quảng cáo / key | `GameUpAdsConfig`, `GameUpSdkConfig` trong `Assets/_MainProject/Resources/GameUpSDK/` — sửa qua `GameUp → SDK → Setup` |

Luật SDK:
- Code game **không** gọi thẳng API Firebase / AppsFlyer / AdMob / MAX / LevelPlay — đi qua bảng trên.
- Code buộc phải chạm SDK bên thứ ba bọc `#if <X>_DEPENDENCIES_INSTALLED` (hằng trong `GUDefinetion`); không sửa define tay (`GameUp → SDK → Sync Define Symbols`).
- Mọi lệnh show ads xử lý **cả** `onSuccess` và `onFail`; thưởng rewarded chỉ trong `onSuccess`.
- `SDK.prefab` chỉ có một, ở scene load đầu.

### GameUp IAP (`com.ohze.gameup.iap`) — chỉ khi project có cài

Namespace `GameUp.IAP` · asmdef `GameUp.IAP.Runtime` (reference Core + SDK + `Unity.Purchasing`). Unity IAP v5 bọc sẵn trong `MyIAPManager`: init sản phẩm, mua, giá localize, kiểm chữ ký receipt chống hack, analytics mua hàng.

| Cần gì | Dùng cái này |
|---|---|
| Khai báo sản phẩm | `IAPProductDefinition(id, ProductType, localPackCost)` |
| Khởi tạo | `MyIAPManager.Instance.Initialize(products)` / `await InitializeAsync(products)`, kiểm `IsIAPInitialized` |
| Mua | `BuyProduct(productId, onPurchaseComplete, level)` |
| Giá hiển thị | `GetLocalizedPrice`, `GetMultipliedLocalizedPrice` |
| Chống hack receipt | `IAPReceiptValidator` (bật sẵn, cần tangle từ Receipt Validation Obfuscator) |

Luật IAP:
- `MyIAPManager` đặt ở scene loading, một instance (`GameUp → IAP → Create MyIAPManager`).
- Cấp hàng **chỉ** khi callback mua trả `true`; gói Remove Ads → set `RemoveAdsSetting.Instance.IsRemoveAllAds.Value = true`.
- Không tự dựng `StoreController` riêng; `testMode` không bật trong build release.

---

## 5. Scene & Prefab

- **Prefab is King** — phần tái sử dụng phải thành Prefab; không lắp logic phức tạp trực tiếp trên Scene.
- Scene "nhạt": Environment, Light, Camera, Manager tĩnh. Gameplay/UI đi qua Prefab.
- Biến thể gần giống base → **Prefab Variant**, không Unpack rồi copy.
- UI/entity lớn → **Nested Prefab** để nhiều người sửa song song, giảm conflict merge.
- Root chuẩn do `GameUp → Project → Core setup` tạo: `====Manager====` và `=====UI=====`.

---

## 6. Cách làm việc (workflow gates)

Mỗi task đi qua các cửa sau. Việc nhỏ thì làm nhanh trong đầu, việc lớn thì viết ra.

1. **Brief** — player value, in/out scope, acceptance criteria đo được.
2. **Rủi ro & giả định** — liệt kê thứ có thể gây làm lại.
3. **Tăng dần** — implement từng increment nhỏ, validate sau mỗi increment.
4. **Test** — non-trivial thì phải có ít nhất một chiến lược test (EditMode / PlayMode / manual có bước tái hiện).
5. **Báo cáo** — file đã đổi, đã test gì, rủi ro còn lại.

Skill tương ứng: `unity-feature-kickoff` → `unity-design-to-tasks` → `unity-implement-story` → `unity-test-plan` → `unity-release-checklist`.
Lệnh tắt: `/gu-kickoff`, `/gu-tasks`, `/gu-story`, `/gu-refactor`, `/gu-test`, `/gu-bug`, `/gu-perf`, `/gu-release`, `/gu-core`, `/gu-sdk`, `/gu-iap`, `/gu-review`.

---

## 7. Test

- **EditMode** cho logic thuần; **PlayMode** cho hành vi phụ thuộc scene/lifecycle.
- Mỗi bug fix → thêm regression check **fail trước fix, pass sau fix**.
- Logic non-trivial: phủ success path + failure path + ít nhất 1 edge case.
- Không assert theo thời gian thực (flaky) — setup tất định, tolerance rõ ràng.
- Test của Core: `Assets/GameUpCore/Tests/{Editor,Runtime}`. Test game: đặt cạnh feature (`Feature/Tests`).

---

## 8. Ngân sách hiệu năng (mobile là mặc định)

| Chỉ số | Mục tiêu |
|---|---|
| Frame rate | 60 FPS ổn định (tối thiểu 30 trên máy low-end) |
| Thời gian vào game | < 3s tới màn đầu tiên |
| GC alloc trong gameplay loop | ~0 B/frame ở `Update`/`FixedUpdate` |
| Draw call scene chính | càng thấp càng tốt, batch/atlas trước khi tối ưu shader |
| Crash rate | < 0.1% |

Nguyên tắc: **đo trước, sửa sau** (Profiler / Frame Debugger). Không "tối ưu" theo cảm giác.
Ưu tiên thường gặp, theo thứ tự hiệu quả: pool thay `Instantiate`/`Destroy` → cache `GetComponent` → bỏ alloc trong vòng lặp (LINQ, `foreach` trên struct enumerator, string concat) → atlas sprite → giảm overdraw UI → nén texture/audio.

---

## 9. Lệnh Unity hữu ích (menu Editor)

| Việc | Menu |
|---|---|
| Hub cài đặt tổng | `GameUp → Settings` |
| Cài dependency + folder + core | `GameUp → Project → GameUpCore Installer` |
| Xem/sửa dữ liệu đã lưu | `GameUp → Data → Data Save Viewer` |
| Sinh `AudioID` từ clip | `GameUp → Audio → Setup AudioManager` |
| Bật/tắt log | `GameUp → Logger → Enable/Disable Logs` |
| Chép source Core/SDK/IAP + sinh lại `API_INDEX.md` cho AI | `GameUp → Project → Sync GameUp source for AI` |
| Cài dependency ads/analytics · điền ID · tạo SDK trong scene | `GameUp → SDK → Setup Dependencies` · `GameUp → SDK → Setup` |
| Đồng bộ define theo SDK bên thứ ba đã cài | `GameUp → SDK → Sync Define Symbols` |
| Tạo `MyIAPManager` trong scene | `GameUp → IAP → Create MyIAPManager` |

---

## 10. Điều Claude **không** tự làm

- Không chạy build Unity, không mở Unity Editor bằng CLI trừ khi được yêu cầu rõ.
- Không `git push`, không tạo commit trừ khi được yêu cầu.
- Không xoá/di chuyển asset hàng loạt (kéo theo mất `.meta` và reference) — đề xuất, để người dùng làm trong Editor.
- Không sửa `ProjectSettings/` hay `Packages/manifest.json` mà không nói trước.
- Không đọc `Library/`, `Temp/`, `Logs/`, `obj/` — sinh tự động, vô nghĩa với context. Source Core/SDK/IAP cài qua UPM đã chép sẵn ở `.claude/gameup-<core|sdk|iap>/src/` — đọc ở đó.
