#!/usr/bin/env python3
"""
GameUp UI Builder — đọc file PSD thay cho dò sprite trên ảnh demo.

PSD có sẵn tọa độ từng layer, nội dung/font/màu/viền của text layer, layer ẩn/hiện. Script:
  - tìm các trạng thái (tab) của màn: nhóm layer ẩn/hiện chồng khít nhau (vd "leaderboard" / "ranking"),
  - mỗi trạng thái → một file locate cùng định dạng ui_locate.py (locate.json, locate_2.json…), nên phần sinh spec /
    dựng prefab dùng chung với chế độ dò sprite,
  - nối mỗi layer với art đã cắt: theo tên layer trước, rồi theo kích thước + pixel (so trên ảnh trạng thái — ảnh demo
    của designer nếu có, không thì ảnh ghép từ PSD), chỉnh vị trí trong cửa sổ nhỏ quanh layer,
  - layer không có art: xuất PNG từ chính layer (tuỳ chọn --export), layer nằm dưới lớp dim đen phủ màn = gameplay phía
    sau, bỏ; layer nằm gọn trong art đã khớp và pixel đã có trong art đó (hình vẽ sẵn trong art), bỏ.

Tiến trình: dòng stdout "@progress {json}" giống ui_locate.py.
"""
import argparse
import hashlib
import json
import logging
import math
import os
import re
import sys
import time
import warnings

import cv2
import numpy as np

warnings.filterwarnings("ignore")
logging.disable(logging.CRITICAL)  # psd-tools log cảnh báo cho mọi tag chưa hỗ trợ

from psd_tools import PSDImage  # noqa: E402

VERSION = 1
PROGRESS_PREFIX = "@progress "

ALPHA_OPAQUE = 200          # pixel art có alpha > ngưỡng mới dùng để so khớp (giống ui_locate.py)
ALPHA_TRIM = 8              # khung nội dung art: bỏ viền trong suốt (art hay chừa lề vài px so với layer)
STATE_MIN_AREA = 0.15       # nhóm trạng thái chiếm ≥ 15% khung màn
STATE_IOU = 0.6             # ... và chồng khít nhau (IoU khung nội dung ≥ 0.6)
DIM_COVER = 0.95            # layer phủ ≥ 95% màn, gần đen, đều màu = lớp dim của popup
DIM_MAX_RGB = 40
SIZE_SLACK = 6              # art cắt lệch khung layer vài px (khử răng cưa, làm tròn)
SCALE_ASPECT = 0.04         # tỉ lệ ngang/dọc lệch ≤ 0.04 mới coi là scale đều
SCALE_RANGE = (0.25, 1.6)
SEARCH_PAD = 8              # cửa sổ tìm vị trí = khung layer (+ hiệu ứng) nới thêm 8 px
FLAT_STD = 6.0              # art gần như một màu: vị trí theo tâm layer, không tìm theo pixel
EXACT_TOLERANCE = 14        # sai lệch màu (0-255) coi là "trùng" — art xuất lại / nén nhẹ
ACCEPT_ZNCC = 0.80
ACCEPT_DIFF = 20.0
ACCEPT_INLIER = 0.50
EXACT_INLIER = 0.85         # ≥ 85% pixel trùng gần tuyệt đối là đủ (nút có chữ vẽ sẵn: ZNCC chỉ 0.71, trùng 90%)
NAMED_ZNCC = 0.50           # cùng tên + đúng cỡ = bằng chứng mạnh; phần giữa art bị layer con che nhiều
NAMED_INLIER = 0.30
FLAT_BAND = 4               # art một màu: kiểm dải viền trong dày 4 px (ruột panel bị nội dung che gần hết)
FLAT_BAND_INLIER = 0.75
NAMED_FLAT_BAND_INLIER = 0.50  # cùng tên: cho phép một cạnh bị che (banner tiêu đề đè mép trên popup)
FLAT_PLAIN_INLIER = 0.60    # art một màu lạ (9-slice): cả ruột cũng phải trùng — không thì khớp vào mọi mảng cùng màu
PREFILTER_ZNCC = 0.30       # art không cùng tên: đặt thử ở tâm layer, lệch quá xa thì khỏi tìm vị trí (tiết kiệm ~90%)
PREFILTER_INLIER = 0.20
INNER_ZNCC = 0.85           # art nằm bên trong pixel layer ở tỉ lệ khác (avatar trong khung avatar flatten)
INNER_MIN_SCALE = 0.5
INNER_COARSE_STEP = 0.05    # dò thô theo bước 0.05 rồi tinh chỉnh ±0.04 bước 0.01
INNER_MAX_AREA = 400 * 400  # chỉ layer cỡ icon/avatar — layer lớn thử nhiều tỉ lệ rất chậm
REPEAT_BAND = 0.80          # art đã khớp ở chỗ khác trong màn (hàng danh sách): nới ngưỡng như cùng tên, viền lộ ≥ 80%
SUB_PEAKS_DIFF = 60.0       # đỉnh ứng viên khi dò trong layer gộp: sai lệch RMS ≤ 60 (hàng bị nội dung che nhiều)
INNER_MIN_COVER = 0.25      # ... art chiếm ≥ 25% diện tích layer
INNER_ASPECT = 0.2
RESIDUAL_DIFF = 40          # pixel layer khác art tại chỗ > 40 = phần art không có (chữ vẽ sẵn, khung) → xuất riêng
RESIDUAL_MIN = 0.02         # ... khi chiếm ≥ 2% layer
EXPLAINED_INLIER = 0.75     # layer nằm trong art đã khớp và ≥ 75% pixel của layer trùng art → hình vẽ sẵn trong art
SUB_MIN_AREA = 0.02         # dò art bên trong layer gộp (nhiều hàng flatten thành một) — art ≥ 2% diện tích layer
SUB_MAX_INSTANCES = 12
SUB_MAX_ARTS = 400          # thư mục art quá rộng → bỏ bước dò trong layer gộp (chậm, dễ khớp nhầm)
MIN_EXPORT = 4              # layer nhỏ hơn 4 px mỗi chiều → bỏ, không xuất
FLAT_SHAPE_STD = 4.0        # layer một màu (lệch ≤ 4) hình chữ nhật bo góc / tròn → sprite trắng 9-slice dùng chung + màu
FLAT_SHAPE_MISS = 0.02      # hình bo góc vẽ lại lệch hình layer ≤ 2% diện tích (bỏ qua dải mép 1 px khử răng cưa)
NEAR_DUPLICATE = 3.0        # PNG xuất lệch màu trung bình ≤ 3 với file đã xuất cùng cỡ = dùng chung file
PART_DILATE = 2             # tách layer gộp: các mảng cách nhau > 2 px là phần tử riêng
PART_MIN_PIXELS = 64
TEXT_OCR_SCORE = 0.80       # chữ vẽ sẵn trong layer: OCR đọc được (≥ 0.8) → thành text TMP thay vì ảnh
DECOMPOSE_MIN_AREA = 150 * 60  # mảng flatten lớn (cả hàng danh sách) → tách chữ / art / khối màu, còn lại mới là nền
TEXT_INK_DIFF = 40          # pixel chữ = khác màu nền quanh khung chữ > 40
SHAPE_MIN_PIXELS = 300      # khối một màu trong mảng flatten (vòng hạng, ô vuông, pill điểm) ≥ 300 px
SHAPE_COLOR_TOL = 10
SEEN_ZNCC = 0.80            # art đã khớp ở chỗ khác trong màn, tìm lại trong mảng flatten (khung avatar 0.83, lệch màu 17)
SEEN_NEW_PIXELS = 0.25      # ... nhận nếu thêm ≥ 25% pixel chưa art nào phủ (khung ngoài avatar ~33%; art gần trùng ~0)


def emit(phase, **data):
    print(PROGRESS_PREFIX + json.dumps({"phase": phase, **data}, ensure_ascii=False), flush=True)


# ─── Art ────────────────────────────────────────────────────────────────────

class Art:
    """Một file art: ảnh BGRA, khung nội dung (bỏ viền trong suốt), border 9-slice đọc từ .meta của Unity."""

    def __init__(self, path, rgba):
        self.path = path
        self.name = os.path.splitext(os.path.basename(path))[0]
        self.key = normalize_name(self.name)
        self.rgba = rgba
        self.h, self.w = rgba.shape[:2]
        ys, xs = np.nonzero(rgba[:, :, 3] > ALPHA_TRIM)
        if len(xs) == 0:
            self.trim = (0, 0, self.w, self.h)
        else:
            self.trim = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)
        self.tw = self.trim[2] - self.trim[0]
        self.th = self.trim[3] - self.trim[1]
        opaque = rgba[:, :, 3] > ALPHA_OPAQUE
        self.opaque = int(opaque.sum())
        self.flat = self.opaque > 0 and float(rgba[:, :, :3][opaque].std(axis=0).mean()) < FLAT_STD
        self.border = read_border(path)


def normalize_name(name):
    """'border_popup copy 15' → 'border_popup'; 'Btn Tab 1' → 'btn_tab_1'."""
    name = re.sub(r"(\s+copy(\s+\d+)?)+$", "", name.strip(), flags=re.IGNORECASE)
    return re.sub(r"[\s\-]+", "_", name).lower()


def read_border(path):
    """spriteBorder trong .meta (x = trái, y = dưới, z = phải, w = trên); None nếu chưa set."""
    try:
        with open(path + ".meta", encoding="utf-8") as f:
            m = re.search(r"spriteBorder:\s*\{x:\s*([\d.]+),\s*y:\s*([\d.]+),\s*z:\s*([\d.]+),\s*w:\s*([\d.]+)\}", f.read())
    except OSError:
        return None
    if not m:
        return None
    left, bottom, right, top = (int(float(v)) for v in m.groups())
    return (left, top, right, bottom) if left or top or right or bottom else None


def list_art(paths, recursive):
    files = []
    for p in paths:
        if os.path.isfile(p) and p.lower().endswith(".png"):
            files.append(p)
            continue
        if not os.path.isdir(p):
            continue
        if recursive:
            for root, _, names in os.walk(p):
                files.extend(os.path.join(root, n) for n in names if n.lower().endswith(".png"))
        else:
            files.extend(os.path.join(p, n) for n in os.listdir(p) if n.lower().endswith(".png"))
    # demo*.png nằm cùng thư mục art là ảnh demo, không phải art (giống ui_locate.py)
    files = [f for f in files if not os.path.basename(f).lstrip("_").lower().startswith("demo")]
    return sorted(set(os.path.normpath(f) for f in files))


def load_rgba(path):
    img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    if img is None:
        return None
    if img.ndim == 2:
        return cv2.cvtColor(img, cv2.COLOR_GRAY2BGRA)
    if img.shape[2] == 3:
        return cv2.cvtColor(img, cv2.COLOR_BGR2BGRA)
    return img


def pil_to_bgra(image):
    rgba = np.asarray(image.convert("RGBA"))
    return cv2.cvtColor(rgba, cv2.COLOR_RGBA2BGRA)


