# Changelog

Định dạng theo [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), phiên bản theo [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.2.0] - 2026-08-05

### Added
- `GUBootstrap`: chạy các bước khởi tạo theo thứ tự, có tiến độ và timeout cho từng bước.
- `GUSceneLoader`: load scene async có tiến độ, `minDuration` và kiểm soát thời điểm kích hoạt.
- `BaseDataSave`: đánh version dữ liệu (`dataVersion`) và hook `Migrate(fromVersion)` để nâng cấp save cũ; `Save()` chuyển thành public.
- `IPoolable` (`OnSpawn`/`OnDespawn`) và `GUPool.Prewarm(prefab, count)`.
- `AddressableLoad.WhenReady`: gom mẫu load Addressables dùng chung, có callback `onFailed`.
- Audio: `AudioCategory` (Sfx/Ui/Music), `AudioSetting.MusicVolume` / `SoundVolume`, crossfade nhạc nền, `PauseMusic`/`ResumeMusic`/`StopAllSfx`, và cập nhật volume ngay khi đổi cài đặt.
- Audio: dừng được âm thanh cụ thể — `AudioHandle` do `PlayAudio` trả về (`Stop(fade)`, `IsPlaying`) để dừng đúng một lần phát, và `AudioManager.StopAudio(identity | tên)` / `IsPlaying(...)` để dừng theo ID. Thêm `PlayAudio(string identityName)`.
- Test EditMode và PlayMode cho local storage, signal, save versioning và object pool.
- `README.md`, `CHANGELOG.md`.

### Changed
- Object pool dùng stack các object rảnh: Spawn/DeSpawn còn O(1) thay vì quét toàn bộ danh sách clone mỗi lần.
- Mọi API Open/Preload của screen và popup đi chung một đường `ResolveScreenAsync` / `ResolvePopupAsync`.
- `package.json`: khai báo thiếu `com.unity.textmeshpro`, chuyển hướng dẫn cài đặt sang README.

### Fixed
- `MonoSingleton` chết vĩnh viễn sau khi đổi scene do cờ "đang thoát app" bị bật trong `OnDestroy`.
- Cache static của `UIScreen`/`UIPopup` giữ lại instance đã bị hủy theo scene, gây `NullReferenceException` khi mở lại màn.
- `UIBaseAnimation.OnStart` gọi nhầm callback đóng thay vì callback mở.
- `UIBaseView.OnClose` set callback sau khi chạy reverse, khiến view không bao giờ ẩn khi không có DOTween.
- `LocalStorageUtils` đọc/ghi số theo culture của máy và không bắt exception khi dữ liệu hỏng.
- `GULogger` strip cả `Error`/`Exception` khỏi build release, làm mất log lỗi trên máy thật.
- `OnValidate` của `UIScreen`/`UIPopup`/`UIShowMoveItemAnimation` che mất bản của lớp cha; phần thêm/xoá component chuyển sang `delayCall`.
- `AudioManager` giữ `AudioSource` ở trạng thái busy vĩnh viễn khi clip load lỗi.
- `AddressableDataHolder` chờ vô hạn nếu một handle không hợp lệ.
- Pool rò rỉ dictionary theo dõi clone qua mỗi lần đổi scene.
- `CoroutineRunner` không chạy gì nếu instance chưa được tạo trước đó.

## [0.1.11] và trước đó

Chưa ghi changelog.
