# Changelog

Định dạng theo [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), phiên bản theo [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **`GUInstallerUI`** — bộ widget dùng chung cho các cửa sổ setup (card, badge trạng thái, dòng trạng thái có nút, thanh tiến độ), cùng ngôn ngữ thiết kế với cửa sổ Setup Dependencies của GameUpSDK.
- **GameUpCore Installer trở thành cửa sổ duy nhất cần mở**: 3 bước có đánh số (DOTween → Folder Setup → Core setup) kèm badge trạng thái, thanh tiến độ tổng, phần Tùy chọn gom GameUpSDK / GameUpIAP (hiện version đã cài, nút copy Git URL), Helper packages, Logger và Audio setup.
- **Helper Package Installer nhận biết gói đã cài**: chọn nhiều gói bằng checkbox, cài theo hàng đợi có tiến độ "n/m", nút cài riêng từng gói. Trạng thái dựa trên marker path đã biết + so ảnh chụp thư mục `Assets` trước/sau khi import (ghi nhận lại để lần sau vẫn đúng, và tự trở về "chưa cài" nếu thư mục bị xoá).
- **Folder Setup**: thanh tiến độ, bộ lọc "chỉ hiện mục còn thiếu", nút **Tạo** cho từng thư mục thiếu và nút **Tạo** cho từng ScriptableObject thiếu, nút **Mở** để ping asset.
- **Audio Setup**: hiển thị rõ 2 giai đoạn với badge, số AudioClip trong Audio Folder, số AudioIdentity đã sinh, trạng thái `AudioID.cs`; nút Scan bị khoá kèm lý do khi chưa đủ điều kiện.
- `GUCoreProjectSetup.HasCoreObjectsInScene()` để installer biết scene đã có root Manager + UI hay chưa.

### Fixed
- **Trạng thái GameUpSDK / GameUpIAP luôn báo "chưa cài" khi cài qua Git UPM.** Package Git nằm trong `Library/PackageCache` chứ không phải `Packages/<tên>`, mà code chỉ kiểm tra thư mục vật lý. Nay hỏi AssetDatabase theo path ảo `Packages/<tên>` (Unity map mọi package vào đó) và đọc thêm version qua `PackageInfo`.
- **Folder Setup bị coi là "chưa xong" dù project đã đủ thư mục**, vì điều kiện phụ thuộc cờ `EditorPrefs` (mất khi clone project / đổi máy / đổi user) — kéo theo Core setup, Logger, Audio bị khoá menu vô lý. Nay trạng thái tính từ file thật trong project, cờ chỉ được đồng bộ lại theo đó.
- **Core setup chạy lần 2 tạo trùng `====Manager====` / `=====UI=====` trong scene**: prefab instance bị unpack ngay sau khi tạo nên không còn liên kết prefab để nhận diện; nay đối chiếu thêm theo tên root.
- Các cửa sổ setup tự vẽ lại khi project thay đổi hoặc khi lấy lại focus, thay vì giữ trạng thái cũ đến khi người dùng rê chuột vào.
- Thông báo cài DOTween / GameUpSDK / GameUpIAP không còn biến mất sau khi Unity reload domain (lưu qua `SessionState`).
- Trạng thái define `DOTween__DEPENDENCIES_INSTALLED` liệt kê đúng platform còn thiếu thay vì chỉ báo có/không.

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
- `LocalStorageUtils`: lỗi khi ĐỌC dữ liệu (giải mã/deserialize hỏng) hạ từ `Error` xuống `Warning` — đây là tình huống lường trước và đã có giá trị mặc định để lui. Lỗi khi GHI vẫn là `Error` vì có nguy cơ mất dữ liệu.

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