# ─── Cây layer ──────────────────────────────────────────────────────────────

class Leaf:
    """Layer lá (pixel / shape / smart object / type) của một trạng thái, tọa độ theo khung màn (artboard)."""

    def __init__(self, layer, order, box, opacity, group):
        self.layer = layer
        self.order = order
        self.box = box  # (x0, y0, x1, y1), đã kẹp trong khung màn
        self.opacity = opacity
        self.name = layer.name
        self.kind = layer.kind
        self.group = group  # đường dẫn nhóm layer PSD từ khung màn, "popup/top1-3/flag" — để đặt tên box

    @property
    def w(self):
        return self.box[2] - self.box[0]

    @property
    def h(self):
        return self.box[3] - self.box[1]


def enabled_effects(layer):
    try:
        return [e for e in layer.effects if e.enabled]
    except Exception:  # noqa: BLE001 — layer không có tag hiệu ứng / tag lạ
        return []


def effect_names(layer):
    return [type(e).__name__ for e in enabled_effects(layer)]


def effect_pad(layer):
    """Hiệu ứng tràn ra ngoài khung layer (px): stroke ngoài/giữa, bóng đổ, glow ngoài — art cắt kèm phần này."""
    pad = 0.0
    for e in enabled_effects(layer):
        name = type(e).__name__
        size = float(getattr(e, "size", 0) or 0)
        if name == "Stroke":
            position = str(getattr(e, "position", "")).lower()
            pad = max(pad, 0.0 if "in" in position and "out" not in position else size / 2 if "cent" in position else size)
        elif name == "DropShadow":
            pad = max(pad, float(getattr(e, "distance", 0) or 0) + size)
        elif name == "OuterGlow":
            pad = max(pad, size)
    return int(math.ceil(pad))


def content_box(layer):
    """Khung hợp mọi layer lá bên trong (kể cả đang ẩn) — nhóm ẩn có bbox rỗng trong psd-tools."""
    if not layer.is_group():
        return layer.bbox if layer.bbox[2] > layer.bbox[0] else None
    box = None
    for child in layer.descendants():
        if child.is_group() or child.bbox[2] <= child.bbox[0]:
            continue
        b = child.bbox
        box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    return box


def iou(a, b):
    ix = max(0, min(a[2], b[2]) - max(a[0], b[0]))
    iy = max(0, min(a[3], b[3]) - max(a[1], b[1]))
    inter = ix * iy
    union = (a[2] - a[0]) * (a[3] - a[1]) + (b[2] - b[0]) * (b[3] - b[1]) - inter
    return inter / union if union > 0 else 0.0


def find_root(psd, artboard):
    """Khung màn: artboard (theo tên, hoặc artboard duy nhất / đầu tiên), không có thì cả file."""
    boards = [l for l in psd if l.kind == "artboard"]
    if artboard:
        boards = [b for b in boards if b.name == artboard] or boards
    if boards:
        b = boards[0]
        return b, b.bbox, len([l for l in psd if l.kind == "artboard"])
    return psd, (0, 0, psd.width, psd.height), 0


def detect_states(root, canvas, names):
    """
    Nhóm trạng thái: các nhóm anh em, có nhóm ẩn và nhóm hiện, chồng khít nhau và đủ lớn. Lấy bộ lớn nhất trong cây —
    bộ nhỏ bên trong (bản nháp ẩn cạnh bản thật) là rác của designer, không phải trạng thái. Thứ tự = thứ tự vẽ.
    """
    groups = [g for g in [root] + list(root.descendants()) if g.is_group()]
    if names:
        wanted = [n.strip() for n in names.split(",") if n.strip()]
        for g in groups:
            kids = [c for c in g if c.is_group() and c.name in wanted]
            if len(kids) == len(wanted):
                return sorted(kids, key=lambda c: wanted.index(c.name))
        return []

    area = (canvas[2] - canvas[0]) * (canvas[3] - canvas[1])
    best, best_area = [], 0
    for g in groups:
        kids = []
        for c in g:
            box = content_box(c) if c.is_group() else None
            if box and (box[2] - box[0]) * (box[3] - box[1]) >= area * STATE_MIN_AREA:
                kids.append((c, box))
        hidden = [(c, b) for c, b in kids if not c.visible]
        shown = [(c, b) for c, b in kids if c.visible]
        if not hidden or not shown:
            continue
        members = {id(c): c for c, b in hidden for s, sb in shown if iou(b, sb) >= STATE_IOU}
        members.update({id(s): s for c, b in hidden for s, sb in shown if iou(b, sb) >= STATE_IOU})
        if len(members) < 2:
            continue
        chosen = [c for c, _ in kids if id(c) in members]
        cover = max((b[2] - b[0]) * (b[3] - b[1]) for c, b in kids if id(c) in members)
        if cover > best_area:
            best, best_area = chosen, cover
    return best


def collect_leaves(root, canvas, visible_override):
    """Layer lá đang hiện theo thứ tự vẽ (dưới → trên); nhóm trạng thái bật/tắt theo visible_override."""
    leaves = []
    _walk(root, 1.0, canvas, visible_override, leaves, "")
    return leaves


def _walk(group, opacity, canvas, visible_override, leaves, path):
    for layer in group:
        if not visible_override.get(id(layer), layer.visible):
            continue
        alpha = opacity * layer.opacity / 255.0
        if layer.is_group():
            _walk(layer, alpha, canvas, visible_override, leaves, f"{path}/{layer.name}" if path else layer.name)
            continue
        if getattr(layer, "clipping", False):
            continue  # layer clip vào layer dưới: là một phần hình của layer đó (ảnh trạng thái đã có)
        b = layer.bbox
        box = (max(b[0], canvas[0]) - canvas[0], max(b[1], canvas[1]) - canvas[1],
               min(b[2], canvas[2]) - canvas[0], min(b[3], canvas[3]) - canvas[1])
        if box[2] - box[0] <= 0 or box[3] - box[1] <= 0:
            continue
        leaves.append(Leaf(layer, len(leaves), box, alpha, path))


def layer_raster(leaf, canvas):
    """
    Pixel của layer (BGRA, đã áp mask, chưa có hiệu ứng) đặt đúng khung leaf.box; None nếu layer không có pixel.
    Không dùng composite() của psd-tools: nó trả ảnh trong suốt cho layer nằm trong nhóm đang ẩn (trạng thái khác).
    """
    layer = leaf.layer
    try:
        image = layer.topil()
    except Exception:  # noqa: BLE001
        image = None
    if image is None:
        return None
    rgba = pil_to_bgra(image)
    if rgba.shape[1] != layer.width or rgba.shape[0] != layer.height:
        return None
    apply_mask(layer, rgba)
    lx, ly = layer.bbox[0] - canvas[0], layer.bbox[1] - canvas[1]
    x0, y0, x1, y1 = leaf.box
    return rgba[y0 - ly:y1 - ly, x0 - lx:x1 - lx].copy()


def apply_mask(layer, rgba):
    """Mask pixel của layer: trong khung mask theo ảnh mask, ngoài khung theo màu nền của mask (0 = ẩn, 255 = hiện)."""
    mask = layer.mask if layer.has_mask() else None
    if mask is None or mask.disabled:
        return
    try:
        mimg = mask.topil()
    except Exception:  # noqa: BLE001
        return
    full = np.full(rgba.shape[:2], int(mask.background_color), np.uint8)
    if mimg is not None:
        m = np.asarray(mimg.convert("L"))
        ox, oy = mask.left - layer.left, mask.top - layer.top
        x0, y0 = max(0, ox), max(0, oy)
        x1, y1 = min(full.shape[1], ox + m.shape[1]), min(full.shape[0], oy + m.shape[0])
        if x1 > x0 and y1 > y0:
            full[y0:y1, x0:x1] = m[y0 - oy:y1 - oy, x0 - ox:x1 - ox]
    rgba[:, :, 3] = (rgba[:, :, 3].astype(np.uint16) * full // 255).astype(np.uint8)


def render_state(leaves, canvas, raster_of):
    """Ghép các layer lá (thường, theo opacity) thành ảnh trạng thái — thiếu hiệu ứng layer nhưng đúng hình và màu."""
    cw, ch = canvas[2] - canvas[0], canvas[3] - canvas[1]
    out = np.zeros((ch, cw, 3), np.float32)
    for leaf in leaves:
        raster = raster_of(leaf)
        if raster is None:
            continue
        x0, y0, x1, y1 = leaf.box
        alpha = raster[:, :, 3:4].astype(np.float32) / 255.0 * leaf.opacity
        out[y0:y1, x0:x1] = raster[:, :, :3] * alpha + out[y0:y1, x0:x1] * (1 - alpha)
    return out.astype(np.uint8)


def find_dim(leaves, canvas, raster_of):
    """Lớp dim: layer phủ gần kín màn, gần đen, đều màu → (thứ tự vẽ, độ đậm 0-1); không có → (-1, 0)."""
    cw, ch = canvas[2] - canvas[0], canvas[3] - canvas[1]
    found = (-1, 0.0)
    for leaf in leaves:
        if leaf.kind == "type" or leaf.w * leaf.h < cw * ch * DIM_COVER:
            continue
        raster = raster_of(leaf)
        if raster is None:
            continue
        alpha = raster[:, :, 3]
        solid = alpha > 0
        if solid.mean() < DIM_COVER:
            continue
        rgb = raster[:, :, :3][solid]
        if rgb.mean() > DIM_MAX_RGB or rgb.std() > 12:
            continue
        found = (leaf.order, leaf.opacity * float(alpha[solid].mean()) / 255.0)
    return found


# ─── So khớp art ────────────────────────────────────────────────────────────

class Score:
    """Độ khớp của art đặt tại một chỗ: tương quan, lệch màu (75% pixel tốt nhất), tỉ lệ pixel trùng, trùng ở dải viền."""

    def __init__(self, zncc=0.0, diff=255.0, inlier=0.0, band=0.0):
        self.zncc, self.diff, self.inlier, self.band = zncc, diff, inlier, band

    def value(self):
        return self.zncc + self.inlier + self.band


def _visible_part(image, rgba, x, y):
    """Phần art nằm trong ảnh → (art cắt, x, y); None nếu hụt ra ngoài quá 20%."""
    h, w = rgba.shape[:2]
    H, W = image.shape[:2]
    x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + w), min(H, y + h)
    if x1 - x0 < w * 0.8 or y1 - y0 < h * 0.8:
        return None
    return rgba[y0 - y:y1 - y, x0 - x:x1 - x], x0, y0


