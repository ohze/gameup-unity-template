# GameUp Core — API index (tự sinh)

> **Không sửa tay.** Sinh bởi `GameUp → Project → Sync GameUp source for AI` từ assembly thật của `com.ohze.gameup.core` `0.8.2`; lần sync sau sẽ ghi đè.

- Source đọc được: `Assets/GameUpCore/` — cột **File** bên dưới là đường dẫn tương đối so với thư mục này.
- Đường dẫn trong Unity (dùng cho asmdef/AssetDatabase): `Assets/GameUpCore/`.
- Chữ ký lấy bằng reflection → khớp bản đang cài. Hành vi, comment, ví dụ → mở file nguồn.
- Chỉ liệt kê member `public`/`protected` và field `[SerializeField]`. `[Obsolete]` = đừng dùng cho code mới.

## Assembly (asmdef cần reference)

| asmdef | Namespace |
|---|---|
| `GameUp.Core.Runtime` | `GameUp.Core`, `GameUp.Core.Serializer` |
| `GameUp.Runtime.LocalTracking` | `GameUpCore.Runtime.LocalTracking` |
| `GameUp.UI.Runtime` | `GameUp.Core.UI` |

## Class nền để kế thừa

Kế thừa những class này thay vì tự viết lại. **abstract** = bắt buộc override; **virtual** = hook tuỳ chọn (nhớ gọi `base.` nếu class nền có logic).

| Type | Namespace | abstract | virtual | File |
|---|---|---|---|---|
| `AudioManager` | `GameUp.Core` | — | `Awake()`, `IsPersistent` | [Runtime/Core/Audio/AudioManager.cs](Assets/GameUpCore/Runtime/Core/Audio/AudioManager.cs) |
| `BaseDataSave<T>` | `GameUp.Core` | `InitDefault()`, `InitHasKey()` | `Key`, `Version`, `Migrate()` | [Runtime/Core/DataHelper/BaseDataSave.cs](Assets/GameUpCore/Runtime/Core/DataHelper/BaseDataSave.cs) |
| `BaseSignal` | `GameUp.Core` | — | `GetTypes()` | [Runtime/Core/Signal/impl/BaseSignal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/BaseSignal.cs) |
| `BooleanVar` | `GameUp.Core` | — | `Value`, `AddValueWithoutDispatch()` | [Runtime/Core/Utils/SettingVar.cs](Assets/GameUpCore/Runtime/Core/Utils/SettingVar.cs) |
| `CoroutineRunner` | `GameUp.Core` | — | `IsPersistent`, `Awake()` | [Runtime/Core/CoroutineRunner/CoroutineRunner.cs](Assets/GameUpCore/Runtime/Core/CoroutineRunner/CoroutineRunner.cs) |
| `FloatVar` | `GameUp.Core` | — | `Value`, `AddValueWithoutDispatch()` | [Runtime/Core/Utils/SettingVar.cs](Assets/GameUpCore/Runtime/Core/Utils/SettingVar.cs) |
| `GUPoolers` | `GameUp.Core` | — | `Awake()`, `IsPersistent` | [Runtime/Core/ObjectPools/GUPoolers.cs](Assets/GameUpCore/Runtime/Core/ObjectPools/GUPoolers.cs) |
| `IntVar` | `GameUp.Core` | — | `Value`, `AddValueWithoutDispatch()` | [Runtime/Core/Utils/SettingVar.cs](Assets/GameUpCore/Runtime/Core/Utils/SettingVar.cs) |
| `LongVar` | `GameUp.Core` | — | `Value`, `AddValueWithoutDispatch()` | [Runtime/Core/Utils/SettingVar.cs](Assets/GameUpCore/Runtime/Core/Utils/SettingVar.cs) |
| `MonoSingleton<T>` | `GameUp.Core` | — | `IsPersistent`, `Awake()` | [Runtime/Core/Singleton/MonoSingleton.cs](Assets/GameUpCore/Runtime/Core/Singleton/MonoSingleton.cs) |
| `ScriptableObjectSingleton<T>` | `GameUp.Core` | — | — | [Runtime/Core/Singleton/ScriptableObjectSingleton.cs](Assets/GameUpCore/Runtime/Core/Singleton/ScriptableObjectSingleton.cs) |
| `SettingVar<T>` | `GameUp.Core` | `Value`, `AddValueWithoutDispatch()` | — | [Runtime/Core/Utils/SettingVar.cs](Assets/GameUpCore/Runtime/Core/Utils/SettingVar.cs) |
| `Signal` | `GameUp.Core` | — | `GetTypes()` | [Runtime/Core/Signal/impl/Signal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/Signal.cs) |
| `Signal<T>` | `GameUp.Core` | — | `GetTypes()` | [Runtime/Core/Signal/impl/Signal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/Signal.cs) |
| `Signal<T, U>` | `GameUp.Core` | — | `GetTypes()` | [Runtime/Core/Signal/impl/Signal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/Signal.cs) |
| `Signal<T, U, V>` | `GameUp.Core` | — | `GetTypes()` | [Runtime/Core/Signal/impl/Signal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/Signal.cs) |
| `Signal<T, U, V, W>` | `GameUp.Core` | — | `GetTypes()` | [Runtime/Core/Signal/impl/Signal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/Signal.cs) |
| `TimeManager` | `GameUp.Core` | — | `IsPersistent`, `Awake()` | [Runtime/Core/TimeSystem/TimeManager.cs](Assets/GameUpCore/Runtime/Core/TimeSystem/TimeManager.cs) |
| `BaseChangeActiveView` | `GameUp.Core.UI` | `IsActive`, `ChangeView()` | — | [Runtime/UI/Helpers/SelectView/BaseChangeActiveView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/BaseChangeActiveView.cs) |
| `BaseSelectView` | `GameUp.Core.UI` | `ChangeSelect()` | — | [Runtime/UI/Helpers/SelectView/BaseSelectView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/BaseSelectView.cs) |
| `ChangeActiveGameObject` | `GameUp.Core.UI` | — | `ChangeSelect()` | [Runtime/UI/Helpers/SelectView/Change/ChangeActiveGameObject.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeActiveGameObject.cs) |
| `ChangeAndMoveTransform` | `GameUp.Core.UI` | — | `ChangeSelect()` | [Runtime/UI/Helpers/SelectView/Change/ChangeAndMoveTransform.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeAndMoveTransform.cs) |
| `ChangeGameObjectsActive` | `GameUp.Core.UI` | — | `IsActive`, `ChangeView()` | [Runtime/UI/Helpers/SelectView/Change/ChangeGameObjectsActive.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeGameObjectsActive.cs) |
| `ChangeGraphicColorView` | `GameUp.Core.UI` | — | `ChangeSelect()` | [Runtime/UI/Helpers/SelectView/Change/ChangeGraphicColorView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeGraphicColorView.cs) |
| `ChangeImageScaleView` | `GameUp.Core.UI` | — | `ChangeSelect()` | [Runtime/UI/Helpers/SelectView/Change/ChangeImageScaleView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeImageScaleView.cs) |
| `ChangeImageSpriteView` | `GameUp.Core.UI` | — | `ChangeSelect()` | [Runtime/UI/Helpers/SelectView/Change/ChangeImageSpriteView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeImageSpriteView.cs) |
| `ChangeSpriteResizeTweenNav` | `GameUp.Core.UI` | — | `ChangeSelect()` | [Runtime/UI/Helpers/SelectView/Change/ChangeSpriteResizeTweenNav.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeSpriteResizeTweenNav.cs) |
| `ChangeSpriteResizeTweenNav2` | `GameUp.Core.UI` | — | `ChangeSelect()` | [Runtime/UI/Helpers/SelectView/Change/ChangeSpriteResizeTweenNav2.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeSpriteResizeTweenNav2.cs) |
| `EnhancedScrollerCellView` | `GameUp.Core.UI` | — | `RefreshCellView()` | [Runtime/UI/Helpers/EnhancedScroll/EnhancedScrollerCellView.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScrollerCellView.cs) |
| `Loading` | `GameUp.Core.UI` | — | `IsPersistent`, `Awake()` | [Runtime/UI/Helpers/Loading/Loading.cs](Assets/GameUpCore/Runtime/UI/Helpers/Loading/Loading.cs) |
| `LoadingItem` | `GameUp.Core.UI` | — | `ResetVisualStateForPool()`, `PlayIntro()`, `StopIntroTweens()`, `Close()`, `OnDisable()` | [Runtime/UI/Helpers/Loading/LoadingItem.cs](Assets/GameUpCore/Runtime/UI/Helpers/Loading/LoadingItem.cs) |
| `LoadingItemCutoutMask` | `GameUp.Core.UI` | — | `ResetVisualStateForPool()`, `PlayIntro()`, `StopIntroTweens()`, `Close()`, `OnDisable()` | [Runtime/UI/Helpers/Loading/LoadingItemCutoutMask.cs](Assets/GameUpCore/Runtime/UI/Helpers/Loading/LoadingItemCutoutMask.cs) |
| `LoadingOverlayBase` | `GameUp.Core.UI` | `PlayIntro()`, `StopIntroTweens()` | `Close()`, `ResetVisualStateForPool()`, `OnDisable()` | [Runtime/UI/Helpers/Loading/LoadingOverlayBase.cs](Assets/GameUpCore/Runtime/UI/Helpers/Loading/LoadingOverlayBase.cs) |
| `ObjectFinder` | `GameUp.Core.UI` | — | `Awake()`, `IsPersistent` | [Runtime/UI/Helpers/ObjectFinder.cs](Assets/GameUpCore/Runtime/UI/Helpers/ObjectFinder.cs) |
| `Toast` | `GameUp.Core.UI` | — | `IsPersistent`, `Awake()` | [Runtime/UI/Helpers/Toast/Toast.cs](Assets/GameUpCore/Runtime/UI/Helpers/Toast/Toast.cs) |
| `UIBaseAnimation` | `GameUp.Core.UI` | — | `OnValidate()`, `OnStart()`, `OnReverse()`, `OnStop()` | [Runtime/UI/TransitionUtils/UIBaseAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/UIBaseAnimation.cs) |
| `UIBaseView` | `GameUp.Core.UI` | — | `Awake()`, `OnValidate()`, `OnOpen()`, `OnClose()` | [Runtime/UI/BaseView/UIBaseView.cs](Assets/GameUpCore/Runtime/UI/BaseView/UIBaseView.cs) |
| `UIDefaultAnimation` | `GameUp.Core.UI` | — | `OnStart()`, `OnReverse()`, `OnStop()` | [Runtime/UI/TransitionUtils/UIDefaultAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/UIDefaultAnimation.cs) |
| `UIFadeAnimation` | `GameUp.Core.UI` | — | `OnStart()`, `OnReverse()`, `OnValidate()`, `OnStop()` | [Runtime/UI/TransitionUtils/Animation/UIFadeAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/Animation/UIFadeAnimation.cs) |
| `UIMoveAnimation` | `GameUp.Core.UI` | — | `OnStart()`, `OnReverse()`, `OnValidate()`, `OnStop()` | [Runtime/UI/TransitionUtils/Animation/UIMoveAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/Animation/UIMoveAnimation.cs) |
| `UIPopup` | `GameUp.Core.UI` | — | `Awake()`, `OnOpen()`, `OnClose()`, `OnValidate()` | [Runtime/UI/Popups/UIPopup.cs](Assets/GameUpCore/Runtime/UI/Popups/UIPopup.cs) |
| `UIPopup<T>` | `GameUp.Core.UI` | — | `Awake()`, `OnOpen()`, `OnClose()`, `OnValidate()` | [Runtime/UI/Popups/UIPopup.cs](Assets/GameUpCore/Runtime/UI/Popups/UIPopup.cs) |
| `UIScaleAnimation` | `GameUp.Core.UI` | — | `OnStart()`, `OnReverse()`, `OnValidate()`, `OnStop()` | [Runtime/UI/TransitionUtils/Animation/UIScaleAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/Animation/UIScaleAnimation.cs) |
| `UIScreen` | `GameUp.Core.UI` | — | `Awake()`, `OnOpen()`, `OnClose()`, `OnValidate()` | [Runtime/UI/Screens/UIScreen.cs](Assets/GameUpCore/Runtime/UI/Screens/UIScreen.cs) |
| `UIScreen<T>` | `GameUp.Core.UI` | — | `Awake()`, `OnOpen()`, `OnClose()`, `OnValidate()` | [Runtime/UI/Screens/UIScreen.cs](Assets/GameUpCore/Runtime/UI/Screens/UIScreen.cs) |
| `UIShowMoveItemAnimation` | `GameUp.Core.UI` | — | `OnValidate()`, `OnStart()`, `OnReverse()`, `OnStop()` | [Runtime/UI/TransitionUtils/Animation/UIShowMoveItemAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/Animation/UIShowMoveItemAnimation.cs) |

