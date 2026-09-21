# GameUp UI Builder — ghi chú phát triển (bàn giao)

Ghi chú cho người/AI phát triển tiếp **chính tool** (không phải hướng dẫn dùng — xem `../README.md`).
Cập nhật: 2026-09-21. Nhánh: `feat/ui-builder` (chưa merge vào `main`).

## Bắt đầu phiên mới

Mở Claude Code ở gốc repo `gameup-unity-template`, checkout `feat/ui-builder`, rồi nhắn:

> Đọc `Assets/GameUpUIBuilder/Documentation~/dev-handoff.md` và làm tiếp mục "Việc tiếp theo".

Cần Unity Editor mở project này + Unity MCP (`unity-editor-mcp`) để recompile/chạy test/dựng thử.

## Tool làm gì

Ảnh demo của designer (1080×2160) + thư mục art → prefab uGUI đúng pixel. Luồng:

```
demo*.png + art ──► Tools~/ui_locate.py (OpenCV + RapidOCR) ──► UIBuilder/<Tên>/locate.json (+ locate_2.json… mỗi tab)
                 ──► UISpecGenerator (C#) ──► UIBuilder/<Tên>/spec.json
                 ──► UISpecBuilder ──► prefab chính + item prefab (templates)
                 ──► UIPrefabRenderer ──► compare.png (demo | prefab | chồng 50%) mỗi tab
```

## Bản đồ code

| File | Vai trò |
|---|---|
| `Tools~/ui_locate.py` | Định vị sprite, tìm + đọc chữ, đo viền chữ, tint, lớp dim. `ALGO_VERSION` đổi → cache cũ tự bỏ. |
| `Tools~/requirements*.txt` | venv `~/.gameup/ui-builder/venv`; `rapidocr` cài `--no-deps` (tránh `opencv-python` trùng bản headless). |
| `Editor/UIBuilderWindow.cs` | Cửa sổ 2 cột (các bước bên trái, ảnh demo bên phải), nhiều demo = nhiều tab. |
| `Editor/UISpecGenerator.cs` | locate → spec: node, cha/con theo chứa nhau, căn lề chữ, gộp nhiều trạng thái (`grp<Tab>`), `imgDim`. |
| `Editor/UIListExtractor.cs` | Hàng lặp → `scroll` + item prefab (`templates`), instance + `overrides`; hàng lẻ cùng bố cục → instance. |
| `Editor/UISpecBuilder.cs` | spec → prefab (cập nhật theo tên node, giữ phần làm tay), scroll/instance, cỡ chữ tự khớp, material outline. |
| `Editor/UIPrefabRenderer.cs` | Render PreviewScene + ảnh so sánh từng tab. |
| `Editor/UIBuilderApi.cs` | `BuildAndCompare(job)` cho AI qua MCP `eval`. |
| `AI~/SKILL.md`, `AI~/gu-ui.md` | Skill Claude `/gu-ui` (cài vào `.claude/` của project dùng tool). |
| `Tests/Editor/*` | 29 EditMode test (builder, generator, list extractor). |

## Quyết định thiết kế đã chốt (đừng đổi ngược nếu không có lý do)

- Tọa độ lấy từ máy dò, AI chỉ bổ sung ngữ nghĩa. Spec dùng pixel **tuyệt đối** trên demo; builder quy đổi theo anchor.
- Khớp sprite: ZNCC ≥ 0.8 **hoặc** bị che (≥ 50% pixel trùng tuyệt đối + viền ngoài ≥ 85%). Sprite một màu: chốt bằng hình dáng (viền trong + vành ngoài tương phản ≥ 3/4 cạnh), bỏ nếu nằm trong panel cùng màu hoặc chồng lấn nhau.
- Ứng viên dò thô từ 3 nguồn: điểm màu, nét (Canny), hình bóng ngoài. Tất cả qua cùng bước kiểm tra chặt.
- Tint (`Image.color`) chỉ ở 1:1; tint xám đều ≤ 0.6 = vật sau lớp dim → bỏ khỏi UI, lấy độ đậm làm `imgDim`.
- Chữ: model phát hiện chữ của OCR trên vùng UI; bỏ chữ vẽ sẵn trong art; màu = ruột nét; viền đo riêng.
- Thứ tự vẽ: vật nằm trong vật khác vẽ sau; cùng cấp thì sprite phẳng trước, lớn trước nhỏ. Sprite phẳng không làm cha khi có sprite hoạ tiết chứa được.
- Border 9-slice: chỉ gợi ý, không tự sửa import settings (người dùng tự set).

## Đã test trên dữ liệu thật

| Màn | Đường dẫn art | Kết quả |
|---|---|---|
| Arena — Popup No Ads, Win/Lose | `Arena-Image-Fight/Assets/_MainProject/Art/Popup/...` + `_Shared`, font `Art/Fonts/BOLDPIXELS SDF` | Gần như trùng khít; thiếu `₫` (OCR), chữ nằm ngoài sprite |
| Dungeon — popup_ranking (2 tab) | `dungeon-master-remake/.../UI_v2/Challenge Mode/popup_ranking` + `Party/Base` + `UI_v2/_Shared`, font `_MainProject/Fonts/FONNTS` | ScrollView + `LeaderboardItem`/`RankingRewardsItem`, hàng người chơi = instance, avatar cờ top, viền chữ; thiếu khung avatar (art chưa có), ô điểm/ô vật phẩm (chưa có art) |

Cách test nhanh (trong template): chép tạm art vào `Assets/_UIBuilderTest/`, chạy luồng qua MCP `eval`
(gọi tên đầy đủ `GameUp.UIBuilder.Editor.*`, eval không nhận `using`), đọc `compare*.png`, xoá `Assets/_UIBuilderTest` và `UIBuilder/` khi xong.

## Việc tiếp theo (đã đề xuất với người dùng, chưa làm)

1. **Ô giữ chỗ cho phần không có art** (avatar, ô điểm, ô vật phẩm): tạo `Image` trống đúng vị trí/cỡ trong item prefab từ vùng "lạ" trong hàng.
2. **Lấy lại phần tử bị vẽ lệch khi các hàng khác của danh sách có nó** (vương miện hàng 3 tab 1: viền chỉ khớp 76%) — nới ngưỡng tại đúng slot của template.
3. **Danh sách dạng lưới / hàng ngang** (GridLayoutGroup cho shop/inventory, ô thưởng ngang trong hàng).
4. **Sinh script popup** kế thừa `UIPopup` (GameUp Core) với field đã gán + hàm chuyển tab bật/tắt `stateGroups`.
5. Rút gọn `locate.json` khi đưa cho Claude (bỏ danh sách sprite không khớp) để giảm token.
6. Nút gợi ý set border 9-slice / import Sprite (hỏi trước khi sửa import settings).

Chờ phản hồi người dùng sau khi test bản `f9b0e4a` ở dự án dungeon.

## Lưu ý môi trường

- Shell của Claude trên máy cũ không có credential GitHub → người dùng tự push (VS Code *Sync Changes*).
- Hai file `.claude/gameup-core/*` và thư mục `Assets/TextMesh Pro/` có thay đổi không thuộc tool (TMP do Unity tự nâng cấp) — không commit chung.
- Hook project chặn `rm -rf`: xoá từng file rồi `rmdir`.
- `GrabPixels` qua reflection chụp được cửa sổ Editor kể cả khi Unity bị che (màu hơi nhạt do không gian màu).
