---
name: gameup-core-api
description: Tra cứu API GameUp Core và mẫu code chuẩn (logger, singleton, signal, pool, UI screen/popup, save, audio, addressables, bootstrap). Dùng trước khi viết bất kỳ class hạ tầng nào, hoặc khi cần biết Core đã có sẵn thứ gì.
---

# GameUp Core — tra API trước khi viết mới

## Bước 0 — mở đúng nguồn

1. **Đọc `.claude/gameup-core/API_INDEX.md` trước.** File tự sinh bằng reflection từ bản Core đang cài: chữ ký
   member thật, bảng *class nền để kế thừa* (member abstract/virtual), prefab có sẵn, asmdef cần reference.
   Bảng tra nhanh bên dưới chỉ để định hướng — chữ ký lấy từ index, không lấy từ trí nhớ.
2. **Grep/Read source** với `path` tường minh (thư mục ẩn `.claude/` có thể bị bỏ qua khi tìm từ gốc):
   - Cài qua Git UPM: `.claude/gameup-core/src/` (bản chép — file thật nằm trong `Library/PackageCache`, bị chặn đọc).
   - Embedded: `Assets/GameUpCore/` hoặc `Packages/com.ohze.gameup.core/`.
   - Dòng đầu `API_INDEX.md` ghi đúng thư mục source của project này.
3. Thiếu `.claude/gameup-core/` hoặc index ghi version khác `Packages/packages-lock.json` → **dừng**, nhờ người dùng
   chạy `GameUp → Project → Sync GameUp source for AI`. Không đoán API khi không đọc được nguồn.

Muốn kế thừa (`UIScreen`, `UIPopup`, `UIBaseView`, `MonoSingleton<T>`, `BaseDataSave<T>`, `BaseSelectView`…) → xem mục
*Class nền để kế thừa* trong index, mở file nguồn để biết override nào phải gọi `base.`.

## Bảng tra nhanh

| Cần | Type | Namespace |
|---|---|---|
| Log | `GULogger` | `GameUp.Core` |
| Singleton Mono | `MonoSingleton<T>` | `GameUp.Core` |
| Singleton C#/SO | `Singleton<T>`, `ScriptableObjectSingleton<T>`, `ResourcesSingleton` | `GameUp.Core` |
| Event type-safe | `Signal`, `BaseSignal`, `IBaseSignal` | `GameUp.Core` |
| Pool | `GUPool`, `GUPoolers`, `IPoolable` | `GameUp.Core` |
| Save | `BaseDataSave<T>`, `LocalStorageUtils`, `FileStorageUtils`, `JsonHelper`, `EncryptUtils` | `GameUp.Core` |
| Giá trị đơn persist | `SettingVar` (`BooleanVar`/`IntVar`/`FloatVar`/`LongVar`) | `GameUp.Core` |
| Audio | `AudioManager`, `AudioIdentity`, `AudioIdentityReference`, `AudioDatabase`, `AudioSetting`, `AudioCategory`, `AudioHandle` | `GameUp.Core` |
| Preload audio (không phát) | `AudioManager.PreloadAudio`, `IsAudioReady`, `AudioIdentity.preloadClips` | `GameUp.Core` |
| Unload audio (RAM) | `AudioManager.UnloadAudioData`, `ReleaseAudio`, `ReleaseUnusedAudio` | `GameUp.Core` |
| Addressables | `ComponentReference<T>`, `DataReference`, `AddressableDataHolder`, `AddressableLoad` | `GameUp.Core` |
| Coroutine | `CoroutineRunner`, `CoroutineExtension` | `GameUp.Core` |
| Thời gian | `TimeManager`, `TimeUtils`, `ConvertTimeExtension` | `GameUp.Core` |
| Khởi động | `GUBootstrap` | `GameUp.Core` |
| Scene | `GUSceneLoader` | `GameUp.Core` |
| UI view | `UIBaseView`, `UIScreen`, `UIPopup`, `IView`, `IAnimate` | `GameUp.Core.UI` |
| UI data | `ScreenData`, `PopupData`, `UIScreenReference`, `UIPopupReference` | `GameUp.Core.UI` |
| UI animation | `UIBaseAnimation`, `UIDefaultAnimation`, `UIAnimationMode` | `GameUp.Core.UI` |
| Adaptation | `SafeArea`, `MultiResolution` | `GameUp.Core.UI` |
| Helper | `ObjectFinder`, `GameUtils`, `StringUtils`, `UIExtension`, `MonoExtension`, `EnumExtension`, `ListCollectionExtension` | `GameUp.Core` |
| Attribute | `[Button]`, `[ReadOnlyInInspector]` | `GameUp.Core` |
| Level tracking | `LocalLevelTracking`, `ILevelTracking` | `GameUp.Core` |

