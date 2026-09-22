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

## Nguồn PSD (2026-09-22)

`Tools~/ui_psd.py` ghi cùng định dạng `locate*.json` (`source: "psd"`) nên generator/builder dùng chung. Luồng: tìm nhóm
trạng thái → ảnh so khớp **từ PSD** (ảnh ghép Photoshop cho trạng thái đang hiện trong file; trạng thái khác tự ghép từ
pixel layer, thiếu hiệu ứng) → lượt 1 art khớp theo tên (mọi tab) → xử lý tab ghép chuẩn trước, layer dùng chung + vị trí
art của shape có hiệu ứng (`Context.offsets`) dùng lại cho tab còn lại → layer chưa có art: `explained` / `sub_search`
(art 1:1 trong layer gộp) / `inner_search` (art ở tỉ lệ khác trong layer cỡ avatar) / xuất PNG → phần dư (`residual`).

Đã đo, đừng làm lại:
- `layer.composite()` của psd-tools trả ảnh **trong suốt** cho layer trong nhóm đang ẩn → dùng `topil()` + tự áp mask.
- `psd.composite(force=True)` cần scikit-image (~290 MB) cho hiệu ứng, aggdraw cho vector → không dùng; `topil()` của shape
  đã đúng màu fill.
- So art trên **ảnh demo** sai khi demo là bản cũ (Dungeon ranking: PSD hàng top 1-3 có màu, `demo_1.png` xám) → so trên
  ảnh từ PSD; pixel layer / smart object so trên chính pixel của layer (không bị che, đúng vị trí tuyệt đối).

Test thật: `~/Downloads/bossscreen_DuyLV.psd` (Dungeon popup_ranking, 2 tab) + art `UI_v2/Challenge Mode/popup_ranking`
+ `_Shared` → ~30 s, prefab 99 node, 2 item prefab; tab ranking trùng khít demo.

## Bản đồ code

| File | Vai trò |
|---|---|
| `Tools~/ui_locate.py` | Định vị sprite, tìm + đọc chữ, đo viền chữ, tint, lớp dim. `ALGO_VERSION` đổi → cache cũ tự bỏ. |
| `Tools~/ui_psd.py` | Đọc PSD → `locate*.json` mọi tab một lượt, nối layer ↔ art, xuất PNG layer thiếu art. |
| `Tools~/bench/` | Bộ sinh case khó có đáp án + bộ chấm (xem mục bên dưới). |
| `Editor/LocateProgress.cs` | Đọc dòng `@progress {json}` của script → giai đoạn, nhật ký, kết quả tạm cho preview khi đang chạy. |
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
- Vùng UI chính (`_ui_region`): chỉ bật khi viền màn hình tối (có lớp phủ). Sprite không cần lọc theo vùng — sprite dưới
  lớp phủ bị tối nên chỉ khớp dạng tint xám (đã bỏ ở `_split_dimmed`); vùng dùng để lọc chữ và ước lượng `dimAlpha`.
  Khung sprite gộp vào vùng đúng từng pixel (nới theo ô 16 px lấn sang chữ mờ sát nút → chữ rác "vunycun").

## Đã test trên dữ liệu thật

| Màn | Đường dẫn art | Kết quả |
|---|---|---|
| Arena — Popup No Ads, Win/Lose | `Arena-Image-Fight/Assets/_MainProject/Art/Popup/...` + `_Shared`, font `Art/Fonts/BOLDPIXELS SDF` | Gần như trùng khít; thiếu `₫` (OCR), chữ nằm ngoài sprite |
| Dungeon — popup_ranking (2 tab) | `dungeon-master-remake/.../UI_v2/Challenge Mode/popup_ranking` + `Party/Base` + `UI_v2/_Shared`, font `_MainProject/Fonts/FONNTS` | ScrollView + `LeaderboardItem`/`RankingRewardsItem`, hàng người chơi = instance, avatar cờ top, viền chữ; thiếu khung avatar (art chưa có), ô điểm/ô vật phẩm (chưa có art) |

### Bộ case khó tổng hợp (kiểm tra hồi quy khi sửa `ui_locate.py`)

`Tools~/bench/make_cases.py` sinh 7 demo có đáp án từ art UI_v2 thật (scale kiểu Photoshop, 9-slice cả panel một màu,
lớp dim + tint + bị che + chữ viền, danh sách lặp, JPEG, sprite gần giống / trùng art / icon 2048 px) + 120 sprite mồi;
`evaluate.py` chấm recall, khớp thừa, lệch vị trí, chữ. Chạy bằng python của venv:

```
~/.gameup/ui-builder/venv/Scripts/python Tools~/bench/make_cases.py <…/Art/UI_v2> <thư mục tạm>
~/.gameup/ui-builder/venv/Scripts/python Tools~/bench/evaluate.py <thư mục tạm> [A B …]
```

Kết quả hiện tại (2026-09-21): A 9/9 · B 9/9 · C 5/5 · D 9/9 · E 18/22 · F 8/9 · G 7/9, **0 khớp thừa** ở mọi case.
Hụt còn lại là giới hạn đã biết: ô bị icon che gần hết (E), avatar vừa bị chữ viền đè vừa nén JPEG (F), icon art 2048 px
thu nhỏ ~0.07 (G — chưa hỗ trợ tỉ lệ < 0.5). Thư mục art cả UI_v2 (842 sprite) cho demo ranking: 240 s → ~60–100 s lần
đầu (12 process; dao động theo máy), 1.3 s khi có cache (cache cả kết quả OCR thô), khớp đúng 15 sprite của màn.