## Prefab có sẵn trong package

Không chép sang thư mục source (file YAML lớn). Prefab trong package cài qua UPM là read-only — dùng tool setup của package (hoặc Prefab Variant trong `_MainProject`), không sửa bản gốc.

- `Assets/GameUpCore/Prefab/Core/=====UI=====.prefab`
- `Assets/GameUpCore/Prefab/Core/====Manager====.prefab`
- `Assets/GameUpCore/Prefab/UI/Loading/Loading.prefab`
- `Assets/GameUpCore/Prefab/UI/Loading/LoadingItem.prefab`
- `Assets/GameUpCore/Prefab/UI/Loading/LoadingItemCutoutMask.prefab`
- `Assets/GameUpCore/Prefab/UI/Toast/Toast.prefab`
- `Assets/GameUpCore/Prefab/UI/Toast/ToastItem.prefab`

## Runtime/Core/Attributes

### `public sealed class ButtonAttribute : Attribute`

`GameUp.Core` · [Runtime/Core/Attributes/ButtonAttribute.cs](Assets/GameUpCore/Runtime/Core/Attributes/ButtonAttribute.cs)

> Đánh dấu method để hiển thị nút trên Inspector (chỉ trong Editor), gọi method khi bấm. Tương tự [Button] của Odin Inspector. Method có kiểu trả về khác void: Inspector hiển thị kết quả sau khi bấm. Kiểu gắn `SerializableAttribute` được vẽ dạng field mở rộng (giống property trong Inspector).

- `public string Name { get; }`
- `public int Height { get; }`
- `public ButtonAttribute(string name)`
- `public ButtonAttribute(string name, int height)`

### `public sealed class ReadOnlyInInspectorAttribute : PropertyAttribute`

`GameUp.Core` · [Runtime/Core/Attributes/ReadOnlyInInspectorAttribute.cs](Assets/GameUpCore/Runtime/Core/Attributes/ReadOnlyInInspectorAttribute.cs)

## Runtime/Core/Audio

### `public enum AudioCategory`

`GameUp.Core` · [Runtime/Core/Audio/AudioCategory.cs](Assets/GameUpCore/Runtime/Core/Audio/AudioCategory.cs)

> Nhóm âm thanh, quyết định lấy volume và mute từ kênh nào trong `AudioSetting`. `Sfx` và `Ui` dùng chung kênh Sound; `Music` có kênh riêng.

- `Sfx, Ui, Music`

### `public class AudioClipReference : AssetReferenceT<AudioClip>`

`GameUp.Core` · [Runtime/Core/Audio/AudioClipReference.cs](Assets/GameUpCore/Runtime/Core/Audio/AudioClipReference.cs)

- `public AudioClipReference(string guid)`

### `public class AudioDatabase : ScriptableObject`

`GameUp.Core` · [Runtime/Core/Audio/AudioDatabase.cs](Assets/GameUpCore/Runtime/Core/Audio/AudioDatabase.cs)

- `public List<AudioIdentityReference> identityReferences`

### `public struct AudioHandle`

`GameUp.Core` · [Runtime/Core/Audio/AudioHandle.cs](Assets/GameUpCore/Runtime/Core/Audio/AudioHandle.cs)

> Tham chiếu tới đúng MỘT lần phát âm thanh, để dừng riêng nó mà không đụng tới các lần phát khác của cùng identity. Cần cho SFX loop (tiếng động cơ, tiếng môi trường...). var engine = AudioManager.PlayAudio(engineLoopIdentity); ... engine.Stop(fadeDuration: 0.3f); Handle là struct và không giữ tham chiếu tới AudioSource: sau khi âm thanh kết thúc, handle đơn giản là hết hiệu lực và mọi thao tác trên nó trở thành no-op.

- `public static AudioHandle None { get; }`
- `public bool IsValid { get; }`
- `public bool IsPlaying { get; }`
- `public void Stop(float fadeDuration = 0f)`

### `public class AudioIdentity : ScriptableObject`

`GameUp.Core` · [Runtime/Core/Audio/AudioIdentity.cs](Assets/GameUpCore/Runtime/Core/Audio/AudioIdentity.cs)

- `public List<AudioClipReference> clipRefs`
- `public AudioCategory category`
- `public float volume`
- `public bool isLoop`
- `public bool preloadClips`

### `public class AudioIdentityReference : AssetReferenceT<AudioIdentity>`

`GameUp.Core` · [Runtime/Core/Audio/AudioIdentityReference.cs](Assets/GameUpCore/Runtime/Core/Audio/AudioIdentityReference.cs)

- `public AudioIdentityReference(string guid)`

### `public class AudioManager : MonoSingleton<AudioManager>`

`GameUp.Core` · [Runtime/Core/Audio/AudioManager.cs](Assets/GameUpCore/Runtime/Core/Audio/AudioManager.cs)

- `[SerializeField] private AudioSource musicSource`
- `[SerializeField] private int maxSource`
- `[SerializeField] private bool preloadIdentityOnAwake`
- `[SerializeField] private AudioDatabase database`
- `[SerializeField] private bool releaseUnusedOnLowMemory`
- `[SerializeField] private float defaultMusicFade`
- `public static AudioIdentity CurrentMusic { get; }`
- `protected override void Awake()`
- `public void RefreshVolumes()`
- `public static void PreloadIdentities(Action onCompleted = null)`
- `public static void PreloadAudio(AudioIdentity identity, Action onCompleted = null)`
- `public static void PreloadAudio(IReadOnlyList<AudioIdentity> identities, Action onCompleted = null)`
- `public static void PreloadAudio(string identityName, Action onCompleted = null)`
- `public static void PreloadAudio(AudioIdentityReference identityReference, Action onCompleted = null)`
- `public static bool IsAudioReady(AudioIdentity identity)`
- `public static bool IsAudioReady(string identityName)`
- `public static void UnloadAudioData(AudioIdentity identity)`
- `public static void UnloadAudioData(string identityName)`
- `public static void ReleaseAudio(AudioIdentity identity)`
- `public static void ReleaseAudio(string identityName)`
- `public static int ReleaseUnusedAudio()`
- `public static bool TryGetIdentity(string identityName, out AudioIdentity identity)`
- `public AudioHandle Play(AudioIdentity identity, bool isRandomClip = false)`
- `public void Play(AudioIdentityReference identityReference)`
- `public static AudioHandle PlayAudio(AudioIdentity identity, bool isRandomClip = false)`
- `public static AudioHandle PlayAudio(string identityName, bool isRandomClip = false)`
- `public static void PlayAudio(AudioIdentityReference identityReference)`
- `public static void StopHandle(int handleId, float fadeDuration = 0f)`
- `public static bool IsHandlePlaying(int handleId)`
- `public static void StopAudio(AudioIdentity identity, float fadeDuration = 0f)`
- `public static void StopAudio(string identityName, float fadeDuration = 0f)`
- `public static bool IsPlaying(AudioIdentity identity)`
- `public static bool IsPlaying(string identityName)`
- `public static void StopAllSfx()`
- `public static void PlayMusic(AudioIdentity identity, float fadeDuration = -1f)`
- `public static void StopMusic(float fadeDuration = 0f)`
- `public static void PauseMusic()`
- `public static void ResumeMusic()`

### `public class AudioSetting : Singleton<AudioSetting>`

`GameUp.Core` · [Runtime/Core/Audio/AudioSetting.cs](Assets/GameUpCore/Runtime/Core/Audio/AudioSetting.cs)

> Cài đặt âm thanh lưu vào bộ nhớ cục bộ: bật/tắt và độ lớn cho từng kênh. Mọi thay đổi đều dispatch signal, `AudioManager` nghe để cập nhật ngay các source đang phát.

- `public const string SoundKey`
- `public const string MusicKey`
- `public const string SoundVolumeKey`
- `public const string MusicVolumeKey`
- `public readonly BooleanVar IsMusicOn`
- `public readonly BooleanVar IsSoundOn`
- `public readonly FloatVar MusicVolume`
- `public readonly FloatVar SoundVolume`
- `public float GetVolume(AudioCategory category)`
- `public bool IsOn(AudioCategory category)`
- `public FloatVar GetVolumeVar(AudioCategory category)`
- `public void SetVolume(AudioCategory category, float value)`

### `public class BaseAudio`

`GameUp.Core` · [Runtime/Core/Audio/AudioManager.cs](Assets/GameUpCore/Runtime/Core/Audio/AudioManager.cs)

- `public AudioClipReference clipRef`
- `public AudioCategory category`
- `public float volume`
- `public bool isLoop`
- `public void PlayClip(AudioSource source)`
- `public void StopAudio()`

## Runtime/Core/Bootstrap

### `public static class GUBootstrap`

`GameUp.Core` · [Runtime/Core/Bootstrap/GUBootstrap.cs](Assets/GameUpCore/Runtime/Core/Bootstrap/GUBootstrap.cs)

> Chạy các bước khởi tạo theo đúng thứ tự đăng ký và báo tiến độ ra ngoài. Mỗi bước có timeout riêng: bước treo sẽ bị bỏ qua kèm log lỗi thay vì làm kẹt cả game ở màn Loading. Dùng ở scene Boot: GUBootstrap.AddStep(AddressableDataHolder.Instance); GUBootstrap.AddStep("Audio", () => AudioManager.PreloadIdentities()); GUBootstrap.OnProgress += (p, step) => loadingBar.Set(p, step); GUBootstrap.Run(() => GUSceneLoader.LoadAsync("MainMenu"));

- `public static GUBootstrap.State CurrentState { get; }`
- `public static float Progress { get; }`
- `public static IReadOnlyList<string> FailedSteps { get; }`
- `public static event Action<float, string> OnProgress`
- `public static event Action OnCompleted`
- `public static void AddStep(IInitial service, string name = null, float timeout = 15f)`
- `public static void AddStep(string name, Action begin, Func<bool> isDone = null, float timeout = 15f)`
- `public static void Run(Action onCompleted = null)`

### `public enum GUBootstrap.State`

`GameUp.Core` · [Runtime/Core/Bootstrap/GUBootstrap.cs](Assets/GameUpCore/Runtime/Core/Bootstrap/GUBootstrap.cs)

- `Idle, Running, Done`