def score_at(image, rgba, x, y, band=False):
    part = _visible_part(image, rgba, x, y)
    if part is None:
        return Score()
    rgba, x, y = part
    h, w = rgba.shape[:2]
    mask = rgba[:, :, 3] > ALPHA_OPAQUE
    if mask.sum() < 16:
        return Score()
    region = image[y:y + h, x:x + w]
    t = rgba[:, :, :3][mask].astype(np.float32)
    r = region[mask].astype(np.float32)
    d = np.abs(t - r).mean(axis=1)
    keep = np.sort(d)[: max(1, int(len(d) * 0.75))]
    tc, rc = t - t.mean(axis=0), r - r.mean(axis=0)
    denom = math.sqrt(float((tc * tc).sum()) * float((rc * rc).sum()))
    score = Score(float((tc * rc).sum()) / denom if denom > 1e-6 else 0.0, float(keep.mean()),
                  float((d <= EXACT_TOLERANCE).mean()))
    if band:
        m8 = mask.astype(np.uint8)
        ring = (m8 > 0) & (cv2.erode(m8, np.ones((3, 3), np.uint8), iterations=FLAT_BAND) == 0)
        if ring.sum() > 0:
            rd = np.abs(rgba[:, :, :3][ring].astype(np.float32) - region[ring].astype(np.float32)).mean(axis=1)
            score.band = float((rd <= EXACT_TOLERANCE).mean())
    return score


def best_position(image, rgba, window):
    """Vị trí art khớp nhất (SQDIFF có mask) trong cửa sổ (x0, y0, x1, y1) của ảnh trạng thái."""
    h, w = rgba.shape[:2]
    H, W = image.shape[:2]
    x0, y0, x1, y1 = window
    # cửa sổ phải chứa được art: nới đều hai phía quanh tâm
    if x1 - x0 < w + 2:
        cx = (x0 + x1) // 2
        x0, x1 = cx - w // 2 - 2, cx - w // 2 + w + 2
    if y1 - y0 < h + 2:
        cy = (y0 + y1) // 2
        y0, y1 = cy - h // 2 - 2, cy - h // 2 + h + 2
    x0, y0, x1, y1 = max(0, x0), max(0, y0), min(W, x1), min(H, y1)
    if x1 - x0 < w or y1 - y0 < h:
        return None
    mask = np.where(rgba[:, :, 3] > ALPHA_OPAQUE, 255, 0).astype(np.uint8)
    if mask.sum() == 0:
        return None
    res = cv2.matchTemplate(np.ascontiguousarray(image[y0:y1, x0:x1]), np.ascontiguousarray(rgba[:, :, :3]),
                            cv2.TM_SQDIFF, mask=cv2.merge([mask, mask, mask]))
    _, _, loc, _ = cv2.minMaxLoc(res)
    return x0 + loc[0], y0 + loc[1]


def nine_slice(rgba, border, w, h):
    """Vẽ art kéo giãn 9-slice ra cỡ w×h (border = trái, trên, phải, dưới) — giống Image.type = Sliced."""
    left, top, right, bottom = border
    H, W = rgba.shape[:2]
    if w < left + right or h < top + bottom:
        return None
    xs = [(0, left, 0, left), (left, W - right, left, w - right), (W - right, W, w - right, w)]
    ys = [(0, top, 0, top), (top, H - bottom, top, h - bottom), (H - bottom, H, h - bottom, h)]
    out = np.zeros((h, w, 4), np.uint8)
    for sy0, sy1, dy0, dy1 in ys:
        for sx0, sx1, dx0, dx1 in xs:
            if sy1 <= sy0 or sx1 <= sx0 or dy1 <= dy0 or dx1 <= dx0:
                continue
            out[dy0:dy1, dx0:dx1] = cv2.resize(rgba[sy0:sy1, sx0:sx1], (dx1 - dx0, dy1 - dy0), interpolation=cv2.INTER_LINEAR)
    return out


def scaled(rgba, scale):
    if abs(scale - 1.0) < 1e-6:
        return rgba
    h, w = rgba.shape[:2]
    return cv2.resize(rgba, (max(1, round(w * scale)), max(1, round(h * scale))),
                      interpolation=cv2.INTER_AREA if scale < 1 else cv2.INTER_LINEAR)


class Match:
    def __init__(self, art, leaf, x, y, w, h, scale, sliced, score, trust):
        self.art = art
        self.leaf = leaf
        self.x, self.y, self.w, self.h = int(x), int(y), int(w), int(h)
        self.scale = scale
        self.sliced = sliced
        self.score = score
        self.trust = trust
        self.color = None  # "#RRGGBBAA" cho sprite trắng dùng chung (shape phẳng)

    def entry(self):
        tint = self.color
        opacity = self.leaf.opacity if self.leaf is not None else 1.0
        if opacity < 0.995:
            base = tint or "#FFFFFFFF"
            tint = base[:7] + "%02X" % int(round(int(base[7:9], 16) * opacity))
        s = self.score
        return {
            "x": self.x, "y": self.y, "w": self.w, "h": self.h, "scale": round(self.scale, 4), "sliced": self.sliced,
            "diff": round(s.diff, 2), "zncc": round(s.zncc, 4), "inlier": round(max(s.inlier, s.band), 4), "tint": tint,
            "layer": self.leaf.name if self.leaf is not None else "",
            "group": self.leaf.group if self.leaf is not None else "",
        }


# Mức tin: cùng tên layer > art đã khớp theo tên ở chỗ khác trong màn (hàng danh sách, tab khác) > art lạ.
NAMED, KNOWN, PLAIN = 2, 1, 0


def accepted(score, trust, flat):
    if flat:
        if trust == PLAIN:
            return score.band >= FLAT_BAND_INLIER and score.inlier >= FLAT_PLAIN_INLIER
        return score.band >= NAMED_FLAT_BAND_INLIER
    if trust != PLAIN:
        loose = score.zncc >= NAMED_ZNCC or score.inlier >= NAMED_INLIER
        return loose if trust == NAMED else loose and score.band >= REPEAT_BAND
    return score.inlier >= EXACT_INLIER or (score.zncc >= ACCEPT_ZNCC and (score.diff <= ACCEPT_DIFF or score.inlier >= ACCEPT_INLIER))


def size_options(art, leaf, pad):
    """Cách art có thể nằm vừa khung layer: [(scale, sliced)] — 1:1 (kể cả hiệu ứng tràn), scale đều, 9-slice."""
    bw, bh = leaf.w, leaf.h
    options = []
    if bw - SIZE_SLACK <= art.tw <= bw + 2 * pad + SIZE_SLACK and bh - SIZE_SLACK <= art.th <= bh + 2 * pad + SIZE_SLACK:
        options.append((1.0, False))
    for tw, th in ((bw, bh), (bw + 2 * pad, bh + 2 * pad)):
        sw, sh = tw / art.tw, th / art.th
        s = (sw + sh) / 2
        if abs(sw - sh) <= SCALE_ASPECT and SCALE_RANGE[0] <= s <= SCALE_RANGE[1] and abs(s - 1) > 0.03:
            options.append((round(s, 4), False))
    if art.border:
        options.append((1.0, True))
    return options


def centered(leaf, w, h):
    x0, y0, x1, y1 = leaf.box
    return (x0 + x1) // 2 - w // 2, (y0 + y1) // 2 - h // 2


def try_art(image, art, leaf, pad, trust):
    """Thử đặt art vào khung layer; trả về Match tốt nhất đạt ngưỡng, hoặc None."""
    best = None
    x0, y0, x1, y1 = leaf.box
    for scale, sliced in size_options(art, leaf, pad):
        if sliced:
            candidates = []
            for p in sorted({pad, 0}):
                rendered = nine_slice(art.rgba, art.border, leaf.w + 2 * p, leaf.h + 2 * p)
                if rendered is not None:
                    candidates.append((rendered, 0, 0))
        else:
            full = scaled(art.rgba, scale)
            tx0, ty0 = round(art.trim[0] * scale), round(art.trim[1] * scale)
            tx1, ty1 = round(art.trim[2] * scale), round(art.trim[3] * scale)
            candidates = [(full[ty0:ty1, tx0:tx1], tx0, ty0, full)]
        for cand in candidates:
            templ, ox, oy = cand[0], cand[1], cand[2]
            th, tw = templ.shape[:2]
            pos = centered(leaf, tw, th)
            if not art.flat:
                # đặt thử ở tâm trước: art khác hẳn thì khỏi tìm vị trí (bước đắt nhất)
                quick = score_at(image, templ, *pos)
                if trust == PLAIN and quick.zncc < PREFILTER_ZNCC and quick.inlier < PREFILTER_INLIER:
                    continue
                pos = best_position(image, templ, (x0 - pad - SEARCH_PAD, y0 - pad - SEARCH_PAD,
                                                   x1 + pad + SEARCH_PAD, y1 + pad + SEARCH_PAD))
                if pos is None:
                    continue
            # một màu: tìm theo pixel sẽ trượt khắp mảng cùng màu → giữ ở tâm layer, kiểm bằng dải viền
            score = score_at(image, templ, *pos, band=art.flat or trust == KNOWN)
            if not accepted(score, trust, art.flat) or (best is not None and score.value() <= best.score.value()):
                continue
            if sliced:
                best = Match(art, leaf, pos[0], pos[1], tw, th, 1.0, True, score, trust)
            else:
                full = cand[3]
                best = Match(art, leaf, pos[0] - ox, pos[1] - oy, full.shape[1], full.shape[0], scale, False, score, trust)
    return best


def resolve_leaf(image, leaf, arts, by_key, known):
    """
    Art cho một layer: cùng tên trước, rồi mọi art vừa khung (1:1 / scale / 9-slice) — art đã khớp theo tên ở chỗ khác
    trong màn được ưu tiên hơn art lạ (hàng trong tab leaderboard vẽ bằng shape nhưng chính là art boder_rankslot).
    """
    pad = effect_pad(leaf.layer)
    named = by_key.get(normalize_name(leaf.name), [])
    for art in named:
        match = try_art(image, art, leaf, pad, NAMED)
        if match:
            return match, None
    best = None
    for art in arts:
        if art in named or art.opaque == 0:
            continue
        match = try_art(image, art, leaf, pad, KNOWN if art.path in known else PLAIN)
        if match and (best is None or (match.trust, match.score.value()) > (best.trust, best.score.value())):
            best = match
    note = None
    if named and best is None:
        note = (f"Layer '{leaf.name}' {leaf.w}×{leaf.h}: có art cùng tên {named[0].name} ({named[0].w}×{named[0].h}) "
                "nhưng khác hình/cỡ — nếu là 9-slice, set border rồi đọc lại PSD.")
    return best, note


def isolated(image, leaf, raster, exact):
    """
    Ảnh để so art của một layer: ảnh trạng thái với vùng layer thay bằng pixel của chính layer — không bị layer phía
    trên che, vị trí đúng tuyệt đối. Layer có hiệu ứng trên ảnh ghép chuẩn (exact) giữ nguyên ảnh: art cắt kèm hiệu
    ứng, pixel layer thì không.
    """
    if raster is None or (exact and enabled_effects(leaf.layer)):
        return image
    out = image.copy()
    x0, y0, x1, y1 = leaf.box
    alpha = raster[:, :, 3:4].astype(np.float32) / 255.0
    region = out[y0:y1, x0:x1].astype(np.float32)
    out[y0:y1, x0:x1] = (raster[:, :, :3] * alpha + region * (1 - alpha)).astype(np.uint8)
    return out