## Mẫu chuẩn

```csharp
using GameUp.Core;

// Log — KHÔNG dùng UnityEngine.Debug trong code feature
GULogger.Log("Gameplay", $"Enemy chết tại wave {waveIndex}");
GULogger.Warning("Save", "Không đọc được save, dùng mặc định");

// Singleton MonoBehaviour
public class GameController : MonoSingleton<GameController>
{
    protected override bool IsPersistent => true;   // sống xuyên scene
}

// Pool thay cho Instantiate/Destroy
var bullet = GUPoolers.Spawn(bulletPrefab, position, rotation);
GUPoolers.Despawn(bullet);
// object cần reset state khi tái sử dụng thì implement IPoolable (OnSpawn/OnDespawn)

// Save có versioning
public class PlayerSave : BaseDataSave<PlayerSave>
{
    public int level;
    protected override int CurrentVersion => 2;
    protected override void Migrate(int fromVersion) { /* nâng cấp schema cũ */ }
}

// Audio — phát theo identity, giữ handle để dừng riêng SFX loop
var engine = AudioManager.PlayAudio(engineLoop);
engine.Stop(fadeDuration: 0.3f);

// Preload (không phát) để lúc cần phát ngay, không chờ Addressables load.
// Không tự cache AudioClip / tự LoadAssetAsync clip — dùng API này.
AudioManager.PreloadAudio(levelSfxList, onCompleted: StartLevel); // identity | tên | AudioIdentityReference | list
AudioManager.IsAudioReady(hitIdentity);                           // đã phát tức thì được chưa
// Hoặc tick AudioIdentity.preloadClips để preload cùng AudioDatabase; bootstrap chờ bằng:
var audioReady = false;
GUBootstrap.AddStep("Audio", () => AudioManager.PreloadIdentities(() => audioReady = true), () => audioReady);

// Unload tối ưu RAM (clip đang phát / đang load luôn được giữ lại)
AudioManager.UnloadAudioData(hitIdentity); // nhẹ: xả data, giữ asset
AudioManager.ReleaseAudio(bossTheme);      // triệt để: xả data + nhả Addressables
AudioManager.ReleaseUnusedAudio();         // đổi scene / rời màn: nhả mọi clip không dùng

// UI
// Kế thừa bản generic để có sẵn ShopPopup.OpenViewAsync()/CloseView(); override OnOpen/OnClose (xem API_INDEX.md)
public class ShopPopup : UIPopup<ShopPopup> { }
```

*Chữ ký chính xác của `Spawn`/`Despawn`/`CurrentVersion`… có thể khác giữa các version — mở file nguồn xác nhận trước khi dùng.*

## Điều KHÔNG được giả định

- **Không có FSM riêng** trong Core dù `package.json` ghi từ khoá rộng. Chỉ có `Signal`.
- Không có DI container, không có networking, không có save cloud.
- `GameUp.UI.Runtime` cần DOTween + define `DOTween__DEPENDENCIES_INSTALLED`; code tween phải có nhánh `#else` chạy tức thì.

## Ranh giới

- Code game mới → `Assets/_MainProject/Scripts/`, namespace riêng (không phải `GameUp.Core*`).
- Không sửa `Packages/com.ohze.gameup.core/` (bản restore từ registry/Git) và `.claude/gameup-core/` (bản chép tự sinh).
- Trong một project chỉ có **một** nguồn Core: embedded *hoặc* UPM.
