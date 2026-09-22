# Changelog

Định dạng theo [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), phiên bản theo [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- **Nguồn PSD** (`Tools~/ui_psd.py`, psd-tools): Bước 2 chọn *Dò sprite trên ảnh demo* hoặc *Đọc file PSD*. Tọa độ từ
  layer; tab = nhóm layer ẩn/hiện; layer nối art đã cắt (tên → cỡ + pixel, dò art trong layer gộp, art trong khung ở tỉ
  lệ khác); text layer → chữ TMP đúng nội dung / rich text nhiều màu / căn lề / viền; lớp dim → `imgDim`; layer không có
  art xuất PNG (tự import Sprite), tách chữ vẽ sẵn (`*_extra`) và khung avatar (`*_frame`). Ảnh demo tuỳ chọn để so sánh,
  báo khi demo khác PSD. `locate.json` thêm `source`, `state(s)`, `exported`, `notes`, `matches[].layer`, `texts[].align/font/fontSize`.
- Generator: chữ dùng chung nằm trên ảnh riêng từng tab → nhân vào từng tab.
- **PSD dựng gọn như dò ảnh** (Dungeon ranking: 101 → 61 node, PNG shape rời → sprite dùng chung):
  - shape một màu (chữ nhật bo góc / tròn) → sprite trắng 9-slice dùng chung `shape_round_<r>` + `Image.color`
    (border tự set khi import); PNG gần trùng dùng chung file;
  - layer flatten tách theo mảng rời (ô vật phẩm, các hàng) thay vì một ảnh lớn làm cha mọi thứ; mảng lớn (cả hàng
    danh sách gộp một layer) **phân rã** thành chữ (OCR), art đã gặp trong màn (khung, avatar đúng tỉ lệ), khối một màu
    (vòng hạng, ô vuông, pill — kể cả khi bị khối khác che một đầu); phần còn lại mới là nền sạch;
  - chữ vẽ sẵn trong pixel layer → OCR thành chữ TMP (đo màu + viền);
  - danh sách nhận hàng khác nền / màu (top 1-3 màu riêng) → override `sprite` + `color` (`UISpecOverride.color` mới);
    slot chữ nới theo chữ dài nhất ("1" / "4-10"), chữ ghi đè tự co theo khung;
  - nút cùng chỗ đổi sprite giữa các tab → một nút dùng chung (không nhân vào từng nhóm tab);
  - con của panel lớn chia **box theo dải dọc**, tên theo nhóm layer PSD chung (`top1-3` → `boxTop13`);
  - tên node theo vai trò khi layer vô danh: `imgFill` (shape), `imgIcon` (≤ 96 px), `imgPart`.

### Fixed
- **Dò sprite (ảnh demo) thiếu phần tử** — màn Dungeon ranking (`ALGO_VERSION` 13):
  - khung viền mảnh thu nhỏ (khung avatar `card_list_frame` 0.53 / 0.545 / 0.62): sprite dạng viền (đục < 35%) chưa
    khớp 1:1 → quét tỉ lệ dày 0.01 lấy các đáy cục bộ, dò mịn ±0.015 bước 0.005; khớp thu nhỏ nới lệch màu (ZNCC ≥ 0.83
    → ≤ 20, ZNCC ≥ 0.86 → ≤ 25);
  - art 1:1 có viền khác demo (vương miện hàng 3, designer thêm stroke): nhận khi ruột trùng gần tuyệt đối (lệch ≤ 4,
    trùng ≥ 60%, ZNCC ≥ 0.6);
  - cùng art ở tỉ lệ lân cận (avatar hàng của mình 0.68 cạnh các hàng 0.66): dò thêm ±0.02 quanh tỉ lệ đã khớp;
  - gợi ý chéo giữa các tab kiểm thêm bằng `_verify` ±3 px khi viền mảnh không trùng tuyệt đối;
  - chữ số đứng riêng bị bộ phát hiện chữ bỏ sót (hạng "4") → hàng danh sách có chữ ở vị trí nào thì đọc thẳng cùng
    vị trí ở hàng khác (chỉ trên nền hàng, có mực khác sprite, OCR ≥ 0.9).
  Bench không đổi (A 9/9 · B 9/9 · C 5/5 · D 9/9 · E 18/22 · F 8/9 · G 7/9, 0 khớp thừa).
- Danh sách: khung avatar chứa avatar bị gộp chung một slot — slot ảnh phải cùng cỡ (±10%) và mỗi slot chỉ nhận một
  phần tử của hàng (khung 125 px + avatar 120 px cùng góc là 2 slot);
- OCR đọc ngược chữ ("999.99B" → "866'666") — tắt bộ xoay hướng chữ của RapidOCR;
  item trong ScrollRect xếp theo thứ tự hàng (trước theo diện tích).
- Chữ trong nhóm tab đang tắt không co theo bề ngang khung (TMP không đo được object inactive) — builder bật node trong
  lúc dựng, tắt lại sau.

- **Bước 3 xem trực tiếp máy đang định vị gì**: các giai đoạn (Chuẩn bị → Dò sprite → Lọc chéo → Tìm & đọc chữ), thanh tiến độ, việc đang làm và nhật ký (sprite khớp thế nào, vị trí bị loại vì sao, dòng chữ OCR đọc được). Ảnh demo bên phải vẽ khung ngay khi dò được, tô vàng sprite vừa dò xong.
- Preview bật/tắt 3 lớp: *Sprite* / *Chữ* / *Bị loại* (đỏ đứt). Rê chuột lên khung → tooltip: cách khớp (1:1, scale, 9-slice, tint, một màu), kích thước gốc, ZNCC / lệch màu / % pixel trùng; chữ: nội dung, độ tin cậy OCR, màu, viền.
- Kết quả Bước 3 có dòng tóm tắt (dò → khớp → lọc bỏ → chữ, thời gian, lớp dim) và mục *Chữ tìm được*, *Bị lọc bỏ*.
- `ui_locate.py` in sự kiện `@progress {json}` và ghi `dropped` (vị trí bị bộ lọc chéo bỏ + lý do) vào `locate.json`.
- Bước 3: nhiều demo có bảng trạng thái từng tab (đã/đang/chờ định vị, bấm để xem); nút mờ thì nói rõ còn thiếu gì; lỗi hiện bằng hộp lỗi. Bước 2 cảnh báo khi thư mục art quá rộng (≥ 200 PNG).
- **9-slice cho panel một màu**: dò 4 góc theo hình dáng (2 cạnh ngoài mỗi góc), kiểm tra cả khung sau khi kéo giãn.
- **Lọc khớp yếu từ màn khác** (`foreign-weak`) khi chọn thư mục art bao trùm nhiều màn (cả UI_v2): panel một màu / tint / 9-slice lỏng / scale sprite nhỏ từ thư mục không phải của màn (và không có sprite nào khớp chắc) → bỏ. Thư mục chứa demo và thư mục art không chứa demo (_Shared) luôn được tin — cách dùng thường không bị ảnh hưởng.
- Chữ: đọc cả chữ không nằm trên sprite nào (popup nổi trên lớp dim, panel chưa có art) nếu không bị làm tối — chữ gameplay sau lớp dim vẫn bị bỏ; quét thêm theo dải để bắt chữ ngắn trên nút / huy hiệu một chữ cái; tách khung OCR trùm 2 nhãn cạnh nhau.

- **Vùng UI chính**: tự nhận màn popup có lớp phủ tối (viền màn hình không có điểm nào sáng quá 170), khoanh vùng UI =
  phần sáng hơn hẳn phần bị phủ + chỗ sprite đã khớp, lấp kín ruột popup. Chữ ngoài vùng (title, thanh điều hướng của màn
  phía sau) bị bỏ; màn toàn màn hình (không lớp phủ) giữ nguyên. Preview có lớp *Vùng UI* (làm tối phần bị phủ).
  `locate.json` thêm `uiRegions`, `dimEstimated`.
- Độ đậm lớp phủ ước lượng từ điểm sáng nhất của phần bị phủ khi không đo được từ sprite gameplay (cả 6 demo thật trước
  đây đều không đo được) → spec luôn có `imgDim` cho popup, ghi chú rõ là ước lượng (cận trên).

- **Phần đã có sẵn — không dựng** (Bước 4): kéo khung trên ảnh demo quanh thanh điều hướng / thanh trên…; node nằm
  ≥ 60% trong vùng bị bỏ khỏi spec, vùng có prefab → node `instance` đặt đúng khung. Spec ghi lại `skipRegions`; prompt
  cho Claude liệt kê vùng không dựng.
- **Nhiều tab của cùng màn**: đối chiếu chéo (`--hint`) — vị trí tìm thấy ở tab khác được kiểm tra tại chỗ trên tab này
  (phần chung như navbar, nền không còn bị hiểu nhầm là phần riêng từng tab); cửa sổ tự chạy thêm lượt đối chiếu (có cache,
  vài giây). Dò bổ sung ô xếp sát nhau (navbar, lưới) kể cả khi ô đang chọn rộng hơn chen giữa.
- Ảnh nền cỡ cả màn bị che quá nửa: xét riêng tại (0,0); không làm cha của cả UI, anchor stretch, vẽ dưới cùng.
- Hình bóng một màu được tô màu (icon trắng + `Image.color`, vd icon ô trang bị): tìm theo hình dáng, ghi màu tô.
- Chữ: đọc khung từng ký tự, bỏ ký tự là icon đã khớp và khác màu chữ (icon hạng "S" "A" trước tên); màu chữ lấy ruột nét
  sáng nhất, trừ màu nền đo ở viền khung (nút đỏ chưa có art không còn nhuộm đỏ chữ); sprite hình ký tự / hộp một màu nằm
  trong dòng chữ bị bỏ (`inside-text`).

### Changed
- Định vị nhanh ~2× với thư mục art lớn: cổng "có mặt" rẻ trước khi dò kỹ — tương quan nét (mọi tỉ lệ) và tỉ lệ màu có trên demo (chỉ cho dò scale / 9-slice, vì tint đổi màu). Ngưỡng đặt từ đo đạc: khớp thật ≥ 0.38 / ≥ 0.88, cổng ở 0.25 / 0.6.
- Cửa sổ vẽ lại tối đa ~10 lần/giây khi đang chạy, kiểm tra file đổi mỗi giây một lần (trước: mỗi editor update). Timeout định vị 5 → 30 phút.
- Hai sprite khớp cùng chỗ: ưu tiên bản không tint, 1:1 hơn 9-slice, bản lớn hơn, rồi mới tới điểm khớp.
- Số process mặc định tối đa 12 (trước: số luồng − 1): đo A/B trên i5-14600K, 842 sprite — 12 process ~62 s, 19 process ~94 s
  (quá nhiều process chỉ tranh cache/băng thông bộ nhớ). Bước 3 có thanh *Process song song* (0 = tự động).
- Dò mịn quanh ứng viên trên ảnh xám (bước kiểm tra vẫn so đủ màu); số ứng viên theo số bản đặt vừa ảnh (panel cỡ popup
  không dò 32 ứng viên); sprite lớn chạy trước và nhận kết quả theo thứ tự xong — tiến trình trên Unity chạy đều.
- Chạy lại có cache: cache cả kết quả OCR thô theo hash demo (842 sprite: 6.2 s → 1.3 s).

### Fixed
- Windows: định vị hỏng ở bước cuối với `UnicodeEncodeError: 'charmap' codec…` — stdout của script giờ luôn UTF-8.
- Cửa sổ đọc `locate.json` đúng lúc script đang ghi → `IOException` làm vỡ layout cửa sổ.
- Bấm *Huỷ* định vị → `NullReferenceException` trong cùng lần vẽ.
- Khớp nhầm chỉ nhờ tương quan (ZNCC) khi gần như không có pixel trùng — thêm ngưỡng ≥ 30% pixel trùng (khớp thật đo được ≥ 60%).
- Popup bị title / nút X đè mép trên bị bỏ sót — viền nay xét theo từng cạnh (đủ ở ≥ 3/4 cạnh).
- Tint nhầm: sprite gần một màu nhân tint khớp vào nền phẳng — đòi hoạ tiết còn lại sau tô và chi tiết trùng.
- 9-slice nhầm nhỏ hơn tổng border; panel một màu kéo giãn bằng cả màn hình; bản 9-slice "góc giả" dưới title thắng bản 1:1 đúng.
- Chữ có viền biến mất khi font không có preset outline cho UI: builder chọn nhầm material shader 3D "(Surface)".
- Sprite một màu dạng hình bóng (icon cung, gậy phép trắng) khớp nhầm vào mũi tên trắng; 9-slice một màu chỉ 10% cùng màu.
- `imgDim` bị rơi khỏi spec: node tạo với `parent = null` nên bước sắp thứ tự vẽ (duyệt từ cha rỗng) bỏ qua.
- Chữ trắng không viền trên nền sáng bị coi là chữ vẽ sẵn trong art; nhãn và giá trị cùng hàng bị gộp thành một dòng.

## [0.1.0] - 2026-09-21

### Added
- Cửa sổ `GameUp → UI → UI Builder (Demo → Prefab)`: 5 bước có trạng thái — môi trường Python, đầu vào, định vị sprite, spec, dựng prefab — và overlay đối chiếu demo trên Scene View.
- `Tools~/ui_locate.py`: định vị sprite trên demo bằng OpenCV — đúng tỉ lệ, scale đều (dò thô rồi tinh chỉnh ±0.04), 9-slice (dò 4 góc), lặp nhiều lần, bị che ~25%; lọc khớp nhầm bằng ZNCC và loại vùng trùng pixel sprite khác. Chạy song song theo process, cache theo hash file. Demo 1080×2160 + 33 sprite: ~2.6 s lần đầu, ~0.2 s khi có cache.
- Cửa sổ 2 cột: bên trái các bước + thông tin chi tiết, bên phải ảnh demo cao bằng cửa sổ — đọc thẳng file gốc (không bị Max Size/nén của import settings), đúng tỉ lệ, tự ẩn khi cửa sổ quá hẹp; vẽ khung sprite đã định vị, rê chuột xem tên, bấm để chọn. Overlay Scene View dùng chung texture này. Cửa sổ tự tải lại khi `locate.json`/`spec.json` đổi từ bên ngoài.
- Nhận sprite bị che nhiều (nền popup dưới chữ/icon/nút): ≥ 50% pixel trùng tuyệt đối + viền ngoài lộ ≥ 85%; sprite một màu (panel) cần ≥ 70% + mép tương phản. Bỏ bản thường nằm trong bản 9-slice của chính nó.
- Tìm dòng chữ: ghép lại demo từ sprite đã khớp, vùng lệch dày đặc trên sprite → khung chữ + màu; spec nháp có node text (nội dung giữ chỗ), căn lề theo cột/lề; builder tự tính cỡ chữ khớp khung khi `fontSize` = 0. Cửa sổ có ô *Font cho text*.
- Đọc chữ bằng RapidOCR (chạy trên máy, chỉ bước nhận dạng): tách chữ theo màu chữ + thử 2 cỡ, lấy kết quả tin cậy nhất — 9/9 dòng đúng trên 3 demo mẫu, ~0.5 s. Spec nháp điền sẵn nội dung, id theo nội dung (`txtRemoveAds`), ghi chú dòng OCR không chắc. Không có OCR thì vẫn chạy, chữ giữ chỗ.
- Venv tự báo *Cập nhật* khi requirements trong package đổi (so hash). `rapidocr` cài `--no-deps` để không kéo `opencv-python` trùng với bản headless.
- **Danh sách → ScrollRect + item prefab**: nhận ≥ 3 khung cùng sprite cách đều; item = hợp nội dung các hàng, instance ghi đè sprite/chữ/ẩn; hàng lẻ cùng bố cục (hàng người chơi) thành instance ghi đè nền. Spec có `templates`, node `scroll` và `instance` (prefab lồng + `overrides`).
- **Nhiều demo = nhiều tab**: phần chung dựng một lần, phần riêng vào `grp<Tab>` (tab đầu bật); tên item/nhóm theo nhãn tab; ảnh so sánh cho từng tab. Cửa sổ: *＋ Demo tab khác*, chọn demo xem trước.
- Định vị mạnh hơn cho UI phức tạp: sprite một màu chốt bằng hình dáng (viền trong + vành ngoài tương phản ≥ 3/4 cạnh); ứng viên thêm theo nét và hình bóng (sprite bị chữ che gần hết); hàng lặp bị che nhiều được dò tiếp theo nhịp; 9-slice kiểm tra cả sprite sau kéo giãn; nhận tint (`Image.color`) và tách vật sau lớp dim (+ tự thêm `imgDim`); bỏ qua file `demo*`. Sửa lỗi viền sprite kín mép ảnh chỉ còn ở góc.
- Chữ: model phát hiện chữ của OCR trên vùng UI (bỏ qua avatar/panel), gộp từ cùng dòng, bỏ chữ vẽ sẵn trong art, màu theo ruột nét.
- **Viền chữ**: đo viền (màu, độ dày) quanh ruột chữ trên demo; builder gán material preset outline của chính font (cùng atlas, bỏ preset bóng đổ) có độ dày gần nhất, hoặc material chọn ở ô *Material viền chữ*. Chữ không viền giữ material hiện có.
- Một sprite ở nhiều tỉ lệ (avatar 0.66 trong hàng, 0.78 trong cờ top): dò tới 3 nhóm tỉ lệ cách nhau ≥ 0.08.
- Cột phần tử nằm trong hàng danh sách không còn bị tách thành danh sách thứ 2.
- Bước 2 gọn lại: danh sách demo một dòng mỗi ảnh (nút Tab chọn ảnh xem trước), nhóm ẢNH DEMO / THƯ MỤC ART / ĐẦU RA, cảnh báo demo khác kích thước.
- Venv dùng chung `~/.gameup/ui-builder/venv` (OpenCV + numpy), cài bằng một nút.
- `UISpecBuilder`: spec JSON → prefab uGUI đúng pixel ở độ phân giải tham chiếu; prefab có sẵn thì cập nhật theo tên node, giữ object/component làm tay; gắn component root và tự gán field trùng tên node.
- `UIPrefabRenderer`: render prefab trong PreviewScene (không đụng scene, không cần Play Mode) + ảnh so sánh demo | prefab | chồng 50%.
- `UIBuilderApi.BuildAndCompare(job)` cho AI gọi qua Unity MCP; skill Claude Code `gameup-ui-builder` + lệnh `/gu-ui`, cài từ cửa sổ.