def rendered_art(match):
    """Art của match vẽ đúng cỡ (scale / 9-slice) — để so với pixel của layer."""
    art = match.art
    if match.sliced:
        return nine_slice(art.rgba, art.border, match.w, match.h)
    if match.w == art.w and match.h == art.h:
        return art.rgba
    return cv2.resize(art.rgba, (match.w, match.h), interpolation=cv2.INTER_AREA)


def paint(matches, box, cache):
    """Các art đã khớp vẽ chồng lên nhau trong khung box (x0, y0, x1, y1) → BGRA."""
    x0, y0, x1, y1 = box
    canvas = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
    for m in matches:
        art = cache.setdefault(id(m), rendered_art(m))
        if art is None:
            continue
        ix0, iy0 = max(x0, m.x), max(y0, m.y)
        ix1, iy1 = min(x1, m.x + m.w), min(y1, m.y + m.h)
        if ix1 <= ix0 or iy1 <= iy0:
            continue
        src = art[iy0 - m.y:iy1 - m.y, ix0 - m.x:ix1 - m.x]
        dst = canvas[iy0 - y0:iy1 - y0, ix0 - x0:ix1 - x0]
        on = src[:, :, 3] > ALPHA_OPAQUE
        dst[on] = src[on]
    return canvas


def explained(leaf, raster, matches, cache):
    """Layer nằm gọn trong một art đã khớp và pixel của layer trùng pixel art tại đó → đã vẽ sẵn trong art."""
    if raster is None:
        return None
    alpha = raster[:, :, 3] > ALPHA_OPAQUE
    if alpha.sum() < 16:
        return None
    x0, y0, x1, y1 = leaf.box
    for m in matches:
        if m.leaf is leaf or not (m.x - 2 <= x0 and m.y - 2 <= y0 and x1 <= m.x + m.w + 2 and y1 <= m.y + m.h + 2):
            continue
        sub = paint([m], leaf.box, cache)
        both = alpha & (sub[:, :, 3] > ALPHA_OPAQUE)
        if both.sum() < alpha.sum() * 0.8:
            continue
        d = np.abs(raster[:, :, :3][both].astype(np.int16) - sub[:, :, :3][both].astype(np.int16)).mean(axis=1)
        if (d <= EXACT_TOLERANCE * 2).mean() >= EXPLAINED_INLIER:
            return m
    return None


