# GameUp Unity Template

**GameUp Core Framework** là nền tảng cốt lõi cho Unity (2022.3 LTS trở lên): Singleton, Signal, Object Pool, Logger, Audio, lưu trữ có mã hóa, **UI (Screen / Popup)** tích hợp Addressables và DOTween, cùng các cửa sổ Editor để setup nhanh.

---

## Mục lục

### A. Cài đặt dự án mới (đọc theo thứ tự)

1. [Tổng quan luồng cài đặt](#a1-tổng-quan-luồng-cài-đặt)
2. [Bước 1 — DOTween Pro & assembly `DOTween.Modules`](#a2-bước-1--dotween-pro--assembly-dotweenmodules)
3. [Bước 2 — Thêm GameUpCore qua Git UPM](#a3-bước-2--thêm-gameupcore-qua-git-upm)
4. [Bước 3 — Folder Setup (`GUProjectFolderSetupWindow`)](#a4-bước-3--folder-setup-guprojectfoldersetupwindow)
5. [Bước 4 — Logger (`GULoggerMenu`)](#a5-bước-4--logger-guloggermenu)
6. [Bước 5 — Core setup (`GUCoreProjectSetup`)](#a6-bước-5--core-setup-gucoreprojectsetup)
7. [Bước 6 — Audio (`GUAudioManagerWindow`, tùy chọn)](#a7-bước-6--audio-guaudiomanagerwindow-tùy-chọn)
8. [Checklist: khi nào coi như “xong” phần setup](#a8-checklist-khi-nào-coi-như-xong-phần-setup)

### B. Tài liệu framework

9. [Cấu trúc `Assets/GameUpCore`](#b1-cấu-trúc-assetsgameupcore)
10. [Hệ thống UI: Popup & Screen](#b2-hệ-thống-ui-popup--screen)
    - [Vị trí prefab & script trong dự án](#b21-vị-trí-prefab--script-trong-dự-án)
    - [Popup: mở / đóng](#b22-popup-mở--đóng)
    - [Screen: mở / lịch sử](#b23-screen-mở--lịch-sử)
    - [Chọn animation (Default / Custom)](#b24-chọn-animation-default--custom)
    - [Tự động cập nhật dữ liệu: `ViewCreatorPostProcessor`](#b25-tự-động-cập-nhật-dữ-liệu-viewcreatorpostprocessor)
    - [Nút mở Screen nhanh: `ButtonOpenScreen`](#b26-nút-mở-screen-nhanh-buttonopenscreen)
    - [Helper UI có sẵn](#b27-helper-ui-có-sẵn)
11. [Cài UPM (tham chiếu nhanh)](#b3-cài-upm-tham-chiếu-nhanh)
12. [Bắt đầu nhanh với template repo (clone)](#b4-bắt-đầu-nhanh-với-template-repo-clone)
13. [Cấu trúc thư mục khuyến nghị](#b5-cấu-trúc-thư-mục-khuyến-nghị)
14. [Workflow khuyến nghị](#b6-workflow-khuyến-nghị)
15. [Hệ thống cốt lõi (`GameUpCore/Runtime/Core`)](#b7-hệ-thống-cốt-lõi-gameupcoreruntimecore)
16. [Công cụ Editor (`GameUpCore/Editor`)](#b8-công-cụ-editor-gameupcoreeditor)
17. [Ví dụ code (Signal, Pool, Save, Audio, Time)](#b9-ví-dụ-code-signal-pool-save-audio-time)

---

## A1. Tổng quan luồng cài đặt

Thứ tự **bắt buộc** cho dự án trống hoặc dự án mới chỉ thêm package:

1. Cài **DOTween Pro** và chạy **DOTween Setup** để có assembly **`DOTween.Modules`** (GameUp UI reference assembly này trong `GameUp.UI.Runtime.asmdef`).
2. Thêm **GameUpCore** qua **Git UPM**.
3. **`GameUp → Project → Folder Setup`** → **Create All Folders** (tạo `_MainProject`, Resources, `PopupData` / `ScreenData`, Addressables cơ bản…).
4. **`GameUp → Logger → Enable Logs (Debug)`** (menu chỉ bật sau khi Folder Setup đã hoàn tất).
5. **`GameUp → Project → Core setup`** (copy prefab Manager/UI, đặt vào scene hiện tại).
6. (Tuỳ chọn) **`GameUp → Audio → Setup AudioManager`** nếu dùng Audio của framework.

---

## A2. Bước 1 — DOTween Pro & assembly `DOTween.Modules`

**Vì sao cần:** package `com.gameup.core` không khai báo DOTween trong `package.json` (OpenUPM không có gói Demigiant chính thức). Module UI (`GameUp.UI.Runtime`) **reference `DOTween.Modules`** — assembly này được tạo sau khi import DOTween và chạy wizard setup của Demigiant.

### Tải và import

1. Tải gói **DOTween Pro** (file `.unitypackage` do team cung cấp), ví dụ qua link:  
   [DOTween Pro — Google Drive](https://drive.google.com/file/d/1Zz0nFNgwxcP1IbKvsw6ttA2zPLQmJ_iC/view?usp=sharing)
2. Trong Unity: **Assets → Import Package → Custom Package…**, chọn file vừa tải, import đầy đủ theo hướng dẫn của gói.

### Tạo `DOTween.Modules` (bắt buộc)

1. Sau khi import, mở cửa sổ setup của DOTween (thường menu **Tools → Demigiant → DOTween Utility Panel** hoặc **Window** tương đương theo phiên bản DOTween).
2. Chạy **Setup DOTween** / **Create ASMDEF** (tên nút có thể khác theo bản Pro) để Unity tạo **Assembly Definition** cho module runtime, trong đó có assembly tên **`DOTween.Modules`**.
3. Đợi Unity **recompile** xong, kiểm tra trong **Project** có asmdef liên quan DOTween Modules và không có lỗi compile.

**Khi nào xong bước này:** project compile được, `GameUp.UI.Runtime` không báo thiếu reference `DOTween.Modules`.

---

## A3. Bước 2 — Thêm GameUpCore qua Git UPM

**Cách 1 — Package Manager**

1. **Window → Package Manager**.
2. **+ → Add package from git URL…**
3. Dán:

   ```
   https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpCore
   ```

4. Chờ tải xong; Unity sẽ kéo thêm dependency **Addressables** theo `package.json` của core.

**Cách 2 — `Packages/manifest.json`**

```json
"dependencies": {
    "com.gameup.core": "https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpCore"
}
```

**Khi nào xong:** trong Package Manager thấy **GameUp Core Framework**, không lỗi resolve package.

---

## A4. Bước 3 — Folder Setup (`GUProjectFolderSetupWindow`)

**Menu:** `GameUp → Project → Folder Setup` (class `GUProjectFolderSetupWindow`).

**Việc cần làm:**

1. Mở cửa sổ, xem danh sách folder / ScriptableObject mặc định.
2. Bấm **Create All Folders**.

**Hệ quả chính:**

- Tạo cây thư mục **`Assets/_MainProject/...`** (Resources, Data, Prefabs/UI gồm **`Prefabs/UI/Popups`**, **`Prefabs/UI/Screens`**, Scenes, Scripts…).
- Tạo **`PopupData`** / **`ScreenData`** tại  
  `Assets/_MainProject/Resources/Data/PopupData.asset` và `ScreenData.asset` (load runtime qua `Resources` path `Data/PopupData`, `Data/ScreenData`).
- Cấu hình Addressables cơ bản cho nhóm UI/Data (theo logic trong tool).

**Khi nào xong:** sau **Create All Folders**, Editor lưu `EditorPrefs` đánh dấu hoàn tất; các menu phụ thuộc (Logger, Core setup) mới active đúng. Điều kiện “đủ” còn được kiểm tra lại: mọi folder bắt buộc tồn tại và hai asset `PopupData` / `ScreenData` có mặt (xem `IsSetupCompleted()` trong cùng file Editor).

---

## A5. Bước 4 — Logger (`GULoggerMenu`)

**Menu:**

- `GameUp → Logger → Enable Logs (Debug)`
- `GameUp → Logger → Disable Logs (Release)`

**Khi nào dùng:** sau Folder Setup. Tool gắn/bỏ define **`ENABLE_LOG`** cho Standalone, Android, iOS.

**Khi nào xong:** khi đang dev, bật Debug; trước bản release store, tắt log bằng Release.

---

## A6. Bước 5 — Core setup (`GUCoreProjectSetup`)

**Menu:** `GameUp → Project → Core setup` (chỉ khả dụng khi Folder Setup đã hoàn tất).

**Việc làm:** copy prefab **Core** / **UI Helpers** (Loading, Toast, …) từ package sang `_MainProject`, remap GUID, rồi đảm bảo trong **scene đang mở** có instance prefab **`====Manager====`** và **`=====UI=====`** (tên file trong package: `Prefab/Core`).

**Khi nào xong:** log “Đã hoàn tất Core setup”; trong hierarchy có root Manager + UI, `ObjectFinder`/`ScreenHolder`/`PopupHolder` hoạt động theo prefab.

---

## A7. Bước 6 — Audio (`GUAudioManagerWindow`, tùy chọn)

**Menu:** `GameUp → Audio → Setup AudioManager`.

- **Initial:** tìm/tạo **AudioManager** trong scene, khởi tạo **Audio Database**.
- **Scan & Update:** quét thư mục audio, sinh identity, cập nhật database, build **`AudioID`**.

**Khi nào xong:** khi bạn đã có `AudioManager` trong scene và pipeline scan chạy không lỗi cho thư mục audio dự án.

---

## A8. Checklist: khi nào coi như “xong” phần setup

| Mục | Điều kiện |
|-----|-----------|
| DOTween | Import Pro + Setup → có `DOTween.Modules`, compile OK |
| GameUpCore | Package `com.gameup.core` cài qua Git URL |
| Folder Setup | **Create All Folders** đã chạy, `PopupData`/`ScreenData` tồn tại đúng path Resources |
| Logger | Đã chọn Debug/Release theo nhu cầu |
| Core setup | Scene có Manager + UI root từ tool |
| UI Addressables | Prefab Popup/Screen đặt dưới `_MainProject/.../Popups` và `.../Screens`, đã mark Addressables theo nhóm tool tạo (khi cần) |
| Audio | (Tuỳ chọn) Audio window đã setup và scan |

Sau đó có thể tạo prefab Popup/Screen, để `ViewCreatorPostProcessor` hoặc nút **SetUp** trên asset data tự đồng bộ danh sách.

---

## B1. Cấu trúc `Assets/GameUpCore`

- **`Runtime/Core`** — Singleton, Signal, Pool, Logger, Audio, Time, CoroutineRunner, DataHelper, Utils/Extensions, Addressable holder, v.v.
- **`Runtime/UI`** — `UIPopup` / `UIScreen`, `PopupData` / `ScreenData`, animation (DOTween), adaptation (SafeArea, MultiResolution), **Helpers** (Loading, Toast, EnhancedScroll, SelectView, CustomButton…), `ViewCreatorPostProcessor`.
- **`Prefab/`** — Prefab mẫu Core + UI (Manager, UI root, Loading, Toast…) được **Core setup** copy sang `_MainProject`.
- **`Editor/`** — `GUProjectFolderSetupWindow`, `GUCoreProjectSetup`, `GULoggerMenu`, `GUAudioManagerWindow`, menu Poolers, v.v.

File `package.json` của core ghi rõ Unity **2022.3** và dependency **Addressables**.

---

## B2. Hệ thống UI: Popup & Screen

### B2.1. Vị trí prefab & script trong dự án

- **Script / logic** nằm trong package: ví dụ `Assets/GameUpCore/Runtime/UI/Popups`, `.../Screens` (class kế thừa `UIPopup`, `UIScreen`).
- **Prefab thực tế** đặt trong dự án game tại (mặc định sau Folder Setup):
  - `Assets/_MainProject/Prefabs/UI/Popups`
  - `Assets/_MainProject/Prefabs/UI/Screens`  

`PopupData` / `ScreenData` quét đúng hai path này (`pathPopup` / `pathScreen` trong asset).

**Tạo màn mới (quy trình gợi ý):**

1. Tạo class `public class MyPopup : UIPopup` hoặc `public class MyScreen : UIScreen` trong asmdef dự án (reference `GameUp.UI.Runtime` nếu cần).
2. Tạo prefab trong folder **Popups** hoặc **Screens**, gắn component đó (thường trên root hoặc con — hệ thống dùng `GetComponentInChildren`).
3. Lưu prefab → post-processor hoặc nút **SetUp** trên `PopupData`/`ScreenData` sẽ cập nhật registry (xem B2.5).

### B2.2. Popup: mở / đóng

- Dùng generic: `UIPopup<T>.OpenViewAsync(callback)` / `UIPopup<T>.CloseView()` với `T` là lớp popup cụ thể.
- `UIPopup.CloseAllPopup()` đóng toàn bộ popup đang mở.

### B2.3. Screen: mở / lịch sử

- `UIScreen.OpenScreenByTypeAsync(typeof(MyScreen), rememberInHistory)` hoặc overload theo type.
- Có stack **History** khi `rememberInHistory == true` (ví dụ điều hướng “back”).

### B2.4. Chọn animation (Default / Custom)

`UIBaseView` (base của Popup/Screen) có:

- **`UIAnimationMode.Default`** — dùng `UIDefaultAnimation` (sequence DOTween mặc định, phù hợp không cần tuỳ biến).
- **`UIAnimationMode.Custom`** — chỉ định **`animationTypeName`** là **Assembly Qualified Name** của một component implement `IAnimation` kế thừa `UIBaseAnimation` (ví dụ `UIFadeAnimation`, `UIScaleAnimation`, `UIMoveAnimation`, … trong `TransitionUtils/Animation`).

Trong Inspector: chọn mode; nếu Custom, gán đúng loại animation — `OnValidate` sẽ đồng bộ component animation trên GameObject.

### B2.5. Tự động cập nhật dữ liệu: `ViewCreatorPostProcessor`

Editor script `Assets/GameUpCore/Runtime/UI/Helpers/ViewCreatorPostProcessor.cs` là **`AssetPostprocessor`**:

- Khi import/di chuyển/xóa **file `.prefab`** có đường dẫn chứa segment **`/Popups/`** hoặc **`/Screens/`**, nó schedule refresh.
- Sau đó gọi **`PopupData.SetUp()`** hoặc **`ScreenData.SetUp()`** trên asset load từ Resources (`Data/PopupData`, `Data/ScreenData`).

Nhờ vậy không cần luôn luôn bấm tay: thêm prefab mới đúng folder → danh sách popup/screen trong ScriptableObject được rebuild (miễn là asset `PopupData`/`ScreenData` tồn tại đúng path Resources).

**Lưu ý:** với asset trong `Assets/_MainProject/Data/Singletons/`, post-processor còn có thể kích hoạt đồng bộ **AddressableDataHolder** (xem code).

### B2.6. Nút mở Screen nhanh: `ButtonOpenScreen`

Component **`ButtonOpenScreen`** (`Assets/GameUpCore/Runtime/UI/Screens/ButtonScreen/ButtonOpenScreen.cs`):

- Gắn cùng **Button** (hoặc để trống, `OnValidate` tự tìm `Button` trên object/con).
- Điền **`screenTypeName`** = **tên class** của `UIScreen` (không cần namespace), ví dụ `MainMenuScreen`.
- **`rememberInHistory`**: có đẩy screen hiện tại vào history hay không.
- Khi click: resolve type qua reflection → `UIScreen.OpenScreenByTypeAsync(type, rememberInHistory)`.

**Khi nào dùng:** màn hình và `ScreenData` đã setup sẵn; chỉ cần kéo component vào nút và chọn đúng tên class — không viết script listener tay.

### B2.7. Helper UI có sẵn (trong `Runtime/UI/Helpers`)

- **Loading** — overlay loading (cutout mask, item…).
- **Toast** — thông báo nhẹ.
- **EnhancedScroll** — scroller/cell view.
- **SelectView** — đổi sprite/màu/scale/trạng thái GameObject theo lựa chọn.
- **CustomView** — nhóm nút, lock button, v.v.

Sau **Core setup**, bản copy prefab nằm dưới `_MainProject/Prefabs/UI/Helpers` để chỉnh sửa không sửa trực tiếp package.

---

## B3. Cài UPM (tham chiếu nhanh)

Git URL:

```
https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpCore
```

Hoặc key `com.gameup.core` trong `manifest.json` như mục A3.

---

## B4. Bắt đầu nhanh với template repo (clone)

Dùng repo này làm project gốc:

1. Clone, mở bằng Unity Hub (**2022.3+**).
2. Làm **Bước 1 DOTween** nếu chưa có Modules trong project.
3. Mở scene mẫu (ví dụ `Assets/Scenes/SampleScene.unity`).
4. Chạy **Folder Setup → Create All Folders** nếu `_MainProject` chưa có.
5. **Logger**, **Core setup**, (tuỳ chọn) **Audio**, **Poolers** như checklist.
6. Play để kiểm tra.

---

## B5. Cấu trúc thư mục khuyến nghị

Ngoài `_MainProject` do tool tạo, có thể bổ sung `_Project` cho asset game thuần (xem `doc/unity_conventions.md`).

```
Assets/
  _MainProject/          # sinh bởi GameUp Folder Setup
  GameUpCore/            # hoặc nằm trong Packages khi cài UPM
  _Project/              # tuỳ team (Art, Audio, Scenes, Scripts…)
```

---

## B6. Workflow khuyến nghị

- **Prefab is King**; scene giữ manager / environment.
- **Signal** để decouple.
- **Pool** cho spawn thường xuyên.
- **Release build:** `Disable Logs (Release)`.
- UI Popup/Screen: giữ prefab trong folder Popups/Screens để auto sync data.

---

## B7. Hệ thống cốt lõi (`GameUpCore/Runtime/Core`)

Thư mục `Assets/GameUpCore/Runtime/Core` gồm các module dùng chung.

### 1. Singleton (`Singleton`)

`Singleton<T>`, `MonoSingleton<T>` — ví dụ `AudioManager.Instance`.

### 2. Signal / Event Bus (`Signal`)

Publish/Subscribe: `AddListener`, `AddOnce`, `RemoveListener`, `Dispatch`. Nên gom signal vào class tĩnh (vd. `GameSignals`).

### 3. Object Pools (`ObjectPools`)

`GUPoolers` — spawn/release thay Instantiate/Destroy liên tục.

### 4. Logger (`Logger`)

Dùng **`GULogger`** thay `Debug.Log`; điều khiển bằng define `ENABLE_LOG`.

### 5. Audio (`Audio`)

`AudioManager`, `AudioDatabase`, `AudioIdentity`; có thể gắn Addressables.

### 6. TimeSystem (`TimeSystem`)

`TimeManager` — time scale / slow motion.

### 7. CoroutineRunner (`CoroutineRunner`)

Chạy coroutine từ class không phải `MonoBehaviour`.

### 8. DataHelper (`DataHelper`)

`EncryptUtils`, `FileStorageUtils`, `LocalStorageUtils`, FullSerializer, v.v.

### 9. Extension (`Extension`)

`MonoExtension`, `ListCollectionExtension`, `UIExtension`, … — thường `using GameUp.Core`.

### 10. Utils (`Utils`)

`GameUtils`, `SettingVar<T>`, `TimeUtils`, `StringUtils`, …

---

## B8. Công cụ Editor (`GameUpCore/Editor`)

- **`GameUp → Project → Folder Setup`** — `GUProjectFolderSetupWindow`: folder `_MainProject`, `PopupData`/`ScreenData`, Addressables.
- **`GameUp → Project → Core setup`** — `GUCoreProjectSetup`: copy prefab Core/UI vào scene.
- **`GameUp → Logger → …`** — `GULoggerMenu`.
- **`GameUp → Audio → Setup AudioManager`** — `GUAudioManagerWindow`.
- **`GameUp → Poolers → Setup GUPoolers in Scene`** — `GUPoolersMenu` (tạo/tìm `GUPoolersSingleton`).

---

## B9. Ví dụ code (Signal, Pool, Save, Audio, Time)

> Namespace/class có thể đổi theo phiên bản; tra trực tiếp trong `Assets/GameUpCore/Runtime/` khi cần.

### 1. Signal: Publish/Subscribe decoupling

```csharp
using UnityEngine;
using GameUp.Core;

public struct PlayerDiedSignal
{
    public int KillerId;
}

public static class GameSignals
{
    public static readonly Signal<PlayerDiedSignal> PlayerDied = new Signal<PlayerDiedSignal>();
}

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;

    private int _currentHealth;

    private void Awake()
    {
        _currentHealth = maxHealth;
    }

    public void TakeDamage(int amount, int killerId)
    {
        _currentHealth = Mathf.Max(0, _currentHealth - amount);
        if (_currentHealth == 0)
        {
            GameSignals.PlayerDied.Dispatch(new PlayerDiedSignal { KillerId = killerId });
        }
    }
}
```

```csharp
using UnityEngine;
using GameUp.Core;

public class GameOverUI : MonoBehaviour
{
    private void OnEnable()
    {
        GameSignals.PlayerDied.AddListener(OnPlayerDied);
    }

    private void OnDisable()
    {
        GameSignals.PlayerDied.RemoveListener(OnPlayerDied);
    }

    private void OnPlayerDied(PlayerDiedSignal signal)
    {
        gameObject.Show();
    }
}
```

```csharp
using UnityEngine;
using GameUp.Core;

public class PlayerDeathSfx : MonoBehaviour
{
    private void OnEnable()
    {
        GameSignals.PlayerDied.AddListener(OnPlayerDied);
    }

    private void OnDisable()
    {
        GameSignals.PlayerDied.RemoveListener(OnPlayerDied);
    }

    private void OnPlayerDied(PlayerDiedSignal signal)
    {
        AudioManager.PlaySFX(AudioID.Hit);
    }
}
```

### 2. Object Pool: Spawn/Release prefab

```csharp
using UnityEngine;
using GameUp.Core;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float lifeTime = 2f;

    private float _timeLeft;

    private void OnEnable()
    {
        _timeLeft = lifeTime;
    }

    private void Update()
    {
        _timeLeft -= Time.deltaTime;
        if (_timeLeft <= 0f)
        {
            GUPoolers.Release(gameObject);
        }
    }
}
```

```csharp
using UnityEngine;
using GameUp.Core;

public class Shooter : MonoBehaviour
{
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform muzzleTransform;

    public void Shoot()
    {
        var bullet = GUPoolers.Spawn(bulletPrefab, muzzleTransform.position, muzzleTransform.rotation);
        bullet.gameObject.Show();
    }
}
```

> Nếu chưa có `GUPoolers` trong scene: `GameUp → Poolers → Setup GUPoolers in Scene`.

### 3. LocalStorageUtils: Lưu & đọc dữ liệu (có mã hoá)

```csharp
using System;
using UnityEngine;
using GameUp.Core;

[Serializable]
public class PlayerStats
{
    public int Level;
    public int Damage;
}

public class SaveExample : MonoBehaviour
{
    private const string CoinKey = "player_coin";
    private const string StatsKey = "player_stats";

    public void Save()
    {
        LocalStorageUtils.SetLong(CoinKey, 100);
        LocalStorageUtils.SetObject(StatsKey, new PlayerStats { Level = 3, Damage = 12 });
    }

    public void Load()
    {
        var coin = LocalStorageUtils.GetLong(CoinKey, 0);
        var stats = LocalStorageUtils.GetObject<PlayerStats>(StatsKey, new PlayerStats());

        GULogger.Log($"Coin={coin}, Level={stats.Level}, Damage={stats.Damage}");
    }
}
```

### 4. Audio: Setup + phát SFX/BGM

1. `GameUp → Audio → Setup AudioManager`.
2. Chọn thư mục audio, **Scan & Update**.

```csharp
using UnityEngine;
using GameUp.Core;

public class AudioExample : MonoBehaviour
{
    public void PlayHit()
    {
        AudioManager.PlaySFX(AudioID.Hit);
    }
}
```

### 5. TimeSystem: Slow motion theo tình huống

```csharp
using UnityEngine;
using GameUp.Core;

public class SlowMotionExample : MonoBehaviour
{
    [SerializeField] private float slowTimeScale = 0.2f;
    [SerializeField] private float slowDuration = 0.25f;

    public void PlayPerfectDodge()
    {
        TimeManager.SetTimeScale(slowTimeScale);
        CoroutineRunner.Instance.StartCoroutine(RestoreTime());
    }

    private System.Collections.IEnumerator RestoreTime()
    {
        yield return new WaitForSecondsRealtime(slowDuration);
        TimeManager.SetTimeScale(1f);
    }
}
```