## Runtime/Core/CoroutineRunner

### `public class CoroutineRunner : MonoSingleton<CoroutineRunner>`

`GameUp.Core` · [Runtime/Core/CoroutineRunner/CoroutineRunner.cs](Assets/GameUpCore/Runtime/Core/CoroutineRunner/CoroutineRunner.cs)

- `public static Coroutine RunCoroutineWithReturn(IEnumerator ie)`
- `public static void RunCoroutineWithoutReturn(IEnumerator ie)`
- `public static void StopIEnumerator(Coroutine ct)`
- `public static void StopAll()`

## Runtime/Core/DataHelper

### `public abstract class BaseDataSave<T> where T : BaseDataSave<T>, new()`

`GameUp.Core` · [Runtime/Core/DataHelper/BaseDataSave.cs](Assets/GameUpCore/Runtime/Core/DataHelper/BaseDataSave.cs)

> Lớp cơ sở cho dữ liệu lưu cục bộ, có đánh version để nâng cấp save cũ khi đổi cấu trúc dữ liệu. public class PlayerData : BaseDataSave&lt;PlayerData&gt; { public int coin; public int gem; protected override int Version => 2; // tăng mỗi lần đổi schema protected override void InitDefault() => coin = 100; protected override void InitHasKey() { } protected override void Migrate(int fromVersion) { if (fromVersion &lt; 2) gem = 0; // field mới thêm ở v2 } }

- `public int dataVersion`
- `protected virtual string Key { get; }`
- `protected virtual int Version { get; }`
- `protected abstract void InitDefault()`
- `protected abstract void InitHasKey()`
- `protected virtual void Migrate(int fromVersion)`
- `public static T Create()`
- `public static T CreateWithInit(Action<T> initCallback)`
- `public void Save()`

### `public static class EncryptUtils`

`GameUp.Core` · [Runtime/Core/DataHelper/EncryptUtils.cs](Assets/GameUpCore/Runtime/Core/DataHelper/EncryptUtils.cs)

- `public static string Encrypt(string plainText)`
- `public static string Decrypt(string cipherText)`

### `public static class FileStorageUtils`

`GameUp.Core` · [Runtime/Core/DataHelper/FileStorageUtils.cs](Assets/GameUpCore/Runtime/Core/DataHelper/FileStorageUtils.cs)

- `public static void SaveData<T>(string key, T data, bool encrypt = true)`
- `public static T LoadData<T>(string key, bool isEncrypted = true)`

### `public static class JsonHelper`

`GameUp.Core` · [Runtime/Core/DataHelper/JsonHelper.cs](Assets/GameUpCore/Runtime/Core/DataHelper/JsonHelper.cs)

- `public static List<T> FromJson<T>(string json)`
- `public static string ToJson<T>(List<T> array)`
- `public static string ToJson<T>(List<T> array, bool prettyPrint)`

### `public static class LocalStorageUtils`

`GameUp.Core` · [Runtime/Core/DataHelper/LocalStorageUtils.cs](Assets/GameUpCore/Runtime/Core/DataHelper/LocalStorageUtils.cs)

> Bọc PlayerPrefs với mã hóa. Mọi getter đều fail-safe: dữ liệu hỏng/đổi format sẽ trả về giá trị mặc định thay vì ném exception làm crash lúc khởi động. Số luôn đọc/ghi theo InvariantCulture để không phụ thuộc ngôn ngữ máy.

- `public static bool HasKey(string key)`
- `public static string GetString(string key, string defaultStr = "")`
- `public static void SetString(string key, string value)`
- `public static void SetInt(string key, int value)`
- `public static int GetInt(string key, int d = 0)`
- `public static void SetLong(string key, long value)`
- `public static long GetLong(string key, long d = 0)`
- `public static void SetFloat(string key, float value)`
- `public static float GetFloat(string key, float d = 0f)`
- `public static string GetDeviceID()`
- `public static void SetDeviceID(string id)`
- `public static void SetBoolean(string key, bool v)`
- `public static bool GetBoolean(string key, bool v = false)`
- `public static void SetObject<T>(string key, T obj)`
- `public static T GetObject<T>(string key, T defaultValue = null)`

## Runtime/Core/DataHelper/FullSerializerJson

### `public class BinarySerializer : ISerializer`

`GameUp.Core.Serializer` · [Runtime/Core/DataHelper/FullSerializerJson/BinarySerializer.cs](Assets/GameUpCore/Runtime/Core/DataHelper/FullSerializerJson/BinarySerializer.cs)

- `public Encoding Encoding { get; set; }`
- `public string Serialize<T>(T obj)`
- `public T Deserialize<T>(string data)`

### `public interface ISerializer`

`GameUp.Core.Serializer` · [Runtime/Core/DataHelper/FullSerializerJson/ISerializer.cs](Assets/GameUpCore/Runtime/Core/DataHelper/FullSerializerJson/ISerializer.cs)

- `Encoding Encoding { get; set; }`
- `string Serialize<T>(T obj)`
- `T Deserialize<T>(string data)`

### `public static class JsonExtension`

`GameUp.Core.Serializer` · [Runtime/Core/DataHelper/FullSerializerJson/JsonExtension.cs](Assets/GameUpCore/Runtime/Core/DataHelper/FullSerializerJson/JsonExtension.cs)

- `public static void Dump(this object obj)`
- `public static string Serialize(this object obj)`
- `public static T Deserialize<T>(this string data)`

### `public class JsonSerializer : ISerializer`

`GameUp.Core.Serializer` · [Runtime/Core/DataHelper/FullSerializerJson/JsonSerializer.cs](Assets/GameUpCore/Runtime/Core/DataHelper/FullSerializerJson/JsonSerializer.cs)

- `public Encoding Encoding { get; set; }`
- `public string Serialize<T>(T obj)`
- `public T Deserialize<T>(string data)`

### `public class NullSerializer : ISerializer`

`GameUp.Core.Serializer` · [Runtime/Core/DataHelper/FullSerializerJson/NullSerializer.cs](Assets/GameUpCore/Runtime/Core/DataHelper/FullSerializerJson/NullSerializer.cs)

- `public Encoding Encoding { get; set; }`
- `public string Serialize<T>(T obj)`
- `public T Deserialize<T>(string data)`

### `public class XmlSerializer : ISerializer`

`GameUp.Core.Serializer` · [Runtime/Core/DataHelper/FullSerializerJson/XmlSerializer.cs](Assets/GameUpCore/Runtime/Core/DataHelper/FullSerializerJson/XmlSerializer.cs)

- `public Encoding Encoding { get; set; }`
- `public string Serialize<T>(T obj)`
- `public T Deserialize<T>(string data)`

## Runtime/Core/Extension

### `public static class ConvertTimeExtension`

`GameUp.Core` · [Runtime/Core/Extension/ConvertTimeExtension.cs](Assets/GameUpCore/Runtime/Core/Extension/ConvertTimeExtension.cs)

- `public static DateTime ToDateTime(this long unixSeconds)`
- `public static long ToUnixTimestamp(this DateTime date)`
- `public static long CurrentUnixTimestamp()`
- `public static string ToDurationString(this float seconds)`
- `public static string ToAbbreviatedString(this float seconds)`
- `public static bool IsExpired(this long targetUnixTimestamp)`
- `public static string GetTimeRemaining(this long targetUnixTimestamp)`
- `public static string ToReadableTime(this int totalSeconds)`
- `public static string ToTimerFormat(this float seconds)`

### `public static class CoroutineExtension`

`GameUp.Core` · [Runtime/Core/Extension/CoroutineExtension.cs](Assets/GameUpCore/Runtime/Core/Extension/CoroutineExtension.cs)

- `public static Coroutine DelayFrame(this MonoBehaviour mono, Action callback)`
- `public static Coroutine Delay(this MonoBehaviour mono, float seconds, Action callback)`
- `public static Coroutine DelayUnscaled(this MonoBehaviour mono, float seconds, Action callback)`
- `public static Coroutine WaitUntil(this MonoBehaviour mono, Func<bool> predicate, Action callback)`
- `public static Coroutine WaitEndOfFrame(this MonoBehaviour mono, Action callback)`

### `public static class EnumExtension`

`GameUp.Core` · [Runtime/Core/Extension/EnumExtension.cs](Assets/GameUpCore/Runtime/Core/Extension/EnumExtension.cs)

- `public static T[] GetValues<T>()`
- `public static T GetRandom<T>()`
- `public static T GetRandom<T>(params T[] excludedValues)`
- `public static T Next<T>(this T src)`
- `public static T ToEnum<T>(this string value, T defaultValue = default)`
- `public static bool IsDefined<T>(string value)`

### `public static class ListCollectionExtension`

`GameUp.Core` · [Runtime/Core/Extension/ListCollectionExtension.cs](Assets/GameUpCore/Runtime/Core/Extension/ListCollectionExtension.cs)

- `public static List<T> Split<T>(this List<T> ls, int start, int length)`
- `public static List<T> Splice<T>(this List<T> list, int offset, int count)`
- `public static List<T> SpliceGetLast<T>(this List<T> ls, int count)`
- `public static void SetCount<T>(this List<T> list, int count, T defaulValue)`
- `public static List<T> Clone<T>(this List<T> list)`
- `public static List<T> GetClone<T>(this List<T> list)`
- `public static int SumRange(this List<int> ls, int index)`
- `public static T[] Clone<T>(this T[] list)`
- `public static void Shuffle<T>(this IList<T> list)`
- `public static void Shuffle<T>(this List<T> list)`
- `public static T GetRandom<T>(this List<T> list)`
- `public static T GetRandom<T>(this T[] arr)`
- `public static List<T> GetRandomListWithoutDuplicate<T>(this List<T> list, int count)`
- `public static List<T> GetRandomUniqueElements<T>(List<T> list, int numberOfElements)`

### `public static class MonoExtension`

`GameUp.Core` · [Runtime/Core/Extension/MonoExtension.cs](Assets/GameUpCore/Runtime/Core/Extension/MonoExtension.cs)

- `public static T Cast<T>(this object o)`
- `public static void Hide(this GameObject go)`
- `public static void Show(this GameObject go)`
- `public static void Hide(this Component c)`
- `public static void Show(this Component c)`
- `public static void Reset(this Transform t, Transform parent = null)`
- `public static void SetPosX(this Transform t, float x)`
- `public static void SetPosY(this Transform t, float y)`
- `public static T GetOrAdd<T>(this GameObject go)`
- `public static void SetLayerRecursive(this GameObject go, int layer)`
- `public static void RotateTarget(this Transform a, Transform target, float speed)`

### `public static class UIExtension`

`GameUp.Core` · [Runtime/Core/Extension/UIExtension.cs](Assets/GameUpCore/Runtime/Core/Extension/UIExtension.cs)

- `public static void SetAnchor(this Image img, Vector2 anchor)`
- `public static void ChangeAlpha(this Graphic graphic, float a)`
- `public static void ChangeAnchorX(this RectTransform rect, float x)`
- `public static void ChangeAnchorY(this RectTransform rect, float y)`
- `public static void ChangeSizeX(this RectTransform rect, float x)`
- `public static void ChangeSizeY(this RectTransform rect, float y)`
- `public static void SetLeft(this RectTransform rt, float left)`
- `public static void SetRight(this RectTransform rt, float right)`
- `public static void SetFullStretch(this RectTransform rt)`
- `public static void SetAnchorCenter(this RectTransform rt)`
- `public static void RotateTarget(this RectTransform a, Transform target, float speed)`
- `public static void SetScaleByViewSize(this Image im, Sprite sp, float viewSize)`
- `public static void SetRaycastable(this GameObject go, bool enabled)`
- `public static void ChangeColor(this Graphic graphic, Color newColor, bool keepAlpha = true)`
- `public static void ScrollToTop(this ScrollRect scrollRect)`
- `public static void ScrollToBottom(this ScrollRect scrollRect)`
- `public static void OnClick(this Button btn, UnityAction action)`
- `public static bool IsPointInside(this RectTransform rect, Vector2 screenPoint, Camera cam = null)`
- `public static void SetNativeSizeWithMaxWidth(this Image img, float maxWidth)`

## Runtime/Core/Logger

### `public static class GULogger`

`GameUp.Core` · [Runtime/Core/Logger/GULogger.cs](Assets/GameUpCore/Runtime/Core/Logger/GULogger.cs)

> Log có phân cấp. Verbose/Log/Warning bị strip khỏi build release (chỉ biên dịch khi có UNITY_EDITOR hoặc ENABLE_LOG) nên chuỗi truyền vào cũng không bị dựng — không tốn GC trên máy thật. Error/Exception LUÔN được biên dịch để crash reporter (Crashlytics...) còn thấy sự cố trên device; muốn tắt hẳn thì set `MinLevel` = `LogLevel.None`.

- `public static LogLevel MinLevel { get; set; }`
- `public static void SetLogLevel(LogLevel level)`
- `public static bool IsLoggable(LogLevel level)`
- `public static void Log(string tag, string message)`
- `public static void Log(string message)`
- `public static void Verbose(string tag, string message)`
- `public static void Warning(string tag, string message)`
- `public static void Error(string tag, string message)`
- `public static void Exception(Exception exception, string tag = "Exception")`

### `public enum LogLevel`

`GameUp.Core` · [Runtime/Core/Logger/GULogger.cs](Assets/GameUpCore/Runtime/Core/Logger/GULogger.cs)

- `Verbose, Info, Warning, Error, None`

## Runtime/Core/ObjectPools

### `public static class GUPool`

`GameUp.Core` · [Runtime/Core/ObjectPools/GUPool.cs](Assets/GameUpCore/Runtime/Core/ObjectPools/GUPool.cs)

- `public static void Prewarm(GameObject prefab, int count)`
- `public static void Prewarm<T>(T prefab, int count)`
- `public static T Spawn<T>(T prefab, Transform parent = null, bool worldPositionStays = false)`
- `public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null)`
- `public static GameObject Spawn(GameObject prefab, Transform parent = null, bool worldPositionStays = false)`
- `public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null, bool worldPositionStays = true)`
- `public static void DeSpawn(Component clone, float delay = 0f)`
- `public static void DeSpawn(GameObject clone, float delay)`
- `public static void DeSpawn(GameObject clone)`
- `public static void DeSpawnAll(GameObject prefab)`
- `public static void DeSpawnAll<T>(T prefab)`

### `public class GUPoolers : MonoSingleton<GUPoolers>`

`GameUp.Core` · [Runtime/Core/ObjectPools/GUPoolers.cs](Assets/GameUpCore/Runtime/Core/ObjectPools/GUPoolers.cs)

> Object pool: lấy/trả object trong O(1) nhờ stack các clone đang rảnh. Mọi clone được quản lý trong `CloneInfo` nên không còn dictionary rác khi đổi scene.

- `protected override void Awake()`
- `public void Prewarm(GameObject prefab, int count)`
- `public T Spawn<T>(T go, Transform parent = null, bool worldPositionStays = false)`
- `public T Spawn<T>(T go, Vector3 position, Quaternion rotation, Transform parent = null)`
- `public GameObject Spawn(GameObject go, Transform parent = null, bool worldPositionStays = false)`
- `public GameObject Spawn(GameObject go, Vector3 position, Quaternion rotation, Transform parent = null)`
- `public void DeSpawn<T>(T go)`
- `public void DeSpawn(GameObject go)`
- `public void DeSpawn<T>(T go, float timeDelay)`
- `public void DeSpawn(GameObject go, float timeDelay)`
- `public void DeSpawnAll<T>(T go)`
- `public void DeSpawnAll(GameObject go)`
- `public void DestroyObject(GameObject go)`
- `public void DestroyObject<T>(T go)`
- `public void Prune()`

### `public interface IPoolable`

`GameUp.Core` · [Runtime/Core/ObjectPools/IPoolable.cs](Assets/GameUpCore/Runtime/Core/ObjectPools/IPoolable.cs)

> Cho object tự reset trạng thái khi được pool lấy ra / trả về. Đặt trên chính clone hoặc bất kỳ component con nào — `GUPoolers` gọi tất cả. Vì object tái sử dụng không chạy lại Awake/OnEnable theo vòng đời mới, mọi state (HP, tween đang chạy, coroutine, sự kiện đã đăng ký) nên được dọn ở đây.

- `void OnSpawn()`
- `void OnDespawn()`

## Runtime/Core/SceneSystem

### `public static class GUSceneLoader`

`GameUp.Core` · [Runtime/Core/SceneSystem/GUSceneLoader.cs](Assets/GameUpCore/Runtime/Core/SceneSystem/GUSceneLoader.cs)

> Load scene bất đồng bộ có tiến độ và có kiểm soát thời điểm kích hoạt. Không phụ thuộc tầng UI: màn Loading chỉ cần lắng nghe `OnLoadStarted` / `OnProgress` / `OnLoadCompleted`.

- `public static bool IsLoading { get; }`
- `public static string LoadingSceneName { get; }`
- `public static float Progress { get; }`
- `public static event Action<string> OnLoadStarted`
- `public static event Action<float> OnProgress`
- `public static event Action<string> OnLoadCompleted`
- `public static void LoadAsync(string sceneName, Action onCompleted = null, float minDuration = 0f, LoadSceneMode mode = LoadSceneMode.Single)`
- `public static void LoadAsync(int buildIndex, Action onCompleted = null, float minDuration = 0f, LoadSceneMode mode = LoadSceneMode.Single)`
- `public static void ReloadCurrent(Action onCompleted = null, float minDuration = 0f)`

## Runtime/Core/Signal/api

### `public interface IBaseSignal`

`GameUp.Core` · [Runtime/Core/Signal/api/IBaseSignal.cs](Assets/GameUpCore/Runtime/Core/Signal/api/IBaseSignal.cs)

- `void Dispatch(object[] args)`
- `void AddListener(Action<IBaseSignal, object[]> callback)`
- `void AddOnce(Action<IBaseSignal, object[]> callback)`
- `void RemoveListener(Action<IBaseSignal, object[]> callback)`
- `List<Type> GetTypes()`

### `public enum SignalExceptionType`

`GameUp.Core` · [Runtime/Core/Signal/api/SignalExceptionType.cs](Assets/GameUpCore/Runtime/Core/Signal/api/SignalExceptionType.cs)

- `COMMAND_VALUE_CONFLICT, COMMAND_VALUE_NOT_FOUND, COMMAND_NULL_INJECTION`

## Runtime/Core/Signal/impl

### `public class BaseSignal : IBaseSignal`

`GameUp.Core` · [Runtime/Core/Signal/impl/BaseSignal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/BaseSignal.cs)

- `public event Action<IBaseSignal, object[]> BaseListener`
- `public event Action<IBaseSignal, object[]> OnceBaseListener`
- `public void Dispatch(object[] args)`
- `public virtual List<Type> GetTypes()`
- `public void AddListener(Action<IBaseSignal, object[]> callback)`
- `public void AddOnce(Action<IBaseSignal, object[]> callback)`
- `public void RemoveListener(Action<IBaseSignal, object[]> callback)`

### `public class Signal : BaseSignal`

`GameUp.Core` · [Runtime/Core/Signal/impl/Signal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/Signal.cs)

- `public event Action Listener`
- `public event Action OnceListener`
- `public void AddListener(Action callback)`
- `public void AddOnce(Action callback)`
- `public void RemoveListener(Action callback)`
- `public override List<Type> GetTypes()`
- `public void Dispatch()`

### `public class SignalException : Exception`

`GameUp.Core` · [Runtime/Core/Signal/impl/SignalException.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/SignalException.cs)

- `public SignalExceptionType type { get; set; }`
- `public SignalException(string message, SignalExceptionType exceptionType)`

### `public class Signal<T> : BaseSignal`

`GameUp.Core` · [Runtime/Core/Signal/impl/Signal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/Signal.cs)

- `public event Action<T> Listener`
- `public event Action<T> OnceListener`
- `public void AddListener(Action<T> callback)`
- `public void AddOnce(Action<T> callback)`
- `public void RemoveListener(Action<T> callback)`
- `public override List<Type> GetTypes()`
- `public void Dispatch(T type1)`

### `public class Signal<T, U> : BaseSignal`

`GameUp.Core` · [Runtime/Core/Signal/impl/Signal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/Signal.cs)

- `public event Action<T, U> Listener`
- `public event Action<T, U> OnceListener`
- `public void AddListener(Action<T, U> callback)`
- `public void AddOnce(Action<T, U> callback)`
- `public void RemoveListener(Action<T, U> callback)`
- `public override List<Type> GetTypes()`
- `public void Dispatch(T type1, U type2)`

### `public class Signal<T, U, V> : BaseSignal`

`GameUp.Core` · [Runtime/Core/Signal/impl/Signal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/Signal.cs)

- `public event Action<T, U, V> Listener`
- `public event Action<T, U, V> OnceListener`
- `public void AddListener(Action<T, U, V> callback)`
- `public void AddOnce(Action<T, U, V> callback)`
- `public void RemoveListener(Action<T, U, V> callback)`
- `public override List<Type> GetTypes()`
- `public void Dispatch(T type1, U type2, V type3)`

### `public class Signal<T, U, V, W> : BaseSignal`

`GameUp.Core` · [Runtime/Core/Signal/impl/Signal.cs](Assets/GameUpCore/Runtime/Core/Signal/impl/Signal.cs)

- `public event Action<T, U, V, W> Listener`
- `public event Action<T, U, V, W> OnceListener`
- `public void AddListener(Action<T, U, V, W> callback)`
- `public void AddOnce(Action<T, U, V, W> callback)`
- `public void RemoveListener(Action<T, U, V, W> callback)`
- `public override List<Type> GetTypes()`
- `public void Dispatch(T type1, U type2, V type3, W type4)`

## Runtime/Core/Singleton

### `public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour`

`GameUp.Core` · [Runtime/Core/Singleton/MonoSingleton.cs](Assets/GameUpCore/Runtime/Core/Singleton/MonoSingleton.cs)

- `public static bool IsInitialized { get; }`
- `protected virtual bool IsPersistent { get; }`
- `public static T Instance { get; }`
- `protected virtual void Awake()`

### `public class ResourcesSingleton<T> : ScriptableObject where T : ScriptableObject`

`GameUp.Core` · [Runtime/Core/Singleton/ResourcesSingleton.cs](Assets/GameUpCore/Runtime/Core/Singleton/ResourcesSingleton.cs)

- `public static T Instance { get; }`

### `public abstract class ScriptableObjectSingleton<T> : ScriptableObject where T : ScriptableObject`

`GameUp.Core` · [Runtime/Core/Singleton/ScriptableObjectSingleton.cs](Assets/GameUpCore/Runtime/Core/Singleton/ScriptableObjectSingleton.cs)

- `public static T Instance { get; }`

### `public class Singleton<T> where T : class, new()`

`GameUp.Core` · [Runtime/Core/Singleton/Singleton.cs](Assets/GameUpCore/Runtime/Core/Singleton/Singleton.cs)

- `public static T Instance { get; }`

## Runtime/Core/TimeSystem

### `public class TimeManager : MonoSingleton<TimeManager>`

`GameUp.Core` · [Runtime/Core/TimeSystem/TimeManager.cs](Assets/GameUpCore/Runtime/Core/TimeSystem/TimeManager.cs)

> Manages game speed independently of Time.timeScale.

- `[SerializeField] private float baseSpeed`
- `[SerializeField] private float boostDuration`
- `public static readonly Signal<float> OnBoostCountdownChanged`
- `public static readonly Signal OnGameSpeedChanged`
- `public static float GameSpeed { get; set; }`
- `public static float BoostTimeRemaining { get; }`
- `public static bool IsBoostActive { get; }`
- `public float BoostDuration { get; }`
- `public static void SetBaseSpeed(float speed)`
- `public static void ActivateBoost()`
- `public static void ActivateBoost(float multiplier)`
- `public static void ToggleX2Speed()`
- `public static void StopBoost()`
- `public static void PauseBoost()`
- `public static void ResumeBoost()`

## Runtime/Core/Utils

### `public class BooleanVar : SettingVar<bool>`

`GameUp.Core` · [Runtime/Core/Utils/SettingVar.cs](Assets/GameUpCore/Runtime/Core/Utils/SettingVar.cs)

- `public override bool Value { get; set; }`
- `public BooleanVar(string key, bool defaultV = true)`
- `public override void AddValueWithoutDispatch(bool newValue)`

### `public class ComponentReference<TComponent> : AssetReference where TComponent : Component`

`GameUp.Core` · [Runtime/Core/Utils/ComponentReference.cs](Assets/GameUpCore/Runtime/Core/Utils/ComponentReference.cs)

- `public ComponentReference(string guid)`
- `public AsyncOperationHandle<TComponent> InstantiateAsync(Vector3 position, Quaternion rotation, Transform parent = null)`
- `public AsyncOperationHandle<TComponent> InstantiateAsync(Transform parent = null, bool instantiateInWorldSpace = false)`
- `public AsyncOperationHandle<TComponent> LoadAssetAsync()`
- `public override bool ValidateAsset(Object obj)`
- `public override bool ValidateAsset(string path)`
- `public void ReleaseInstance(AsyncOperationHandle<TComponent> handle)`

### `public class FloatVar : SettingVar<float>`

`GameUp.Core` · [Runtime/Core/Utils/SettingVar.cs](Assets/GameUpCore/Runtime/Core/Utils/SettingVar.cs)

- `public override float Value { get; set; }`
- `public FloatVar(string key, float defaultV = 0f)`
- `public override void AddValueWithoutDispatch(float newValue)`

### `public class GameTime`

`GameUp.Core` · [Runtime/Core/Utils/TimeUtils.cs](Assets/GameUpCore/Runtime/Core/Utils/TimeUtils.cs)

- `public bool IsFirstSessionAllTime { get; }`
- `public bool IsFirstSessionInDay { get; }`
- `public bool IsFirstLoginInWeek { get; }`
- `public bool IsNewMonth { get; }`
- `public long CurrentTimestamp { get; }`
- `public void Init(DateTime currentUtcTime)`
- `public long SecondsToNextDay()`
- `public long SecondsToNextWeek()`
- `public long SecondsToNextMonth()`

### `public static class GameUtils`

`GameUp.Core` · [Runtime/Core/Utils/GameUtils.cs](Assets/GameUpCore/Runtime/Core/Utils/GameUtils.cs)

- `public static bool IsAndroid { get; }`
- `public static bool IsIOS { get; }`
- `public static bool IsWeb { get; }`
- `public static bool IsEditor { get; }`
- `public static string GetPlatform()`
- `public static string GetDeviceId()`
- `public static string CreateID()`
- `public static string GetVersion()`
- `public static string GetBundleId()`
- `public static List<T> GetAssetList<T>(string path)`
- `public static List<T> GetAssetList<T>(string path, List<T> result)`
- `public static List<GameObject> GetPrefabAssetsWithComponent<TComponent>(string path)`
- `public static List<GameObject> GetPrefabAssetsWithComponent<TComponent>(string path, List<GameObject> result)`
- `public static void SaveAssets(Object target)`
- `public static string GetAssetPath(Object o)`
- `public static string GetGuid(Object o)`
- `public static string RandomString(int length)`
- `public static int GetMaxIndexHasValue(int[] arr, int targetValue)`
- `public static Vector2 GetIntersectionPointCoordinates(Vector2 p1, Vector2 p2, Vector2 v1, Vector2 v2, out bool found)`
- `public static string ConvertTimeSpanStr(TimeSpan timeSpan)`
- `public static string GetSecondStr(int seconds)`
- `public static DateTime UnixTimeStampToDateTime(long unixTimeStamp)`
- `public static long DateTimeToTimeStamp(DateTime dateTime)`
- `public static int GetWeekOfYear(DateTime dateTime)`
- `public static Quaternion ToRotationY(Vector3 from, Vector3 to)`
- `public static Texture2D TakeScreenShot()`

### `public interface IInitial`

`GameUp.Core` · [Runtime/Core/Utils/IInitial.cs](Assets/GameUpCore/Runtime/Core/Utils/IInitial.cs)

- `bool Initialized { get; set; }`
- `void Initialize()`

### `public class IntVar : SettingVar<int>`

`GameUp.Core` · [Runtime/Core/Utils/SettingVar.cs](Assets/GameUpCore/Runtime/Core/Utils/SettingVar.cs)

- `public override int Value { get; set; }`
- `public IntVar(string key, int defaultV = 0)`
- `public override void AddValueWithoutDispatch(int newValue)`

### `public class LongVar : SettingVar<long>`

`GameUp.Core` · [Runtime/Core/Utils/SettingVar.cs](Assets/GameUpCore/Runtime/Core/Utils/SettingVar.cs)

- `public override long Value { get; set; }`
- `public LongVar(string key, long defaultV = 0)`
- `public override void AddValueWithoutDispatch(long newValue)`

### `public class ScreenshotCapture : MonoBehaviour`

`GameUp.Core` · [Runtime/Core/Utils/ScreenshotCapture.cs](Assets/GameUpCore/Runtime/Core/Utils/ScreenshotCapture.cs)

- `[SerializeField] private string fileName`
- `[SerializeField] private bool useCustomSize`
- `[SerializeField] private int width`
- `[SerializeField] private int height`
- `[SerializeField] private int superSize`
- `[SerializeField] private string editorOutputFolder`
- `[SerializeField] private bool appendTimestampToFileName`
- `[SerializeField] private bool enableKeyboardShortcut`
- `[SerializeField] private KeyCode screenshotKey`
- `[SerializeField] private bool requireLeftControl`
- `[SerializeField] private bool requireLeftShift`
- `public void TakeScreenshot()`
- `public void TakeScreenshotInEditor()`

### `public abstract class SettingVar<T> where T : struct`

`GameUp.Core` · [Runtime/Core/Utils/SettingVar.cs](Assets/GameUpCore/Runtime/Core/Utils/SettingVar.cs)

- `public readonly Signal<T> OnValueChange`
- `protected readonly string Key`
- `protected T DefaultValue`
- `protected T? V`
- `public abstract T Value { get; set; }`
- `protected SettingVar(string key, T defaultV = default)`
- `public void UpdateWithoutDispatch(T newValue)`
- `public abstract void AddValueWithoutDispatch(T newValue)`
- `public void Dispatch()`

### `public static class StringUtils`

`GameUp.Core` · [Runtime/Core/Utils/StringUtils.cs](Assets/GameUpCore/Runtime/Core/Utils/StringUtils.cs)

- `public static string FormatMoney(this long v, long max = 1000000000)`
- `public static string FormatMoney(this float v, float max = 1E+09f)`
- `public static string FormatMoneyK(double value, int digit = 2)`
- `public static string FormatTimeOffline(this long second)`
- `public static string ConvertSecondsToMinutesAndSeconds(this long seconds)`
- `public static string ConvertSecondsToTimeFormat(this long seconds)`
- `public static string CheckAvailableUserName(string userName)`
- `public static string ToBold(this string msg)`
- `public static string ToColor(this string msg, string color)`
- `public static string ToColor(this string msg, Color color)`
- `public static string ProcessStringLine(this string msg, int maxLengthInLine)`
- `public static string CutNumberFromString(this string msg, string color)`

### `public static class TimeUtils`

`GameUp.Core` · [Runtime/Core/Utils/TimeUtils.cs](Assets/GameUpCore/Runtime/Core/Utils/TimeUtils.cs)

- `public static GameTime GameTime { get; }`
- `public static void Initialize(DateTime? serverTime = null)`

## Runtime/Core/Utils/Addressables

### `public class AddressableDataHolder : ResourcesSingleton<AddressableDataHolder>, IInitial`

`GameUp.Core` · [Runtime/Core/Utils/Addressables/AddressableDataHolder.cs](Assets/GameUpCore/Runtime/Core/Utils/Addressables/AddressableDataHolder.cs)

- `public List<DataReferenceInfo> dataReferenceInfos`
- `public bool Initialized { get; set; }`
- `public void Initialize()`
- `public T GetData<T>()`
- `public void SetUp()`
- `public void Editor_RebuildReferencesFromFolder()`
- `public static AddressableDataHolder Editor_LoadFromResourcesOrNull()`
- `public static AddressableDataHolder Editor_EnsureAssetExists()`

### `public static class AddressableLoad`

`GameUp.Core` · [Runtime/Core/Utils/Addressables/AddressableLoad.cs](Assets/GameUpCore/Runtime/Core/Utils/Addressables/AddressableLoad.cs)

> Gom một chỗ mẫu lặp đi lặp lại khi dùng Addressables: kiểm tra handle hợp lệ → nếu đã xong thì dùng ngay, chưa xong thì đăng ký Completed, và luôn kiểm tra kết quả trước khi gọi callback.

- `public static bool WhenReady<T>(AsyncOperationHandle<T> handle, Action<T> onLoaded, string tag, string context = null, Action onFailed = null)`

### `public class DataReference : AssetReferenceT<ScriptableObject>`

`GameUp.Core` · [Runtime/Core/Utils/Addressables/DataReference.cs](Assets/GameUpCore/Runtime/Core/Utils/Addressables/DataReference.cs)

- `public DataReference(string guid)`

### `public class DataReferenceInfo`

`GameUp.Core` · [Runtime/Core/Utils/Addressables/AddressableDataHolder.cs](Assets/GameUpCore/Runtime/Core/Utils/Addressables/AddressableDataHolder.cs)

- `public string typeName`
- `public DataReference dataRef`

## Runtime/LocalTracking/Runtime

### `public interface ILevelTracking`

`GameUpCore.Runtime.LocalTracking` · [Runtime/LocalTracking/Runtime/ILevelTracking.cs](Assets/GameUpCore/Runtime/LocalTracking/Runtime/ILevelTracking.cs)

- `void StartLevel(int level)`
- `void WinLevel(int level)`
- `void LoseLevel(int level, string reason)`

### `public class LocalLevelTracking : Singleton<LocalLevelTracking>, ILevelTracking`

`GameUpCore.Runtime.LocalTracking` · [Runtime/LocalTracking/Runtime/LocalLevelTracking.cs](Assets/GameUpCore/Runtime/LocalTracking/Runtime/LocalLevelTracking.cs)

- `public void StartLevel(int level)`
- `public void WinLevel(int level)`
- `public void LoseLevel(int level, string reason)`
- `public void GenerateFakeData()`

### `public class PlayerLevelTracking`

`GameUpCore.Runtime.LocalTracking` · [Runtime/LocalTracking/Runtime/LocalLevelTracking.cs](Assets/GameUpCore/Runtime/LocalTracking/Runtime/LocalLevelTracking.cs)

- `public int level`
- `public int startAttempt`
- `public string reason`
- `public float levelDuration`

### `public class RuntimeLevelTrackingViewer : MonoBehaviour`

`GameUpCore.Runtime.LocalTracking` · [Runtime/LocalTracking/Runtime/RuntimeLevelTrackingViewer.cs](Assets/GameUpCore/Runtime/LocalTracking/Runtime/RuntimeLevelTrackingViewer.cs)

- `[SerializeField] private bool _isViewerEnabled`
- `[SerializeField] private bool _autoBlockInProduction`
- `[SerializeField] private KeyCode _toggleKey`
- `[SerializeField] private int _requiredTouchCount`

## Runtime/UI/Adaptation

### `public class MultiResolution : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Adaptation/MultiResolution.cs](Assets/GameUpCore/Runtime/UI/Adaptation/MultiResolution.cs)

- `[SerializeField] private CanvasScaler canvasScaler`

### `public class SafeArea : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Adaptation/SafeArea.cs](Assets/GameUpCore/Runtime/UI/Adaptation/SafeArea.cs)

- `[SerializeField] private bool includeBottom`
- `[SerializeField] private bool includeTop`

## Runtime/UI/BaseView

### `public interface IAnimate`

`GameUp.Core.UI` · [Runtime/UI/BaseView/IAnimate.cs](Assets/GameUpCore/Runtime/UI/BaseView/IAnimate.cs)

- `void OnOpen()`
- `void OnStop()`
- `void OnClose(Action onComplete = null)`

### `public interface IView`

`GameUp.Core.UI` · [Runtime/UI/BaseView/IView.cs](Assets/GameUpCore/Runtime/UI/BaseView/IView.cs)

- `void Open()`
- `void Close()`

### `public class UIBaseView : MonoBehaviour, IAnimate, IView`

`GameUp.Core.UI` · [Runtime/UI/BaseView/UIBaseView.cs](Assets/GameUpCore/Runtime/UI/BaseView/UIBaseView.cs)

- `[SerializeField] private UIAnimationMode animationMode`
- `[SerializeField] private string animationTypeName`
- `protected IAnimation _anim`
- `protected virtual void Awake()`
- `protected virtual void OnValidate()`
- `public void Open()`
- `public void Close()`
- `public virtual void OnOpen()`
- `public void OnStop()`
- `public virtual void OnClose(Action onComplete = null)`

## Runtime/UI/Helpers

### `public class ObjectFinder : MonoSingleton<ObjectFinder>`

`GameUp.Core.UI` · [Runtime/UI/Helpers/ObjectFinder.cs](Assets/GameUpCore/Runtime/UI/Helpers/ObjectFinder.cs)

- `[SerializeField] private List<ObjectType> objects`
- `protected override void Awake()`
- `public static Transform GetObject(ObjectID t)`

### `public enum ObjectID`

`GameUp.Core.UI` · [Runtime/UI/Helpers/ObjectFinder.cs](Assets/GameUpCore/Runtime/UI/Helpers/ObjectFinder.cs)

- `PopupHolder, ScreenHolder`

### `public class ObjectType`

`GameUp.Core.UI` · [Runtime/UI/Helpers/ObjectFinder.cs](Assets/GameUpCore/Runtime/UI/Helpers/ObjectFinder.cs)

- `public ObjectID type`
- `public Transform transform`

### `public class ViewCreatorPostProcessor : AssetPostprocessor`

`GameUp.Core.UI` · [Runtime/UI/Helpers/ViewCreatorPostProcessor.cs](Assets/GameUpCore/Runtime/UI/Helpers/ViewCreatorPostProcessor.cs)

## Runtime/UI/Helpers/CustomView

### `public class CustomButtonGroup : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Helpers/CustomView/CustomButtonGroup.cs](Assets/GameUpCore/Runtime/UI/Helpers/CustomView/CustomButtonGroup.cs)

- `[SerializeField] private List<CustomSelectButton> buttons`
- `[SerializeField] private int defaultSelectedIndex`
- `public void AddListener(Action<int> callback)`
- `public void RemoveListener(Action<int> callback)`
- `public void SetSelected(int index, bool notify = true)`

### `public class CustomSelectButton : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Helpers/CustomView/CustomSelectButton.cs](Assets/GameUpCore/Runtime/UI/Helpers/CustomView/CustomSelectButton.cs)

- `[SerializeField] private Button btn`
- `[SerializeField] private CustomSelectViews view`
- `public UnityEvent onClick`
- `public bool IsSelect { get; set; }`

### `public class CustomSelectViews : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Helpers/CustomView/CustomSelectViews.cs](Assets/GameUpCore/Runtime/UI/Helpers/CustomView/CustomSelectViews.cs)

- `[SerializeField] private List<BaseSelectView> views`
- `public bool IsSelect { get; set; }`

### `public class LockButton : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Helpers/CustomView/LockButton.cs](Assets/GameUpCore/Runtime/UI/Helpers/CustomView/LockButton.cs)

- `[SerializeField] private Button myBtn`

## Runtime/UI/Helpers/EnhancedScroll

### `public delegate void CellViewInstantiated(EnhancedScroller scroller, EnhancedScrollerCellView cellView)`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> This delegate is called when a cell view is created for the first time (not reused)

### `public delegate void CellViewReused(EnhancedScroller scroller, EnhancedScrollerCellView cellView)`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> This delegate is called when a cell view is reused from the recycled cell view list

### `public delegate void CellViewVisibilityChangedDelegate(EnhancedScrollerCellView cellView)`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> This delegate handles the visibility changes of cell views

### `public delegate void CellViewWillRecycleDelegate(EnhancedScrollerCellView cellView)`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> This delegate will be fired just before the cell view is recycled

### `public class EnhancedScroller : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> The EnhancedScroller allows you to easily set up a dynamic scroller that will recycle views for you. This means that using only a handful of views, you can display thousands of rows. This will save memory and processing power in your application.

- `public EnhancedScroller.ScrollDirectionEnum scrollDirection`
- `public float spacing`
- `public RectOffset padding`
- `[SerializeField] private bool loop`
- `[SerializeField] private EnhancedScroller.ScrollbarVisibilityEnum scrollbarVisibility`
- `public bool snapping`
- `public float snapVelocityThreshold`
- `public float snapWatchOffset`
- `public float snapJumpToOffset`
- `public float snapCellCenterOffset`
- `public bool snapUseCellSpacing`
- `public EnhancedScroller.TweenType snapTweenType`
- `public float snapTweenTime`
- `public CellViewVisibilityChangedDelegate cellViewVisibilityChanged`
- `public CellViewWillRecycleDelegate cellViewWillRecycle`
- `public ScrollerScrolledDelegate scrollerScrolled`
- `public ScrollerSnappedDelegate scrollerSnapped`
- `public ScrollerScrollingChangedDelegate scrollerScrollingChanged`
- `public ScrollerTweeningChangedDelegate scrollerTweeningChanged`
- `public CellViewInstantiated cellViewInstantiated`
- `public CellViewReused cellViewReused`
- `public float _scrollPosition`
- `public IEnhancedScrollerDelegate Delegate { get; set; }`
- `public float ScrollPosition { get; set; }`
- `public float ScrollSize { get; }`
- `public float NormalizedScrollPosition { get; }`
- `public bool Loop { get; set; }`
- `public EnhancedScroller.ScrollbarVisibilityEnum ScrollbarVisibility { get; set; }`
- `public Vector2 Velocity { get; set; }`
- `public float LinearVelocity { get; set; }`
- `public bool IsScrolling { get; }`
- `public bool IsTweening { get; }`
- `public int StartCellViewIndex { get; }`
- `public int EndCellViewIndex { get; }`
- `public int StartDataIndex { get; }`
- `public int EndDataIndex { get; }`
- `public int NumberOfCells { get; }`
- `public ScrollRect ScrollRect { get; }`
- `public float ScrollRectSize { get; }`
- `public RectTransform Container { get; }`
- `public EnhancedScrollerCellView GetCellView(EnhancedScrollerCellView cellPrefab)`
- `public void ReloadData(float scrollPositionFactor = 0f)`
- `public void RefreshActiveCellViews()`
- `public void ClearAll()`
- `public void ClearActive()`
- `public void ClearRecycled()`
- `public void ToggleLoop()`
- `public void JumpToDataIndex(int dataIndex, float scrollerOffset = 0f, float cellOffset = 0f, bool useSpacing = true, EnhancedScroller.TweenType tweenType = EnhancedScroller.TweenType.immediate, float tweenTime = 0f, Action jumpComplete = null, EnhancedScroller.LoopJumpDirectionEnum loopJumpDirection = EnhancedScroller.LoopJumpDirectionEnum.Closest)`
- `public void Snap()`
- `public float GetScrollPositionForCellViewIndex(int cellViewIndex, EnhancedScroller.CellViewPositionEnum insertPosition)`
- `public float GetScrollPositionForDataIndex(int dataIndex, EnhancedScroller.CellViewPositionEnum insertPosition)`
- `public int GetCellViewIndexAtPosition(float position)`
- `public EnhancedScrollerCellView GetCellViewAtDataIndex(int dataIndex)`
- `public void _Resize(bool keepPosition)`

### `public enum EnhancedScroller.CellViewPositionEnum`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> Which side of a cell to reference. For vertical scrollers, before means above, after means below. For horizontal scrollers, before means to left of, after means to the right of.

- `Before, After`

### `public enum EnhancedScroller.LoopJumpDirectionEnum`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

- `Closest, Up, Down`

### `public enum EnhancedScroller.ScrollDirectionEnum`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> The direction this scroller is handling

- `Vertical, Horizontal`

### `public enum EnhancedScroller.ScrollbarVisibilityEnum`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> This will set how the scroll bar should be shown based on the data. If no scrollbar is attached, then this is ignored. OnlyIfNeeded will hide the scrollbar based on whether the scroller is looping or there aren't enough items to scroll.

- `OnlyIfNeeded, Always, Never`

### `public enum EnhancedScroller.TweenType`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> The easing type

- `immediate, linear, spring, easeInQuad, easeOutQuad, easeInOutQuad, easeInCubic, easeOutCubic, easeInOutCubic, easeInQuart, easeOutQuart, easeInOutQuart, easeInQuint, easeOutQuint, easeInOutQuint, easeInSine, easeOutSine, easeInOutSine, easeInExpo, easeOutExpo, easeInOutExpo, easeInCirc, easeOutCirc, easeInOutCirc, easeInBounce, easeOutBounce, easeInOutBounce, easeInBack, easeOutBack, easeInOutBack, easeInElastic, easeOutElastic, easeInOutElastic`

### `public class EnhancedScrollerCellView : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScrollerCellView.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScrollerCellView.cs)

> This is the base class that all cell views should derive from

- `public string cellIdentifier`
- `public bool active`
- `public int cellIndex`
- `public int dataIndex`
- `public virtual void RefreshCellView()`

### `public interface IEnhancedScrollerDelegate`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/IEnhancedScrollerDelegate.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/IEnhancedScrollerDelegate.cs)

> All scripts that handle the scroller's callbacks should inherit from this interface

- `int GetNumberOfCells(EnhancedScroller scrl)`
- `float GetCellViewSize(EnhancedScroller s, int dataIndex)`
- `EnhancedScrollerCellView GetCellView(EnhancedScroller s, int dataIndex, int cellIndex)`

### `public delegate void ScrollerScrolledDelegate(EnhancedScroller scroller, Vector2 val, float scrollPosition)`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> This delegate handles the scrolling callback of the ScrollRect.

### `public delegate void ScrollerScrollingChangedDelegate(EnhancedScroller scroller, bool scrolling)`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> This delegate handles the change in state of the scroller (scrolling or not scrolling)

### `public delegate void ScrollerSnappedDelegate(EnhancedScroller scroller, int cellIndex, int dataIndex, EnhancedScrollerCellView cellView)`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> This delegate handles the snapping of the scroller.

### `public delegate void ScrollerTweeningChangedDelegate(EnhancedScroller scroller, bool tweening)`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/EnhancedScroller.cs)

> This delegate handles the change in state of the scroller (jumping or not jumping)

### `public class SmallList<T>`

`GameUp.Core.UI` · [Runtime/UI/Helpers/EnhancedScroll/SmallList.cs](Assets/GameUpCore/Runtime/UI/Helpers/EnhancedScroll/SmallList.cs)

> This is a super light implementation of an array that behaves like a list, automatically allocating new memory when needed, but not releasing it to garbage collection.

- `public int Count`
- `public T[] data`
- `public T this[int i] { get; set; }`
- `public void Clear()`
- `public T First()`
- `public T Last()`
- `public void Add(T item)`
- `public void AddStart(T item)`
- `public void Insert(T item, int index)`
- `public T RemoveStart()`
- `public T RemoveAt(int index)`
- `public T Remove(T item)`
- `public T RemoveEnd()`
- `public bool Contains(T item)`

## Runtime/UI/Helpers/Loading

### `public class CutoutMaskUI : Image`

`GameUp.Core.UI` · [Runtime/UI/Helpers/Loading/CutoutMaskUI.cs](Assets/GameUpCore/Runtime/UI/Helpers/Loading/CutoutMaskUI.cs)

- `public override Material materialForRendering { get; }`

### `public class Loading : MonoSingleton<Loading>`

`GameUp.Core.UI` · [Runtime/UI/Helpers/Loading/Loading.cs](Assets/GameUpCore/Runtime/UI/Helpers/Loading/Loading.cs)

> Điều phối một overlay loading duy nhất: mỗi lần Open mới đóng instance trước (nếu có). Callback đóng chỉ cập nhật `_currentItem` khi đúng instance — tránh race khi đóng/mở chồng lấn.

- `[SerializeField] private LoadingOverlayBase prefabItem`
- `[SerializeField] private LoadingOverlayBase prefabItemCutoutMask`
- `[SerializeField] private RectTransform contentHolder`
- `public static void Open(bool autoClose = true, float autoCloseTime = 1f, Action onOpened = null, Action onClosed = null)`
- `public static void OpenCutoutMask(bool autoClose = true, float autoCloseTime = 1f, Action onOpened = null, Action onClosed = null, Vector3? cutoutStartLocalPosition = null, Transform cutoutStartReference = null, Camera cutoutWorldSpaceCamera = null)`
- `public static void Close()`

### `public class LoadingItem : LoadingOverlayBase`

`GameUp.Core.UI` · [Runtime/UI/Helpers/Loading/LoadingItem.cs](Assets/GameUpCore/Runtime/UI/Helpers/Loading/LoadingItem.cs)

> Loading dạng fade + scale + icon xoay.

- `[SerializeField] private RectTransform rotateTarget`
- `[SerializeField] private float rotateSpeed`
- `protected override void ResetVisualStateForPool()`
- `protected override void PlayIntro()`
- `protected override void StopIntroTweens()`

### `public class LoadingItemCutoutMask : LoadingOverlayBase`

`GameUp.Core.UI` · [Runtime/UI/Helpers/Loading/LoadingItemCutoutMask.cs](Assets/GameUpCore/Runtime/UI/Helpers/Loading/LoadingItemCutoutMask.cs)

> Loading iris cutout: mở thì vòng tròn đóng kín vào tâm; đóng thì mở rộng ra rồi trả pool.

- `[SerializeField] private RectTransform cutoutMask`
- `[SerializeField] private RectTransform boundsParent`
- `[SerializeField] private float maskAnimationDuration`
- `[SerializeField] private float centerBlackHoldDuration`
- `[SerializeField] private CanvasGroup centerBlack`
- `protected override void ResetVisualStateForPool()`
- `public void ApplyCutoutStartForOpen(Vector3? localPositionInMaskParent = null, Transform startReference = null, Camera worldObjectCamera = null)`
- `protected override void PlayIntro()`
- `protected override void StopIntroTweens()`
- `public override void Close()`

### `public abstract class LoadingOverlayBase : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Helpers/Loading/LoadingOverlayBase.cs](Assets/GameUpCore/Runtime/UI/Helpers/Loading/LoadingOverlayBase.cs)

> Khung chung cho mọi biến thể loading: callback, tự đóng, fade + thu nhỏ khi đóng, trả pool. Phần mở (intro) do lớp con định nghĩa qua `PlayIntro` / `StopIntroTweens`.

- `[SerializeField] private CanvasGroup canvasGroup`
- `protected Tween _autoCloseTween`
- `protected Action OnOpened`
- `protected Action OnClosed`
- `protected CanvasGroup OverlayGroup { get; }`
- `public void Open(bool autoClose, float autoCloseTime, Action onOpened, Action onClosed)`
- `public virtual void Close()`
- `protected virtual void ResetVisualStateForPool()`
- `protected abstract void PlayIntro()`
- `protected abstract void StopIntroTweens()`
- `protected void FinishClose()`
- `protected void KillCloseTweens()`
- `protected virtual void OnDisable()`

## Runtime/UI/Helpers/SelectView

### `public abstract class BaseChangeActiveView : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/BaseChangeActiveView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/BaseChangeActiveView.cs)

- `protected bool isActive`
- `public abstract bool IsActive { get; set; }`
- `public abstract void ChangeView(bool isActive)`

### `public abstract class BaseSelectView : MonoBehaviour, ISelectView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/BaseSelectView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/BaseSelectView.cs)

- `public bool IsSelected { get; set; }`
- `public abstract void ChangeSelect(bool isSelected)`

### `public interface ISelectView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/ISelectView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/ISelectView.cs)

- `bool IsSelected { get; set; }`
- `void ChangeSelect(bool isSelected)`

## Runtime/UI/Helpers/SelectView/Change

### `public class ChangeActiveGameObject : BaseSelectView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/Change/ChangeActiveGameObject.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeActiveGameObject.cs)

- `[SerializeField] private List<GameObject> activeObjects`
- `[SerializeField] private List<GameObject> disableObjects`
- `public override void ChangeSelect(bool isSelected)`

### `public class ChangeAndMoveTransform : BaseSelectView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/Change/ChangeAndMoveTransform.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeAndMoveTransform.cs)

- `[SerializeField] private MoveType moveType`
- `[SerializeField] private float duration`
- `[SerializeField] private RectTransform moveItemTrs`
- `[SerializeField] private float disablePos`
- `[SerializeField] private float enablePos`
- `public override void ChangeSelect(bool isSelected)`

### `public class ChangeGameObjectsActive : BaseChangeActiveView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/Change/ChangeGameObjectsActive.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeGameObjectsActive.cs)

- `[SerializeField] private List<GameObjectChangeActive> items`
- `public override bool IsActive { get; set; }`
- `public override void ChangeView(bool enable)`

### `public class ChangeGraphicColorView : BaseSelectView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/Change/ChangeGraphicColorView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeGraphicColorView.cs)

- `[SerializeField] private List<Graphic> graphics`
- `[SerializeField] private Color[] imColors`
- `[SerializeField] private Color selectedColor`
- `[SerializeField] private Color deselectedColor`
- `public override void ChangeSelect(bool isSelected)`

### `public class ChangeImageScaleView : BaseSelectView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/Change/ChangeImageScaleView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeImageScaleView.cs)

- `[SerializeField] private float duration`
- `[SerializeField] private float scale`
- `[SerializeField] private Ease ease`
- `[SerializeField] private Image image`
- `[SerializeField] private Sprite[] sprites`
- `public override void ChangeSelect(bool isSelected)`

### `public class ChangeImageSpriteView : BaseSelectView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/Change/ChangeImageSpriteView.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeImageSpriteView.cs)

- `[SerializeField] private bool setNativeSize`
- `[SerializeField] private Image image`
- `[SerializeField] private Sprite selectSprite`
- `[SerializeField] private Sprite disableSprite`
- `public override void ChangeSelect(bool isSelected)`

### `public class ChangeSpriteResizeTweenNav : BaseSelectView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/Change/ChangeSpriteResizeTweenNav.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeSpriteResizeTweenNav.cs)

- `[SerializeField] private Image targetImage`
- `[SerializeField] private Image targetIconImage`
- `[SerializeField] private Sprite selectSprite`
- `[SerializeField] private Sprite disableSprite`
- `[SerializeField] private GameObject obj`
- `[SerializeField] private RectTransform posSelect`
- `[SerializeField] private RectTransform posNotSelect`
- `[SerializeField] private Vector2 sizeNotSelectBg`
- `[SerializeField] private Vector2 sizeSelectBg`
- `[SerializeField] private Vector2 sizeNotSelectIcon`
- `[SerializeField] private Vector2 sizeSelectIcon`
- `public override void ChangeSelect(bool isSelected)`

### `public class ChangeSpriteResizeTweenNav2 : BaseSelectView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/Change/ChangeSpriteResizeTweenNav2.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeSpriteResizeTweenNav2.cs)

- `[SerializeField] private Image targetImage`
- `[SerializeField] private Image targetIconImage`
- `[SerializeField] private Sprite selectSprite`
- `[SerializeField] private Sprite disableSprite`
- `[SerializeField] private GameObject obj`
- `[SerializeField] private RectTransform posSelect`
- `[SerializeField] private RectTransform posNotSelect`
- `[SerializeField] private Vector2 sizeNotSelectBg`
- `[SerializeField] private Vector2 sizeSelectBg`
- `[SerializeField] private Vector2 sizeNotSelectIcon`
- `[SerializeField] private Vector2 sizeSelectIcon`
- `public override void ChangeSelect(bool isSelected)`

### `public class GameObjectChangeActive`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/Change/ChangeGameObjectsActive.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/ChangeGameObjectsActive.cs)

- `public GameObject obj`
- `public bool isActiveOnViewActive`
- `public void ChangeView(bool isActive)`

### `public sealed class SelectViewComposite : BaseSelectView`

`GameUp.Core.UI` · [Runtime/UI/Helpers/SelectView/Change/SelectViewComposite.cs](Assets/GameUpCore/Runtime/UI/Helpers/SelectView/Change/SelectViewComposite.cs)

- `[SerializeField] private List<MonoBehaviour> views`
- `public override void ChangeSelect(bool isSelected)`

## Runtime/UI/Helpers/Toast

### `public class Toast : MonoSingleton<Toast>`

`GameUp.Core.UI` · [Runtime/UI/Helpers/Toast/Toast.cs](Assets/GameUpCore/Runtime/UI/Helpers/Toast/Toast.cs)

- `[SerializeField] private ToastItem prefabItem`
- `[SerializeField] private RectTransform contentHolder`
- `public static void Show(string str, float timeShow = 1.3f, float showPosY = 0f)`
- `public static void Close()`

### `public class ToastItem : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Helpers/Toast/ToastItem.cs](Assets/GameUpCore/Runtime/UI/Helpers/Toast/ToastItem.cs)

- `[SerializeField] private CanvasGroup group`
- `[SerializeField] private TextMeshProUGUI toastTxt`
- `[SerializeField] private RectTransform rectTrs`
- `public ToastItem SetTimeShow(float t)`
- `public ToastItem SetStartPosY(float pos)`
- `public ToastItem SetText(string str)`
- `public void ShowToast()`
- `public static void RemoveOtherToast()`

## Runtime/UI/Popups

### `public class PopupData : ScriptableObject`

`GameUp.Core.UI` · [Runtime/UI/Popups/PopupData.cs](Assets/GameUpCore/Runtime/UI/Popups/PopupData.cs)

- `[SerializeField] private string pathPopup`
- `[SerializeField] private List<PopupInfo> popups`
- `public AsyncOperationHandle<UIPopup> GetPopupAsync<T>()`
- `public AsyncOperationHandle<UIPopup> GetPopupAsync(Type type)`
- `public void SetupPathPopup(string path)`
- `public void SetUp()`

### `public class PopupInfo`

`GameUp.Core.UI` · [Runtime/UI/Popups/PopupData.cs](Assets/GameUpCore/Runtime/UI/Popups/PopupData.cs)

- `public string name`
- `public string typeName`
- `public UIPopupReference popupRef`

### `public class UIPopup : UIBaseView`

`GameUp.Core.UI` · [Runtime/UI/Popups/UIPopup.cs](Assets/GameUpCore/Runtime/UI/Popups/UIPopup.cs)

- `protected static readonly Dictionary<Type, UIPopup> Popups`
- `public static bool IsPopupOn { get; }`
- `public static Signal<UIPopup> OnPopupOpened { get; }`
- `public static Signal<UIPopup> OnPopupClosed { get; }`
- `public static Signal OnAllPopupClosed { get; }`
- `protected static PopupData PopupData { get; }`
- `public static void PurgeDestroyed()`
- `public override void OnOpen()`
- `public override void OnClose(Action callbackClose = null)`
- `public void ActionClose(Action callbackClose = null)`
- `public static void CloseAllPopup()`
- `protected static UIPopup GetOrCreatePopup(Type type, UIPopup prefab)`
- `public static void PreloadPopupByTypeAsync(Type type, Action<UIPopup> onComplete = null)`
- `protected static void ResolvePopupAsync(Type type, Action<UIPopup> onResolved)`
- `public static void PreloadPopupByTypesAsync(params Type[] types)`
- `protected override void OnValidate()`

### `public class UIPopupReference : ComponentReference<UIPopup>`

`GameUp.Core.UI` · [Runtime/UI/Popups/UIPopupReference.cs](Assets/GameUpCore/Runtime/UI/Popups/UIPopupReference.cs)

- `public UIPopupReference(string guid)`

### `public class UIPopup<T> : UIPopup where T : UIPopup`

`GameUp.Core.UI` · [Runtime/UI/Popups/UIPopup.cs](Assets/GameUpCore/Runtime/UI/Popups/UIPopup.cs)

- `public static void OpenViewAsync(Action<T> onComplete = null)`
- `public static void CloseView()`
- `public static void PreloadViewAsync(Action<T> onComplete = null)`

## Runtime/UI/Screens

### `public class ScreenData : ScriptableObject`

`GameUp.Core.UI` · [Runtime/UI/Screens/ScreenData.cs](Assets/GameUpCore/Runtime/UI/Screens/ScreenData.cs)

- `[SerializeField] private string pathScreen`
- `[SerializeField] private List<ScreenInfo> screens`
- `public AsyncOperationHandle<UIScreen> GetScreenAsync<T>()`
- `public AsyncOperationHandle<UIScreen> GetScreenAsync(Type type)`
- `public void SetUp()`

### `public class ScreenInfo`

`GameUp.Core.UI` · [Runtime/UI/Screens/ScreenData.cs](Assets/GameUpCore/Runtime/UI/Screens/ScreenData.cs)

- `public string name`
- `public string typeName`
- `public UIScreenReference screenRef`

### `public class UIScreen : UIBaseView`

`GameUp.Core.UI` · [Runtime/UI/Screens/UIScreen.cs](Assets/GameUpCore/Runtime/UI/Screens/UIScreen.cs)

- `protected static readonly Dictionary<Type, UIScreen> Screens`
- `protected static readonly Stack<UIScreen> HistoryView`
- `public static ScreenData ScreenData { get; }`
- `public static Signal<UIScreen> OnScreenOpened { get; }`
- `public static Signal<UIScreen> OnScreenClosed { get; }`
- `public static UIScreen currentScreen { get; set; }`
- `public static event Action<UIScreen> OnCurrentScreenChanged`
- `public static void PurgeDestroyed()`
- `public override void OnOpen()`
- `public override void OnClose(Action onComplete = null)`
- `public void ShowLast(bool isUseTransition = false)`
- `protected override void OnValidate()`
- `public static void OpenPrevious()`
- `protected static UIScreen GetOrCreateScreen(Type type, UIScreen prefab)`
- `protected static void OpenScreenWithInstance(Type type, UIScreen ins, bool remember = true)`
- `public static void OpenScreenByTypeAsync(Type type, bool remember = true)`
- `public static void PreloadAsyncView(Type type)`
- `public static void PreloadViewByTypeAsync(Type type, Action<UIScreen> onComplete = null)`
- `protected static void ResolveScreenAsync(Type type, Action<UIScreen> onResolved)`
- `public static void PreloadViewByTypesAsync(params Type[] types)`

### `public sealed class UIScreenReference : ComponentReference<UIScreen>`

`GameUp.Core.UI` · [Runtime/UI/Screens/UIScreenReference.cs](Assets/GameUpCore/Runtime/UI/Screens/UIScreenReference.cs)

- `public UIScreenReference(string guid)`

### `public class UIScreen<T> : UIScreen where T : UIScreen`

`GameUp.Core.UI` · [Runtime/UI/Screens/UIScreen.cs](Assets/GameUpCore/Runtime/UI/Screens/UIScreen.cs)

- `public static void OpenViewAsync(Action<T> onComplete = null, bool remember = true, bool isUseTransition = true)`
- `public static void CloseView()`
- `public static T GetView()`
- `public static AsyncOperationHandle<UIScreen> PreloadView()`
- `public static void PreloadViewAsync(Action<T> onComplete = null)`

## Runtime/UI/Screens/ButtonScreen

### `public class ButtonOpenScreen : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Screens/ButtonScreen/ButtonOpenScreen.cs](Assets/GameUpCore/Runtime/UI/Screens/ButtonScreen/ButtonOpenScreen.cs)

- `[SerializeField] private Button btn`
- `[SerializeField] private bool rememberInHistory`
- `[SerializeField] private string screenTypeName`
- `public void OpenSelectedScreen()`

### `public class CustomButtonOpenScreen : MonoBehaviour`

`GameUp.Core.UI` · [Runtime/UI/Screens/ButtonScreen/CustomButtonOpenScreen.cs](Assets/GameUpCore/Runtime/UI/Screens/ButtonScreen/CustomButtonOpenScreen.cs)

- `[SerializeField] private Button btn`
- `[SerializeField] private CustomSelectButton _selectButton`
- `[SerializeField] private bool rememberInHistory`
- `[SerializeField] private string screenTypeName`
- `public void OpenSelectedScreen()`
- `public void RefreshSelectState()`
- `public static void RefreshAllSelectStates()`

## Runtime/UI/TransitionUtils

### `public interface IAnimation`

`GameUp.Core.UI` · [Runtime/UI/TransitionUtils/IAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/IAnimation.cs)

- `Action OnReverseCompleteCallback { get; set; }`
- `Action OnStartCompleteCallback { get; set; }`
- `IAnimation OnStart()`
- `IAnimation OnReverse()`
- `IAnimation OnStop()`
- `IAnimation SetStartCompleteCallback(Action a)`
- `IAnimation SetReverseCompleteCallback(Action a)`

### `public enum UIAnimationMode`

`GameUp.Core.UI` · [Runtime/UI/TransitionUtils/UIAnimationMode.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/UIAnimationMode.cs)

- `Default, Custom`

### `public abstract class UIBaseAnimation : UIDefaultAnimation`

`GameUp.Core.UI` · [Runtime/UI/TransitionUtils/UIBaseAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/UIBaseAnimation.cs)

- `public RectTransform content`
- `protected CanvasGroup canvasGroup`
- `protected virtual void OnValidate()`
- `public override IAnimation OnStart()`
- `public override IAnimation OnReverse()`
- `public override IAnimation OnStop()`

### `public class UIDefaultAnimation : MonoBehaviour, IAnimation`

`GameUp.Core.UI` · [Runtime/UI/TransitionUtils/UIDefaultAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/UIDefaultAnimation.cs)

- `protected Sequence mainSequence`
- `public Action OnReverseCompleteCallback { get; set; }`
- `public Action OnStartCompleteCallback { get; set; }`
- `protected void InvokeStartComplete()`
- `protected void InvokeReverseComplete()`
- `public virtual IAnimation OnStart()`
- `public virtual IAnimation OnReverse()`
- `public virtual IAnimation OnStop()`
- `public IAnimation SetStartCompleteCallback(Action a)`
- `public IAnimation SetReverseCompleteCallback(Action a)`

## Runtime/UI/TransitionUtils/Animation

### `public enum MoveType`

`GameUp.Core.UI` · [Runtime/UI/TransitionUtils/Animation/UIMoveAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/Animation/UIMoveAnimation.cs)

- `MoveX, MoveY`

### `public class UIFadeAnimation : UIBaseAnimation`

`GameUp.Core.UI` · [Runtime/UI/TransitionUtils/Animation/UIFadeAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/Animation/UIFadeAnimation.cs)

- `public float fadeTime`
- `public override IAnimation OnStart()`
- `public override IAnimation OnReverse()`

### `public class UIMoveAnimation : UIBaseAnimation`

`GameUp.Core.UI` · [Runtime/UI/TransitionUtils/Animation/UIMoveAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/Animation/UIMoveAnimation.cs)

- `public MoveType moveType`
- `public float startPos`
- `public float middlePos`
- `public float endPos`
- `public float firstTime`
- `public float secondTime`
- `public override IAnimation OnStart()`
- `public override IAnimation OnReverse()`

### `public class UIScaleAnimation : UIBaseAnimation`

`GameUp.Core.UI` · [Runtime/UI/TransitionUtils/Animation/UIScaleAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/Animation/UIScaleAnimation.cs)

- `public float startSize`
- `public float middleSize`
- `public float endSize`
- `public float firstTime`
- `public float secondTime`
- `public override IAnimation OnStart()`
- `public override IAnimation OnReverse()`

### `public class UIShowMoveItemAnimation : UIBaseAnimation`

`GameUp.Core.UI` · [Runtime/UI/TransitionUtils/Animation/UIShowMoveItemAnimation.cs](Assets/GameUpCore/Runtime/UI/TransitionUtils/Animation/UIShowMoveItemAnimation.cs)

- `[SerializeField] private List<RectTransform> itemList`
- `[SerializeField] private List<CanvasGroup> canvasGroups`
- `[SerializeField] private List<Vector2> originalPositions`
- `[SerializeField] private float startYOffset`
- `[SerializeField] private float duration`
- `[SerializeField] private float delayBetweenItems`
- `protected override void OnValidate()`
- `public override IAnimation OnStart()`
- `public override IAnimation OnReverse()`