**Hiệu năng — đã đo, đừng thử lại:**
- GPU không đáng: OpenCL của OpenCV (`cv2.UMat`) không nhanh hơn trên RTX 5070 (ảnh dò thô nhỏ, tốn chép qua GPU).
  Tương quan toàn ảnh chỉ ~46% thời gian → viết lại bằng CuPy tối đa ~1.5–1.8× lần đầu, thêm ~1 GB phụ thuộc; người
  dùng chọn không làm. OCR trên GPU (onnxruntime-directml) chỉ tiết kiệm ~3 s/demo.
- Lọc ứng viên bằng tương quan ở ảnh thu nhỏ: có ứng viên đúng tương quan ~0 → ngưỡng an toàn chỉ loại ~20% rác, bỏ.
- Tinh chỉnh tỉ lệ scale 3 điểm thay 5: điểm dò thô theo tỉ lệ KHÔNG có một đáy → mất avatar (ranking 8 → 4), đã hoàn tác.
- Thời gian trên máy dev dao động mạnh (cùng cấu hình 62–108 s) dù CPU không hạ xung, không tải khác; so sánh A/B phải
  chạy xen kẽ nhiều vòng. Tắt EcoQoS (`SetProcessInformation`) đã thử — không đổi, đã gỡ.

Ngưỡng mới đều đặt từ số đo khớp đúng vs khớp nhầm (ghi trong comment hằng số): `MIN_INLIER`, `TINT_MIN_STD`,
`TINT_DETAIL_*`, `PRESENCE_GATE`, `COLOR_GATE`, `FLOAT_TEXT_*`. Đổi ngưỡng → chạy lại bench + các demo thật.

Cách test nhanh (trong template): chép tạm art vào `Assets/_UIBuilderTest/`, chạy luồng qua MCP `eval`
(gọi tên đầy đủ `GameUp.UIBuilder.Editor.*`, eval không nhận `using`), đọc `compare*.png`, xoá `Assets/_UIBuilderTest` và `UIBuilder/` khi xong.

## Việc tiếp theo (đã đề xuất với người dùng, chưa làm)

1. **Ô giữ chỗ cho phần không có art** (avatar, ô điểm, ô vật phẩm): tạo `Image` trống đúng vị trí/cỡ trong item prefab từ vùng "lạ" trong hàng.
2. **Lấy lại phần tử bị vẽ lệch khi các hàng khác của danh sách có nó** (vương miện hàng 3 tab 1: viền chỉ khớp 76%) — nới ngưỡng tại đúng slot của template.
3. **Danh sách dạng lưới / hàng ngang** (GridLayoutGroup cho shop/inventory, ô thưởng ngang trong hàng).
4. **Sinh script popup** kế thừa `UIPopup` (GameUp Core) với field đã gán + hàm chuyển tab bật/tắt `stateGroups`.
5. Rút gọn `locate.json` khi đưa cho Claude (bỏ danh sách sprite không khớp) để giảm token.
6. Nút gợi ý set border 9-slice / import Sprite (hỏi trước khi sửa import settings).
9. **9-slice có góc thu nhỏ** (`Image.pixelsPerUnitMultiplier` = 2 — art xuất gấp đôi): nút Reset / "x1" màn Party
   (`btn_red`, `btn_gray` 512×190 hiện ~245×100). Dò góc ở bản 0.5 bằng `cv2.resize` chưa khớp (Unity lấy mẫu khác);
   cần thêm `pixelsPerUnitMultiplier` vào spec/builder.
10. Màn Party (thử 2026-09-22, `UIBuilder/PartyScreen`): còn thiếu nhân vật (art ngoài thư mục đã chọn), khung mô tả kỹ
    năng / icon kim cương / nền tab chưa chọn (art `bode_inforskill`, `frame_upgrade` không khớp — chưa rõ scale hay 9-slice).
7. Icon art độ phân giải lớn (Store 2048 px) dùng thu nhỏ ~0.07: thêm thang tỉ lệ theo cạnh đích (64–400 px) cho sprite
   lớn hơn demo, thay vì báo `too-large`.
8. Panel trắng một màu + `Image.color` (tint panel phẳng) — hiện nhánh một màu so khớp theo màu nên chưa nhận.

11. **PSD**: cỡ chữ trong item prefab lấy theo hàng mẫu ("1") → hàng ghi đè chữ dài hơn ("4-10") tràn khung — builder
    cần co lại chữ khi override `text`. Map font Photoshop (`texts[].font`) → TMP font asset theo tên. Chưa có cache
    (~30 s/lần với 78 art); thư mục art rộng > 400 PNG bỏ bước dò trong layer gộp. Chưa hỗ trợ Figma (cùng định dạng
    locate — chỉ cần script đọc REST API).

Chờ phản hồi người dùng sau khi test bản `f9b0e4a` ở dự án dungeon.

## Lưu ý môi trường

- Shell của Claude trên máy cũ không có credential GitHub → người dùng tự push (VS Code *Sync Changes*).
- Hai file `.claude/gameup-core/*` và thư mục `Assets/TextMesh Pro/` có thay đổi không thuộc tool (TMP do Unity tự nâng cấp) — không commit chung.
- Hook project chặn `rm -rf`: xoá từng file rồi `rmdir`.
- `GrabPixels` qua reflection chụp được cửa sổ Editor kể cả khi Unity bị che (màu hơi nhạt do không gian màu).