def residual(raster, matches, box, cache, covers, fill):
    """
    Pixel của layer mà các art đã khớp không có (chữ vẽ sẵn trên nút, khung quanh avatar) → BGRA chỉ giữ phần đó,
    cắt sát; None nếu không đáng kể. Bỏ phần nằm dưới text layer phía trên (covers — chữ số trên vương miện: text
    đè lên, khác biệt không lộ ra). fill = art nằm bên trong layer (avatar trong khung): vá chỗ art để lại lỗ trên khung
    bằng inpaint, đổi avatar khác hình vẫn không hở.
    """
    if raster is None:
        return None
    painted = paint(matches, box, cache)
    solid = raster[:, :, 3] > ALPHA_OPAQUE
    diff = np.abs(raster[:, :, :3].astype(np.int16) - painted[:, :, :3].astype(np.int16)).max(axis=2)
    extra = solid & ((painted[:, :, 3] <= ALPHA_OPAQUE) | (diff > RESIDUAL_DIFF))
    for cx0, cy0, cx1, cy1 in covers:
        extra[max(0, cy0 - box[1]):max(0, cy1 - box[1]), max(0, cx0 - box[0]):max(0, cx1 - box[0])] = False
    extra = cv2.morphologyEx(extra.astype(np.uint8), cv2.MORPH_OPEN, np.ones((3, 3), np.uint8)) > 0
    if extra.sum() < max(64, solid.sum() * RESIDUAL_MIN):
        return None
    keep = cv2.dilate(extra.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0  # giữ viền khử răng cưa
    out = raster.copy()
    if fill:
        hole = (solid & ~keep).astype(np.uint8)
        out[:, :, :3] = cv2.inpaint(np.ascontiguousarray(raster[:, :, :3]), hole, 5, cv2.INPAINT_TELEA)
        return out, 0, 0
    out[~keep, 3] = 0
    ys, xs = np.nonzero(out[:, :, 3] > 0)
    return out[ys.min():ys.max() + 1, xs.min():xs.max() + 1], int(xs.min()), int(ys.min())


def sub_search(image, leaf, arts, known):
    """
    Layer gộp (nhiều hàng flatten thành một pixel layer): dò các art nhỏ hơn bên trong vùng của layer, 1:1. Art đã khớp
    ở chỗ khác trong màn (known — hàng đầu là smart object riêng, các hàng sau bị gộp) được nới ngưỡng như cùng tên.
    """
    x0, y0, x1, y1 = leaf.box
    region = np.ascontiguousarray(image[y0:y1, x0:x1])
    found = []
    for art in arts:
        if art.flat or art.opaque == 0 or art.tw > leaf.w or art.th > leaf.h or art.tw * art.th < leaf.w * leaf.h * SUB_MIN_AREA:
            continue
        trimmed = np.ascontiguousarray(art.rgba[art.trim[1]:art.trim[3], art.trim[0]:art.trim[2]])
        mask = np.where(trimmed[:, :, 3] > ALPHA_OPAQUE, 255, 0).astype(np.uint8)
        count = int(mask.sum() // 255)
        if count < 64:
            continue
        trust = KNOWN if art.path in known else PLAIN
        res = cv2.matchTemplate(region, np.ascontiguousarray(trimmed[:, :, :3]), cv2.TM_SQDIFF,
                                mask=cv2.merge([mask, mask, mask]))
        res = res / (count * 3)  # sai lệch bình phương trung bình mỗi kênh
        for _ in range(SUB_MAX_INSTANCES):
            _, _, loc, _ = cv2.minMaxLoc(res)
            if res[loc[1], loc[0]] > (SUB_PEAKS_DIFF if trust == KNOWN else ACCEPT_DIFF * 1.5) ** 2:
                break
            px, py = x0 + loc[0], y0 + loc[1]
            score = score_at(image, trimmed, px, py, band=trust == KNOWN)
            if accepted(score, trust, False):
                found.append(Match(art, leaf, px - art.trim[0], py - art.trim[1], art.w, art.h, 1.0, False, score, trust))
            ry0, ry1 = max(0, loc[1] - art.th // 2), loc[1] + art.th // 2 + 1
            rx0, rx1 = max(0, loc[0] - art.tw // 2), loc[0] + art.tw // 2 + 1
            res[ry0:ry1, rx0:rx1] = np.inf
    return best_distinct(found)


def inner_search(image, leaf, arts):
    """Một art nằm bên trong pixel layer ở tỉ lệ khác (avatar thu nhỏ trong khung avatar đã flatten)."""
    if leaf.w * leaf.h > INNER_MAX_AREA:
        return []
    aspect = leaf.w / leaf.h
    best = None
    for art in arts:
        if art.flat or art.opaque == 0 or abs(art.tw / art.th - aspect) > INNER_ASPECT * aspect:
            continue
        top = min(leaf.w / art.tw, leaf.h / art.th)
        coarse = [top - i * INNER_COARSE_STEP for i in range(int((top - INNER_MIN_SCALE) / INNER_COARSE_STEP) + 1)]
        found = best_scale(image, leaf, art, coarse)
        if found is None:
            continue
        fine = [found.scale + d / 100 for d in range(-4, 5) if d and INNER_MIN_SCALE <= found.scale + d / 100 <= top]
        refined = best_scale(image, leaf, art, fine)
        if refined is not None and refined.score.value() > found.score.value():
            found = refined
        if found.score.zncc >= INNER_ZNCC and (best is None or found.score.value() > best.score.value()):
            best = found
    return [best] if best else []


def best_scale(image, leaf, art, scales):
    best = None
    for s in scales:
        if art.tw * art.th * s * s < leaf.w * leaf.h * INNER_MIN_COVER:
            continue
        full = scaled(art.rgba, s)
        tx0, ty0 = round(art.trim[0] * s), round(art.trim[1] * s)
        templ = full[ty0:ty0 + round(art.th * s), tx0:tx0 + round(art.tw * s)]
        pos = best_position(image, templ, leaf.box)
        if pos is None:
            continue
        score = score_at(image, templ, *pos)
        if best is None or score.value() > best.score.value():
            best = Match(art, leaf, pos[0] - tx0, pos[1] - ty0, full.shape[1], full.shape[0], round(s, 4), False, score, PLAIN)
    return best


def best_distinct(found):
    """Cùng chỗ nhiều art khớp (art gần giống nhau) → giữ khớp tốt nhất."""
    found.sort(key=lambda m: -m.score.value())
    kept = []
    for m in found:
        if all(iou((m.x, m.y, m.x + m.w, m.y + m.h), (k.x, k.y, k.x + k.w, k.y + k.h)) < 0.5 for k in kept):
            kept.append(m)
    return kept


# ─── Text layer ─────────────────────────────────────────────────────────────

def color_hex(values):
    """FillColor của engine data: [A, R, G, B] 0-1 → '#RRGGBB'."""
    if not values:
        return "#FFFFFF"
    v = list(values)
    r, g, b = (v[1], v[2], v[3]) if len(v) >= 4 else (v[0], v[1], v[2])
    return "#%02X%02X%02X" % tuple(int(round(max(0.0, min(1.0, float(c))) * 255)) for c in (r, g, b))


def effect_color(effect):
    c = getattr(effect, "color", None)
    if not isinstance(c, dict):
        return None
    vals = {}
    for k, v in c.items():
        key = k.decode("latin-1").strip() if isinstance(k, bytes) else str(k).strip()
        vals[key] = float(v)
    if not {"Rd", "Grn", "Bl"} <= vals.keys():
        return None
    return "#%02X%02X%02X" % tuple(int(round(max(0.0, min(255.0, vals[k])))) for k in ("Rd", "Grn", "Bl"))


def read_text(leaf):
    """Text layer → dict LocateText: nội dung (nhiều màu → rich text TMP), màu, font, cỡ, căn lề, viền."""
    layer = leaf.layer
    x0, y0, x1, y1 = leaf.box
    entry = {"x": x0, "y": y0, "w": x1 - x0, "h": y1 - y0, "confidence": 1.0, "text": "", "color": "#FFFFFF",
             "group": leaf.group}
    try:
        engine = layer.engine_dict
        resource = layer.resource_dict
        raw = str(layer.text).replace("\r", "\n")
        runs = engine["StyleRun"]
        lengths = list(runs["RunLengthArray"])
        sheets = [r["StyleSheet"]["StyleSheetData"] for r in runs["RunArray"]]
        fonts = [f["Name"] for f in resource["FontSet"]]
    except Exception:  # noqa: BLE001 — text layer không có engine data
        entry["text"] = str(getattr(layer, "text", "") or "").replace("\r", "\n").strip()
        return entry

    colors = [color_hex(s.get("FillColor", {}).get("Values")) for s in sheets]
    weight = {}
    pos = 0
    for n, c in zip(lengths, colors):
        weight[c] = weight.get(c, 0) + len(raw[pos:pos + n].strip())
        pos += n
    base = max(weight, key=weight.get) if weight else "#FFFFFF"
    parts, pos = [], 0
    for n, c in zip(lengths, colors):
        seg = raw[pos:pos + n]
        pos += n
        parts.append(seg if c == base or not seg.strip() else f"<color={c}>{seg}</color>")
    entry["text"] = "".join(parts).rstrip("\n")
    entry["color"] = base

    sheet = sheets[0] if sheets else {}
    t = layer.transform if hasattr(layer, "transform") else (1, 0, 0, 1, 0, 0)
    scale = math.hypot(float(t[2]), float(t[3])) or 1.0
    entry["fontSize"] = round(float(sheet.get("FontSize", 0)) * scale, 1)
    font_index = int(sheet.get("Font", 0))
    entry["font"] = str(fonts[font_index]).strip("'") if 0 <= font_index < len(fonts) else ""
    try:
        just = int(engine["ParagraphRun"]["RunArray"][0]["ParagraphSheet"]["Properties"].get("Justification", 0))
    except Exception:  # noqa: BLE001
        just = 0
    entry["align"] = {0: "left", 1: "right", 2: "center"}.get(just, "center")
    for e in enabled_effects(layer):
        if type(e).__name__ == "Stroke":
            entry["outlineColor"] = effect_color(e) or "#000000"
            entry["outlineWidth"] = float(getattr(e, "size", 0) or 0)
    return entry


# ─── Xuất layer không có art ────────────────────────────────────────────────

def safe_name(name):
    name = re.sub(r"[^\w\-]+", "_", name.strip()).strip("_").lower()
    return name or "layer"


class Exporter:
    """Ghi PNG cho layer không có art; layer trùng / gần trùng pixel dùng chung một file."""

    def __init__(self, folder):
        self.folder = folder
        self.by_hash = {}
        self.names = set()
        self.files = []
        self.shapes = {}

    def export(self, name, raster):
        digest = hashlib.sha1(raster.tobytes() + bytes(str(raster.shape), "ascii")).hexdigest()
        if digest in self.by_hash:
            return self.by_hash[digest]
        for art in self.by_hash.values():
            if art.rgba.shape == raster.shape and np.abs(art.rgba.astype(np.int16) - raster).mean() <= NEAR_DUPLICATE:
                self.by_hash[digest] = art
                return art
        art = Art(self._write(name, raster), raster)
        self.by_hash[digest] = art
        return art

    def rounded(self, radius):
        """Sprite trắng bo góc bán kính radius (0 = vuông), border 9-slice = radius + 1 — dùng chung cho mọi shape phẳng."""
        near = [r for r in self.shapes if abs(r - radius) <= 2]
        if near:
            return self.shapes[near[0]]  # bán kính lệch ≤ 2 px (đo trên khung lệch 1 px) → cùng một sprite
        if radius not in self.shapes:
            size = 2 * radius + 3
            rgba = np.zeros((size, size, 4), np.uint8)
            rgba[:, :, :3] = 255
            rgba[:, :, 3] = rounded_mask(size, size, radius)
            art = Art(self._write(f"shape_round_{radius}", rgba), rgba)
            art.border = (radius + 1,) * 4
            self.shapes[radius] = art
        return self.shapes[radius]

    def _write(self, name, raster):
        base = safe_name(name)
        name, i = base, 2
        while name in self.names:
            name, i = f"{base}_{i}", i + 1
        self.names.add(name)
        os.makedirs(self.folder, exist_ok=True)
        path = os.path.join(self.folder, f"{name}.png")
        cv2.imwrite(path, raster)
        self.files.append(path)
        return path


def rounded_mask(w, h, radius):
    """Alpha hình chữ nhật bo góc (khử răng cưa bằng vẽ 4× rồi thu nhỏ)."""
    k = 4
    big = np.zeros((h * k, w * k), np.uint8)
    r = radius * k
    if r <= 0:
        big[:] = 255
    else:
        cv2.rectangle(big, (r, 0), (w * k - 1 - r, h * k - 1), 255, -1)
        cv2.rectangle(big, (0, r), (w * k - 1, h * k - 1 - r), 255, -1)
        for cx, cy in ((r, r), (w * k - 1 - r, r), (r, h * k - 1 - r), (w * k - 1 - r, h * k - 1 - r)):
            cv2.circle(big, (cx, cy), r, 255, -1, lineType=cv2.LINE_AA)
    return cv2.resize(big, (w, h), interpolation=cv2.INTER_AREA)


def flat_shape(raster, ignore=None):
    """
    Layer một màu dạng chữ nhật bo góc / hình tròn → (màu '#RRGGBBAA', bán kính); None nếu không phải. Bán kính đo trên
    đường chéo ở cả 4 góc (cung tròn bán kính r cắt đường chéo cách góc r·(1 − 1/√2) px). ignore = pixel bị khối khác đè
    lên (không biết hình thật ở đó) — không tính vào sai số.
    """
    if raster is None:
        return None
    alpha = raster[:, :, 3]
    solid = alpha > ALPHA_OPAQUE
    h, w = alpha.shape
    if solid.sum() < PART_MIN_PIXELS or w < MIN_EXPORT or h < MIN_EXPORT:
        return None
    rgb = raster[:, :, :3][solid].astype(np.float64)
    if rgb.std(axis=0).max() > FLAT_SHAPE_STD:
        return None
    hidden = ignore if ignore is not None else np.zeros((h, w), bool)
    guesses = []
    for flip in ((slice(None), slice(None)), (slice(None), slice(None, None, -1)),
                 (slice(None, None, -1), slice(None)), (slice(None, None, -1), slice(None, None, -1))):
        corner, covered = alpha[flip], hidden[flip]
        diag = [i for i in range(min(w, h)) if corner[i, i] > 127]
        if diag and not covered[diag[0], diag[0]]:
            guesses.append(int(round(diag[0] / (1 - 1 / math.sqrt(2)))))
    if not guesses:
        return None
    shape = (alpha > 127).astype(np.uint8)
    edge = (cv2.dilate(shape, np.ones((3, 3), np.uint8)) > 0) & ~(cv2.erode(shape, np.ones((3, 3), np.uint8)) > 0)
    best = None
    for radius in sorted({min(max(0, g + d), w // 2, h // 2) for g in guesses for d in range(-4, 5)}):
        miss = ((rounded_mask(w, h, radius) > 127) ^ (shape > 0)) & ~edge & ~hidden
        if best is None or miss.sum() < best[0]:
            best = (int(miss.sum()), radius)
    if best[0] > shape.sum() * FLAT_SHAPE_MISS:
        return None
    b, g, r = (int(np.clip(round(c), 0, 255)) for c in rgb.mean(axis=0))
    a = int(np.clip(round(float(alpha[solid].mean())), 0, 255))
    return "#%02X%02X%02X%02X" % (r, g, b, a), best[1]


def split_parts(raster):
    """Layer gộp nhiều phần tử rời nhau (ô vật phẩm, các hàng flatten) → [(pixel, x, y)] theo từng mảng; [] nếu chỉ 1 mảng."""
    if raster is None:
        return []
    mask = (raster[:, :, 3] > 0).astype(np.uint8)
    joined = cv2.dilate(mask, np.ones((3, 3), np.uint8), iterations=PART_DILATE)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(joined, connectivity=8)
    parts = []
    for i in range(1, count):
        x, y, w, h, _ = stats[i]
        own = (labels[y:y + h, x:x + w] == i) & (mask[y:y + h, x:x + w] > 0)
        if own.sum() < PART_MIN_PIXELS:
            continue
        part = raster[y:y + h, x:x + w].copy()
        part[~own, 3] = 0
        parts.append((part, int(x), int(y)))
    return parts if len(parts) > 1 else []


class TextReader:
    """OCR (RapidOCR trong venv) cho chữ vẽ sẵn trong pixel layer — nạp model lần đầu cần tới."""

    def __init__(self):
        self._engine = None
        self._failed = False

    def _ready(self):
        if self._engine is None and not self._failed:
            try:
                from rapidocr import RapidOCR  # noqa: PLC0415 — nạp chậm (~1 s), chỉ khi có chữ vẽ sẵn
                # tắt bộ xoay hướng chữ: chữ UI không lộn ngược, bật thì "999.99B" bị đọc ngược thành "866'666"
                self._engine = RapidOCR(params={"Global.use_cls": False})
            except Exception:  # noqa: BLE001 — venv chưa có OCR
                self._failed = True
        return self._engine is not None

    def detect(self, bgr):
        """Mọi dòng chữ trên ảnh BGR → [((x0, y0, x1, y1), nội dung, độ tin)]; [] nếu chưa cài OCR."""
        if not self._ready():
            return []
        try:
            result = self._engine(np.ascontiguousarray(bgr))
        except Exception:  # noqa: BLE001
            return []
        boxes = getattr(result, "boxes", None)
        if boxes is None:
            return []
        lines = []
        for box, text, score in zip(boxes, result.txts or [], result.scores or []):
            pts = np.asarray(box)
            lines.append(((int(pts[:, 0].min()), int(pts[:, 1].min()), int(pts[:, 0].max()) + 1, int(pts[:, 1].max()) + 1),
                          str(text).strip(), float(score)))
        return lines

    def read(self, rgba):
        """Chữ trên ảnh BGRA → (nội dung, độ tin); None nếu không đọc được / chưa cài OCR."""
        if not self._ready():
            return None
        h, w = rgba.shape[:2]
        pad = max(8, h // 2)
        canvas = np.full((h + 2 * pad, w + 2 * pad, 3), 128, np.uint8)
        alpha = rgba[:, :, 3:4].astype(np.float32) / 255.0
        region = canvas[pad:pad + h, pad:pad + w].astype(np.float32)
        canvas[pad:pad + h, pad:pad + w] = (rgba[:, :, :3] * alpha + region * (1 - alpha)).astype(np.uint8)
        try:
            result = self._engine(canvas)
        except Exception:  # noqa: BLE001
            return None
        texts = list(getattr(result, "txts", None) or [])
        scores = list(getattr(result, "scores", None) or [])
        if not texts:
            return None
        return " ".join(t.strip() for t in texts).strip(), float(min(scores)) if scores else 0.0


def baked_text(rgba, x, y, reader):
    """Phần dư là chữ (OCR đọc được) → dict LocateText: màu ruột nét, viền = màu dải mép (nếu tối hơn hẳn)."""
    read = reader.read(rgba)
    if read is None or not read[0] or read[1] < TEXT_OCR_SCORE:
        return None
    return text_entry(rgba, x, y, read[0], read[1], "center")


def text_entry(rgba, x, y, text, score, align):
    """Pixel một dòng chữ (alpha = nét chữ) → dict LocateText: màu ruột nét, viền tối (nếu có) và độ dày viền."""
    solid = (rgba[:, :, 3] > ALPHA_OPAQUE).astype(np.uint8)
    inner = cv2.erode(solid, np.ones((3, 3), np.uint8), iterations=2) > 0
    core = rgba[:, :, :3][inner] if inner.any() else rgba[:, :, :3][solid > 0]
    color = np.median(core, axis=0)
    entry = {"x": x, "y": y, "w": rgba.shape[1], "h": rgba.shape[0], "text": text, "confidence": round(score, 3),
             "color": "#%02X%02X%02X" % (int(color[2]), int(color[1]), int(color[0]))}
    if align:
        entry["align"] = align
    # viền = pixel tối hẳn so với ruột (bỏ pixel lẫn màu nền phía sau ở mép); dày ≈ số px tối tính từ mép vào
    dark = (rgba[:, :, :3].mean(axis=2) < color.mean() - 100) & (solid > 0)
    if dark.sum() > solid.sum() * 0.15:
        edge = np.median(rgba[:, :, :3][dark], axis=0)
        entry["outlineColor"] = "#%02X%02X%02X" % (int(edge[2]), int(edge[1]), int(edge[0]))
        depth = cv2.distanceTransform(solid, cv2.DIST_L2, 3)
        entry["outlineWidth"] = round(float(np.median(depth[dark]) * 2), 1)
    return entry


# ─── Một trạng thái ─────────────────────────────────────────────────────────

def index_arts(arts):
    by_key = {}
    for art in arts:
        by_key.setdefault(art.key, []).append(art)
    return by_key


def named_arts(leaves, image, arts, by_key):
    """Art khớp theo tên layer trong một trạng thái — lượt đầu, để mọi trạng thái biết art nào thuộc màn này."""
    known = set()
    for leaf in leaves:
        if leaf.kind == "type":
            continue
        for art in by_key.get(normalize_name(leaf.name), []):
            if try_art(image, art, leaf, effect_pad(leaf.layer), NAMED):
                known.add(art.path)
                break
    return known


def text_covers(leaves, leaf):
    """Khung các text layer vẽ phía trên leaf (nới 4 px cho viền chữ)."""
    return [(l.box[0] - 4, l.box[1] - 4, l.box[2] + 4, l.box[3] + 4) for l in leaves if l.kind == "type" and l.order > leaf.order]


class Context:
    """Dữ liệu dùng chung cho mọi trạng thái của một lần đọc PSD."""

    def __init__(self, canvas, arts, known, exporter, raster_of):
        self.canvas = canvas
        self.arts = arts
        self.by_key = index_arts(arts)
        self.known = known
        self.exporter = exporter
        self.raster_of = raster_of
        # layer dùng chung giữa các trạng thái (ngoài nhóm tab) → kết quả của trạng thái xử lý trước (ảnh ghép chuẩn)
        self.layers = {}
        # (art, cỡ layer) → vị trí art so với khung layer, học trên ảnh ghép chuẩn: shape có hiệu ứng ở trạng thái
        # ghép lại (thiếu hiệu ứng) đặt art theo đó thay vì dò trên ảnh thiếu hiệu ứng
        self.offsets = {}
        self.reader = TextReader()


class Record:
    """Kết quả một layer: các art đặt được (chính + bên trong + phần dư) hoặc lý do bỏ, kèm thông tin cho ghi chú."""

    def __init__(self):
        self.matches = []
        self.drop = None
        self.note = None
        self.exported = False
        self.lost_fx = None
        self.flattened = None
        self.residual = False
        self.texts = []  # chữ vẽ sẵn trong pixel layer, OCR đọc được


def remembered_offset(ctx, leaf, image):
    """Shape có hiệu ứng trên ảnh ghép thiếu hiệu ứng: đặt art cùng tên theo vị trí đã học ở trạng thái chuẩn."""
    for art in ctx.by_key.get(normalize_name(leaf.name), []):
        known = ctx.offsets.get((art.path, leaf.w, leaf.h))
        if known is None:
            continue
        dx, dy, w, h, scale, sliced = known
        x, y = leaf.box[0] + dx, leaf.box[1] + dy
        return Match(art, leaf, x, y, w, h, scale, sliced, score_at(image, rendered_art_at(art, w, h, sliced), x, y), NAMED)
    return None


def rendered_art_at(art, w, h, sliced):
    if sliced:
        return nine_slice(art.rgba, art.border, w, h)
    return art.rgba if (w, h) == (art.w, art.h) else cv2.resize(art.rgba, (w, h), interpolation=cv2.INTER_AREA)


def resolve_primary(ctx, leaf, image, exact):
    view = isolated(image, leaf, ctx.raster_of(leaf), exact)
    if not exact and enabled_effects(leaf.layer):
        match = remembered_offset(ctx, leaf, view)
        if match is not None:
            return match, None
    return resolve_leaf(view, leaf, ctx.arts, ctx.by_key, ctx.known)


def resolve_rest(ctx, leaf, rec, image, exact, matches, cache, leaves, placed):
    """Layer chưa có art: đã vẽ sẵn trong art khác → bỏ; layer gộp → dò art bên trong; còn lại → xuất PNG."""
    raster = ctx.raster_of(leaf)
    if explained(leaf, raster, matches, cache) is not None:
        rec.drop = "explained-by-other"
        return
    inner, fill = [], False
    if leaf.kind in ("pixel", "smartobject") and len(ctx.arts) <= SUB_MAX_ARTS:
        view = isolated(image, leaf, raster, exact)
        inner = sub_search(view, leaf, ctx.arts, ctx.known | {m.art.path for m in matches})
        if not inner:
            inner, fill = inner_search(view, leaf, ctx.arts), True
    if inner:
        rec.matches.extend(inner)
        rec.flattened = f"'{leaf.name}' → {len(inner)}× {', '.join(sorted({m.art.name for m in inner}))}"
        if ctx.exporter is not None:
            rec.residual = add_residual(leaf, raster, inner, ctx.exporter, rec.matches, cache, text_covers(leaves, leaf), fill,
                                        ctx.reader, rec.texts)
        return
    if raster is None or leaf.w < MIN_EXPORT or leaf.h < MIN_EXPORT or (raster[:, :, 3] > 0).sum() == 0:
        rec.drop = "psd-empty"
        return
    if ctx.exporter is None:
        rec.drop = "psd-no-art"
        return
    parts = split_parts(raster) if leaf.kind in ("pixel", "smartobject") else []
    if parts:
        rec.flattened = f"'{leaf.name}' tách {len(parts)} phần tử rời"
    # art đã khớp ở chỗ khác trong màn, đúng tỉ lệ đã gặp — để tìm lại trong mảng flatten (avatar 0.66 trong hàng)
    seen = {(m.art.path, m.scale): (m.art, m.scale) for m in matches + placed() if not m.sliced}
    decomposed = 0
    for part, px, py in parts or [(raster, 0, 0)]:
        x, y = leaf.box[0] + px, leaf.box[1] + py
        split = decompose_part(ctx, leaf, part, x, y, list(seen.values())) if leaf.kind in ("pixel", "smartobject") else None
        if split is None:
            rec.matches.append(export_part(ctx.exporter, leaf, part, x, y))
            continue
        rec.matches.extend(split[0])
        rec.texts.extend(split[1])
        decomposed += 1
    if decomposed:
        rec.flattened = (rec.flattened or f"'{leaf.name}'") + f", phân rã {decomposed} mảng thành nền + chữ + art + khối màu"
    rec.exported = True
    fx = [n for n in effect_names(leaf.layer) if n != "ColorOverlay"]
    if leaf.kind == "shape" and fx:
        rec.lost_fx = f"{leaf.name} ({', '.join(fx)})"


def ring_color(bgr, mask):
    """Màu trung vị của dải 3 px ngay ngoài vùng mask — màu nền để lấp chỗ vừa tách phần tử ra."""
    m8 = mask.astype(np.uint8)
    ring = (cv2.dilate(m8, np.ones((3, 3), np.uint8), iterations=3) > 0) & ~mask
    return np.median(bgr[ring], axis=0) if ring.any() else np.median(bgr.reshape(-1, 3), axis=0)


def decompose_part(ctx, leaf, part, px, py, seen):
    """
    Mảng flatten lớn (cả hàng danh sách gộp một layer) → tách: chữ (OCR), art đã gặp trong màn (avatar, khung, icon ở
    đúng tỉ lệ đã khớp), khối một màu (vòng hạng, ô vuông, pill điểm); phần còn lại là nền. None nếu không tách được
    chữ hay art nào (ô vật phẩm, hình vẽ) — khi đó xuất nguyên mảng.
    Trả về (matches, texts): nền trước, rồi khối màu, art; tọa độ theo khung màn.
    """
    h, w = part.shape[:2]
    if w * h < DECOMPOSE_MIN_AREA:
        return None
    work = np.ascontiguousarray(part[:, :, :3].copy())
    solid = part[:, :, 3] > ALPHA_OPAQUE
    texts, arts = [], []

    for (x0, y0, x1, y1), text, score in ctx.reader.detect(work):
        if score < TEXT_OCR_SCORE or not text:
            continue
        x0, y0, x1, y1 = max(0, x0 - 3), max(0, y0 - 3), min(w, x1 + 3), min(h, y1 + 3)
        crop = work[y0:y1, x0:x1]
        edge = np.concatenate([crop[0], crop[-1], crop[:, 0], crop[:, -1]])
        bg = np.median(edge, axis=0)
        ink = (np.abs(crop.astype(np.int16) - bg).max(axis=2) > TEXT_INK_DIFF) & solid[y0:y1, x0:x1]
        if ink.sum() < 20:
            continue
        ys, xs = np.nonzero(ink)
        tx0, ty0, tx1, ty1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
        rgba = np.dstack([crop[ty0:ty1, tx0:tx1], np.where(ink[ty0:ty1, tx0:tx1], 255, 0).astype(np.uint8)])
        entry = text_entry(rgba, px + x0 + int(tx0), py + y0 + int(ty0), text, score, None)
        entry["group"] = leaf.group
        texts.append(entry)
        crop[cv2.dilate(ink.astype(np.uint8), np.ones((3, 3), np.uint8), iterations=2) > 0] = bg

    # art đã gặp trong màn: dò hết trên cùng một ảnh (đã bỏ chữ) rồi mới lấp — khung avatar và avatar bên trong đều thấy
    base = work.copy()
    covered = np.zeros((h, w), bool)
    found = []
    for art, scale in sorted(seen, key=lambda a: a[0].w * a[0].h * a[1] * a[1]):
        templ = scaled(art.rgba, scale)
        th, tw = templ.shape[:2]
        if tw > w or th > h:
            continue
        pos = best_position(base, templ, (0, 0, w, h))
        if pos is None:
            continue
        score = score_at(base, templ, *pos)
        if score.zncc < SEEN_ZNCC or score.diff > ACCEPT_DIFF:
            continue
        mask = np.zeros((h, w), bool)
        mask[pos[1]:pos[1] + th, pos[0]:pos[0] + tw] = templ[:, :, 3] > ALPHA_TRIM
        if (mask & ~covered).sum() < mask.sum() * SEEN_NEW_PIXELS:
            continue  # art khác (gần giống) đã phủ gần hết chỗ này
        arts.append(Match(art, leaf, px + pos[0], py + pos[1], tw, th, scale, False, score, KNOWN))
        found.append(mask)
        covered |= mask
    for mask in found:
        work[cv2.dilate(mask.astype(np.uint8), np.ones((3, 3), np.uint8), iterations=3) > 0] = ring_color(base, covered)

    if not texts and not arts:
        return None

    shapes = split_flat_shapes(ctx.exporter, leaf, work, solid, px, py)
    background = np.dstack([work, part[:, :, 3]])
    return [export_part(ctx.exporter, leaf, background, px, py)] + shapes + arts, texts


def part_of_background(x, y, w, h, shape):
    """Khối rộng gần hết mảng hoặc chạm ≥ 2 mép (viền, vạch sáng của chính nền hàng) → để lại trong nền, không tách."""
    ph, pw = shape[:2]
    edges = (x <= 8) + (y <= 8) + (x + w >= pw - 8) + (y + h >= ph - 8)
    return w >= pw * 0.9 or h >= ph * 0.9 or edges >= 2


def split_flat_shapes(exporter, leaf, work, solid, px, py):
    """
    Khối một màu khác màu nền trong mảng flatten → sprite bo góc dùng chung + màu; lấp chỗ đó bằng màu nền. Lượt 2 thử
    lại khối bị khối đã tách đè lên (pill điểm bị ô vuông che đầu trái): phần bị che coi là không biết, khung nới tới mép
    khối che.
    """
    pixels = work[solid]
    if len(pixels) == 0:
        return []
    codes = (work // 8).astype(np.int32) @ np.array([1, 32, 1024])
    keys, counts = np.unique(codes[solid], return_counts=True)
    bg_key = keys[counts.argmax()]
    bg = np.median(work[solid & (codes == bg_key)], axis=0)
    accepted = np.zeros(work.shape[:2], bool)
    shapes, pending = [], []
    for key in keys[np.argsort(-counts)]:
        if key == bg_key:
            continue
        color = np.array([key % 32, key // 32 % 32, key // 1024], np.float64) * 8 + 4
        if np.abs(color - bg).max() <= SHAPE_COLOR_TOL * 2:
            continue  # màu nền rơi sang bin lượng tử bên cạnh
        near = solid & (np.abs(work.astype(np.int16) - color).max(axis=2) <= SHAPE_COLOR_TOL)
        if near.sum() < SHAPE_MIN_PIXELS:
            continue
        count, labels, stats, _ = cv2.connectedComponentsWithStats(near.astype(np.uint8), connectivity=8)
        for i in range(1, count):
            x, y, bw, bh, area = stats[i]
            if area >= SHAPE_MIN_PIXELS and not part_of_background(x, y, bw, bh, work.shape):
                pending.append(labels == i)
    for _ in range(2):
        retry = []
        for own in pending:
            if (own & accepted).sum() > own.sum() * 0.5:
                continue
            match = fit_flat(exporter, leaf, work, own, accepted, px, py)
            if match is None:
                retry.append(own)
                continue
            accepted |= own
            box = (match.x, match.y, match.x + match.w, match.y + match.h)
            if any(iou(box, (s.x, s.y, s.x + s.w, s.y + s.h)) > 0.8 for s in shapes):
                continue  # mảnh viền khử răng cưa (màu lệch một bin) của khối vừa tách
            shapes.append(match)
        pending = retry
    work[cv2.dilate(accepted.astype(np.uint8), np.ones((3, 3), np.uint8)) > 0] = bg
    shapes.sort(key=lambda m: -m.w * m.h)  # khối lớn vẽ trước (ô vuông rồi mới tới vòng hạng bên trong)
    return shapes


def fit_flat(exporter, leaf, work, own, accepted, px, py):
    """Một khối cùng màu → Match sprite bo góc + màu, hoặc None. Khối đã tách chạm vào nó = vùng bị che (không biết)."""
    ys, xs = np.nonzero(own)
    x0, y0, x1, y1 = xs.min(), ys.min(), xs.max() + 1, ys.max() + 1
    vx0, vx1 = x0, x1  # phần lộ ra
    touching = accepted & (cv2.dilate(own.astype(np.uint8), np.ones((5, 5), np.uint8)) > 0)
    touching[:y0] = False
    touching[y1:] = False  # chỉ nới theo chiều ngang trong dải của khối (pill bị ô vuông che đầu trái)
    if touching.any():
        tys, txs = np.nonzero(accepted & (np.arange(work.shape[0])[:, None] >= y0) & (np.arange(work.shape[0])[:, None] < y1)
                              & (np.abs(np.arange(work.shape[1])[None, :] - (x0 + x1) / 2) <= (x1 - x0) / 2 + 100))
        if len(txs):
            x0, x1 = min(x0, txs.min()), max(x1, txs.max() + 1)
    region = own[y0:y1, x0:x1]
    hidden = accepted[y0:y1, x0:x1] & ~region
    filled = (region | hidden).astype(np.uint8) * 255
    flood = np.pad(255 - filled, 1, constant_values=255)  # lấp lỗ (vòng hạng trong ô vuông) để thử hình bo góc
    cv2.floodFill(flood, None, (0, 0), 0)
    filled = np.maximum(filled, flood[1:-1, 1:-1])
    fill = np.median(work[y0:y1, x0:x1][region], axis=0)
    rgba = np.dstack([np.broadcast_to(fill.astype(np.uint8), region.shape + (3,)), filled])
    shape = flat_shape(rgba, hidden)
    if shape is None or part_of_background(x0, y0, x1 - x0, y1 - y0, work.shape):
        return None
    # phần bị che: không biết khối kéo dài tới đâu → chỉ nới thêm đúng một bán kính bo góc (đủ cho đầu tròn khuất sau
    # khối che), không kéo tới mép kia của khối che
    x0, x1 = max(x0, vx0 - shape[1]), min(x1, vx1 + shape[1])
    match = Match(exporter.rounded(shape[1]), leaf, px + x0, py + y0, x1 - x0, y1 - y0, 1.0, True, Score(1.0, 0.0, 1.0), PLAIN)
    match.color = shape[0]
    return match


def export_part(exporter, leaf, raster, x, y):
    """Một phần tử không có art: shape một màu → sprite trắng bo góc dùng chung + màu (9-slice); còn lại → PNG."""
    h, w = raster.shape[:2]
    shape = flat_shape(raster)
    if shape is not None:
        color, radius = shape
        match = Match(exporter.rounded(radius), leaf, x, y, w, h, 1.0, True, Score(1.0, 0.0, 1.0), PLAIN)
        match.color = color
        return match
    return Match(exporter.export(leaf.name, raster), leaf, x, y, w, h, 1.0, False, Score(1.0, 0.0, 1.0), PLAIN)


def process_state(ctx, leaves, image, exact, emit_live):
    dim_order, dim_alpha = find_dim(leaves, ctx.canvas, ctx.raster_of)
    dropped, texts = [], []
    ui = [l for l in leaves if l.order > dim_order]
    for leaf in leaves:
        if leaf.order < dim_order:
            dropped.append(drop(leaf, "psd-backdrop"))
    if dim_order >= 0:
        dropped.append(drop(next(l for l in leaves if l.order == dim_order), "psd-dim"))

    records, fresh = {}, []
    for i, leaf in enumerate(ui):
        if leaf.kind == "type":
            continue
        key = id(leaf.layer)
        if key in ctx.layers:
            records[key] = ctx.layers[key]
            continue
        rec = Record()
        match, rec.note = resolve_primary(ctx, leaf, image, exact)
        if match:
            rec.matches.append(match)
            if exact:
                ctx.offsets[(match.art.path, leaf.w, leaf.h)] = (match.x - leaf.box[0], match.y - leaf.box[1], match.w,
                                                                 match.h, match.scale, match.sliced)
        records[key] = rec
        fresh.append(leaf)
        if emit_live:
            emit("filter", message=f"Layer {i + 1}/{len(ui)}: {leaf.name}", done=i + 1, total=len(ui))

    cache = {}
    primary = [m for r in records.values() for m in r.matches]
    for leaf in fresh:
        rec = records[id(leaf.layer)]
        if not rec.matches:
            resolve_rest(ctx, leaf, rec, image, exact, primary, cache, leaves,
                         lambda: [m for r in records.values() for m in r.matches])
        elif ctx.exporter is not None and leaf.kind in ("pixel", "smartobject"):
            # đã khớp art nhưng layer còn phần art không có (chữ "Back" vẽ sẵn trên nút)
            rec.residual = add_residual(leaf, ctx.raster_of(leaf), list(rec.matches), ctx.exporter, rec.matches, cache,
                                        text_covers(leaves, leaf), False, ctx.reader, rec.texts)
        ctx.layers[id(leaf.layer)] = rec

    for leaf in ui:
        if leaf.kind == "type":
            entry = read_text(leaf)
            if entry["text"].strip():
                texts.append(entry)
                if emit_live:
                    emit("text", text=entry)
        elif records[id(leaf.layer)].drop:
            dropped.append(drop(leaf, records[id(leaf.layer)].drop))

    recs = [records[id(l.layer)] for l in ui if l.kind != "type"]
    matches = [m for r in recs for m in r.matches]
    baked = [t for r in recs for t in r.texts]
    texts.extend(baked)
    notes = [r.note for r in recs if r.note]
    flattened = [r.flattened for r in recs if r.flattened]
    exported = [l.name for l in ui if l.kind != "type" and records[id(l.layer)].exported]
    residuals = [l.name for l in ui if l.kind != "type" and records[id(l.layer)].residual and not records[id(l.layer)].texts]
    lost_fx = [r.lost_fx for r in recs if r.lost_fx]
    if flattened:
        notes.append(f"Layer gộp nhiều phần (flatten) — đã dò art bên trong: {'; '.join(flattened)}.")
    if exported:
        notes.append(f"Xuất {len(exported)} layer không có art thành PNG: {', '.join(dict.fromkeys(exported))}.")
    if baked:
        notes.append(f"Chữ vẽ sẵn trong layer ảnh → OCR thành chữ TMP: {', '.join(repr(t['text']) for t in baked)} — "
                     "nên để designer giữ dạng text layer.")
    if residuals:
        notes.append("Phần layer mà art không có (chữ vẽ sẵn, khung…) xuất thành PNG riêng đặt đè lên art: "
                     f"{', '.join(dict.fromkeys(residuals))} — chữ vẽ sẵn nên đổi thành text layer để dựng TMP.")
    if lost_fx:
        notes.append("Shape có hiệu ứng layer (Stroke/InnerShadow/DropShadow…) — PNG xuất ra THIẾU hiệu ứng, cần art cắt "
                     f"sẵn hoặc designer Convert to Smart Object: {', '.join(dict.fromkeys(lost_fx))}.")
    fonts = sorted({t.get("font") for t in texts if t.get("font")})
    if fonts:
        notes.append(f"Font trong PSD: {', '.join(fonts)} — chọn TMP font tương ứng ở Bước 4.")

    sprites = {}
    for m in matches:
        s = sprites.setdefault(m.art.path, {
            "sprite": os.path.abspath(m.art.path), "name": m.art.name, "status": "matched", "reason": "",
            "spriteWidth": m.art.w, "spriteHeight": m.art.h, "lowTexture": bool(m.art.flat), "matches": [],
            "suggestedBorder": border_hint(m.art)})
        box = (m.x, m.y, m.x + m.w, m.y + m.h)
        if any(iou(box, (e["x"], e["y"], e["x"] + e["w"], e["y"] + e["h"])) > 0.9 for e in s["matches"]):
            continue  # hai layer cùng một art ở cùng chỗ (bản sao chồng lên nhau)
        s["matches"].append(m.entry())
    sprite_list = list(sprites.values())
    if emit_live:
        for i, s in enumerate(sprite_list):
            emit("sprite", sprite=s, done=i + 1, total=len(sprite_list))
    return {
        "sprites": sprite_list, "texts": texts, "dropped": dropped, "dimAlpha": round(dim_alpha, 4),
        "notes": notes,
    }


def add_residual(leaf, raster, inner, exporter, matches, cache, covers, fill, reader=None, texts=None):
    """
    Phần pixel của layer mà art đã khớp không có, đặt đúng chỗ; True nếu có. Chữ vẽ sẵn (OCR đọc được) → thêm vào
    texts thành chữ TMP; còn lại xuất PNG.
    """
    part = residual(raster, inner, leaf.box, cache, covers, fill)
    if part is None:
        return False
    rgba, ox, oy = part
    if not fill and reader is not None and texts is not None:
        entry = baked_text(rgba, leaf.box[0] + ox, leaf.box[1] + oy, reader)
        if entry is not None:
            texts.append(entry)
            return True
    art = exporter.export(f"{leaf.name}_frame" if fill else f"{leaf.name}_extra", rgba)
    h, w = rgba.shape[:2]
    matches.append(Match(art, leaf, leaf.box[0] + ox, leaf.box[1] + oy, w, h, 1.0, False, Score(1.0, 0.0, 1.0), PLAIN))
    return True


def border_hint(art):
    """Border 9-slice của sprite xuất ra (hình bo góc dùng chung) để Unity set khi import; None nếu không có."""
    if art.border is None:
        return None
    left, top, right, bottom = art.border
    return {"left": left, "right": right, "top": top, "bottom": bottom}


def drop(leaf, reason):
    x0, y0, x1, y1 = leaf.box
    return {"name": leaf.name, "reason": reason, "x": x0, "y": y0, "w": x1 - x0, "h": y1 - y0}


DEMO_BLOCK = 32              # so demo với PSD theo ô 32 px
DEMO_BLOCK_DIFF = 40        # ô lệch màu trung bình > 40 = khác
DEMO_DIFF_RATIO = 0.03      # ≥ 3% ô khác → báo demo không khớp PSD


def state_image(psd, canvas, states, visible_override, file_visibility, leaves, raster_of):
    """
    Ảnh để so khớp art của một trạng thái, luôn lấy từ PSD (demo của designer có thể là bản cũ): ảnh ghép Photoshop lưu
    sẵn khi trạng thái hiện đúng như trong file (đủ hiệu ứng), không thì tự ghép từ pixel các layer (thiếu hiệu ứng).
    """
    same = all(visible_override.get(id(s), s.visible) == file_visibility[id(s)] for s in states)
    if same:
        bgra = pil_to_bgra(psd.topil())
        x0, y0 = canvas[0], canvas[1]
        return np.ascontiguousarray(bgra[y0:canvas[3], x0:canvas[2], :3]), True
    return render_state(leaves, canvas, raster_of), False


def compare_demo(demo, image, k):
    """Ảnh demo so với PSD: (ảnh demo đã khớp cỡ, ghi chú khác biệt hoặc None)."""
    ch, cw = image.shape[:2]
    shot = cv2.imread(demo, cv2.IMREAD_COLOR)
    if shot is None:
        return None, f"Không đọc được ảnh demo {demo}."
    note = None
    if shot.shape[1] != cw or shot.shape[0] != ch:
        note = f"Ảnh demo {os.path.basename(demo)} {shot.shape[1]}×{shot.shape[0]} khác khung PSD {cw}×{ch}."
        shot = cv2.resize(shot, (cw, ch), interpolation=cv2.INTER_AREA)
    diff = np.abs(shot.astype(np.int16) - image.astype(np.int16)).mean(axis=2)
    bh, bw = ch // DEMO_BLOCK, cw // DEMO_BLOCK
    blocks = diff[:bh * DEMO_BLOCK, :bw * DEMO_BLOCK].reshape(bh, DEMO_BLOCK, bw, DEMO_BLOCK).mean(axis=(1, 3))
    bad = blocks > DEMO_BLOCK_DIFF
    if bad.mean() >= DEMO_DIFF_RATIO:
        ys, xs = np.nonzero(bad)
        box = (xs.min() * DEMO_BLOCK, ys.min() * DEMO_BLOCK, (xs.max() + 1) * DEMO_BLOCK, (ys.max() + 1) * DEMO_BLOCK)
        note = (f"Tab {k + 1}: ảnh demo {os.path.basename(demo)} KHÁC PSD ở {bad.mean():.0%} diện tích (vùng "
                f"{box[0]},{box[1]} → {box[2]},{box[3]}) — prefab dựng theo PSD; demo có thể là bản cũ.")
    return shot, note


def locate_path(out_dir, k):
    return os.path.join(out_dir, "locate.json" if k == 0 else f"locate_{k + 1}.json")


def main():
    ap = argparse.ArgumentParser(description="Đọc PSD → locate.json (tọa độ layer + art) cho GameUp UI Builder.")
    ap.add_argument("--psd", required=True, help="File PSD/PSB")
    ap.add_argument("--art", action="append", default=[], help="Thư mục art hoặc file PNG; lặp lại được")
    ap.add_argument("--recursive", action="store_true", help="Quét cả thư mục con")
    ap.add_argument("--out-dir", required=True, help="Thư mục job (ghi locate.json, locate_2.json…, psd_state_k.png)")
    ap.add_argument("--demo", action="append", default=[], help="Ảnh demo theo thứ tự trạng thái (tuỳ chọn, để so)")
    ap.add_argument("--export", default="", help="Thư mục (trong Assets) ghi PNG cho layer không có art; trống = không xuất")
    ap.add_argument("--states", default="", help="Tên các nhóm trạng thái, cách nhau dấu phẩy (mặc định tự tìm)")
    ap.add_argument("--artboard", default="", help="Tên artboard (mặc định artboard đầu tiên)")
    args = ap.parse_args()

    started = time.time()
    psd = PSDImage.open(args.psd)
    root, canvas, board_count = find_root(psd, args.artboard)
    cw, ch = canvas[2] - canvas[0], canvas[3] - canvas[1]
    arts = []
    for path in list_art(args.art, args.recursive):
        rgba = load_rgba(path)
        if rgba is not None:
            arts.append(Art(path, rgba))

    states = detect_states(root, canvas, args.states)
    file_visibility = {id(s): s.visible for s in states}
    state_names = [s.name for s in states] or [root.name if root is not psd else os.path.basename(args.psd)]
    emit("start", total=len(state_names), cachedCount=0, workers=1, demoWidth=cw, demoHeight=ch,
         message=f"{len(arts)} art · {len(state_names)} trạng thái")
    os.makedirs(args.out_dir, exist_ok=True)
    exporter = Exporter(args.export) if args.export else None

    rasters = {}

    def raster_of(leaf):
        if id(leaf.layer) not in rasters:
            rasters[id(leaf.layer)] = layer_raster(leaf, canvas)
        return rasters[id(leaf.layer)]

    prepared = []
    for k, name in enumerate(state_names):
        override = {id(s): (i == k) for i, s in enumerate(states)}
        leaves = collect_leaves(root, canvas, override)
        image, exact = state_image(psd, canvas, states, override, file_visibility, leaves, raster_of)
        notes = []
        demo = args.demo[k] if k < len(args.demo) and args.demo[k] else None
        if demo:
            _, note = compare_demo(demo, image, k)
            if note:
                notes.append(note)
        else:
            demo = os.path.abspath(os.path.join(args.out_dir, f"psd_state_{k + 1}.png"))
            cv2.imwrite(demo, image)
            if not exact:
                notes.append(f"Tab {k + 1}: chưa có ảnh demo — ảnh so sánh ghép từ PSD, thiếu hiệu ứng layer (stroke, bóng…).")
        prepared.append((name, leaves, image, exact, demo, notes))

    # lượt 1: art khớp theo tên ở bất kỳ trạng thái nào = art của màn → lượt 2 nới ngưỡng cho art đó ở mọi trạng thái
    by_key = index_arts(arts)
    known = set()
    for _, leaves, image, _, _, _ in prepared:
        known |= named_arts(leaves, image, arts, by_key)

    # trạng thái có ảnh ghép chuẩn (đủ hiệu ứng) xử lý trước — layer dùng chung và vị trí art của shape có hiệu ứng
    # học ở đó, dùng lại cho trạng thái ghép thiếu hiệu ứng
    ctx = Context(canvas, arts, known, exporter, raster_of)
    for k in sorted(range(len(prepared)), key=lambda i: not prepared[i][3]):
        name, leaves, image, exact, demo_path, image_notes = prepared[k]
        emit("filter", message=f"Trạng thái {k + 1}/{len(state_names)} '{name}': {len(leaves)} layer")
        result = process_state(ctx, leaves, image, exact, k == 0)
        notes = image_notes + result.pop("notes")
        if k == 0 and board_count > 1:
            notes.insert(0, f"PSD có {board_count} artboard — đang đọc '{root.name}' (đổi bằng --artboard).")
        out = {
            "version": VERSION, "source": "psd", "psd": os.path.abspath(args.psd), "state": name, "states": state_names,
            "demo": demo_path, "demoWidth": cw, "demoHeight": ch, "elapsedMs": int((time.time() - started) * 1000),
            "cachedCount": 0, "uiRegions": [], "dimEstimated": False, "ocr": "psd",
            "exported": [os.path.abspath(f) for f in exporter.files] if exporter else [], "notes": notes, **result,
        }
        with open(locate_path(args.out_dir, k), "w", encoding="utf-8") as f:
            json.dump(out, f, ensure_ascii=False, indent=1)

    # file locate của lần đọc trước còn thừa (PSD bớt trạng thái) → xoá để Unity không nạp nhầm
    k = len(state_names)
    while os.path.exists(locate_path(args.out_dir, k)):
        os.remove(locate_path(args.out_dir, k))
        k += 1
    print(f"OK {len(state_names)} trạng thái · {time.time() - started:.1f} s")


if __name__ == "__main__":
    sys.exit(main())
