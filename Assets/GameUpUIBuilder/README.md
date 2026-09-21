# GameUp UI Builder

Dựng prefab UI (uGUI) từ **ảnh demo của designer + art đã cắt**. Máy dò vị trí và kích thước từng sprite trên demo (OpenCV), AI (Claude Code) bổ sung phần ngữ nghĩa, builder dựng prefab đúng pixel, rồi render ảnh so sánh để kiểm chứng.

```
demo.png + thư mục art ──► ① định vị (Python/OpenCV) ──► locate.json
                                                            │
                              ② spec nháp (tool) / hoàn thiện (Claude /gu-ui)
                                                            ▼
                                                        spec.json
                                                            │
                          ③ dựng / cập nhật prefab ──► ④ render so sánh (demo | prefab | chồng 50%)
```

## Cài đặt

- **Qua GameUp Core:** `GameUp → Project → GameUpCore Installer` → mục *Tùy chọn* → **GameUpUIBuilder** → *Cài qua Git UPM*.
- **Thủ công:** Package Manager → *Add package from git URL* → `https://github.com/ohze/gameup-unity-template.git?path=Assets/GameUpUIBuilder`

Cần GameUp Core và **Python 3.9+** trên máy (Ubuntu/Debian: thêm `python3-venv`). Lần đầu mở cửa sổ, bấm *Cài môi trường*: tool tạo venv dùng chung `~/.gameup/ui-builder/venv` và cài OpenCV + numpy, chỉ một lần cho mọi project.

## Dùng

`GameUp → UI → UI Builder (Demo → Prefab)`

1. **Môi trường Python**: cài một lần.
2. **Đầu vào**: tên UI, ảnh demo, các thư mục art (thư mục riêng của màn + `_Shared`), thư mục prefab.
3. **Chạy định vị**: danh sách sprite khớp (vị trí, scale, 9-slice) và sprite không khớp kèm lý do. Sprite dùng dạng 9-slice mà chưa có border sẽ có nhãn **CẦN BORDER** và gợi ý giá trị; set trong Sprite Editor.
4. **Spec**: chọn *Font cho text* (font TMP của game) rồi bấm *Tạo spec nháp*. Spec gồm mọi sprite đã định vị (lồng theo quan hệ chứa nhau) và mọi dòng chữ tìm được: đúng vị trí, màu, cỡ, căn lề, nhưng nội dung tạm là "Text" vì tool không đọc chữ. Sau đó chọn một trong hai cách:
   - *Copy prompt cho Claude* rồi dán vào Claude Code (đã *Cài skill* `/gu-ui`). Claude điền nội dung chữ, đặt tên node, gom nhóm, đặt anchor, ước lượng glow/art thiếu, tự dựng và đối chiếu qua Unity MCP.
   - Sửa tay `UIBuilder/<Tên>/spec.json`.
5. **Dựng prefab từ spec**: dựng xong tự render `compare.png`. Nếu prefab đã có, builder chỉ cập nhật node theo tên, giữ nguyên phần làm tay.

*Đối chiếu*: bật overlay để phủ ảnh demo lên Scene View, khớp với prefab đang mở trong Prefab Mode.

## Quy ước bàn giao art (để máy dò chính xác)

- Demo và art **cùng tỉ lệ** (mặc định 1080×2160). Art bị scale trên demo vẫn dò được, nhưng 1:1 là nhanh và chắc nhất.
- Tên file ảnh demo bắt đầu bằng `_demo` (vd `_demo_win.png`) để tool không coi demo là sprite.
- Nút, khung kéo giãn: cắt ở kích thước gốc. Tool tự nhận ra dạng 9-slice trên demo.
- Nền popup bị chữ/icon/nút che vẫn dò được, miễn phần lộ ra trùng pixel với art và viền ngoài còn thấy.
- Glow, vfx bán trong suốt không dò được bằng hình; AI hoặc người đặt theo mắt.
- Chữ chỉ tìm được khi nằm **trên** một sprite đã khớp; chữ nằm thẳng trên nền gameplay thì AI hoặc người thêm.

## Thư mục làm việc

`UIBuilder/<Tên>/` ở gốc project, nằm ngoài `Assets` nên không sinh `.meta`:

| File | Nội dung | Commit? |
|---|---|---|
| `spec.json` | Mô tả UI (người và AI cùng sửa) | Nên commit |
| `locate.json` | Kết quả định vị (sinh lại được) | Không |
| `render.png`, `compare.png` | Ảnh đối chiếu | Không |

Cache định vị nằm ở `Library/GameUpUIBuilder/cache`.

## Spec

Xem schema đầy đủ trong `AI~/SKILL.md`. Tóm tắt:
- `x,y,w,h` là pixel **tuyệt đối** trên demo, gốc trên-trái, áp dụng cho cả node con.
- Node cha phải đứng trước node con trong mảng; thứ tự trong mảng là thứ tự vẽ.
- `kind`: `empty` · `image` · `button` · `text` (TMP).
- `anchor`: `auto` hoặc một trong 13 preset. Ở độ phân giải tham chiếu, vị trí luôn đúng pixel; anchor chỉ quyết định cách co giãn trên màn khác tỉ lệ.
- `rootComponent`: tên class (vd `PopupResult : UIPopup`). Builder gắn component này vào root và tự gán các field `[SerializeField]` còn trống có tên trùng `id` node.

## API cho AI / script

```csharp
// Qua Unity MCP eval — ghi tên đầy đủ vì eval không nhận using:
return GameUp.UIBuilder.Editor.UIBuilderApi.BuildAndCompare("PopupResult");
```

Chạy định vị trực tiếp (không cần Unity):

```bash
~/.gameup/ui-builder/venv/bin/python <package>/Tools~/ui_locate.py \
  --demo Assets/.../_demo_win.png --art Assets/.../Result --art Assets/.../_Shared \
  --out UIBuilder/PopupResult/locate.json --cache Library/GameUpUIBuilder/cache
```

## Giới hạn

- Chỉ đọc file PNG riêng lẻ, không đọc sprite sheet nhiều sprite.
- Sprite bị tint màu hoặc xoay trên demo sẽ không khớp.
- Không tự thêm Layout Group, animation hay set border 9-slice.
