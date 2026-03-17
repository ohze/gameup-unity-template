# GameUp Unity Template

**GameUp Core Framework** là một nền tảng cốt lõi (core framework) dành cho việc phát triển game trên Unity (phiên bản 2022.3 trở lên). Dự án cung cấp một bộ khung cấu trúc chuẩn hóa với các module thiết yếu bao gồm Singleton, Event Bus (Signal), mã hóa/lưu trữ, System Audio, thư viện tiện ích (Utils/Extensions), Logger, Object Pooling, v.v., giúp các team phát triển game đẩy nhanh quá trình sản xuất và dễ dàng maintain code.

---

## 📑 Mục Lục

1. [Cách Cài Đặt và Sử Dụng qua UPM](#-cách-cài-đặt-và-sử-dụng-qua-upm-unity-package-manager)
2. [Bắt Đầu Nhanh Với Template Repo](#-bắt-đầu-nhanh-với-template-repo)
3. [Cấu Trúc Thư Mục Khuyến Nghị](#-cấu-trúc-thư-mục-khuyến-nghị)
4. [Workflow Khuyến Nghị](#-workflow-khuyến-nghị)
5. [Hệ Thống Cốt Lõi (GameUpCore/Runtime/Core)](#-hệ-thống-cốt-lõi-gameupcoreruntimecore)
    - [1. Singleton (Singleton)](#1-singleton-singleton)
    - [2. Signal / Event Bus (Signal)](#2-signal--event-bus-signal)
    - [3. Object Pools (ObjectPools)](#3-object-pools-objectpools)
    - [4. Logger (Logger)](#4-logger-logger)
    - [5. Audio (Audio)](#5-audio-audio)
    - [6. TimeSystem (TimeSystem)](#6-timesystem-timesystem)
    - [7. CoroutineRunner (CoroutineRunner)](#7-coroutinerunner-coroutinerunner)
    - [8. DataHelper (DataHelper)](#8-datahelper-datahelper)
    - [9. Các Hàm Tiện Ích Mở Rộng (Extension)](#9-các-hàm-tiện-ích-mở-rộng-extension)
    - [10. Các Hàm Tiện Ích Hệ Thống (Utils)](#10-các-hàm-tiện-ích-hệ-thống-utils)
6. [Những Công Cụ Cài Đặt / Editor Support (GameUpCore/Editor)](#-những-công-cụ-cài-đặt--editor-support-gameupcoreeditor)
7. [Ví Dụ Chi Tiết](#-ví-dụ-chi-tiết)
    - [1. Signal: Publish/Subscribe decoupling](#1-signal-publishsubscribe-decoupling)
    - [2. Object Pool: Spawn/Release prefab](#2-object-pool-spawnrelease-prefab)
    - [3. LocalStorageUtils: Lưu & đọc dữ liệu (có mã hoá)](#3-localstorageutils-lưu--đọc-dữ-liệu-có-mã-hoá)
    - [4. Audio: Setup + phát SFX/BGM](#4-audio-setup--phát-sfxbgm)
    - [5. TimeSystem: Slow motion theo tình huống](#5-timesystem-slow-motion-theo-tình-huống)

---

## 🚀 Cách Cài Đặt và Sử Dụng qua UPM (Unity Package Manager)

Hiện tại, framework hỗ trợ việc cài đặt dưới dạng native package thông qua UPM. Bạn có thể cài đặt dễ dàng bằng các bước sau:

**Cách 1: Cài đặt trực tiếp qua giao diện Unity Package Manager**
1. Mở Unity, chọn menu **Window** -> **Package Manager**.
2. Bấm vào dấu **+** ở góc trên cùng bên trái.
3. Chọn **"Add package from git URL..."**.
4. Dán đường dẫn sau vào ô trống và nhấn **Add**:
   ```
   https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpCore
   ```
5. Chờ Unity tự động tải và cài đặt package. 

**Cách 2: Khai báo vào manifest.json**
Mở tệp `Packages/manifest.json` trong thư mục dự án của bạn và thêm dòng sau vào khối `"dependencies"`:
```json
"dependencies": {
    "com.gameup.core": "https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpCore",
    ...
}
```

---

## 📂 Hệ Thống Cốt Lõi (`GameUpCore/Runtime/Core`)

Thư mục `Assets/GameUpCore/Runtime/Core` bao gồm nhiều hệ thống và thư viện dùng chung cho toàn bộ dự án.

### 1. **Singleton (`Singleton`)**
- Chứa thiết kế mẫu Singleton cơ bản (`Singleton<T>`) và `MonoSingleton<T>` cho các script kế thừa `MonoBehaviour`. 
- **Cách sử dụng:** Dùng cho các hệ thống có vai trò quản lý duy nhất trên toàn Scene. Kế thừa `MonoSingleton<AudioManager>`, bạn có thể gọi `AudioManager.Instance` một cách an toàn ở bất kỳ đâu.

### 2. **Signal / Event Bus (`Signal`)**
- Hệ thống gửi/phát tín hiệu thông qua cơ chế Publish/Subscribe pattern để liên lạc giữa các Scripts/Systems. Nhờ đó các module hoàn toàn tách biệt (decoupling), không bị phụ thuộc vòng vào nhau.
- **Cách sử dụng:** Tạo cấu trúc event như `public struct OnPlayerDiedEvent {}`, sau đó bất kỳ module nào cũng có thể lắng nghe, đăng ký thông qua Signal: `SignalBus.Subscribe<OnPlayerDiedEvent>(OnDead)`.

### 3. **Object Pools (`ObjectPools`)**
- Hệ thống tối ưu hiệu năng phân bổ và tái sử dụng GameObjects thay vì gọi `Instantiate` và `Destroy` liên tục (GUPool, GUPoolers).
- **Cách sử dụng:** Sử dụng `GUPoolers` thay mặt khởi tạo các prefab như Enemy, Đạn. Khi dùng xong chỉ việc đưa chúng về trạng thái Release.

### 4. **Logger (`Logger`)**
- Wrapper mạnh mẽ của `Debug.Log` đó chính là `GULogger`.
- **Cách sử dụng:** Thay vì dùng `Debug.Log`, bạn dùng `GULogger.Log()`. Khi phát hành bản Release, chỉ việc cấu hình tắt log (bằng Scripting Define Symbol), giúp trò chơi không bị tiêu tốn hiệu năng.

### 5. **Audio (`Audio`)**
- Hệ thống quản lý hệ thống âm thanh (`AudioManager`, `AudioDatabase`, `AudioIdentity`). Cấu trúc được mở rộng và có hỗ trợ liên kết với Addressables nếu có nhu cầu sử dụng Audio từ xa.
- **Cách sử dụng:** Gọi trực tiếp qua AudioManager thông qua định danh được scan và build code-generated tự động. Ví dũ: `AudioManager.PlaySFX(AudioID.Hit)`.

### 6. **TimeSystem (`TimeSystem`)**
- Cung cấp tính năng quản lý thời gian độc lập (`TimeManager`).
- **Cách sử dụng:** Nó dùng để làm chậm (slow motion) toàn cục hoặc một phần của game mà hạn chế phụ thuộc cứng vào `Time.timeScale` duy nhất của Unity.

### 7. **CoroutineRunner (`CoroutineRunner`)**
- Hệ thống chạy đệm hỗ trợ chạy Coroutine cho các class C# thuần túy.
- **Cách sử dụng:** Các Class thông thường hoặc hệ thống không kế thừa `MonoBehaviour` không thể gọi `StartCoroutine`. Bạn có thể thay bằng `CoroutineRunner.Instance.StartCoroutine(MyCoroutineMethod())`.

### 8. **DataHelper (`DataHelper`)**
- Tập hợp các lớp trợ giúp quản lý thao tác với cấu trúc thư mục/tệp và định dạng dữ liệu trong Game.
- Các Components chính:
  - `EncryptUtils`: Mã hóa chuỗi String.
  - `FileStorageUtils`: Các phương thức xử lý thư mục `PersistentDataPath`.
  - `LocalStorageUtils`: Wrapper nâng cao của `PlayerPrefs` có tích hợp sẵn mã hóa `EncryptUtils`, bao gồm lưu trữ kiểu Objects tùy chỉnh thông qua Serialization (Json).
  - Thư viện json `FullSerializer` (ổn định về serialization so với JsonUtility truyền thống).
- **Cách sử dụng:** Ví dụ mã hoá và lưu tiền của người chơi: `LocalStorageUtils.SetLong("player_coin", 100);` hoặc đối với cấu trúc lớp phúc tạp `LocalStorageUtils.SetObject<PlayerStats>("stats", currentStats)`.

### 9. **Các Hàm Tiện Ích Mở Rộng (`Extension`)**
- Các Extension Methods giúp rút ngắn mã nguồn (Cú pháp Sugar). Khi sử dụng, mã nguồn trông sẽ giống như các phương thức sẵn có của lớp đó.
- Các thư viện khả dụng:
  - `MonoExtension`: Chứa những cú pháp rút gọi: `gameObject.Show()`, `gameObject.Hide()`, `transform.Reset()`(reset local pos, rot, sca), tự động Add/Get `gameObject.GetOrAdd<Rigidbody>()`, v.v.
  - `ListCollectionExtension`: Trộn danh sách (`Shuffle`), lấy dữ liệu ngẫu nhiên của danh sách (`GetRandom`), Copy, Tách danh sách v.v.
  - `ConvertTimeExtension`, `CoroutineExtension`, `EnumExtension`, `UIExtension`.
- **Cách sử dụng:** Bạn gọi trực tiếp qua biến khởi tạo nếu `using GameUp.Core;` (Ví dụ thay cho `gameObject.SetActive(false)`, chỉ việc gọi `gameObject.Hide()`).

### 10. **Các Hàm Tiện Ích Hệ Thống (`Utils`)**
- Các Class chứa phương thức Tĩnh (Static) để gọi thực thi tác vụ nhanh chóng.
- `GameUtils`: Lấy id thiết bị (`GetDeviceId()`), lấy thư mục/danh sách file Editor, Format thời gian (`ConvertTimeSpanStr`), Screenshots (`TakeScreenShot`), Lấy version/bundleID ...
- `SettingVar<T>`: Mô hình Biến lưu thiết lập hệ thống (`BooleanVar`, `IntVar`...) hỗ trợ việc lưu trực tiếp và kích hoạt sự kiện `OnValueChange` trong nội bộ hệ thống.
- `TimeUtils`, `StringUtils`: Rất nhiều hàm thao tác thời gian, định dạng chuỗi hữu ích.

---

## 🛠 Những Công Cụ Cài Đặt / Editor Support (`GameUpCore/Editor`)

Ở thư mục này, framework cung cấp các Unity Editor Tool Windows để thiết lập dễ dàng hơn.

### 1. **Setup GUPooler trong Scene (`GUPoolersMenu.cs`)**
- **Sử dụng:** Đi đến menu điều hướng của Unity, chọn `GameUp -> Poolers -> Setup GUPoolers in Scene`.
- **Tác dụng & Ví dụ:** Tính năng này sẽ tự động tạo một `GameObject` có tên `GUPoolersSingleton` được gán sẵn component `GUPoolers` trong Scene hiện tại. Hoặc nếu đã có sẵn, nó sẽ chỉ trỏ (`ping`) đến đúng GameObject đó. Đỡ mất công tạo tay và tìm kiếm trong Scene nặng.

### 2. **Bật / Tắt Log dành cho Phát Hành (`GULoggerMenu.cs`)**
- **Sử dụng:** `GameUp -> Logger -> Enable Logs (Debug)` hoặc `Disable Logs (Release)`.
- **Tác dụng & Ví dụ:** Tool này tự động chèn hoặc xóa cờ Scripting Define Symbols `ENABLE_LOG` trong phần **Player Settings** (cho Standalone, Android và iOS) cùng lúc. 
  - Khi code trong lúc dev, bấm **Enable Logs (Debug)**, console sẽ in ra log chi tiết qua `GULogger`.
  - Lúc build bản Release lên Store, chọn **Disable Logs (Release)**, toàn bộ `GULogger` sẽ bị tắt đi để tiết kiệm CPU mà bạn không cần phải lục tung file để sửa thủ công hoặc cấu hình từng Platforms một.

### 3. **Quản Lý Và Khởi Tạo Âm Thanh Tự Động (`GUAudioManagerWindow.cs`)**
- **Sử dụng:** Mở `GameUp -> Audio -> Setup AudioManager`.
- **Tác dụng & Tích hợp:** Đây là cửa sổ Window Editor (Wizard) mạnh mẽ giúp bạn cấu hình Database tập trung cho âm thanh một cách dễ dàng, phân chia làm 2 giai đoạn:
  - **Giai đoạn 1 (Initial Setup):** Nút **Find/Create AudioManager** giúp tự động tìm hoặc gắn AudioManager vào Scene, sau đó nhấn **Initialize Audio Database** định cấu hình cho `AudioDatabase.asset` trong thư mục quản lý data.
  - **Giai đoạn 2 (Scan & Update):** Một khi đã thiết lập thư mục các File mP3/Wav (`Audio Folder`), thư mục làm định danh và file Output (VD: `Assets/AudioID.cs`).
- **Ví dụ cơ chế:** Khi bạn ném một file tên "Attack 1.wav" vào thư mục Audio Folder, rồi ấn nút **Scan & Update** trong Tool Window, quá trình sau sẽ diễn ra:
  1. Tool tự quét ra Clip, gộp chung nó và tạo một file Scriptable Object tên `Attack.asset` (Identity).
  2. Ghi đè cập nhật vào `AudioDatabase.asset`.
  3. Cập nhật và đóng gói Addressables Label / Group (sẵn sàng làm resource rời) nếu bạn có dùng Package Addressables.
  4. Build ra file C# mới tinh `AudioID.cs` chứa tên Enum: `public static GameUp.Core.AudioIdentity Attack => Get("Attack");`
  - Về sau trong Main Code, để phát ra tiếng đánh, chỉ cần gõ duy nhất: `AudioManager.instance.Play(AudioID.Attack);` - Vô cùng tiện lợi!

---

## ⚡ Bắt Đầu Nhanh Với Template Repo

Phần này dành cho trường hợp bạn dùng **repo này như một dự án mẫu** (clone về và bắt đầu làm game luôn), thay vì nhúng `GameUpCore` qua UPM.

### Yêu cầu môi trường
- **Unity**: 2022.3+ (LTS khuyến nghị).
- **IDE**: Rider hoặc Visual Studio 2022.
- (Tuỳ chọn) **Addressables**: nếu bạn muốn quản lý Audio/Assets theo kiểu resource rời.

### Chạy dự án lần đầu
1. Clone repo và mở bằng Unity Hub.
2. Mở Scene mẫu: `Assets/Scenes/SampleScene.unity`.
3. Nếu có các tool setup trong menu `GameUp`, hãy chạy các tool cần thiết:
   - `GameUp -> Poolers -> Setup GUPoolers in Scene`
   - `GameUp -> Audio -> Setup AudioManager` (nếu dự án có Audio)
4. Nhấn Play để kiểm tra.

### Nhúng `GameUpCore` vào dự án khác (tóm tắt)
- Nếu dự án khác muốn dùng core mà **không clone template**, hãy dùng phần [UPM](#-cách-cài-đặt-và-sử-dụng-qua-upm-unity-package-manager) phía trên.

---

## 📁 Cấu Trúc Thư Mục Khuyến Nghị

Để giảm conflict và giữ dự án “sạch”, khuyến nghị gom toàn bộ game vào một thư mục gốc:

```
Assets/
  _Project/
    Art/
    Audio/
    Prefabs/
    Scenes/
    Scripts/
      Core/
      Gameplay/
      UI/
    ScriptableObjects/
  GameUpCore/
    Runtime/
    Editor/
```

Gợi ý thêm:
- Dùng **Prefab Variant** cho các biến thể.
- UI/gameplay module lớn nên tách thành **Nested Prefab**.
- Tránh để asset rải rác ngoài `Assets/_Project/`.

---

## 🔁 Workflow Khuyến Nghị

- **Prefab is King**: gameplay/UI tạo bằng prefab, scene chỉ giữ camera/light/environment/manager tĩnh.
- **Decoupling bằng Signal**: hệ thống giao tiếp bằng event bus thay vì reference trực tiếp.
- **Tối ưu Instantiate/Destroy**: mọi object spawn thường xuyên (đạn, VFX, enemy) nên qua pool.
- **Data**: dùng ScriptableObject cho cấu hình, `LocalStorageUtils` cho save/load.
- **Release build**: tắt log bằng `GameUp -> Logger -> Disable Logs (Release)`.

---

## 🧩 Ví Dụ Chi Tiết

> Lưu ý: các namespace/class cụ thể có thể khác tuỳ phiên bản, nhưng ý tưởng & pattern là cố định. Nếu bạn gặp sai khác tên API, hãy tra trong `Assets/GameUpCore/Runtime/` để dùng đúng hàm tương ứng.

### 1. Signal: Publish/Subscribe decoupling

Ví dụ player chết thì UI và Audio đều phản ứng, nhưng **không phụ thuộc nhau**.

```csharp
using UnityEngine;
using GameUp.Core;

public struct PlayerDiedSignal
{
    public int KillerId;
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
            SignalBus.Fire(new PlayerDiedSignal { KillerId = killerId });
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
        SignalBus.Subscribe<PlayerDiedSignal>(OnPlayerDied);
    }

    private void OnDisable()
    {
        SignalBus.Unsubscribe<PlayerDiedSignal>(OnPlayerDied);
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
        SignalBus.Subscribe<PlayerDiedSignal>(OnPlayerDied);
    }

    private void OnDisable()
    {
        SignalBus.Unsubscribe<PlayerDiedSignal>(OnPlayerDied);
    }

    private void OnPlayerDied(PlayerDiedSignal signal)
    {
        AudioManager.PlaySFX(AudioID.Hit);
    }
}
```

### 2. Object Pool: Spawn/Release prefab

Ví dụ bắn đạn: spawn từ pool, khi hết vòng đời thì release về pool.

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

> Nếu bạn chưa có `GUPoolers` trong scene, chạy tool: `GameUp -> Poolers -> Setup GUPoolers in Scene`.

### 3. LocalStorageUtils: Lưu & đọc dữ liệu (có mã hoá)

Ví dụ lưu coin và lưu một object stats.

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

#### Setup nhanh
1. Mở `GameUp -> Audio -> Setup AudioManager`.
2. Chọn thư mục chứa audio (ví dụ `Assets/_Project/Audio/`).
3. Bấm **Scan & Update** để tạo identity + cập nhật database + build code `AudioID`.

#### Phát âm thanh

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

Ví dụ khi player “perfect dodge” thì slow motion ngắn, sau đó trả về bình thường.

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