# QuickStart

Ví dụ luồng khởi động chuẩn của một dự án dùng GameUp Core.

## Cách dùng

1. Tạo scene `Boot` và đặt nó ở build index 0.
2. Chạy **GameUp → Project → Core setup** để đưa prefab `====Manager====` và `=====UI=====` vào scene.
3. Gắn `BootstrapExample` lên một GameObject trống trong scene `Boot`.
4. Điền tên scene kế tiếp (ví dụ `MainMenu`) vào field `nextScene` và đảm bảo scene đó có trong Build Settings.

`BootstrapExample` minh hoạ:

- Đăng ký các bước khởi tạo theo thứ tự bằng `GUBootstrap.AddStep`.
- Nhận tiến độ qua `GUBootstrap.OnProgress` để cập nhật thanh loading.
- Chuyển scene bằng `GUSceneLoader.LoadAsync` với `minDuration` để màn loading không nhấp nháy.
- Kiểm tra `GUBootstrap.FailedSteps` để biết bước nào timeout.
