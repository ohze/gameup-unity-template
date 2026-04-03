# Firebase Remote Config Utils

Tiện ích đọc và đồng bộ **Firebase Remote Config** với các biến public trong code bằng reflection. Tên biến trong class phải **trùng với key** trên Firebase Console để tự động map.

## Yêu cầu

- **Firebase SDK** (Firebase.RemoteConfig) đã thêm vào project.
- **FirebaseUtils** đã khởi tạo (dùng khi Firebase chưa sẵn sàng lúc Start).

## Cách dùng

### 1. Singleton

```csharp
FirebaseRemoteConfigUtils.Instance
```

### 2. Đọc giá trị sau khi fetch xong

Các giá trị chỉ đúng sau khi Remote Config đã fetch và activate. Nên dùng `IsRemoteConfigReady` hoặc `OnFetchCompleted` trước khi đọc.

```csharp
// Kiểm tra đã sẵn sàng
if (FirebaseRemoteConfigUtils.Instance.IsRemoteConfigReady)
{
    int capping = FirebaseRemoteConfigUtils.Instance.inter_capping_time;
    bool showBanner = FirebaseRemoteConfigUtils.Instance.enable_banner;
}

// Hoặc đăng ký callback
FirebaseRemoteConfigUtils.Instance.OnFetchCompleted += (activated) =>
{
    // activated = true nếu fetch và activate thành công
    var utils = FirebaseRemoteConfigUtils.Instance;
    int startLevel = utils.inter_start_level;
    bool rateEnabled = utils.enable_rate_app;
};
```

### 3. Refresh config (fetch lại)

Khi cần cập nhật config (ví dụ: sau vài phút chơi, hoặc từ menu):

```csharp
FirebaseRemoteConfigUtils.Instance.FetchAndActivate(success =>
{
    if (success)
        Debug.Log("Remote Config đã cập nhật.");
});
```

## Các key (biến) mặc định

| Key (Firebase Console)     | Kiểu   | Mặc định | Mô tả |
|----------------------------|--------|----------|--------|
| `inter_capping_time`       | int    | 120      | Khoảng thời gian tối thiểu (giây) giữa 2 lần hiển thị Interstitial. |
| `inter_start_level`        | int    | 3        | Level bắt đầu hiện Interstitial (level tính từ 1). |
| `enable_rate_app`          | bool   | false    | Bật/tắt hiển thị Rate App trong game. |
| `level_start_show_rate_app`| int    | 5        | Level bắt đầu hiện Rate App. |
| `no_internet_popup_enable` | bool   | true     | Bật/tắt popup yêu cầu kết nối Internet. |
| `enable_banner`            | bool   | true     | Bật/tắt hiển thị Banner trong game. |

Trên **Firebase Console → Remote Config**, tạo các key trùng tên và set kiểu **Number** (→ map `int`) hoặc **Boolean** (→ map `bool`).

## Hành vi đặc biệt

- **Editor (Windows/macOS):** Không gọi Firebase thật; `IsRemoteConfigReady = true` ngay và dùng giá trị mặc định trong code.
- **Firebase chưa init:** Tự đăng ký với `FirebaseUtils.Instance.onInitialized` và fetch sau khi Firebase sẵn sàng.
- **Lỗi khi init:** Vẫn set `_remoteConfigReady = true` và gọi `OnFetchCompleted(false)` để game không bị chặn; giá trị dùng default.

## Thêm key mới

1. Thêm **public field** trong `FirebaseRemoteConfigUtils.cs` (tên field = key trên Remote Config):
   - `int` → key kiểu Number trên Firebase.
   - `bool` → key kiểu Boolean trên Firebase.
2. Thêm entry tương ứng vào `defaults` trong `SetupAndFetchAsync` (giá trị mặc định khi chưa fetch được).
3. Tạo key cùng tên và kiểu trong Firebase Console.

Reflection sẽ tự map key → field khi `UpdateKeysFromRemote()` chạy (sau fetch/activate).

## API nhanh

| Thành phần           | Mô tả |
|----------------------|--------|
| `IsRemoteConfigReady`| `true` khi đã init (và fetch xong trên device). |
| `OnFetchCompleted`   | `Action<bool>`: được gọi khi fetch xong (thành công = true). |
| `FetchAndActivate(onDone)` | Fetch lại và activate; gọi `onDone(bool)` khi xong. |
| Các field (inter_capping_time, enable_banner, ...) | Đọc trực tiếp sau khi config ready. |

---

*Phần Firebase của GameUp SDK.*
