# CLAUDE.md — Unity + GameUp Core

File này do **GameUp Core** cài (`GameUp → Settings → AI Toolkit`). Sửa thoải mái; muốn lấy lại bản gốc thì bấm **Cập nhật** trong cửa sổ Settings (có xác nhận trước khi ghi đè).

---

## 1. Dự án này là gì

Dự án game Unity dùng framework **GameUp Core** (`com.ohze.gameup.core`).

| Thứ | Ở đâu |
|---|---|
| Code game (feature) | `Assets/_MainProject/Scripts/` |
| Framework Core (embedded) | `Assets/GameUpCore/` |
| Framework Core (UPM) | `Packages/com.ohze.gameup.core/` |
| Prefab / SO game | `Assets/_MainProject/Prefabs/`, `Assets/_MainProject/ScriptableObjects/` |
| Thư viện ngoài | `Assets/Plugins/`, `Assets/ThirdParty/` |

**Chỉ tồn tại MỘT nguồn Core** — embedded *hoặc* UPM, không cả hai (trùng asmdef).

---

## 2. Luật cứng (vi phạm là sai, không phải "tuỳ khẩu vị")

1. **Không `UnityEngine.Debug`** trong code game/feature. Dùng `GameUp.Core.GULogger`:
   `GULogger.Log/Verbose/Warning/Error/Exception`, có overload kèm `tag`.
   Chỉ `GULogger.cs` (và logger nội bộ của Core) được wrap `Debug`.
2. **Không tự chế lại thứ Core đã có** — singleton thủ công, pool tay, event bus riêng, JSON save tự viết. Xem bảng API ở §4 trước khi viết class mới.
3. **Không thêm code game vào `Packages/com.ohze.gameup.core/`**. Package restore từ registry/Git là read-only; mở rộng ở `Assets/_MainProject/`.
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
Lệnh tắt: `/gu-kickoff`, `/gu-tasks`, `/gu-story`, `/gu-refactor`, `/gu-test`, `/gu-bug`, `/gu-perf`, `/gu-release`, `/gu-core`, `/gu-review`.

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

---

## 10. Điều Claude **không** tự làm

- Không chạy build Unity, không mở Unity Editor bằng CLI trừ khi được yêu cầu rõ.
- Không `git push`, không tạo commit trừ khi được yêu cầu.
- Không xoá/di chuyển asset hàng loạt (kéo theo mất `.meta` và reference) — đề xuất, để người dùng làm trong Editor.
- Không sửa `ProjectSettings/` hay `Packages/manifest.json` mà không nói trước.
- Không đọc `Library/`, `Temp/`, `Logs/`, `obj/` — sinh tự động, vô nghĩa với context.
