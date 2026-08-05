# GameUp Core

Bộ khung nền cho dự án game Unity: singleton, signal, object pool, UI screen/popup, audio, lưu dữ liệu cục bộ, bootstrap và scene loader.

- Unity tối thiểu: **2022.3**
- Namespace: `GameUp.Core`, `GameUp.Core.UI`
- Assembly: `GameUp.Core.Runtime`, `GameUp.UI.Runtime`, `GameUp.Core.Editor`

---

## Cài đặt

### 1. Cài DOTween trước

Tầng UI dùng DOTween cho animation. Thứ tự bắt buộc:

1. Import DOTween vào `Assets/Plugins/Demigiant`.
2. Mở **GameUp → Project → GameUpCore Installer**, tạo `DOTween.Modules.asmdef` và bật define `DOTween__DEPENDENCIES_INSTALLED`.

Chưa có define thì code animation vẫn biên dịch được (nhánh `#else` chạy tức thì, không tween), nhưng assembly `GameUp.UI.Runtime` tham chiếu `DOTween.Modules` nên **phải có asmdef đó** trước khi Unity biên dịch.

### 2. Cài package

Thêm vào `Packages/manifest.json`:

```json
"com.ohze.gameup.core": "https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpCore"
```

Package phụ thuộc `com.unity.addressables` và `com.unity.textmeshpro` — Package Manager tự kéo về.

### 3. Dựng cấu trúc dự án

- **GameUp → Project → Project folder setup**: tạo cây thư mục `_MainProject`.
- **GameUp → Project → Core setup**: copy prefab `====Manager====` và `=====UI=====` vào `_MainProject/Prefabs/Core` rồi đặt lên scene.
- **GameUp → Project → Install Cursor IDE rules** (tuỳ chọn): cài `com.boxqkrtm.ide.cursor`, chép `.mdc` rules, `.cursorrules`, `.cursorignore` từ `Documentation~`.

---

## Các hệ thống chính

### Singleton

```csharp
public class GameController : MonoSingleton<GameController>
{
    protected override bool IsPersistent => true;   // sống xuyên scene (mặc định false)
}
```

`Instance` tự tìm hoặc tạo instance, trả `null` khi App đang thoát, và tự bỏ instance cũ khi vào Play mới lúc tắt Domain Reload.

### Signal

```csharp
public static readonly Signal<int> OnCoinChanged = new();

OnCoinChanged.AddListener(HandleCoin);   // nhớ RemoveListener khi hủy object
OnCoinChanged.Dispatch(100);
```

### Object pool

```csharp
GUPool.Prewarm(bulletPrefab, 30);            // tạo sẵn ở màn Loading
var bullet = GUPool.Spawn(bulletPrefab, transform);
GUPool.DeSpawn(bullet, 2f);                  // trả về pool sau 2 giây
GUPool.DeSpawnAll(bulletPrefab);
```

Object tái sử dụng **không chạy lại `Awake`/`OnEnable` theo vòng đời mới**, nên hãy reset state qua `IPoolable`:

```csharp
public class Bullet : MonoBehaviour, IPoolable
{
    public void OnSpawn()   => _hp = 3;
    public void OnDespawn() => _tween?.Kill();
}
```

### UI: Screen và Popup

Screen chiếm toàn màn hình và có ngăn xếp Back; Popup chồng lên trên.

```csharp
public class HomeScreen : UIScreen<HomeScreen> { }
public class ShopPopup  : UIPopup<ShopPopup>   { }

HomeScreen.OpenViewAsync(remember: true);
HomeScreen.PreloadViewAsync();
UIScreen.OpenPrevious();

ShopPopup.OpenViewAsync(popup => popup.Setup(data));
ShopPopup.CloseView();
UIPopup.CloseAllPopup();
```

Yêu cầu:
- Prefab đặt trong `_MainProject/Prefabs/UI/Screens` (hoặc `Popups`) và được đánh Addressable.
- Bấm `SetUp` trên `ScreenData` / `PopupData` (nằm ở `Resources/Data/`) sau khi thêm prefab mới.
- Scene phải có `ObjectFinder` khai báo `ScreenHolder` và `PopupHolder` (prefab `=====UI=====` đã có sẵn).

Animation mở/đóng chọn bằng `animationMode` trên `UIBaseView`: `Default` (không tween) hoặc `Custom` + một `UIBaseAnimation` (`UIFadeAnimation`, `UIScaleAnimation`, `UIMoveAnimation`, `UIShowMoveItemAnimation`).

### Audio

```csharp
AudioManager.PlayAudio(identity);                   // SFX/UI, theo category của identity
AudioManager.PlayAudio("Hit_Death");                // theo tên identity đã preload
AudioManager.PlayMusic(identity, fadeDuration: 1f); // crossfade với nhạc đang phát
AudioManager.StopMusic(fadeDuration: 0.5f);

AudioSetting.Instance.SetVolume(AudioCategory.Music, 0.5f);
AudioSetting.Instance.IsSoundOn.Value = false;
```

