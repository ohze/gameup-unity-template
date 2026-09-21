---
name: gameup-ui-builder
description: Dựng UI uGUI (screen/popup) từ ảnh demo của designer + art đã cắt bằng GameUp UI Builder — dùng tọa độ máy dò (OpenCV), AI chỉ bổ sung ngữ nghĩa (text, button, nhóm, anchor), dựng prefab qua Unity MCP rồi tự đối chiếu ảnh. Dùng khi người dùng đưa ảnh demo/mockup và muốn dựng prefab UI tương ứng.
---

# GameUp UI Builder — ảnh demo → prefab

Nguyên tắc số 1: **tọa độ lấy từ máy, không đoán.** `locate.json` cho vị trí chính xác tới pixel của mọi sprite dò được.
AI chỉ ước lượng những gì máy không dò được (text, glow, art thiếu) và quyết định ngữ nghĩa.

## Đầu vào

Prompt từ nút *Copy prompt cho Claude* (cửa sổ `GameUp → UI → UI Builder`) có đủ: ảnh demo, thư mục art, `locate.json`,
lệnh chạy lại định vị, `spec.json`, prefab đầu ra. Thiếu thì hỏi người dùng hoặc đọc `UserSettings/GameUpUIBuilder.asset`.

Thư mục job: `UIBuilder/<Tên>/` ở gốc project — `locate.json` (+ `locate_2.json`… mỗi demo tab khác), `spec.json`,
`render.png`, `compare.png` (+ `compare_2.png`… mỗi tab).

**Nhiều demo = nhiều trạng thái (tab) của cùng UI.** Spec nháp đã: dựng phần giống nhau một lần; phần riêng của demo k
nằm trong nhóm `stateGroups[k]` (chỉ nhóm đầu `active`); danh sách thành `scroll` + item prefab trong `templates`.

## Quy trình

1. **Xem demo** (Read file PNG). Tách 3 lớp: *phần thuộc UI này* · *nền gameplay/HUD phía sau* (thường bị làm mờ, KHÔNG đưa vào prefab) · *phần designer để sót* (ô xám, chữ ghost, layer thừa) → liệt kê, hỏi nếu không chắc.
2. **Đọc `locate.json`**. Chưa có hoặc art đổi → chạy lệnh trong prompt (Bash). Mỗi sprite: `matches[]` với `x,y,w,h` (pixel demo, gốc trên-trái), `scale`, `sliced` (kéo giãn 9-slice), `suggestedBorder`. `status: unmatched` kèm `reason`:
   - `soft-alpha` — glow/vfx bán trong suốt → ước lượng tâm + kích thước từ demo (thường giữ kích thước gốc `spriteWidth/Height`).
   - `no-match` — không có trên demo, hoặc demo vẽ khác art (vd khung bị che nhiều) → nhìn demo, nếu thấy thì ước lượng rect.
   - `explained-by-other` — trùng pixel sprite khác → bỏ qua.

   `dimAlpha`: độ đậm lớp dim phía sau popup (spec nháp đã có `imgDim`). `matches[].tint`: sprite bị tô màu trên demo (spec đã gán `color`).
   `texts[]`: các dòng chữ máy tìm được (nằm trên sprite đã khớp nhưng khác pixel sprite) — `x,y,w,h` là khung bao nét chữ, `color` là màu chữ chủ đạo, `text` + `confidence` là nội dung đọc bằng OCR (`ocr: "ok"`; `"unavailable"` = venv chưa có OCR → `text` rỗng). Spec nháp đã điền sẵn nội dung và đặt `id` theo nội dung (`txtRemoveAds`). Việc của AI: **soát** nội dung với demo — OCR hay bỏ ký hiệu đặc biệt (`₫`, `×`, icon chèn trong chữ) và đọc nhầm chữ khi `confidence` < 0.9 (có trong `notes`); đổi `id` cho đúng vai trò nếu cần (`txtPrice`); giữ nguyên `x,y,w,h,color,align`. Chữ nằm ngoài mọi sprite (vd "Tap to continue" trên nền) máy không thấy → AI tự thêm.
3. **Viết `spec.json`** (schema bên dưới). Có bản nháp do tool sinh → giữ nguyên tọa độ node đã có, chỉ đổi `id`, `kind`, `parent`, `anchor` và thêm node mới.
4. **Dựng + đối chiếu** qua Unity MCP `eval` (ghi tên đầy đủ, eval không nhận `using`):
   ```csharp
   return GameUp.UIBuilder.Editor.UIBuilderApi.BuildAndCompare("<Tên>");
   ```
   Trả về báo cáo + đường dẫn `compare.png` (demo | prefab | chồng 50%). **Read `compare.png`**, tìm chỗ lệch (bóng đôi ở khung chồng = sai vị trí; thiếu phần tử; text sai cỡ) → sửa spec → chạy lại. Tối đa 3 vòng, sau đó báo phần còn lệch.
   - **Cỡ chữ**: để `fontSize: 0` — builder tự tính theo font để chữ hoa cao bằng `h` và không tràn `w` (tính lúc dựng, không bật Auto Size). Chỉ đặt số cụ thể khi render vẫn lệch rõ (vd dòng toàn chữ thường), đo tỉ lệ chiều rộng demo/render rồi nhân.
   - **Glow/vfx** sáng hơn demo → giảm alpha qua `color` (vd `#FFFFFFB0`), không đổi sprite.
5. **Script (nếu cần)**: popup/screen kế thừa `UIPopup`/`UIScreen` của GameUp Core (tra skill `gameup-core-api`), namespace riêng của game, field `[SerializeField] private` tên **trùng `id` node** (vd `btnReward`) → set `rootComponent` = tên class, build lại: builder tự gắn component và gán các field còn trống.
6. **Báo cáo**: node đã dựng, phần ước lượng (không phải máy dò), art thiếu, sprite cần set border 9-slice (người dùng tự set trong Sprite Editor — không sửa `.meta`), phần designer để sót đã bỏ.

