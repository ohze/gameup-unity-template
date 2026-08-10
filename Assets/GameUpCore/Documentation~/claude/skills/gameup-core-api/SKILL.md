---
name: gameup-core-api
description: Tra cứu API GameUp Core và mẫu code chuẩn (logger, singleton, signal, pool, UI screen/popup, save, audio, addressables, bootstrap). Dùng trước khi viết bất kỳ class hạ tầng nào, hoặc khi cần biết Core đã có sẵn thứ gì.
---

# GameUp Core — tra API trước khi viết mới

Nguồn: `Assets/GameUpCore/Runtime/` (embedded) hoặc `Packages/com.ohze.gameup.core/Runtime/` (UPM).
Nếu cần chi tiết chữ ký hàm → **đọc thẳng file nguồn**, đừng đoán.

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

// UI
public class ShopPopup : UIPopup { /* override lifecycle của UIBaseView */ }
```

*Chữ ký chính xác của `Spawn`/`Despawn`/`CurrentVersion`… có thể khác giữa các version — mở file nguồn xác nhận trước khi dùng.*

## Điều KHÔNG được giả định

- **Không có FSM riêng** trong Core dù `package.json` ghi từ khoá rộng. Chỉ có `Signal`.
- Không có DI container, không có networking, không có save cloud.
- `GameUp.UI.Runtime` cần DOTween + define `DOTween__DEPENDENCIES_INSTALLED`; code tween phải có nhánh `#else` chạy tức thì.

## Ranh giới

- Code game mới → `Assets/_MainProject/Scripts/`, namespace riêng (không phải `GameUp.Core*`).
- Không sửa `Packages/com.ohze.gameup.core/` (bản restore từ registry/Git).
- Trong một project chỉ có **một** nguồn Core: embedded *hoặc* UPM.