**Dừng âm thanh đang phát** — hai cách, chọn theo nhu cầu:

```csharp
// 1. Dừng đúng một lần phát: giữ handle. Bắt buộc với SFX loop khi có nhiều instance cùng lúc.
var engine = AudioManager.PlayAudio(engineLoop);
engine.Stop(fadeDuration: 0.3f);
if (engine.IsPlaying) { }

// 2. Dừng theo ID khi không giữ handle: dừng MỌI lần phát của identity đó.
AudioManager.StopAudio(identity);
AudioManager.StopAudio("Ambient_Rain", fadeDuration: 1f);
AudioManager.IsPlaying("Ambient_Rain");

AudioManager.StopAllSfx();   // dừng hết SFX, không đụng nhạc nền
```

Gọi `Stop` ngay sau `Play` vẫn đúng: lần phát bị hủy kể cả khi clip còn đang load Addressable.

`AudioIdentity` khai báo danh sách clip, `category` (Sfx / Ui / Music), `volume` riêng và `isLoop`.
Volume thực tế = `volume của identity × volume của kênh`, và mọi source đang phát được cập nhật ngay khi người chơi kéo thanh âm lượng.

Khai báo `AudioDatabase` trên `AudioManager` để preload identity lúc `Awake`.

### Lưu dữ liệu

```csharp
public class PlayerData : BaseDataSave<PlayerData>
{
    public int coin;
    public int gem;

    protected override int Version => 2;                  // tăng khi đổi schema
    protected override void InitDefault() => coin = 100;
    protected override void InitHasKey() { }
    protected override void Migrate(int fromVersion)
    {
        if (fromVersion < 2) gem = 0;                     // field mới ở v2
    }
}

var data = PlayerData.Create();
data.coin += 10;
data.Save();
```

`fromVersion == 0` nghĩa là save được tạo từ bản build trước khi có versioning.

Ngoài ra: `LocalStorageUtils` (PlayerPrefs + AES, an toàn với culture và dữ liệu hỏng) và `FileStorageUtils` (ghi file trong `persistentDataPath`).

> ⚠️ Khoá AES mặc định nằm trong `EncryptUtils` và giống nhau cho mọi dự án dùng package này. Hãy đổi khoá riêng cho từng game trước khi phát hành.

### Bootstrap và Scene loader

```csharp
// Scene Boot
GUBootstrap.AddStep(AddressableDataHolder.Instance);
GUBootstrap.AddStep("Audio", () => AudioManager.PreloadIdentities());
GUBootstrap.OnProgress += (progress, step) => loadingBar.Set(progress, step);
GUBootstrap.Run(() => GUSceneLoader.LoadAsync("MainMenu", minDuration: 1f));
```

Mỗi bước có timeout riêng (mặc định 15s): bước treo bị bỏ qua kèm log lỗi và tên bước nằm trong `GUBootstrap.FailedSteps`, thay vì kẹt vĩnh viễn ở màn Loading.

`GUSceneLoader` phát `OnLoadStarted` / `OnProgress` / `OnLoadCompleted` để màn Loading tự lắng nghe — nó không phụ thuộc tầng UI.

### Log

```csharp
GULogger.Log("Gameplay", "Level start");
GULogger.Error("Gameplay", "Missing config");
GULogger.SetLogLevel(LogLevel.Warning);
```

`Verbose`/`Log`/`Warning` bị strip khỏi build release (chỉ biên dịch khi có `UNITY_EDITOR` hoặc define `ENABLE_LOG`). `Error`/`Exception` **luôn** được biên dịch để crash reporter còn thấy sự cố trên máy thật.

### Tiện ích khác

| Thành phần | Công dụng |
|---|---|
| `CoroutineRunner` | Chạy coroutine từ class không phải MonoBehaviour |
| `MonoExtension`, `CoroutineExtension` | `Show/Hide`, `GetOrAdd`, `Delay`, `DelayFrame`, `WaitUntil` |
| `Loading`, `Toast` | Overlay loading và toast dùng chung |
| `SafeArea`, `MultiResolution` | Thích ứng tai thỏ và tỉ lệ màn hình |
| `TimeManager` | Tăng tốc game độc lập với `Time.timeScale` |
| `[Button]`, `[ReadOnlyInInspector]` | Attribute cho Inspector |
| `EnhancedScroller` | Danh sách cuộn tái sử dụng cell |

---

## Test

Test nằm trong `Tests/Editor` (EditMode) và `Tests/Runtime` (PlayMode), chạy bằng **Window → General → Test Runner**.

---

## Quy ước

- Code game nằm trong `Assets/_MainProject`, **không sửa trực tiếp trong `GameUpCore`** — sửa ở đây nghĩa là sửa package dùng chung cho mọi dự án.
- Prefab và data theo đúng đường dẫn mà `ScreenData` / `PopupData` / `AddressableDataHolder` đang trỏ tới.