## Schema `spec.json`

```json
{
  "name": "PopupResult",
  "demo": "Assets/.../_demo_win.png",
  "referenceWidth": 1080, "referenceHeight": 2160,
  "output": "Assets/_MainProject/Prefabs/UI/Popups/PopupResult.prefab",
  "rootComponent": "",
  "nodes": [
    { "id": "imgTitle", "parent": "", "kind": "image", "x": 171, "y": 181, "w": 736, "h": 344,
      "anchor": "top", "sprite": "Assets/.../title_win.png", "sliced": false },
    { "id": "txtTitle", "parent": "imgTitle", "kind": "text", "x": 318, "y": 380, "w": 440, "h": 70,
      "text": "VICTORY", "fontSize": 64, "color": "#8A3A12", "align": "center" },
    { "id": "btnReward", "parent": "", "kind": "button", "x": 225, "y": 1629, "w": 631, "h": 217,
      "anchor": "bottom", "sprite": "Assets/.../btn_green.png", "sliced": true }
  ],
  "notes": []
}
```

| Trường | Ý nghĩa |
|---|---|
| `x,y,w,h` | Pixel **tuyệt đối** trên demo, gốc trên-trái — kể cả node con. Builder tự quy đổi tương đối cha. |
| `parent` | `id` node cha, rỗng = con của root. **Cha phải đứng trước con** trong mảng. |
| thứ tự mảng | Thứ tự vẽ: node sau nằm trên node trước cùng cha. |
| `kind` | `empty` (nhóm) · `image` · `button` (Image + Button) · `text` (TextMeshProUGUI) |
| `anchor` | `auto` · `center` · `top` · `bottom` · `left` · `right` · `top-left` · `top-right` · `bottom-left` · `bottom-right` · `stretch` · `stretch-top` · `stretch-middle` · `stretch-bottom`. Chỉ ảnh hưởng co giãn trên màn khác tỉ lệ; ở độ phân giải tham chiếu vị trí luôn đúng. |
| image/button | `sprite` (asset path), `sliced`, `preserveAspect`, `color` (#RRGGBB[AA]), `raycastTarget` |
| text | `text` (hỗ trợ rich text: `<color=#FFE030>Grandpa</color> Win!`), `fontSize` (0 = tự khớp khung), `font` (asset path TMP_FontAsset — tìm font game đang dùng, rỗng = mặc định TMP), `color`, `align` (left/center/right), `bold` |
| `active` | `false` cho biến thể ẩn (vd phần chỉ hiện khi thua) |
| `kind: scroll` | ScrollRect + Viewport (RectMask2D) + Content (LayoutGroup + ContentSizeFitter). `direction` vertical/horizontal, `spacing`, `padding`. Con của node scroll nằm trong Content, layout tự xếp (x,y chỉ để lấy cỡ). |
| `kind: instance` | Instance prefab lồng: `prefab` (asset path, thường là `templates[].output`), `overrides[]`: `{id, hide, sprite, setText, text}` — `id` = tên node trong item (`imgBg` = nền). |
| `templates[]` | Item prefab dựng trước prefab chính: `{name, output, width, height, nodes}` — tọa độ node **tương đối góc trên-trái item**; node `imgBg` là nền. |
| `stateGroups` | id nhóm của từng demo/tab theo thứ tự `demo`, `extraDemos` — renderer bật đúng nhóm khi render `compare_k.png`. |

## Luật đặt tên & cấu trúc

- `id` theo convention project (CLAUDE.md §3): tiền tố component — `btnReward`, `txtName`, `imgTitle`, `grpRewards` (nhóm). Không dấu cách, duy nhất.
- Text trong demo → node `text` riêng, con của phần tử chứa nó (chữ trên nút → con của nút). Không dùng sprite chứa chữ nếu art tách riêng nền.
- Danh sách (≥ 3 khung cùng sprite cách đều) spec nháp đã chuyển thành `scroll` + item prefab. Phần tử **không có art** trong hàng (avatar, ô điểm, ô vật phẩm) → thêm vào `templates[].nodes` của item (tọa độ tương đối), rồi ghi đè/ẩn ở instance nếu hàng khác nhau. Đổi `name` template cho đúng vai trò (`RankItem`, `RewardItem`) — nhớ đổi cả `output` và `prefab` của các instance.
- Phần lặp ít hơn 3 (2 ô thưởng) → gom vào node `empty` cha; builder không tự thêm Layout Group ngoài scroll.
- Nhiều biến thể (win/lose) cùng bố cục → **một prefab**, phần khác nhau là node riêng, biến thể phụ đặt `active: false` + ghi chú; không dựng hai prefab gần giống nhau (CLAUDE.md §5 — dùng Prefab Variant nếu khác nhiều).
- Nền gameplay/HUD phía sau popup không đưa vào. Lớp tối phủ màn hình (dim) nếu có → `image` `stretch`, không sprite, `color` `#000000B0`.
- Prefab đã tồn tại: builder chỉ cập nhật node theo `id`, giữ nguyên object/component dev thêm tay — **không đổi `id` node đã có** (sẽ tạo node mới, node cũ vẫn còn).

## Không được

- Không đoán lại tọa độ của sprite đã có trong `locate.json`.
- Không sửa `.prefab`/`.meta` bằng text; không tự set border sprite — ghi vào báo cáo cho người dùng.
- Không đọc `Library/` (cache của tool nằm ở đó, không cần).
