# Changelog

Định dạng theo [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), phiên bản theo [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-09-21

### Added
- Cửa sổ `GameUp → UI → UI Builder (Demo → Prefab)`: 5 bước có trạng thái — môi trường Python, đầu vào, định vị sprite, spec, dựng prefab — và overlay đối chiếu demo trên Scene View.
- `Tools~/ui_locate.py`: định vị sprite trên demo bằng OpenCV — đúng tỉ lệ, scale đều (dò thô rồi tinh chỉnh ±0.04), 9-slice (dò 4 góc), lặp nhiều lần, bị che ~25%; lọc khớp nhầm bằng ZNCC và loại vùng trùng pixel sprite khác. Chạy song song theo process, cache theo hash file. Demo 1080×2160 + 33 sprite: ~2.6 s lần đầu, ~0.2 s khi có cache.
- Cửa sổ 2 cột: bên trái các bước + thông tin chi tiết, bên phải ảnh demo cao bằng cửa sổ — đọc thẳng file gốc (không bị Max Size/nén của import settings), đúng tỉ lệ, tự ẩn khi cửa sổ quá hẹp; vẽ khung sprite đã định vị, rê chuột xem tên, bấm để chọn. Overlay Scene View dùng chung texture này. Cửa sổ tự tải lại khi `locate.json`/`spec.json` đổi từ bên ngoài.
- Nhận sprite bị che nhiều (nền popup dưới chữ/icon/nút): ≥ 50% pixel trùng tuyệt đối + viền ngoài lộ ≥ 85%; sprite một màu (panel) cần ≥ 70% + mép tương phản. Bỏ bản thường nằm trong bản 9-slice của chính nó.
- Tìm dòng chữ: ghép lại demo từ sprite đã khớp, vùng lệch dày đặc trên sprite → khung chữ + màu; spec nháp có node text (nội dung giữ chỗ), căn lề theo cột/lề; builder tự tính cỡ chữ khớp khung khi `fontSize` = 0. Cửa sổ có ô *Font cho text*.
- Đọc chữ bằng RapidOCR (chạy trên máy, chỉ bước nhận dạng): tách chữ theo màu chữ + thử 2 cỡ, lấy kết quả tin cậy nhất — 9/9 dòng đúng trên 3 demo mẫu, ~0.5 s. Spec nháp điền sẵn nội dung, id theo nội dung (`txtRemoveAds`), ghi chú dòng OCR không chắc. Không có OCR thì vẫn chạy, chữ giữ chỗ.
- Venv tự báo *Cập nhật* khi requirements trong package đổi (so hash). `rapidocr` cài `--no-deps` để không kéo `opencv-python` trùng với bản headless.
- Venv dùng chung `~/.gameup/ui-builder/venv` (OpenCV + numpy), cài bằng một nút.
- `UISpecBuilder`: spec JSON → prefab uGUI đúng pixel ở độ phân giải tham chiếu; prefab có sẵn thì cập nhật theo tên node, giữ object/component làm tay; gắn component root và tự gán field trùng tên node.
- `UIPrefabRenderer`: render prefab trong PreviewScene (không đụng scene, không cần Play Mode) + ảnh so sánh demo | prefab | chồng 50%.
- `UIBuilderApi.BuildAndCompare(job)` cho AI gọi qua Unity MCP; skill Claude Code `gameup-ui-builder` + lệnh `/gu-ui`, cài từ cửa sổ.
