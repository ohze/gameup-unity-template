#!/usr/bin/env python3
"""
GameUp UI Builder — định vị sprite trên ảnh demo.

Designer bàn giao ảnh demo (vd 1080x2160) cùng art cắt 1:1. Script tìm mỗi sprite nằm ở đâu
trên demo (tọa độ pixel, góc trên-trái), kể cả khi:
  - sprite bị scale đều (thử nhiều tỉ lệ, tinh chỉnh quanh tỉ lệ tốt nhất),
  - sprite bị kéo giãn kiểu 9-slice (dò 4 góc rồi ghép thành khung),
  - sprite xuất hiện nhiều lần, hoặc bị phần tử khác che một phần.

Hiệu năng:
  - Dò thô trên ảnh thu nhỏ, chỉ dò mịn ở cửa sổ nhỏ quanh ứng viên.
  - Mỗi sprite chạy ở một process riêng (ProcessPoolExecutor).
  - Cache kết quả theo hash(demo + sprite + tham số) — lần chạy lại chỉ tính sprite đã đổi.

Đầu ra: JSON (xem --out), được Unity Editor đọc để sinh spec và dựng prefab.
Tiến trình: mỗi dòng stdout "@progress {json}" là một sự kiện (sprite dò xong, khớp bị lọc, dòng chữ đọc được) để
cửa sổ Unity vẽ trực tiếp lên ảnh demo trong lúc chạy.
"""
import os

# Song song ở mức process → mỗi process chỉ 1 luồng BLAS/OpenMP, tránh tranh CPU. Phải đặt trước khi import numpy/cv2.
for _var in ("OMP_NUM_THREADS", "OPENBLAS_NUM_THREADS", "MKL_NUM_THREADS"):
    os.environ.setdefault(_var, "1")

import argparse
import hashlib
import json
import sys
import time
from concurrent.futures import ProcessPoolExecutor, as_completed

import cv2
import numpy as np

ALGO_VERSION = "11"
ALPHA_OPAQUE = 200          # pixel có alpha > ngưỡng mới dùng để so khớp
MIN_OPAQUE_PIXELS = 64      # sprite gần như trong suốt (glow/vfx) → không dò được bằng hình
ROBUST_KEEP = 0.75          # sai lệch màu tính trên 75% pixel khớp nhất → chịu được bị che ~25%
ACCEPT_ZNCC = 0.80          # tương quan cấu trúc tối thiểu (khớp thật ≥ 0.9, khớp nhầm thường < 0.7)
ACCEPT_DIFF = 16.0          # sai lệch màu tối đa (0-255) trên phần không bị che
MIN_INLIER = 0.30           # ... và ≥ 30% pixel trùng gần tuyệt đối: khớp thật (kể cả JPEG, scale bicubic, bị che) đo được
                            #     ≥ 0.6; khớp nhầm chỉ nhờ tương quan (gradient tối giống viền nút) chỉ 0.02–0.16
OCCLUDED_SIDES = 3          # bị che: viền lộ đủ ở ≥ 3/4 cạnh (title/nút X đè mép trên popup) thay cho viền lộ ≥ 85% tổng
OCCLUDED_SIDE_EDGE = 0.65   # ... khi đó tổng viền lộ ≥ 65%
TINT_DETAIL_GRAD = 40.0     # tint: pixel chi tiết của sprite (gradient Sobel > 40 sau khi tô màu)...
TINT_DETAIL_MIN = 200       # ... phải có đủ nhiều — sprite gần một màu nhân tint khớp với mọi nền phẳng...
TINT_DETAIL_INLIER = 0.6    # ... và ≥ 60% trong số đó trùng với demo (tint thật: 0.73 kể cả JPEG; nhầm: ≤ 0.5)
TINT_MIN_STD = 15.0         # tint làm mất hoạ tiết (độ lệch xám sau tô < 15; tab tint thật ~30) → không phân biệt được
STRONG_ZNCC = 0.95          # khớp gần tuyệt đối ở 1:1 → bỏ qua dò scale / 9-slice
LOW_TEXTURE_STD = 6.0       # template gần như một màu → ZNCC vô nghĩa
LOW_TEXTURE_DIFF = 3.0      # ... nên chỉ nhận khi trùng gần tuyệt đối, và chỉ ở tỉ lệ 1:1
EXACT_TOLERANCE = 6         # pixel "trùng tuyệt đối" (demo ghép từ chính art này) — sai lệch màu ≤ ngưỡng
OCCLUDED_INLIER = 0.50      # sprite lớn bị che nhiều (nền popup dưới chữ/icon/nút): ≥ 50% pixel trùng tuyệt đối...
OCCLUDED_MIN_PIXELS = 5000  # ... và đủ nhiều pixel để không thể trùng ngẫu nhiên
FLAT_BOUNDARY = 0.60        # sprite một màu: ≥ 60% dải viền trong trùng màu (phần còn lại có thể bị che)...
FLAT_BOUNDARY_SHAPE = 0.85  # ... hình bóng không phải khối (lấp < 85% khung — icon cung, gậy phép trắng) khớp nhầm vào
FLAT_SHAPE_FILL = 0.85      #     mũi tên trắng với 60–70% viền trùng
FLAT_SLICED_INLIER = 0.30   # panel một màu kéo giãn: ≥ 30% pixel cùng màu
FLAT_RING = 0.75            # ... và vành ngay ngoài khác màu ở ≥ 3/4 cạnh (mỗi cạnh ≥ 75%) — chốt vị trí kể cả khi
FLAT_SIDES = 3              #     nằm giữa mảng cùng màu; cho phép 1 cạnh liền màu (đáy popup nối thanh tab cùng màu)
TINT_BRIGHT = 60            # chỉ ước lượng tint trên pixel sprite đủ sáng (pixel tối nhân màu vẫn tối)
TINT_TOLERANCE = 10         # tint làm tròn màu → nới ngưỡng "trùng" một chút
TINT_MIN = 0.95             # mọi kênh ≥ 0.95 = không tint → để luật thường quyết
DIM_GRAY_SPREAD = 0.06      # tint xám đều (3 kênh lệch ≤ 0.06) và tối ≤ 0.6 = vật nằm sau lớp dim của popup
DIM_MAX = 0.6
FLAT_EDGE_STRONG = 0.95     # sprite có hoạ tiết nhưng phần lộ ra phẳng (nút sau chữ): viền trùng gần tuyệt đối là đủ
OCCLUDED_EDGE_INLIER = 0.85 # ... và viền ngoài của sprite phải lộ ra gần đủ (khúc giữa của nút kéo giãn không có viền)
EDGE_RING = 4               # độ dày vành kiểm tra quanh mép sprite (px)
COARSE_SCALES = [0.9, 0.8, 0.75, 0.7, 0.6, 0.5, 1.1, 1.25, 1.5]
MAX_INSTANCES = 12
PEAK_CANDIDATES = 16
MAX_WORKERS = 12
PRESENCE_GATE = 0.25        # tương quan nét sprite/demo tốt nhất < 0.25 → không có trên demo ở tỉ lệ này (khớp thật ≥ 0.38)
COLOR_GATE = 0.6            # < 60% màu của sprite có trên demo → bỏ dò scale và 9-slice (khớp thật ≥ 0.88)

PROGRESS_PREFIX = "@progress "

_demo = None
_demo_gray = None
_pyramid = {}
_edge_pyramid = {}


def _init_worker(demo_path):
    global _demo, _demo_gray, _demo_colors
    cv2.setNumThreads(1)
    _demo = _load_rgb(demo_path)
    _demo_gray = cv2.cvtColor(_demo, cv2.COLOR_BGR2GRAY)
    _demo_colors = None
    _pyramid.clear()
    _edge_pyramid.clear()
    _flat_maps_cache.clear()


def _gray_at(f):
    """Ảnh demo xám ở tỉ lệ f — bước dò thô chạy trên ảnh xám thu nhỏ (rẻ hơn ~3 lần so với ảnh màu)."""
    if f >= 1.0:
        return _demo_gray
    img = _pyramid.get(f)
    if img is None:
        h, w = _demo_gray.shape[:2]
        img = cv2.resize(_demo_gray, (max(1, round(w * f)), max(1, round(h * f))), interpolation=cv2.INTER_AREA)
        _pyramid[f] = img
    return img


def _edges(gray):
    """Bản đồ nét (Canny, nới 1 px) dạng float — dùng tìm ứng viên khi màu bị che nhiều nhưng đường viền còn nguyên."""
    edges = cv2.Canny(gray, 50, 150)
    return (cv2.dilate(edges, np.ones((3, 3), np.uint8)) > 0).astype(np.float32)


def _edges_at(f):
    img = _edge_pyramid.get(f)
    if img is None:
        img = _edges(_gray_at(f))
        _edge_pyramid[f] = img
    return img


def _load_rgb(path):
    img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    if img is None:
        raise IOError(f"Không đọc được ảnh: {path}")
    if img.ndim == 2:
        img = cv2.cvtColor(img, cv2.COLOR_GRAY2BGR)
    if img.shape[2] == 4:
        # Demo có alpha thì ghép lên nền đen để so khớp ổn định.
        alpha = img[:, :, 3:4].astype(np.float32) / 255.0
        img = (img[:, :, :3].astype(np.float32) * alpha).astype(np.uint8)
    return np.ascontiguousarray(img[:, :, :3])


def _load_rgba(path):
    img = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    if img is None:
        return None
    if img.ndim == 2:
        img = cv2.cvtColor(img, cv2.COLOR_GRAY2BGRA)
    elif img.shape[2] == 3:
        img = cv2.cvtColor(img, cv2.COLOR_BGR2BGRA)
    return img


# ─── So khớp cơ bản ──────────────────────────────────────────────────────────

class _Template:
    """Template ở một tỉ lệ: ảnh BGR + mask pixel đục, kèm bản thu nhỏ cho bước dò thô."""

    def __init__(self, rgba, scale=1.0):
        h, w = rgba.shape[:2]
        if scale != 1.0:
            nw, nh = max(1, round(w * scale)), max(1, round(h * scale))
            interp = cv2.INTER_AREA if scale < 1.0 else cv2.INTER_NEAREST
            rgba = cv2.resize(rgba, (nw, nh), interpolation=interp)
        self.scale = scale
        self.bgr = np.ascontiguousarray(rgba[:, :, :3])
        self.mask = np.where(rgba[:, :, 3] > ALPHA_OPAQUE, 255, 0).astype(np.uint8)
        self.h, self.w = self.mask.shape
        self.opaque = int(np.count_nonzero(self.mask))
        self.gray = cv2.cvtColor(self.bgr, cv2.COLOR_BGR2GRAY)
        m = self.mask > 0
        self.gray_masked = self.gray.astype(np.float32)[m]
        self.texture_std = float(self.gray_masked.std()) if self.opaque else 0.0
        self.boundary = _boundary(self.mask)
        self._boundary_sides = None
        self.shape_cache = None

        self.f = _coarse_factor(self.w, self.h)
        if self.f < 1.0:
            sw, sh = max(1, round(self.w * self.f)), max(1, round(self.h * self.f))
            self.small_gray = cv2.resize(self.gray, (sw, sh), interpolation=cv2.INTER_AREA)
            self.small_mask = cv2.resize(self.mask, (sw, sh), interpolation=cv2.INTER_NEAREST)
            if np.count_nonzero(self.small_mask) < 16:
                self.f, self.small_gray, self.small_mask = 1.0, self.gray, self.mask
        else:
            self.small_gray, self.small_mask = self.gray, self.mask

    @property
    def boundary_sides(self):
        if self._boundary_sides is None:
            self._boundary_sides = _boundary_sides(self.boundary)
        return self._boundary_sides

    @property
    def low_texture(self):
        return self.texture_std < LOW_TEXTURE_STD

    def fits(self, image):
        return self.w <= image.shape[1] and self.h <= image.shape[0] and self.opaque >= MIN_OPAQUE_PIXELS


def _boundary(mask):
    """Dải viền trong ~3 px của vùng đục. Ngoài mép ảnh coi là trong suốt — mặc định erode của OpenCV coi là đầy,
    khiến sprite kín tới mép ảnh chỉ còn viền ở góc bo."""
    eroded = cv2.erode(mask, np.ones((7, 7), np.uint8), borderType=cv2.BORDER_CONSTANT, borderValue=0)
    return (mask > 0) & (eroded == 0)


def _boundary_sides(boundary):
    """Dải viền trong chia theo cạnh gần nhất của khung (0 trên, 1 dưới, 2 trái, 3 phải) — để biết viền bị che ở một
    cạnh (title đè mép trên popup) hay rải rác khắp nơi (vị trí sai)."""
    h, w = boundary.shape
    ys, xs = np.nonzero(boundary)
    side = np.argmin(np.stack([ys, h - 1 - ys, xs, w - 1 - xs]), axis=0)
    return [(ys[side == k], xs[side == k]) for k in range(4)]


def _edge_evidence(exact2d, t):
    """(tỉ lệ viền trùng tổng, số cạnh có viền trùng ≥ 85%). Cạnh quá ít pixel viền coi như đạt."""
    edge = float(exact2d[t.boundary].mean())
    sides = 0
    for ys, xs in t.boundary_sides:
        sides += len(ys) < 8 or float(exact2d[ys, xs].mean()) >= OCCLUDED_EDGE_INLIER
    return edge, sides


def _edge_ok(edge, sides):
    return edge >= OCCLUDED_EDGE_INLIER or (sides >= OCCLUDED_SIDES and edge >= OCCLUDED_SIDE_EDGE)


def _coarse_factor(w, h):
    """Template càng lớn càng thu nhỏ mạnh khi dò thô; template nhỏ giữ nguyên để không mất chi tiết."""
    side = min(w, h)
    if side >= 160:
        return 0.25
    if side >= 32:
        return 0.5
    return 1.0


def _masked_score_map(image, templ, mask):
    """Bản đồ sai số bình phương trung bình trên pixel đục (nhỏ = khớp). Ảnh/template xám hoặc màu đều được."""
    channels = 1 if templ.ndim == 2 else templ.shape[2]
    count = max(1, int(np.count_nonzero(mask)))
    if channels > 1:
        mask = cv2.merge([mask] * channels)
    res = cv2.matchTemplate(image, templ, cv2.TM_SQDIFF, mask=mask)
    res = np.nan_to_num(res, nan=1e12, posinf=1e12, neginf=1e12)
    return res / (count * channels)


def _top_peaks(score_map, k, min_dist):
    """Lấy k điểm cực tiểu, loại các điểm quá gần nhau (NMS đơn giản)."""
    flat = score_map.ravel()
    k_eff = min(len(flat), k * 40)
    idx = np.argpartition(flat, k_eff - 1)[:k_eff]
    idx = idx[np.argsort(flat[idx])]
    w = score_map.shape[1]
    peaks = []
    for i in idx:
        y, x = divmod(int(i), w)
        if all(abs(x - px) >= min_dist or abs(y - py) >= min_dist for px, py in peaks):
            peaks.append((x, y))
            if len(peaks) >= k:
                break
    return peaks


def _coarse_best(t):
    """Điểm dò thô tốt nhất (RMS) — dùng để chọn tỉ lệ scale, rẻ vì chạy trên ảnh thu nhỏ."""
    img = _gray_at(t.f)
    if t.small_gray.shape[0] > img.shape[0] or t.small_gray.shape[1] > img.shape[1]:
        return float("inf")
    score = _masked_score_map(img, t.small_gray, t.small_mask)
    return float(np.sqrt(max(0.0, score.min())))


def _verify(x, y, t):
    """(diff, zncc, inlier) tại (x, y) ở độ phân giải gốc — None nếu không được nhận.
    diff: sai lệch màu trung bình — bản thường với sprite một màu, bản bỏ 25% pixel tệ nhất (bị che) với sprite có hoạ tiết.
    zncc: tương quan cấu trúc trên ảnh xám, bất biến với độ sáng — tách khớp thật khỏi vùng cùng màu.
    inlier: tỉ lệ pixel trùng tuyệt đối — bằng chứng cho sprite bị che nhiều (zncc tụt vì phần che)."""
    patch = _demo[y:y + t.h, x:x + t.w]
    if patch.shape[:2] != (t.h, t.w):
        return None
    m = t.mask > 0
    diff2d = np.abs(patch.astype(np.int16) - t.bgr.astype(np.int16)).mean(axis=2)
    diff = diff2d[m]
    exact = diff <= EXACT_TOLERANCE
    inlier = float(exact.mean())

    keep = max(1, int(diff.size * ROBUST_KEEP))
    robust = float(np.partition(diff, keep - 1)[:keep].mean())
    g = cv2.cvtColor(patch, cv2.COLOR_BGR2GRAY).astype(np.float32)[m]
    a, b = g - g.mean(), t.gray_masked - t.gray_masked.mean()
    denom = float(np.sqrt((a * a).sum() * (b * b).sum()))
    zncc = float((a * b).sum() / denom) if denom > 0 else 0.0
    if zncc >= ACCEPT_ZNCC and robust <= ACCEPT_DIFF and inlier >= MIN_INLIER:
        return robust, zncc, inlier
    # Bị che nhiều: phần lộ ra vẫn trùng tuyệt đối, có hoạ tiết (không phải mảng một màu trùng ngẫu nhiên),
    # và viền ngoài lộ gần đủ (hoặc đủ ở 3/4 cạnh) — loại các khúc của sprite khác trùng pixel một phần.
    if inlier < OCCLUDED_INLIER or exact.sum() < OCCLUDED_MIN_PIXELS:
        return None
    edge, sides = _edge_evidence(diff2d <= EXACT_TOLERANCE, t)
    if _edge_ok(edge, sides) and (t.gray_masked[exact].std() >= LOW_TEXTURE_STD or edge >= FLAT_EDGE_STRONG):
        return robust, zncc, inlier
    return None


def _verify_tinted(x, y, t):
    """Sprite bị tô màu trong Unity (Image.color: demo = sprite × tint, vd tab chưa chọn tối hơn). Ước lượng tint bằng
    trung vị tỉ lệ demo/sprite trên pixel sáng (bền với phần bị che), rồi kiểm tra như sprite thường.
    Trả về (diff, zncc, inlier, "#RRGGBB") hoặc None."""
    patch = _demo[y:y + t.h, x:x + t.w]
    # Chỉ ở 1:1; sprite gần kín màn hình (nền) bị lớp dim của popup làm tối trông như tint nhưng không thuộc UI này.
    if t.scale != 1.0 or patch.shape[:2] != (t.h, t.w) or t.w * t.h >= _demo.shape[0] * _demo.shape[1] * 0.8:
        return None
    m = t.mask > 0
    src = t.bgr.astype(np.float32)
    bright = m & (src.max(axis=2) > TINT_BRIGHT)
    if bright.sum() < 500:
        return None
    ratio = patch.astype(np.float32)[bright] / np.maximum(src[bright], 1.0)
    tint = np.clip(np.median(ratio, axis=0), 0.0, 1.0)
    if tint.min() >= TINT_MIN:
        return None
    tinted = src * tint
    diff2d = np.abs(tinted - patch.astype(np.float32)).mean(axis=2)
    exact = diff2d <= TINT_TOLERANCE
    inlier = float(exact[m].mean())
    edge, sides = _edge_evidence(exact, t)
    # Tint tối làm pixel tối (viền đen) khớp với mọi vùng tối → đòi cả phần sáng của sprite cũng trùng.
    if (inlier < OCCLUDED_INLIER or not _edge_ok(edge, sides) or float(exact[bright].mean()) < OCCLUDED_INLIER
            or exact[m].sum() < min(OCCLUDED_MIN_PIXELS, t.opaque * 0.3)):
        return None
    # Sprite gần một màu nhân tint trùng với mọi nền phẳng → chi tiết (nét, cạnh bên trong) của nó cũng phải trùng.
    gray = cv2.cvtColor(np.clip(tinted, 0, 255).astype(np.uint8), cv2.COLOR_BGR2GRAY)
    if float(gray[m].std()) < TINT_MIN_STD:
        return None
    grad =cv2.magnitude(cv2.Sobel(gray, cv2.CV_32F, 1, 0), cv2.Sobel(gray, cv2.CV_32F, 0, 1))
    detail = (cv2.erode(t.mask, np.ones((3, 3), np.uint8)) > 0) & (grad > TINT_DETAIL_GRAD)
    if detail.sum() < TINT_DETAIL_MIN or float(exact[detail].mean()) < TINT_DETAIL_INLIER:
        return None
    b, g, r = (tint * 255).round().astype(int)
    return float(diff2d[m].mean()), 0.0, inlier, "#{:02X}{:02X}{:02X}".format(r, g, b)


def _search_flat(t, max_instances):
    """Sprite một màu (panel, nền popup): vị trí được chốt bằng hình dáng, không bằng pixel bên trong.
    Điểm = tỉ lệ dải viền trong trùng màu + tỉ lệ vành ngay ngoài khác màu — tính cho mọi vị trí bằng matchTemplate
    (FFT) trên 2 mặt nạ nhị phân. Nằm giữa mảng cùng màu → vành ngoài cùng màu → bị loại; mép bị che (banner đè
    lên mép trên) vẫn tương phản nên vẫn chốt đúng."""
    if t.scale != 1.0 or not t.fits(_demo):
        return []
    same, other = _flat_maps(_flat_color(t))
    boundary, ring, sides = _flat_shape(t.mask, t.boundary)
    if ring.sum() < 16 or boundary.sum() < 16:
        return []
    # Vành ngoài tách theo 4 cạnh: dải tím giữa 2 khối (trên/dưới tương phản, 2 đầu liền màu) không được nhận.
    score, edge = _flat_score(same, other, boundary, ring, sides, (1, 2, 3, 4), FLAT_SIDES, _flat_min_edge(t.mask))
    return [(x, y, 0.0, 0.0, round(float(edge[y, x]), 3)) for x, y in _flat_peaks(score, t.w * 0.5, t.h * 0.5, max_instances)]


def _flat_color(t):
    return tuple(int(c) for c in np.median(t.bgr[t.mask > 0], axis=0))


_flat_maps_cache = {}


def _flat_maps(color):
    """(pixel trùng màu, pixel khác hẳn màu) trên demo, đệm EDGE_RING — ngoài mép ảnh coi là khác màu."""
    maps = _flat_maps_cache.get(color)
    if maps is None:
        r = EDGE_RING
        diff = np.abs(_demo.astype(np.int16) - np.array(color, np.int16)).max(axis=2)
        same = cv2.copyMakeBorder((diff <= EXACT_TOLERANCE).astype(np.float32), r, r, r, r, cv2.BORDER_CONSTANT, value=0)
        other = cv2.copyMakeBorder((diff > 20).astype(np.float32), r, r, r, r, cv2.BORDER_CONSTANT, value=1)
        if len(_flat_maps_cache) > 8:
            _flat_maps_cache.clear()
        maps = _flat_maps_cache[color] = (same, other)
    return maps


def _flat_shape(mask, boundary_mask):
    """Mặt nạ đệm EDGE_RING của một hình: (dải viền trong, vành ngay ngoài, nhãn cạnh 1 trên/2 dưới/3 trái/4 phải)."""
    r = EDGE_RING
    h, w = mask.shape
    padded = cv2.copyMakeBorder(mask, r, r, r, r, cv2.BORDER_CONSTANT, value=0)
    ring = ((cv2.dilate(padded, np.ones((2 * r + 1, 2 * r + 1), np.uint8)) > 0) & (padded == 0)).astype(np.float32)
    boundary = np.zeros(padded.shape, np.float32)
    boundary[r:r + h, r:r + w] = boundary_mask
    sides = np.zeros(ring.shape, np.uint8)
    sides[:r, :], sides[r + h:, :] = 1, 2
    sides[r:r + h, :r], sides[r:r + h, r + w:] = 3, 4
    return boundary, ring, sides


def _flat_min_edge(mask):
    """Tỉ lệ viền trong trùng màu tối thiểu cho sprite một màu, theo cỡ và hình dạng của nó (đã kéo giãn nếu 9-slice)."""
    h, w = mask.shape
    # Không siết theo cỡ: khung thanh máu thật bị phần máu che chỉ lộ 62–66% viền, trùng khoảng với khớp nhầm.
    return FLAT_BOUNDARY_SHAPE if np.count_nonzero(mask) < FLAT_SHAPE_FILL * w * h else FLAT_BOUNDARY


def _flat_score(same, other, boundary, ring, sides, use_sides, need_sides, min_edge=FLAT_BOUNDARY):
    """Điểm hình dáng cho mọi vị trí (matchTemplate/FFT): (điểm, -1 nếu không đạt; tỉ lệ viền trong trùng màu).
    Chỉ xét vành ngoài ở các cạnh use_sides — góc của 9-slice chỉ có 2 cạnh ngoài."""
    edge = cv2.matchTemplate(same, boundary, cv2.TM_CCORR) / max(1.0, float(boundary.sum()))
    contrast = np.zeros(edge.shape, np.float32)
    passing = np.zeros(edge.shape, np.int32)
    ring_total = max(1.0, float(sum(ring[sides == s].sum() for s in use_sides)))
    for side in use_sides:
        part = ring * (sides == side)
        if part.sum() < 4:
            passing += 1  # cạnh không có vành (sprite sát mép ảnh gốc) → không xét
            continue
        c = cv2.matchTemplate(other, part, cv2.TM_CCORR) / part.sum()
        passing += (c >= FLAT_RING).astype(np.int32)
        contrast += c * part.sum() / ring_total
    return np.where((edge >= min_edge) & (passing >= need_sides), edge + contrast, -1.0), edge


def _flat_peaks(score, min_dx, min_dy, limit):
    results = []
    for py, px in zip(*np.unravel_index(np.argsort(score, axis=None)[::-1][:2000], score.shape)):
        if score[py, px] < 0:
            break
        if all(abs(px - k[0]) > min_dx or abs(py - k[1]) > min_dy for k in results):
            results.append((int(px), int(py)))
            if len(results) >= limit:
                break
    return results


def _flat_corners(sprite, cw, ch):
    """Góc của panel một màu kéo giãn 9-slice: mỗi góc chỉ xét 2 cạnh ngoài của nó (2 cạnh trong nối vào thân panel).
    Trả về {tl,tr,bl,br: [(x, y, diff, zncc, inlier)]} — (x, y) là góc trên-trái của mảnh góc trên demo."""
    t = _Template(sprite, 1.0)
    same, other = _flat_maps(_flat_color(t))
    boundary, ring, sides = _flat_shape(t.mask, t.boundary)
    r, h, w = EDGE_RING, t.h, t.w
    # Cửa sổ trên hình đệm (hàng, cột), cạnh ngoài, độ lệch từ gốc cửa sổ tới góc trên-trái mảnh góc.
    windows = {"tl": (slice(0, r + ch), slice(0, r + cw), (1, 3), (r, r)),
               "tr": (slice(0, r + ch), slice(r + w - cw, w + 2 * r), (1, 4), (0, r)),
               "bl": (slice(r + h - ch, h + 2 * r), slice(0, r + cw), (2, 3), (r, 0)),
               "br": (slice(r + h - ch, h + 2 * r), slice(r + w - cw, w + 2 * r), (2, 4), (0, 0))}
    found = {}
    for key, (rows, cols, outer, (ox, oy)) in windows.items():
        b, g, s = boundary[rows, cols], ring[rows, cols], sides[rows, cols]
        if b.sum() < 8:
            return {}
        score, edge = _flat_score(same, other, b, g, s, outer, len(outer))
        # Gốc cửa sổ ở (px, py) trên demo đệm = (px - r, py - r) trên demo; mảnh góc lệch thêm (ox, oy).
        found[key] = [(x - r + ox, y - r + oy, 0.0, 0.0, round(float(edge[y, x]), 3))
                      for x, y in _flat_peaks(score, cw * 0.5, ch * 0.5, MAX_INSTANCES * 2)]
    return found


def _flat_rect_ok(rendered, x, y):
    """Panel một màu đã kéo giãn đặt tại (x, y): viền trong trùng màu và vành ngoài tương phản ở ≥ 3/4 cạnh."""
    mask = np.where(rendered[:, :, 3] > ALPHA_OPAQUE, 255, 0).astype(np.uint8)
    t_color = tuple(int(c) for c in np.median(rendered[:, :, :3][mask > 0], axis=0))
    same, other = _flat_maps(t_color)
    boundary, ring, sides = _flat_shape(mask, _boundary(mask))
    r = EDGE_RING
    h, w = boundary.shape
    if y < 0 or x < 0 or y + h > same.shape[0] or x + w > same.shape[1]:
        return False
    s, o = same[y:y + h, x:x + w], other[y:y + h, x:x + w]
    if float((s * boundary).sum() / max(1.0, boundary.sum())) < _flat_min_edge(mask):
        return False
    # Ngoài mép ảnh coi là tương phản (thanh trên sát 3 mép màn hình) — nhưng phải có ít nhất 1 cạnh tương phản thật
    # trong ảnh, không thì khung kéo giãn bằng cả màn hình khớp với mọi nền cùng màu.
    H, W = _demo.shape[:2]
    inside = [y > 0, y + h - 2 * r < H, x > 0, x + w - 2 * r < W]
    passing = 0
    for side in (1, 2, 3, 4):
        part = ring * (sides == side)
        passing += part.sum() < 4 or float((o * part).sum() / part.sum()) >= FLAT_RING
    return passing >= FLAT_SIDES and any(inside)


def _shape_maps(t):
    """(bản đồ trùng nét trong, bản đồ trùng hình bóng ngoài, độ có mặt 0-1) ở ảnh thu nhỏ — tính một lần mỗi template.
    Độ có mặt = tỉ lệ nét của sprite trùng nét trên demo ở vị trí tốt nhất: khớp thật ≥ 0.86 (kể cả bị che, tint,
    JPEG), sprite không có trên demo thường < 0.7 → bỏ qua cả chục lần dò mịn."""
    if t.shape_cache is None:
        t_edges = _edges(t.small_gray) * (t.small_mask > 0)
        presence = 1.0  # quá ít nét để đánh giá → không chặn
        if t_edges.sum() >= 16:
            # Tương quan chuẩn hoá: demo nhiều chi tiết thì nét dày đặc, tỉ lệ trùng thô cao với mọi sprite.
            ncc = cv2.matchTemplate(_edges_at(t.f), t_edges.astype(np.float32), cv2.TM_CCOEFF_NORMED)
            presence = float(np.nan_to_num(ncc, nan=-1.0, posinf=-1.0, neginf=-1.0).max())
        t.shape_cache = (t_edges, presence)
    return t.shape_cache


def _shape_peaks(t, min_dist, limit):
    """Ứng viên theo nét trong (chữ/icon đè lên làm sai điểm màu nhưng chỉ thêm nét) và theo hình bóng ngoài (ruột bị
    che, bị tint nhưng viền quanh sprite vẫn nguyên). Tỉ lệ trùng thô (CCORR) chọn ứng viên tốt hơn NCC khi bị che."""
    img_edges = _edges_at(t.f)
    t_edges = _shape_maps(t)[0]
    silhouette = (cv2.Canny(t.small_mask, 50, 150) > 0).astype(np.float32)
    peaks = []
    for templ in (t_edges, silhouette):
        if templ.sum() >= 16:
            score = cv2.matchTemplate(img_edges, templ.astype(np.float32), cv2.TM_CCORR)
            peaks += _top_peaks(-score, limit, min_dist)
    return peaks


_demo_colors = None


def _color_presence(sprite):
    """Tỉ lệ pixel đục của sprite có màu (lượng tử 8 mức/kênh) xuất hiện trên demo. Khớp thật (kể cả scale, JPEG)
    ≥ 0.88; sprite của màn khác phần lớn < 0.6. Không dùng cho nhánh 1:1 — sprite bị tint đổi màu."""
    global _demo_colors
    if _demo_colors is None:
        _demo_colors = _color_codes(_demo.reshape(-1, 3))
        _demo_colors = np.unique(_demo_colors)
    px = sprite[:, :, :3][sprite[:, :, 3] > ALPHA_OPAQUE]
    return float(np.isin(_color_codes(px), _demo_colors).mean()) if len(px) else 0.0


def _color_codes(pixels):
    k = (pixels // 8).astype(np.int32)
    return k[:, 0] * 1024 + k[:, 1] * 32 + k[:, 2]


def _fits_coarse(t):
    img = _gray_at(t.f)
    return t.fits(_demo) and t.small_gray.shape[0] <= img.shape[0] and t.small_gray.shape[1] <= img.shape[1]


def _search(t, max_instances, gate=True):
    """Dò thô → dò mịn quanh ứng viên. Trả về [(x, y, diff, zncc, inlier)] đã lọc, tốt nhất trước.
    gate: bỏ qua sớm khi nét của sprite không xuất hiện trên demo (tắt cho mảnh góc 9-slice — quá ít nét)."""
    if t.low_texture:
        return _search_flat(t, max_instances)
    if not _fits_coarse(t):
        return []
    if gate and _shape_maps(t)[1] < PRESENCE_GATE:
        return []
    img = _gray_at(t.f)
    score = _masked_score_map(img, t.small_gray, t.small_mask)
    min_dist = max(2, int(min(t.small_gray.shape[:2]) * 0.5))
    H, W = _demo.shape[:2]
    # Số ứng viên theo số bản có thể đặt vừa ảnh: panel cỡ cả popup chỉ có 1 chỗ, khỏi dò mịn 32 ứng viên.
    fit = max(1, (W // t.w) * (H // t.h))
    peaks = _top_peaks(score, min(PEAK_CANDIDATES, 4 * fit + 4), min_dist)
    # Ứng viên gần trùng nhau (±1 px ở ảnh thu nhỏ) chỉ dò mịn một lần.
    peaks += [p for p in _shape_peaks(t, min_dist, min(PEAK_CANDIDATES // 2, 2 * fit + 2))
              if all(abs(p[0] - q[0]) > 1 or abs(p[1] - q[1]) > 1 for q in peaks)]

    pad = int(np.ceil(1.0 / t.f)) + 2
    results = []
    for cx, cy in peaks:
        x0, y0 = max(0, int(cx / t.f) - pad), max(0, int(cy / t.f) - pad)
        x1, y1 = min(W, int(cx / t.f) + pad + t.w), min(H, int(cy / t.f) + pad + t.h)
        window = _demo[y0:y1, x0:x1]
        if window.shape[0] < t.h or window.shape[1] < t.w:
            continue
        # Dò mịn trên ảnh xám (rẻ ~3 lần ảnh màu) — bước kiểm tra ngay sau vẫn so đủ màu.
        window_gray = cv2.cvtColor(window, cv2.COLOR_BGR2GRAY)
        fine = _masked_score_map(window_gray, t.gray, t.mask)
        _, _, loc, _ = cv2.minMaxLoc(fine)
        x, y = x0 + loc[0], y0 + loc[1]
        verdict = _verify(x, y, t)
        if verdict is None and t.scale == 1.0:
            # Sprite bị tint: điểm màu lệch khỏi chỗ đúng vài px → dò mịn lại bằng tương quan chuẩn hoá (bất biến với tint).
            corr = cv2.matchTemplate(window_gray, t.gray, cv2.TM_CCOEFF_NORMED, mask=t.mask)
            _, _, _, loc = cv2.minMaxLoc(np.nan_to_num(corr, nan=-1.0, posinf=-1.0, neginf=-1.0))
            x, y = x0 + loc[0], y0 + loc[1]
            verdict = _verify_tinted(x, y, t)
        if verdict is not None:
            results.append((x, y) + verdict)

    results.sort(key=lambda r: (len(r) > 5, -max(r[3], r[4]), r[2]))  # bản không tint trước
    kept = []
    for r in results:
        if all(abs(r[0] - k[0]) > t.w * 0.5 or abs(r[1] - k[1]) > t.h * 0.5 for k in kept):
            kept.append(r)
    return kept[:max_instances]


# ─── Scale đều ───────────────────────────────────────────────────────────────

SCALE_GROUPS = 3             # cùng sprite có thể xuất hiện ở vài tỉ lệ (avatar 0.66 trong hàng, 0.77 trong cờ top)
SCALE_GAP = 0.08             # 2 tỉ lệ cách nhau ≥ 0.08 mới coi là 2 nhóm khác nhau
SEED_GATE_MARGIN = 0.05      # tỉ lệ ứng viên lệch tỉ lệ thật tới 0.04 → nét trùng kém hơn, nới cổng thêm chút


def _find_uniform(sprite, try_scales=True):
    """[(template, hits)] theo từng tỉ lệ tìm thấy. Thử 1:1 trước (art bàn giao 1:1); chưa chắc thì chọn tới 3 tỉ lệ
    tốt nhất theo điểm dò thô (cách nhau ≥ 0.08), tinh chỉnh ±0.04 quanh mỗi tỉ lệ rồi dò mịn.
    Sprite một màu không dò scale — ở tỉ lệ khác nó khớp với mọi mảng cùng màu."""
    base = _Template(sprite, 1.0)
    found = _search(base, MAX_INSTANCES)
    if base.low_texture or not try_scales or (found and _is_strong(found[0])):
        return [(base, found)]

    ranked = sorted(((_coarse_best(_Template(sprite, s)), s) for s in COARSE_SCALES))
    seeds = []
    for _, s in ranked:
        if all(abs(s - other) >= SCALE_GAP for other in seeds):
            seeds.append(s)
        if len(seeds) >= SCALE_GROUPS:
            break

    groups = [(base, found)] if found else []
    for seed in seeds:
        seed_t = _Template(sprite, seed)
        if not _fits_coarse(seed_t) or _shape_maps(seed_t)[1] < PRESENCE_GATE - SEED_GATE_MARGIN:
            continue  # nét không có trên demo ở tỉ lệ này → khỏi tinh chỉnh
        local = [(_coarse_best(_Template(sprite, round(seed + d, 2))), round(seed + d, 2))
                 for d in (-0.04, -0.02, 0.0, 0.02, 0.04) if seed + d > 0]
        t = _Template(sprite, min(local)[1])
        hits = _search(t, MAX_INSTANCES)
        if hits:
            groups.append((t, hits))
    return groups


def _is_strong(hit):
    """Khớp chắc ở 1:1 → bỏ qua dò scale/9-slice."""
    return hit[3] >= STRONG_ZNCC or (hit[3] == 0.0 and hit[2] <= LOW_TEXTURE_DIFF)


# ─── 9-slice ────────────────────────────────────────────────────────────────

def _estimate_border(sprite):
    """Ước lượng border 9-slice: vùng giữa gồm các hàng/cột giống hệt hàng/cột chính giữa."""
    h, w = sprite.shape[:2]

    def edges(axis_len, line):
        mid = axis_len // 2
        ref = line(mid).astype(np.int16)
        lo, hi = mid, mid
        while lo - 1 >= 0 and np.abs(line(lo - 1).astype(np.int16) - ref).max() <= 2:
            lo -= 1
        while hi + 1 < axis_len and np.abs(line(hi + 1).astype(np.int16) - ref).max() <= 2:
            hi += 1
        return lo, axis_len - 1 - hi

    left, right = edges(w, lambda i: sprite[:, i])
    top, bottom = edges(h, lambda i: sprite[i, :])
    # Không có vùng giữa đồng nhất đủ rộng theo trục nào → không phải sprite 9-slice.
    if w - left - right < 2 and h - top - bottom < 2:
        return None
    return {"left": int(left), "right": int(right), "top": int(top), "bottom": int(bottom)}


def _find_sliced(sprite, border):
    """Dò 4 góc (kích thước ≈ border) rồi ghép thành khung chữ nhật hợp lệ."""
    h, w = sprite.shape[:2]
    cw = max(8, min(w // 2, max(border["left"], border["right"]) + 4))
    ch = max(8, min(h // 2, max(border["top"], border["bottom"]) + 4))
    flat = _Template(sprite, 1.0).low_texture
    if flat:
        found = _flat_corners(sprite, cw, ch)
        if not found or not all(found.values()):
            return []
    else:
        crops = {"tl": sprite[:ch, :cw], "tr": sprite[:ch, w - cw:],
                 "bl": sprite[h - ch:, :cw], "br": sprite[h - ch:, w - cw:]}
        found = {}
        for key, crop in crops.items():
            t = _Template(crop, 1.0)
            if t.opaque < 16:
                return []
            t.opaque = max(t.opaque, MIN_OPAQUE_PIXELS)  # góc nhỏ vẫn được dò
            found[key] = _search(t, MAX_INSTANCES * 2, gate=False)
            if not found[key]:
                return []

    rects = []
    for tl in found["tl"]:
        trs = [p for p in found["tr"] if abs(p[1] - tl[1]) <= 2 and p[0] >= tl[0] + cw // 2]
        bls = [p for p in found["bl"] if abs(p[0] - tl[0]) <= 2 and p[1] >= tl[1] + ch // 2]
        if not trs or not bls:
            continue
        tr = min(trs, key=lambda p: p[0])
        bl = min(bls, key=lambda p: p[1])
        brs = [p for p in found["br"] if abs(p[0] - tr[0]) <= 2 and abs(p[1] - bl[1]) <= 2]
        if not brs:
            continue
        rw, rh = tr[0] + cw - tl[0], bl[1] + ch - tl[1]
        if abs(rw - w) <= 1 and abs(rh - h) <= 1:
            continue  # đúng kích thước gốc → nhánh scale đều đã xử lý
        # Khung nhỏ hơn tổng border (4 góc chồng lên nhau) = 4 góc khớp lẻ tẻ trên một chi tiết nhỏ, không phải 9-slice.
        if rw < border["left"] + border["right"] or rh < border["top"] + border["bottom"] or min(rw, rh) < 16:
            continue
        # 4 góc khớp chưa đủ (khung thẻ rỗng ruột khớp góc của khung khác) → kiểm tra cả sprite sau khi kéo giãn.
        rendered = _render_match(sprite, {"w": rw, "h": rh, "sliced": True, "scale": 1.0}, border)
        inlier, edge = _pixel_agreement(rendered, tl[0], tl[1])
        if flat:
            # Panel một màu: ruột bị nội dung che thoải mái, vị trí chốt bằng hình dáng — nhưng phải còn đủ pixel cùng
            # màu (khung 232×232 chỉ 10% cùng màu nằm trong ô trang bị tối = khớp nhầm).
            if inlier < FLAT_SLICED_INLIER or not _flat_rect_ok(rendered, tl[0], tl[1]):
                continue
        elif inlier < OCCLUDED_INLIER or edge < OCCLUDED_EDGE_INLIER:
            continue
        corners = (tl, tr, bl, brs[0])
        diff = float(np.mean([c[2] for c in corners]))
        zncc = float(np.mean([c[3] for c in corners]))
        rects.append((tl[0], tl[1], rw, rh, diff, zncc, inlier))
    return rects


def _pixel_agreement(rgba, x, y):
    """(tỉ lệ pixel đục trùng tuyệt đối, tỉ lệ dải viền trùng) khi đặt ảnh RGBA tại (x, y) trên demo."""
    h, w = rgba.shape[:2]
    patch = _demo[y:y + h, x:x + w]
    if patch.shape[:2] != (h, w):
        return 0.0, 0.0
    mask = np.where(rgba[:, :, 3] > ALPHA_OPAQUE, 255, 0).astype(np.uint8)
    if not mask.any():
        return 0.0, 0.0
    exact = np.abs(patch.astype(np.int16) - rgba[:, :, :3].astype(np.int16)).mean(axis=2) <= EXACT_TOLERANCE
    boundary = _boundary(mask)
    return float(exact[mask > 0].mean()), float(exact[boundary].mean()) if boundary.any() else 0.0


# ─── Hàng lặp lại (danh sách) ────────────────────────────────────────────────

REPEAT_MIN = 2              # ≥ 2 bản cùng cột, cùng cỡ → nghi là danh sách, dò tiếp theo nhịp
REPEAT_STEP_TOLERANCE = 6   # các khoảng cách liên tiếp lệch nhau ≤ 6 px mới coi là cùng nhịp
REPEAT_INLIER = 0.30        # ngưỡng nới cho hàng bị nội dung che nhiều (ô vật phẩm) — viền vẫn phải lộ gần đủ
REPEAT_EDGE = 0.80


def _complete_repeats(sprite, matches):
    """Sprite lặp theo cột đều nhau (hàng trong danh sách): dò thêm ở các vị trí theo nhịp với ngưỡng nới —
    hàng bị nội dung che nhiều (demo 2 của bảng xếp hạng) vẫn là cùng một khung."""
    plain = [m for m in matches if not m["sliced"]]
    if len(plain) < REPEAT_MIN:
        return matches
    by_column = {}
    for m in plain:
        by_column.setdefault((round(m["x"] / 4), m["w"], m["h"]), []).append(m)

    H = _demo.shape[0]
    added = []
    for (_, w, h), group in by_column.items():
        if len(group) < REPEAT_MIN:
            continue
        group.sort(key=lambda m: m["y"])
        steps = [b["y"] - a["y"] for a, b in zip(group, group[1:])]
        step = int(np.median(steps))
        if step < h * 0.8 or any(abs(s - step) > REPEAT_STEP_TOLERANCE and abs(s % step) > REPEAT_STEP_TOLERANCE for s in steps):
            continue
        template = sprite if group[0]["scale"] == 1.0 else _render_match(sprite, group[0], None)
        taken = [m["y"] for m in group]
        probes = [group[0]["y"] - k * step for k in range(1, 20)] + [group[-1]["y"] + k * step for k in range(1, 20)]
        probes += [a["y"] + k * step for a, b in zip(group, group[1:]) for k in range(1, (b["y"] - a["y"]) // step)]
        for py in probes:
            if py < -h // 2 or py > H - h // 2 or any(abs(py - ty) < h * 0.5 for ty in taken):
                continue
            best = None
            for dy in range(-REPEAT_STEP_TOLERANCE, REPEAT_STEP_TOLERANCE + 1):
                inlier, edge = _pixel_agreement(template, group[0]["x"], py + dy)
                if inlier >= REPEAT_INLIER and edge >= REPEAT_EDGE and (best is None or edge > best[2]):
                    best = (py + dy, inlier, edge)
            if best:
                taken.append(best[0])
                added.append(_match_entry(group[0]["x"], best[0], w, h, group[0]["scale"], False, 0.0, 0.0, best[1]))
    return matches + added


ADJACENT_MAX = 24           # tối đa số ô kề thêm cho một sprite


def _complete_adjacent(sprite, matches):
    """Ô xếp sát nhau (thanh điều hướng, lưới): sprite có ≥ 2 bản cùng hàng (hoặc cột) → thử các ô ngay cạnh (x ± rộng,
    y ± cao) với ngưỡng nới như hàng lặp, lan dần. Bắt được cả khi nhịp không đều (ô đang chọn rộng hơn chen giữa)."""
    plain = [m for m in matches if not m["sliced"]]
    rows, cols = {}, {}
    for m in plain:
        rows.setdefault((round(m["y"] / 4), m["w"], m["h"]), []).append(m)
        cols.setdefault((round(m["x"] / 4), m["w"], m["h"]), []).append(m)
    H, W = _demo.shape[:2]
    taken = list(matches)
    added = []
    for groups, horizontal in ((rows, True), (cols, False)):
        for group in groups.values():
            if len(group) < REPEAT_MIN:
                continue
            first = group[0]
            template = sprite if first["scale"] == 1.0 else _render_match(sprite, first, None)
            queue = list(group)
            while queue and len(added) < ADJACENT_MAX:
                m = queue.pop()
                for sign in (-1, 1):
                    x = m["x"] + sign * m["w"] if horizontal else m["x"]
                    y = m["y"] if horizontal else m["y"] + sign * m["h"]
                    probe = {"x": x, "y": y, "w": m["w"], "h": m["h"]}
                    if x < 0 or y < 0 or x + m["w"] > W or y + m["h"] > H or any(_iou(probe, t) > 0.3 for t in taken):
                        continue
                    hit = _best_probe(template, x, y, horizontal)
                    if hit:
                        entry = _match_entry(hit[0], hit[1], m["w"], m["h"], first["scale"], False, 0.0, 0.0, hit[2])
                        taken.append(entry)
                        added.append(entry)
                        queue.append(entry)
    return matches + added


def _best_probe(template, x, y, horizontal):
    """(x, y, inlier) khớp tốt nhất trong ±REPEAT_STEP_TOLERANCE px dọc theo trục lặp, hoặc None."""
    best = None
    for d in range(-REPEAT_STEP_TOLERANCE, REPEAT_STEP_TOLERANCE + 1):
        px, py = (x + d, y) if horizontal else (x, y + d)
        inlier, edge = _pixel_agreement(template, px, py)
        if inlier >= REPEAT_INLIER and edge >= REPEAT_EDGE and (best is None or edge > best[3]):
            best = (px, py, inlier, edge)
    return best[:3] if best else None


# ─── Đối chiếu với demo các tab khác của cùng màn ───────────────────────────

HINT_INLIER = 0.3           # vị trí tab khác đã tìm thấy: kiểm tra tại chỗ với ngưỡng nới như hàng lặp
HINT_EDGE = 0.8
HINT_EDGE_FLAT = 0.5        # panel nền một màu bị hàng tab / thanh chỉ số / navbar đè nhiều mép (Upgrade: lộ 57%) — đã có
                            #     bằng chứng ở tab khác đúng chỗ đó nên chỉ cần viền lộ một nửa


def _apply_hints(results, hint_paths):
    """Nhiều demo = nhiều tab của cùng màn: phần chung (thanh điều hướng, nền) có ở mọi tab nhưng mỗi lần dò độc lập
    có thể sót ở tab này, tìm thấy ở tab kia → gộp spec hiểu nhầm là phần riêng. Kiểm tra trực tiếp từng vị trí tab khác
    đã tìm được: pixel khớp thì thêm; phần chỉ tab kia có thì pixel không khớp nên không bị thêm."""
    by_path = {_norm_dir(r["sprite"]): r for r in results}
    added = 0
    for path in hint_paths:
        try:
            with open(path, "r", encoding="utf-8") as f:
                hint = json.load(f)
        except (OSError, ValueError):
            continue
        for hs in hint.get("sprites", []):
            r = by_path.get(_norm_dir(hs.get("sprite", "")))
            if r is None or hs.get("status") != "matched" or r.get("status") not in ("matched", "unmatched"):
                continue
            sprite = None
            for hm in hs["matches"]:
                if hm.get("tint") or any(_iou(hm, m) > 0.5 for m in r.get("matches", [])):
                    continue
                if sprite is None:
                    sprite = _load_rgba(r["sprite"])
                border = hs.get("suggestedBorder") or _estimate_border(sprite)
                inlier, edge = _pixel_agreement(_render_match(sprite, hm, border), hm["x"], hm["y"])
                if inlier < HINT_INLIER or edge < (HINT_EDGE_FLAT if hs.get("lowTexture") else HINT_EDGE):
                    continue
                entry = _match_entry(hm["x"], hm["y"], hm["w"], hm["h"], hm["scale"], hm["sliced"], 0.0, 0.0, inlier)
                if r.get("status") != "matched":
                    r.update(status="matched", lowTexture=bool(hs.get("lowTexture")), matches=[])
                    r.pop("reason", None)
                    if hs.get("flatColor"):
                        r["flatColor"] = hs["flatColor"]
                    if hs.get("suggestedBorder"):
                        r["suggestedBorder"] = hs["suggestedBorder"]
                r["matches"].append(entry)
                added += 1
    return added


# ─── Xử lý 1 sprite ─────────────────────────────────────────────────────────

def _cache_key(demo_hash, sprite_path):
    with open(sprite_path, "rb") as f:
        sprite_hash = hashlib.sha1(f.read()).hexdigest()
    return hashlib.sha1(f"{ALGO_VERSION}|{demo_hash}|{sprite_hash}".encode()).hexdigest()


def _match_entry(x, y, w, h, scale, sliced, diff, zncc, inlier, tint=None):
    entry = {"x": int(x), "y": int(y), "w": int(w), "h": int(h), "scale": scale, "sliced": sliced,
             "diff": round(float(diff), 1), "zncc": round(float(zncc), 3), "inlier": round(float(inlier), 3)}
    if tint:
        entry["tint"] = tint
    return entry


def _locate_sprite(sprite_path):
    sprite = _load_rgba(sprite_path)
    base = {"sprite": sprite_path, "name": os.path.splitext(os.path.basename(sprite_path))[0]}
    if sprite is None:
        return {**base, "status": "error", "reason": "Không đọc được file"}
    h, w = sprite.shape[:2]
    base.update(spriteWidth=int(w), spriteHeight=int(h))
    if w > _demo.shape[1] or h > _demo.shape[0]:
        return {**base, "status": "unmatched", "reason": "too-large"}
    if int(np.count_nonzero(sprite[:, :, 3] > ALPHA_OPAQUE)) < MIN_OPAQUE_PIXELS:
        return {**base, "status": "unmatched", "reason": "soft-alpha"}

    # Màu của sprite phần lớn không có trên demo → chỉ còn khả năng bị tint (chỉ ở 1:1): bỏ dò scale và 9-slice.
    colors_present = _color_presence(sprite) >= COLOR_GATE
    groups = _find_uniform(sprite, try_scales=colors_present)
    matches = [_match_entry(x, y, t.w, t.h, t.scale, False, d, z, i, *extra)
               for t, found in groups for x, y, d, z, i, *extra in found]
    found = [hit for _, hits in groups for hit in hits]
    low_texture = _Template(sprite, 1.0).low_texture

    border = _estimate_border(sprite) if colors_present else None
    # Panel một màu: khớp 1:1 không loại trừ bản kéo giãn (bản 1:1 "khớp" vào đầu bản kéo giãn khi 1 cạnh liền màu).
    if border is not None and (low_texture or not (found and _is_strong(found[0]))):
        matches += [_match_entry(x, y, rw, rh, 1.0, True, d, z, i) for x, y, rw, rh, d, z, i in _find_sliced(sprite, border)]

    matches = _complete_adjacent(sprite, _complete_repeats(sprite, _dedupe_matches(matches)))
    if not matches:
        matches = _find_fullscreen(sprite) or (_find_flat_tinted(sprite) if low_texture else [])
    if not matches:
        return {**base, "status": "unmatched", "reason": "no-match"}
    result = {**base, "status": "matched", "lowTexture": low_texture, "matches": matches}
    if low_texture:
        result["flatColor"] = [int(c) for c in np.median(sprite[:, :, :3][sprite[:, :, 3] > ALPHA_OPAQUE], axis=0)]
    if any(m["sliced"] for m in matches):
        result["suggestedBorder"] = border
    return result


FULLSCREEN_TOLERANCE = 2     # sprite cỡ bằng demo (±2 px) = ảnh nền cả màn, chỉ có một vị trí (0, 0)...
FULLSCREEN_INLIER = 0.20     # ... nhận khi ≥ 20% pixel trùng: nền bị panel nửa dưới che (Party: 29% ≈ 670 000 px trùng
                             #     khít — không thể ngẫu nhiên; nền khác trùng ~0%)


def _find_fullscreen(sprite):
    """Ảnh nền cỡ cả màn bị che quá nửa (panel dưới, nhân vật) không qua được ngưỡng bị che 50% → xét riêng tại (0, 0)."""
    h, w = sprite.shape[:2]
    H, W = _demo.shape[:2]
    if abs(w - W) > FULLSCREEN_TOLERANCE or abs(h - H) > FULLSCREEN_TOLERANCE:
        return []
    inlier, _ = _pixel_agreement(sprite, 0, 0)
    return [_match_entry(0, 0, w, h, 1.0, False, 0.0, 0.0, inlier)] if inlier >= FULLSCREEN_INLIER else []


FLAT_TINT_MIN_PIXELS = 400   # hình bóng một màu bị tô màu (icon trắng + Image.color): đủ lớn để hình dáng có nghĩa...
FLAT_TINT_FILL = 0.85        # ... và không phải khối chữ nhật (lấp < 85% khung) — hộp chữ nhật khớp với mọi mảng cùng màu
FLAT_TINT_UNIFORM = 0.85     # ruột: ≥ 85% pixel cùng một màu (lệch ≤ 12)
FLAT_TINT_COLOR = 12
FLAT_TINT_BOUNDARY = 0.8     # dải viền trong cùng màu ruột ≥ 80%
FLAT_TINT_PEAKS = 8


def _find_flat_tinted(sprite):
    """Hình bóng một màu ở 1:1 được tô màu khác (Image.color): màu không so được nên chốt bằng hình dáng — ứng viên
    theo nét viền, rồi đòi ruột đồng màu, viền trong cùng màu ruột, vành ngoài tương phản ở ≥ 3/4 cạnh."""
    t = _Template(sprite, 1.0)
    if t.opaque < FLAT_TINT_MIN_PIXELS or t.opaque >= FLAT_TINT_FILL * t.w * t.h or not _fits_coarse(t):
        return []
    silhouette = (cv2.Canny(t.small_mask, 50, 150) > 0).astype(np.float32)
    if silhouette.sum() < 16:
        return []
    score = np.nan_to_num(cv2.matchTemplate(_edges_at(t.f), silhouette, cv2.TM_CCOEFF_NORMED), nan=-1.0)
    min_dist = max(2, int(min(t.small_mask.shape) * 0.5))
    inner = cv2.erode(t.mask, np.ones((5, 5), np.uint8)) > 0
    sprite_color = np.median(t.bgr[t.mask > 0], axis=0)
    H, W = _demo.shape[:2]
    pad = int(np.ceil(1.0 / t.f)) + 2
    found = []
    for cx, cy in _top_peaks(-score, FLAT_TINT_PEAKS, min_dist):
        best = None
        for y in range(max(0, int(cy / t.f) - pad), min(H - t.h, int(cy / t.f) + pad) + 1):
            for x in range(max(0, int(cx / t.f) - pad), min(W - t.w, int(cx / t.f) + pad) + 1):
                verdict = _verify_flat_tinted(x, y, t, inner)
                if verdict is not None and (best is None or verdict[0] > best[2]):
                    best = (x, y) + verdict
        if best and all(_iou({"x": best[0], "y": best[1], "w": t.w, "h": t.h}, m) < 0.3 for m in found):
            tint = np.clip(best[3] / np.maximum(sprite_color, 1.0), 0.0, 1.0)
            b, g, r = (tint * 255).round().astype(int)
            entry = _match_entry(best[0], best[1], t.w, t.h, 1.0, False, 0.0, 0.0, best[2], "#{:02X}{:02X}{:02X}".format(r, g, b))
            entry["flatTint"] = True
            found.append(entry)
    return found


def _verify_flat_tinted(x, y, t, inner):
    """(tỉ lệ viền trong cùng màu ruột, màu ruột BGR) nếu (x, y) đúng là hình bóng được tô màu; None nếu không."""
    patch = _demo[y:y + t.h, x:x + t.w].astype(np.int16)
    fill = np.median(patch[inner], axis=0)
    same = np.abs(patch - fill).max(axis=2) <= FLAT_TINT_COLOR
    if float(same[inner].mean()) < FLAT_TINT_UNIFORM:
        return None
    edge = float(same[t.boundary].mean())
    if edge < FLAT_TINT_BOUNDARY:
        return None
    # Vành ngoài khác màu ruột ở ≥ 3/4 cạnh — không phải một mảng cùng màu lớn hơn.
    r = EDGE_RING
    H, W = _demo.shape[:2]
    y0, x0 = max(0, y - r), max(0, x - r)
    big = _demo[y0:min(H, y + t.h + r), x0:min(W, x + t.w + r)].astype(np.int16)
    mask = np.zeros(big.shape[:2], np.uint8)
    mask[y - y0:y - y0 + t.h, x - x0:x - x0 + t.w] = t.mask
    ring = (cv2.dilate(mask, np.ones((2 * r + 1, 2 * r + 1), np.uint8)) > 0) & (mask == 0)
    other = np.abs(big - fill).max(axis=2) > 25
    ys, xs = np.nonzero(ring)
    cy, cx = y - y0 + t.h / 2, x - x0 + t.w / 2
    sides = [(ys < cy - t.h / 4), (ys > cy + t.h / 4), (xs < cx - t.w / 4), (xs > cx + t.w / 4)]
    passing = sum(1 for side in sides if side.sum() < 4 or float(other[ys[side], xs[side]].mean()) >= FLAT_RING)
    return (edge, fill) if passing >= FLAT_SIDES else None


def _dedupe_matches(matches):
    """Một vị trí chỉ giữ 1 kết quả (ưu tiên cấu trúc giống nhất); bỏ bản thường nằm trong bản 9-slice của chính nó."""
    sliced = [m for m in matches if m["sliced"]]
    plain = [m for m in matches if not m["sliced"] and not any(_contains(k, m) for k in sliced)]
    # Ngược lại, bản 9-slice nằm trong bản 1:1 = góc giả (mép trên popup bị title che, góc "thấy" dưới title) → bỏ.
    matches = plain + [s for s in sliced if not any(_contains(p, s) for p in plain)]
    matches.sort(key=lambda m: ("tint" in m, -max(m["zncc"], m["inlier"]), m["diff"]))
    kept = []
    for m in matches:
        cx, cy = m["x"] + m["w"] / 2, m["y"] + m["h"] / 2
        if all(abs(cx - (k["x"] + k["w"] / 2)) > k["w"] * 0.4 or abs(cy - (k["y"] + k["h"] / 2)) > k["h"] * 0.4
               for k in kept):
            kept.append(m)
    kept.sort(key=lambda m: (m["y"], m["x"]))
    return kept


def _contains(outer, inner, tolerance=2):
    return (inner["x"] >= outer["x"] - tolerance and inner["y"] >= outer["y"] - tolerance
            and inner["x"] + inner["w"] <= outer["x"] + outer["w"] + tolerance
            and inner["y"] + inner["h"] <= outer["y"] + outer["h"] + tolerance)


# ─── Lọc chéo giữa các sprite ───────────────────────────────────────────────

def _explained_by(inner, outer_sprite, outer_match):
    """True nếu vùng của `inner` nằm trọn trong `outer` và pixel ở đó đã trùng với sprite `outer`
    → `inner` chỉ là một mảng giống nhau bên trong `outer` (vd dải ruy băng vẽ liền trong title)."""
    ox, oy, ow, oh = outer_match["x"], outer_match["y"], outer_match["w"], outer_match["h"]
    ix, iy, iw, ih = inner["x"], inner["y"], inner["w"], inner["h"]
    if not (ix >= ox and iy >= oy and ix + iw <= ox + ow and iy + ih <= oy + oh):
        return False
    if (iw, ih) == (ow, oh):
        return False
    t = _Template(outer_sprite, outer_match["scale"])
    rx, ry = ix - ox, iy - oy
    m = t.mask[ry:ry + ih, rx:rx + iw] > 0
    if np.count_nonzero(m) < m.size * 0.5:
        return False
    patch = _demo[iy:iy + ih, ix:ix + iw].astype(np.int16)
    diff = np.abs(patch - t.bgr[ry:ry + ih, rx:rx + iw].astype(np.int16)).mean(axis=2)[m]
    return float(diff.mean()) <= 2.0


def _filter_flat_ambiguous(results):
    """Sprite một màu khớp ở nhiều vị trí chồng lên nhau = trượt trên một vùng cùng màu → không biết vị trí nào đúng.
    Chạy sau _filter_flat_nested để khớp nhầm bên trong panel khác không kéo theo khớp thật nằm sát cạnh."""
    for r in results:
        if r.get("status") != "matched" or not r.get("flatColor"):
            continue
        ms = r["matches"]
        overlapping = {id(a) for a in ms for b in ms if a is not b and _iou(a, b) > 0}
        r["matches"] = [m for m in ms if id(m) not in overlapping]
        if not r["matches"]:
            r["status"], r["reason"] = "unmatched", "ambiguous"
            r.pop("matches")


def _filter_flat_nested(results):
    """Panel một màu nằm gọn trong một panel một màu khác cùng màu (lớn hơn) → không phân biệt được bằng hình, gần như
    luôn là khớp nhầm (designer không chồng 2 mảng cùng màu) → bỏ."""
    flats = [(r, m) for r in results if r.get("status") == "matched" and r.get("flatColor") for m in r["matches"]]
    dropped = set()
    for r, m in flats:
        for r2, m2 in flats:
            if m2 is m or m2["w"] * m2["h"] <= m["w"] * m["h"]:
                continue
            same_color = max(abs(a - b) for a, b in zip(r["flatColor"], r2["flatColor"])) <= EXACT_TOLERANCE
            if same_color and _inside_ratio(m, m2) >= 0.9:
                dropped.add(id(m))
                break
    for r in results:
        if r.get("status") == "matched":
            r["matches"] = [m for m in r["matches"] if id(m) not in dropped]
            if not r["matches"]:
                r["status"], r["reason"] = "unmatched", "explained-by-other"
                r.pop("matches")


def _run_filter(results, step, reason, fn, announce=True):
    """Chạy một bộ lọc chéo, báo từng khớp bị bỏ (kèm lý do) → trả về danh sách khớp bị bỏ và kết quả của bộ lọc.
    announce=False: không báo giai đoạn (bộ lọc chạy sau bước đọc chữ)."""
    if announce:
        _emit("filter", message=step)
    before = [(r, m) for r in results if r.get("status") == "matched" for m in r["matches"]]
    value = fn(results)
    kept = {id(m) for r in results if r.get("status") == "matched" for m in r["matches"]}
    dropped = []
    for r, m in before:
        if id(m) in kept:
            continue
        drop = {"name": r["name"], "reason": reason, "x": m["x"], "y": m["y"], "w": m["w"], "h": m["h"]}
        dropped.append(drop)
        _emit("drop", drop=drop)
    return dropped, value


GLYPH_INSIDE = 0.9          # sprite nằm ≥ 90% trong một dòng chữ đã đọc...
GLYPH_HEIGHT = 1.3          # ... không cao hơn 1.3 lần dòng...
GLYPH_AREA = 0.5            # ... và nhỏ hơn nửa dòng → là một chữ cái ("i" của icon info khớp vào "Equipment")
FLAT_IN_TEXT_AREA = 1.2     # hộp một màu nằm trong khung chữ (đã nới) và không lớn hơn chữ → hình do viền chữ tạo thành
GLYPH_PAD = 0.2            # khung chữ OCR bám ruột nét; viền chữ đen thò ra ~20% chiều cao


def _filter_inside_text(results, texts):
    """Sprite hình ký tự khớp vào chữ cái trong dòng chữ → bỏ. Icon cạnh số (xu, kim cương) nằm ngoài khung chữ OCR.
    Hộp một màu nằm trọn trong dòng chữ = hình do viền đen của các chữ tạo thành (panel thật chứa chữ, không nằm trong
    chữ) → bỏ ở bất kỳ vị trí nào trong dòng."""
    for r in results:
        if r.get("status") != "matched":
            continue
        r["matches"] = [m for m in r["matches"] if not any(
            _inside_ratio(m, _pad_box(t, GLYPH_PAD * t["h"])) >= GLYPH_INSIDE and m["h"] <= t["h"] * GLYPH_HEIGHT
            and (r.get("lowTexture") and m["w"] * m["h"] <= t["w"] * t["h"] * FLAT_IN_TEXT_AREA
                 or m["w"] * m["h"] <= t["w"] * t["h"] * GLYPH_AREA and _between_letters(m, t))
            for t in texts if t.get("text"))]
        if not r["matches"]:
            r["status"], r["reason"] = "unmatched", "inside-text"
            r.pop("matches")


def _between_letters(m, t):
    """Sprite nằm giữa dòng (hai bên còn chữ ≥ bề rộng sprite) — chữ "i" của "Equipment". Icon ở đầu/cuối dòng (nốt nhạc
    cạnh "Music") là icon thật cạnh chữ."""
    return m["x"] - t["x"] >= m["w"] and (t["x"] + t["w"]) - (m["x"] + m["w"]) >= m["w"]


def _pad_box(box, pad):
    pad = int(round(pad))
    return {"x": box["x"] - pad, "y": box["y"] - pad, "w": box["w"] + 2 * pad, "h": box["h"] + 2 * pad}


def _inside_ratio(inner, outer):
    ix = max(0, min(inner["x"] + inner["w"], outer["x"] + outer["w"]) - max(inner["x"], outer["x"]))
    iy = max(0, min(inner["y"] + inner["h"], outer["y"] + outer["h"]) - max(inner["y"], outer["y"]))
    return ix * iy / float(inner["w"] * inner["h"])


def _overlap_small(a, b):
    return _inside_ratio(a, b) if a["w"] * a["h"] <= b["w"] * b["h"] else _inside_ratio(b, a)


def _is_dim_tint(hex_tint):
    rgb = [int(hex_tint[i:i + 2], 16) / 255.0 for i in (1, 3, 5)]
    return max(rgb) - min(rgb) <= DIM_GRAY_SPREAD and max(rgb) <= DIM_MAX


def _split_dimmed(results):
    """Khớp có tint xám đều = gameplay/HUD nằm sau lớp dim của popup (icon thanh điều hướng bị tối đều) → không thuộc UI.
    Bỏ khỏi danh sách và trả về độ đậm lớp dim (alpha đen) để dựng imgDim đúng như demo."""
    levels = []
    for r in results:
        if r.get("status") != "matched":
            continue
        keep = []
        for m in r["matches"]:
            if m.get("tint") and not m.get("flatTint") and _is_dim_tint(m["tint"]):
                levels.append(int(m["tint"][1:3], 16) / 255.0)
            else:
                keep.append(m)
        r["matches"] = keep
        if not keep:
            r["status"], r["reason"] = "unmatched", "behind-dim"
            r.pop("matches")
    return round(1.0 - float(np.median(levels)), 2) if levels else None


def _filter_same_spot(results):
    """Hai sprite gần giống nhau (btn_tab_1 / btn_tab_2) khớp cùng một chỗ → giữ sprite khớp tốt hơn; cùng một art ở 2
    thư mục (icon_x) mà bản tint đè lên bản không tint → bỏ bản tint."""
    entries = [(r, m) for r in results if r.get("status") == "matched" for m in r["matches"]]
    dropped = set()
    for i, (ra, ma) in enumerate(entries):
        for rb, mb in entries[i + 1:]:
            if ra is rb:
                continue
            tinted = [m for m in (ma, mb) if m.get("tint")]
            if (len(tinted) == 1 and ra["name"] == rb["name"]
                    and max(_inside_ratio(ma, mb), _inside_ratio(mb, ma)) >= 0.8):
                dropped.add(id(tinted[0]))
                continue
            if _iou(ma, mb) < 0.85:
                continue
            # Hai art giống hệt nhau ở 2 màn (popup Settings / khung thư Mail): ưu tiên bản không tint, 1:1 hơn 9-slice,
            # rồi bản lớn hơn (giải thích được nhiều pixel hơn), cuối cùng mới tới điểm khớp.
            loser = mb if _spot_rank(ma) >= _spot_rank(mb) else ma
            dropped.add(id(loser))
    for r in results:
        if r.get("status") != "matched":
            continue
        r["matches"] = [m for m in r["matches"] if id(m) not in dropped]
        if not r["matches"]:
            r["status"], r["reason"] = "unmatched", "explained-by-other"
            r.pop("matches")


def _spot_rank(m):
    return "tint" not in m, not m["sliced"], m["w"] * m["h"], max(m["zncc"], m["inlier"])


SLICED_STRONG = 0.85        # 9-slice trùng ≥ 85% pixel sau khi kéo giãn = bằng chứng chắc như khớp 1:1


def _is_weak(r, m):
    """Khớp chỉ dựa vào hình dáng/màu (một màu, tint, 9-slice lỏng, scale sprite nhỏ) — trùng được với art của màn khác."""
    if r.get("lowTexture") or m.get("tint"):
        return True
    if m["sliced"]:
        return m["inlier"] < SLICED_STRONG
    if m["scale"] != 1.0:
        return m["zncc"] < STRONG_ZNCC or min(m["w"], m["h"]) < 24
    return False


def _norm_dir(path):
    return os.path.normcase(os.path.abspath(path))


def _under(path, folder):
    return path == folder or path.startswith(folder + os.sep)


def _filter_foreign_weak(results, art_roots, demo_path):
    """Thư mục art bao trùm nhiều màn (chọn cả UI_v2): panel một màu / tint / scale của màn khác khớp vào panel của màn
    này. Tin: thư mục chứa demo (thư mục của màn), thư mục art không chứa demo (người dùng thêm có chủ đích — _Shared),
    và thư mục có ít nhất một sprite khớp chắc. Khớp yếu ngoài các thư mục đó → bỏ (người dùng thấy trong lớp "Bị loại").
    Cách dùng thường (thư mục màn + _Shared) không bị lọc gì."""
    demo_dir = _norm_dir(os.path.dirname(demo_path))
    roots = [_norm_dir(a) for a in art_roots if os.path.isdir(a)]
    if not any(_under(demo_dir, root) and root != demo_dir for root in roots):
        return  # không có thư mục art nào bao trùm nhiều màn
    matched = [r for r in results if r.get("status") == "matched"]
    trusted_dirs = {demo_dir} | {root for root in roots if not _under(demo_dir, root)}
    trusted_dirs |= {_norm_dir(os.path.dirname(r["sprite"])) for r in matched if any(not _is_weak(r, m) for m in r["matches"])}

    def is_trusted(r):
        d = _norm_dir(os.path.dirname(r["sprite"]))
        return any(_under(d, t) for t in trusted_dirs)

    for r in matched:
        if is_trusted(r):
            continue
        r["matches"] = [m for m in r["matches"] if not _is_weak(r, m)]
        if not r["matches"]:
            r["status"], r["reason"] = "unmatched", "foreign-weak"
            r.pop("matches")


def _iou(a, b):
    ix = max(0, min(a["x"] + a["w"], b["x"] + b["w"]) - max(a["x"], b["x"]))
    iy = max(0, min(a["y"] + a["h"], b["y"] + b["h"]) - max(a["y"], b["y"]))
    inter = ix * iy
    return inter / float(a["w"] * a["h"] + b["w"] * b["h"] - inter) if inter else 0.0


def _filter_explained(results):
    """Bỏ match bị một sprite khác (khớp chắc, không 9-slice) giải thích hoàn toàn."""
    anchors = []
    for r in results:
        if r.get("status") != "matched" or r.get("lowTexture"):
            continue
        for m in r["matches"]:
            if not m["sliced"] and (m["zncc"] >= ACCEPT_ZNCC and m["diff"] <= 1.0 or m["inlier"] >= OCCLUDED_INLIER):
                anchors.append((r, m))
    if not anchors:
        return
    sprites = {}
    for r in results:
        if r.get("status") != "matched":
            continue
        kept = []
        for m in r["matches"]:
            dropped = False
            for ar, am in anchors:
                if ar is r:
                    continue
                if ar["sprite"] not in sprites:
                    sprites[ar["sprite"]] = _load_rgba(ar["sprite"])
                if _explained_by(m, sprites[ar["sprite"]], am):
                    dropped = True
                    break
            if not dropped:
                kept.append(m)
        if not kept:
            r["status"], r["reason"] = "unmatched", "explained-by-other"
            r.pop("matches", None)
        else:
            r["matches"] = kept


# ─── Vùng UI chính (phần không nằm dưới lớp phủ tối) ────────────────────────

UI_BLOCK = 16               # dò vùng theo ô 16×16 px (độ sáng lớn nhất trong ô)
OVERLAY_RING_MAX = 170      # viền màn hình (2 ô) sáng nhất ≤ 170 → cả viền nằm dưới lớp phủ tối (popup); màn không có
                            #     lớp phủ luôn có thanh trên/dưới sáng ≥ 238 (đo trên demo chọn chế độ, battle)
OVERLAY_MARGIN = 20         # ô sáng hơn viền ≥ 20 mới là UI nổi trên lớp phủ
UI_REGION_MIN = 0.5         # chữ ngoài sprite phải nằm ≥ 50% trong vùng UI
DIM_ESTIMATE_RANGE = (0.3, 0.85)


def _ui_region(results):
    """(mặt nạ vùng UI theo pixel, độ đậm lớp phủ ước lượng, khung các vùng) — hoặc (None, None, []) nếu màn không có
    lớp phủ tối. Vùng UI = ô sáng hơn hẳn phần bị phủ + chỗ sprite đã khớp (sprite dưới lớp phủ bị tối đi nên không
    khớp thường được), lấp kín bên trong (popup có viền sáng bao ruột tối). Phần còn lại là UI khác bị phủ tối (title,
    thanh điều hướng của màn phía sau) → chữ ở đó bị bỏ, và độ sáng lớn nhất ở đó cho biết lớp phủ đậm cỡ nào."""
    H, W = _demo.shape[:2]
    bh, bw = H // UI_BLOCK, W // UI_BLOCK
    brightness = _demo.max(axis=2)
    blocks = brightness[:bh * UI_BLOCK, :bw * UI_BLOCK].reshape(bh, UI_BLOCK, bw, UI_BLOCK).max(axis=(1, 3))
    ring = np.concatenate([blocks[:2].ravel(), blocks[-2:].ravel(), blocks[:, :2].ravel(), blocks[:, -2:].ravel()])
    ceiling = float(np.percentile(ring, 95))
    if bh < 8 or bw < 8 or ceiling > OVERLAY_RING_MAX:
        return None, None, []

    ui = blocks > ceiling + OVERLAY_MARGIN
    ui = cv2.morphologyEx(ui.astype(np.uint8), cv2.MORPH_CLOSE, np.ones((3, 3), np.uint8))
    region = cv2.resize(ui, (bw * UI_BLOCK, bh * UI_BLOCK), interpolation=cv2.INTER_NEAREST)
    region = cv2.copyMakeBorder(region, 0, H - bh * UI_BLOCK, 0, W - bw * UI_BLOCK, cv2.BORDER_REPLICATE)
    # Sprite đã khớp gộp đúng từng pixel — nới theo ô sẽ lấn sang chữ mờ của màn phía sau sát mép nút.
    for r in results:
        for m in r.get("matches", []) if r.get("status") == "matched" else []:
            region[max(0, m["y"]):m["y"] + m["h"], max(0, m["x"]):m["x"] + m["w"]] = 1
    region = _fill_holes(region)
    if not region.any():
        return None, None, []
    count, _, stats, _ = cv2.connectedComponentsWithStats(region.astype(np.uint8))
    boxes = [{"x": int(x), "y": int(y), "w": int(w), "h": int(h)} for x, y, w, h, _ in stats[1:count]]
    outside = brightness[~region]
    dim = None
    if outside.size:
        lo, hi = DIM_ESTIMATE_RANGE
        dim = round(float(np.clip(1.0 - np.percentile(outside, 99.5) / 255.0, lo, hi)), 2)
    return region, dim, boxes


def _fill_holes(mask):
    """Lấp phần bị bao kín (ruột popup tối nằm trong viền sáng)."""
    flood = np.pad(mask.astype(np.uint8), 1)
    cv2.floodFill(flood, None, (0, 0), 2)
    return flood[1:-1, 1:-1] != 2


# ─── Tìm vùng chữ ───────────────────────────────────────────────────────────

TEXT_RESIDUAL = 60          # chênh lệch màu (0-255) giữa demo và ảnh ghép lại từ sprite → pixel "lạ"
TEXT_COLOR_RESIDUAL = 30    # trong khung chữ đã biết: ngưỡng thấp hơn để lấy được ruột chữ trắng trên nền sáng
TEXT_MIN_HEIGHT = 10
TEXT_MAX_HEIGHT = 200
TEXT_MIN_WIDTH = 16
TEXT_MIN_DENSITY = 0.40     # chữ là khối dày đặc (đo được ≥ 0.53); viền lệch do designer vẽ khác art chỉ 0.1–0.39
TEXT_WORD_GAP = 1.4         # nối 2 khung cùng dòng nếu khoảng trống ≤ 1.4 × chiều cao chữ ("Remove" + "Ads")


def _render_match(sprite, match, border):
    """Sprite như nó xuất hiện trên demo: scale đều, hoặc kéo giãn 9-slice theo border ước lượng."""
    w, h = match["w"], match["h"]
    if not match["sliced"]:
        if match["scale"] == 1.0:
            return sprite
        interp = cv2.INTER_AREA if match["scale"] < 1.0 else cv2.INTER_NEAREST
        return cv2.resize(sprite, (w, h), interpolation=interp)

    sh, sw = sprite.shape[:2]
    l, r = min(border["left"], w // 2), min(border["right"], w - w // 2)
    t, b = min(border["top"], h // 2), min(border["bottom"], h - h // 2)
    src_x, dst_x = [0, l, sw - r, sw], [0, l, w - r, w]
    src_y, dst_y = [0, t, sh - b, sh], [0, t, h - b, h]
    out = np.zeros((h, w, 4), np.uint8)
    for i in range(3):
        for j in range(3):
            piece = sprite[src_y[i]:src_y[i + 1], src_x[j]:src_x[j + 1]]
            size = (dst_x[j + 1] - dst_x[j], dst_y[i + 1] - dst_y[i])
            if piece.size and size[0] > 0 and size[1] > 0:
                out[dst_y[i]:dst_y[i + 1], dst_x[j]:dst_x[j + 1]] = cv2.resize(piece, size, interpolation=cv2.INTER_NEAREST)
    return out


def _compose(results):
    """Ghép lại demo từ các sprite đã khớp (lớn vẽ trước) → (ảnh dự đoán, vùng được sprite phủ)."""
    H, W = _demo.shape[:2]
    predicted = _demo.astype(np.float32)
    covered = np.zeros((H, W), bool)
    items = []
    for r in results:
        if r.get("status") != "matched":
            continue
        sprite = _load_rgba(r["sprite"])
        border = r.get("suggestedBorder") or _estimate_border(sprite)
        items += [(bool(r.get("lowTexture")), m["w"] * m["h"], sprite, m, border) for m in r["matches"]]

    # Cùng thứ tự vẽ với bản dựng: nằm trong sprite khác thì vẽ sau nó (panel phẳng trên popup); cùng cấp thì sprite
    # phẳng (nền thanh tab) trước, rồi lớn trước nhỏ.
    matches = [it[3] for it in items]
    depth = [sum(1 for other in matches if other is not m and other["w"] * other["h"] > m["w"] * m["h"] and _contains(other, m))
             for m in matches]
    order = sorted(range(len(items)), key=lambda i: (depth[i], not items[i][0], -items[i][1]))
    for _, _, sprite, m, border in (items[i] for i in order):
        img = _render_match(sprite, m, border)
        x0, y0 = max(0, m["x"]), max(0, m["y"])
        x1, y1 = min(W, m["x"] + img.shape[1]), min(H, m["y"] + img.shape[0])
        if x1 <= x0 or y1 <= y0:
            continue
        part = img[y0 - m["y"]:y1 - m["y"], x0 - m["x"]:x1 - m["x"]].astype(np.float32)
        if m.get("tint"):
            hex_tint = m["tint"]
            part[:, :, :3] *= np.array([int(hex_tint[5:7], 16), int(hex_tint[3:5], 16), int(hex_tint[1:3], 16)]) / 255.0
        alpha = part[:, :, 3:4] / 255.0
        predicted[y0:y1, x0:x1] = part[:, :, :3] * alpha + predicted[y0:y1, x0:x1] * (1 - alpha)
        covered[y0:y1, x0:x1] |= part[:, :, 3] > ALPHA_OPAQUE
    return predicted, covered


def _residual(results):
    """(vùng được sprite phủ, mức lệch từng pixel so với ảnh ghép lại từ sprite — 0 ngoài vùng phủ)."""
    predicted, covered = _compose(results)
    diff = np.abs(_demo.astype(np.float32) - predicted).max(axis=2) * covered
    return covered, diff


def _detect_texts(residual):
    """Dự phòng khi không có OCR: vùng lạ dày đặc trên sprite đã khớp → chữ vẽ đè. Gom chữ cái thành dòng, trả về
    khung + màu chữ chủ đạo. Kém khi UI có đồ hoạ không phải art (panel, avatar) — chúng nối các dòng thành một khối."""
    if not residual.any():
        return []

    raw = residual.astype(np.uint8)
    lines = cv2.dilate(raw, np.ones((5, 21), np.uint8))  # nối chữ cái/từ trên cùng dòng, không nối các dòng
    count, labels = cv2.connectedComponents(lines)
    H, W = raw.shape
    texts = []
    for label in range(1, count):
        ys, xs = np.nonzero((labels == label) & (raw > 0))
        if len(xs) < 40:
            continue
        x, y = int(xs.min()), int(ys.min())
        w, h = int(xs.max()) - x + 1, int(ys.max()) - y + 1
        if not (TEXT_MIN_HEIGHT <= h <= TEXT_MAX_HEIGHT) or w < TEXT_MIN_WIDTH or w * h > W * H * 0.2:
            continue
        if len(xs) / float(w * h) < TEXT_MIN_DENSITY:
            continue
        texts.append({"x": x, "y": y, "w": w, "h": h, "pixels": (ys, xs)})
    texts = _merge_words(texts)
    for t in texts:
        ys, xs = t.pop("pixels")
        t["color"] = _dominant_color(_demo[ys, xs])
    texts.sort(key=lambda t: (t["y"], t["x"]))
    return texts


def _merge_words(boxes):
    """Nối các khung cùng dòng (chồng dọc ≥ 60%) và sát nhau thành một nhãn."""
    boxes.sort(key=lambda b: b["x"])
    merged = True
    while merged:
        merged = False
        for i, a in enumerate(boxes):
            for b in boxes[i + 1:]:
                overlap = min(a["y"] + a["h"], b["y"] + b["h"]) - max(a["y"], b["y"])
                gap = max(a["x"], b["x"]) - min(a["x"] + a["w"], b["x"] + b["w"])
                if overlap < 0.6 * min(a["h"], b["h"]) or gap > TEXT_WORD_GAP * max(a["h"], b["h"]):
                    continue
                x, y = min(a["x"], b["x"]), min(a["y"], b["y"])
                a.update(x=x, y=y, w=max(a["x"] + a["w"], b["x"] + b["w"]) - x, h=max(a["y"] + a["h"], b["y"] + b["h"]) - y,
                         pixels=(np.concatenate([a["pixels"][0], b["pixels"][0]]),
                                 np.concatenate([a["pixels"][1], b["pixels"][1]])))
                boxes.remove(b)
                merged = True
                break
            if merged:
                break
    return boxes


CORE_LUMINANCE = 0.75         # ruột chữ: sáng ≥ 75% pixel sáng nhất của nét


def _dominant_color(pixels):
    """Màu chữ (ruột nét): màu phổ biến nhất sau khi lượng tử hoá; bỏ viền tối nếu phần sáng chiếm ≥ 15% — chữ trắng viền
    đen dày của font game có viền nhiều pixel hơn ruột."""
    luminance = pixels.astype(np.int32).sum(axis=1)
    bright = pixels[luminance >= 150]
    if len(bright) >= len(pixels) * 0.15:
        pixels = bright
        # Viền dày còn đủ sáng (chữ vàng viền xanh "Lv. 100") vẫn nhiều pixel hơn ruột → chỉ giữ phần sáng gần nhất.
        lum = pixels.astype(np.int32).sum(axis=1)
        core = pixels[lum >= CORE_LUMINANCE * np.percentile(lum, 98)]
        if len(core) >= len(pixels) * 0.15:
            pixels = core
    keys = (pixels // 32).astype(np.int32)
    codes = keys[:, 0] * 64 + keys[:, 1] * 8 + keys[:, 2]
    mode = np.bincount(codes).argmax()
    b, g, r = pixels[codes == mode].mean(axis=0)
    return "#{:02X}{:02X}{:02X}".format(int(r), int(g), int(b))


# ─── Đọc chữ (OCR) ─────────────────────────────────────────────────────────

OCR_COLOR_TOLERANCE = 60    # pixel gần màu chữ → đen, còn lại trắng: bỏ viền/nền để OCR đọc font pixel chuẩn hơn


OCR_DET_LIMIT = 1600         # cạnh dài tối đa khi phát hiện chữ — đủ giữ chữ nhỏ trên ảnh 1080x2160
OCR_MIN_COVER = 0.5          # dòng chữ phải nằm ≥ 50% trên sprite đã khớp (bỏ chữ của nền gameplay phía sau)
OCR_MIN_RESIDUAL = 0.08      # chữ trùng pixel sprite (logo "ADS" vẽ sẵn trong art) → không phải text cần dựng; đo với
                             # ngưỡng lệch 30 — chữ trắng không viền trên nền sáng chỉ lệch ~32 (chữ vẽ sẵn lệch ~0)
OCR_TILE = 760               # quét chữ theo dải cao 760 px...
OCR_TILE_OVERLAP = 160       # ... chồng nhau 160 px để dòng chữ nằm trọn trong ít nhất một dải
FLOAT_TEXT_BRIGHT = 200     # chữ không nằm trên sprite nào: nhận khi nét sáng ≥ 200 (chữ sau lớp dim 60% ≤ ~100)...
FLOAT_TEXT_CONTRAST = 150    # ... hoặc tương phản với nền ≥ 150 (chữ tối trên panel sáng chưa có art)
OCR_WORD_GAP = 0.9           # nối 2 khung OCR cùng dòng khi khoảng trống ≤ 0.9 × chiều cao chữ thấp hơn
OUTLINE_MAX = 8             # dải cùng màu quanh chữ dày hơn 8 px = nền (ô điểm tối), không phải viền chữ
OUTLINE_UNIFORM = 0.6       # ≥ 60% dải quanh ruột chữ cùng một màu, khác hẳn màu chữ → có viền
OCR_TRUSTED = 0.9            # kết quả phát hiện+nhận dạng ≥ 0.9 thì giữ — đọc lại theo màu làm mất số khác màu trong dòng


def _ocr_engine():
    try:
        from rapidocr import RapidOCR
    except ImportError:
        return None
    try:
        return RapidOCR(params={"Global.log_level": "critical", "Det.limit_side_len": OCR_DET_LIMIT,
                                "Det.limit_type": "max"})
    except Exception:  # bản rapidocr khác không nhận params
        return RapidOCR()


def _find_texts(results, use_ocr, ui_region):
    """(danh sách dòng chữ, trạng thái OCR). Có OCR: model phát hiện chữ trên vùng UI (bỏ qua avatar/panel/ô vật phẩm),
    đọc lại từng dòng với ảnh tách theo màu chữ. Không có OCR: tìm theo vùng lạ, nội dung để trống."""
    _emit("text-scan", message="Ghép lại demo từ sprite đã khớp để tìm vùng lạ")
    covered, diff = _residual(results)
    engine = _ocr_engine() if use_ocr else None
    if engine is None:
        texts = _detect_texts(diff > TEXT_RESIDUAL)
        for t in texts:
            _emit("text", text=t)
        return texts, ("skipped" if not use_ocr else "unavailable")
    _emit("text-scan", message="Model OCR phát hiện chữ trên vùng UI")
    icons = [m for r in results if r.get("status") == "matched" for m in r["matches"]]
    texts = _detect_texts_ocr(engine, covered, diff, ui_region, icons)
    for i, t in enumerate(texts):
        _emit("text-read", message=f"Đọc dòng {i + 1}/{len(texts)}", done=i, total=len(texts))
        _refine_text(engine, t)
        _emit("text", text=t, done=i + 1, total=len(texts))
    return texts, ("ok" if texts else "none")


def _detect_texts_ocr(engine, covered, diff, ui_region, icons):
    # Cả ảnh (không chỉ vùng sprite): chữ UI có thể nằm thẳng trên lớp dim (popup "nổi") hoặc trên panel chưa có art.
    # Lượt chính trên cả màn tách các nhãn sát nhau đúng hơn ("Lv.100" ở 2 thẻ cạnh nhau); lượt phụ theo dải chồng lấn
    # chỉ bổ sung chữ lượt chính bỏ sót — chữ ngắn trên nút ("BUY"), huy hiệu một chữ cái.
    words = []
    for n, raw in enumerate(_ocr_passes(engine)):
        found = [line for r in raw for line in _raw_to_lines(r, icons)]
        # Lượt phụ: chữ đã có (hoặc bị dải cắt ngang) → bỏ; giữ khung lớn hơn khi đọc 2 lần ở vùng chồng.
        for f in sorted(found, key=lambda f: -f["w"] * f["h"]):
            if f["w"] < 4 or f["h"] < 4 or (n > 0 and any(_overlap_small(f, k) >= 0.3 for k in words)):
                continue
            if _is_ui_text(f["x"], f["y"], f["w"], f["h"], covered, diff, ui_region):
                words.append(f)

    texts = []
    for line in _merge_ocr_words(words):
        bx, by, bw, bh = line["x"], line["y"], line["w"], line["h"]
        pixels = _ink_pixels(bx, by, bw, bh, diff[by:by + bh, bx:bx + bw] > TEXT_COLOR_RESIDUAL)
        color = _dominant_color(pixels)
        x, y, w, h = _ink_box(bx, by, bw, bh, color)
        entry = {"x": x, "y": y, "w": w, "h": h, "color": color,
                 "text": line["text"], "confidence": round(line["confidence"], 3)}
        entry.update(_text_outline(x, y, w, h, color))
        texts.append(entry)
    texts.sort(key=lambda t: (t["y"], t["x"]))
    return texts


_ocr_cache_file = None  # đặt trong main khi có --cache


def _ocr_passes(engine):
    """Kết quả OCR thô của lượt cả màn + các dải. Chỉ phụ thuộc ảnh demo → cache theo hash demo (chạy lại chỉ còn lọc)."""
    if _ocr_cache_file and os.path.exists(_ocr_cache_file):
        with open(_ocr_cache_file, "r", encoding="utf-8") as f:
            return json.load(f)
    H = _demo.shape[0]
    passes = [(0, H)] + [(y0, min(H, y0 + OCR_TILE)) for y0 in range(0, max(1, H - OCR_TILE_OVERLAP), OCR_TILE - OCR_TILE_OVERLAP)]
    results = [_ocr_lines(engine, _demo[y0:y1], y0) for y0, y1 in passes]
    if _ocr_cache_file:
        with open(_ocr_cache_file, "w", encoding="utf-8") as f:
            json.dump(results, f, ensure_ascii=False)
    return results


def _ocr_lines(engine, image, y_offset):
    """Phát hiện + đọc chữ trên một ảnh (cả màn hoặc một dải) → dòng thô {box, text, score, words} theo toạ độ demo.
    Thô (chưa tách, chưa bỏ từ trên icon) để cache được — phần đó phụ thuộc sprite đã khớp."""
    try:
        result = engine(image, use_det=True, use_cls=False, use_rec=True, return_word_box=True, return_single_char_box=True)
        parts = result.word_results
    except TypeError:  # bản rapidocr cũ không có khung từng ký tự
        result, parts = engine(image, use_det=True, use_cls=False, use_rec=True), None
    if result.boxes is None:
        return []
    raw = []
    for i, (box, text, score) in enumerate(zip(result.boxes, result.txts, result.scores)):
        line_words = parts[i] if parts is not None and i < len(parts) else None
        shift = np.array([0, y_offset])
        raw.append({"box": (np.array(box) + shift).tolist(), "text": text, "score": float(score),
                    "chars": [(np.array(c[2]) + shift).tolist() for c in line_words] if line_words else None})
    return raw


def _raw_to_lines(raw, icons):
    return [{"x": bx, "y": by, "w": bw, "h": bh, "text": seg_text, "confidence": raw["score"]}
            for seg_text, (bx, by, bw, bh) in _split_ocr_line(raw["box"], raw["text"], raw["chars"], icons)]


def _ink_pixels(bx, by, bw, bh, residual):
    """Pixel nét chữ: khác ảnh ghép từ sprite (residual) VÀ khác hẳn màu nền đo ở dải viền khung chữ. Chỉ dùng residual
    thì phần art chưa có dưới chữ (nút đỏ, hộp tối, nhân vật) lấn át màu chữ."""
    box = _demo[by:by + bh, bx:bx + bw].astype(np.int16)
    border = np.concatenate([box[0], box[-1], box[:, 0], box[:, -1]])
    ink = np.abs(box - np.median(border, axis=0)).max(axis=2) > 60
    for mask in (ink & residual, ink, residual):
        if mask.sum() >= 10:
            return box[mask].astype(np.uint8)
    return box.reshape(-1, 3).astype(np.uint8)


def _split_ocr_line(box, text, chars, icons=()):
    """Một khung model trả về có thể trùm 2 nhãn cạnh nhau ("Lv.100   Lv.100" ở 2 thẻ) → tách ở khoảng trống lớn giữa
    các từ; ký tự nằm đè lên icon đã khớp (icon hạng "S" "A" đọc thành chữ, cả khi đọc liền "SAJulius") bị bỏ.
    chars: khung từng ký tự không phải dấu cách, theo thứ tự trong text. Trả về [(nội dung, (x, y, w, h))]."""
    rect = cv2.boundingRect(np.array(box).astype(np.int32))
    glyphs = [c for c in text if not c.isspace()]
    if not chars or len(chars) != len(glyphs):
        return [(text.strip(), rect)]
    rects = [cv2.boundingRect(np.array(c).astype(np.int32)) for c in chars]
    on_icon = _icon_chars(rects, icons)
    words, current, removed, index = [], [], False, 0
    for c in text:
        if c.isspace():
            if current:
                words.append(current)
                current = []
            continue
        r, index = rects[index], index + 1
        if index - 1 in on_icon:
            removed = True
            if current:
                words.append(current)
                current = []
        else:
            current.append((c, r))
    if current:
        words.append(current)
    if not words:
        return []
    items = [("".join(c for c, _ in w), _union_rect([r for _, r in w])) for w in words]
    segments = [[items[0]]]
    for word, r in items[1:]:
        prev = segments[-1][-1][1]
        if r[0] - (prev[0] + prev[2]) > OCR_WORD_GAP * min(prev[3], r[3]):
            segments.append([])
        segments[-1].append((word, r))
    if len(segments) == 1 and not removed:
        return [(text.strip(), _union_rect([r for _, r in items]))]
    return [(" ".join(w for w, _ in seg).strip(), _union_rect([r for _, r in seg])) for seg in segments]


ICON_CHAR_COLOR = 80          # ký tự trên icon khác màu chữ của dòng ≥ 80 → là icon (S/A cam/tím giữa chữ trắng)


def _icon_chars(rects, icons):
    """Chỉ số các ký tự nằm trên icon đã khớp VÀ khác màu phần lớn ký tự trong dòng. Chữ "i" của "Evolution" trùng sprite
    icon_info (hình chữ i) nhưng cùng màu chữ → giữ; icon hạng "S" "A" đọc thành chữ nhưng khác màu → bỏ."""
    candidates = [i for i, r in enumerate(rects) if _on_icon(r, icons)]
    if not candidates:
        return set()
    colors = [_char_color(r) for r in rects]
    others = [colors[i] for i in range(len(rects)) if i not in candidates]
    if not others:
        return set(candidates)  # cả dòng nằm trên icon
    text_color = np.median(np.array(others), axis=0)
    return {i for i in candidates if np.abs(colors[i] - text_color).max() >= ICON_CHAR_COLOR}


def _char_color(rect):
    x, y, w, h = rect
    H, W = _demo.shape[:2]
    patch = _demo[max(0, y):min(H, y + h), max(0, x):min(W, x + w)].reshape(-1, 3)
    return np.array(_hex_to_rgb(_dominant_color(patch))) if len(patch) else np.zeros(3)


def _hex_to_rgb(hex_color):
    return [int(hex_color[i:i + 2], 16) for i in (1, 3, 5)]


def _union_rect(rects):
    x0, y0 = min(r[0] for r in rects), min(r[1] for r in rects)
    x1, y1 = max(r[0] + r[2] for r in rects), max(r[1] + r[3] for r in rects)
    return x0, y0, x1 - x0, y1 - y0


def _on_icon(rect, icons):
    """Từ OCR nằm ≥ 50% trên một icon đã khớp cỡ tương đương (icon không lớn hơn 4 lần từ — nền nút thì lớn hơn nhiều)."""
    x, y, w, h = rect
    word = {"x": x, "y": y, "w": w, "h": h}
    return any(_inside_ratio(word, icon) >= 0.5 and icon["w"] * icon["h"] <= 4 * w * h for icon in icons)


def _is_ui_text(bx, by, bw, bh, covered, diff, ui_region):
    """Chữ nằm trên sprite đã khớp: phải khác pixel sprite (bỏ chữ vẽ sẵn trong art). Chữ không nằm trên sprite nào
    (popup nổi trên lớp dim, panel chưa có art): nhận nếu nằm trong vùng UI chính; không dò được vùng UI thì nhận khi
    không bị làm tối — chữ gameplay sau lớp dim tối và nhạt."""
    if covered[by:by + bh, bx:bx + bw].mean() >= OCR_MIN_COVER:
        return (diff[by:by + bh, bx:bx + bw] > TEXT_COLOR_RESIDUAL).mean() >= OCR_MIN_RESIDUAL
    if ui_region is not None:
        return ui_region[by:by + bh, bx:bx + bw].mean() >= UI_REGION_MIN
    region = _demo[by:by + bh, bx:bx + bw]
    bright = np.percentile(region.max(axis=2), 95)
    gray = cv2.cvtColor(region, cv2.COLOR_BGR2GRAY)
    contrast = np.percentile(gray, 95) - np.percentile(gray, 5)
    return bright >= FLOAT_TEXT_BRIGHT or contrast >= FLOAT_TEXT_CONTRAST


def _merge_ocr_words(words):
    """Model phát hiện đôi khi tách từng từ ("Remove" | "Ads") → nối các khung cùng dòng, sát nhau thành một nhãn."""
    words.sort(key=lambda w: (w["y"], w["x"]))
    lines = []
    for word in sorted(words, key=lambda w: w["x"]):
        for line in lines:
            overlap = min(line["y"] + line["h"], word["y"] + word["h"]) - max(line["y"], word["y"])
            gap = word["x"] - (line["x"] + line["w"])
            # Khung phát hiện có đệm nên 2 từ liền nhau có thể chồng lên nhau (gap âm).
            # Khoảng trống giữa 2 từ < 1 chiều cao chữ; xa hơn là nhãn + giá trị cùng hàng ("Player 5" … "+200%").
            if overlap >= 0.6 * min(line["h"], word["h"]) and -0.6 * max(line["h"], word["h"]) <= gap <= OCR_WORD_GAP * min(line["h"], word["h"]):
                x, y = line["x"], min(line["y"], word["y"])
                line.update(y=y, w=word["x"] + word["w"] - x, h=max(line["y"] + line["h"], word["y"] + word["h"]) - y,
                            text=f"{line['text']} {word['text']}", confidence=min(line["confidence"], word["confidence"]))
                break
        else:
            lines.append(dict(word))
    return lines


def _text_outline(x, y, w, h, hex_color):
    """{"outlineColor", "outlineWidth"} nếu chữ có viền (font game hay dùng chữ trắng viền đen), rỗng nếu không.
    Viền = các dải pixel ngay quanh ruột chữ cùng một màu khác hẳn màu chữ; dày quá OUTLINE_MAX px là nền chứ không phải viền."""
    pad = OUTLINE_MAX + 2
    H, W = _demo.shape[:2]
    x0, y0, x1, y1 = max(0, x - pad), max(0, y - pad), min(W, x + w + pad), min(H, y + h + pad)
    region = _demo[y0:y1, x0:x1].astype(np.int16)
    fill_bgr = np.array([int(hex_color[5:7], 16), int(hex_color[3:5], 16), int(hex_color[1:3], 16)])
    fill = (np.abs(region - fill_bgr).max(axis=2) < OCR_COLOR_TOLERANCE).astype(np.uint8)
    if fill.sum() < 20:
        return {}

    ring = (cv2.dilate(fill, np.ones((3, 3), np.uint8)) > 0) & (fill == 0)
    if ring.sum() < 20:
        return {}
    outline = np.median(region[ring], axis=0)
    if np.abs(outline - fill_bgr).max() < 80:
        return {}

    width, inner = 0, fill
    for k in range(1, OUTLINE_MAX + 2):
        outer = (cv2.dilate(fill, np.ones((2 * k + 1, 2 * k + 1), np.uint8)) > 0).astype(np.uint8)
        band = (outer > 0) & (inner == 0)
        if band.sum() == 0 or float((np.abs(region[band] - outline).max(axis=1) < 40).mean()) < OUTLINE_UNIFORM:
            break
        width, inner = k, outer
    if width == 0 or width > OUTLINE_MAX:
        return {}
    b, g, r = outline.astype(int)
    return {"outlineColor": "#{:02X}{:02X}{:02X}".format(r, g, b), "outlineWidth": width}


def _ink_box(bx, by, bw, bh, hex_color):
    """Khung phát hiện chữ có đệm → co sát theo nét chữ (pixel gần màu chữ) để cỡ chữ tính từ chiều cao nét."""
    bgr = np.array([int(hex_color[5:7], 16), int(hex_color[3:5], 16), int(hex_color[1:3], 16)])
    ink = np.abs(_demo[by:by + bh, bx:bx + bw].astype(np.int16) - bgr).max(axis=2) < OCR_COLOR_TOLERANCE
    ys, xs = np.nonzero(ink)
    if len(xs) < 10:
        return bx, by, bw, bh
    return bx + int(xs.min()), by + int(ys.min()), int(xs.max() - xs.min()) + 1, int(ys.max() - ys.min()) + 1


def _refine_text(engine, t):
    """Đọc lại dòng chữ kém tin cậy với ảnh tách theo màu chữ + 2 cỡ (font pixel đọc đúng hơn hẳn); giữ bản tin cậy nhất.
    Dòng đã tin cậy thì giữ nguyên — tách theo 1 màu làm mất phần chữ khác màu ("Top 10 Promote")."""
    best_text, best_score = t.get("text", ""), t.get("confidence", 0.0)
    if best_text and best_score >= OCR_TRUSTED:
        return
    for variant in _ocr_variants(t):
        result = engine(variant, use_det=False, use_cls=False, use_rec=True)
        if result.txts and float(result.scores[0]) > best_score:
            best_text, best_score = result.txts[0].strip(), float(result.scores[0])
    t["text"], t["confidence"] = best_text, round(best_score, 3)


def _ocr_variants(t):
    pad = 6
    H, W = _demo.shape[:2]
    crop = _demo[max(0, t["y"] - pad):min(H, t["y"] + t["h"] + pad), max(0, t["x"] - pad):min(W, t["x"] + t["w"] + pad)]
    hex_color = t["color"]
    bgr = np.array([int(hex_color[5:7], 16), int(hex_color[3:5], 16), int(hex_color[1:3], 16)])
    ink = np.abs(crop.astype(np.int16) - bgr).max(axis=2) < OCR_COLOR_TOLERANCE
    binary = cv2.cvtColor(np.where(ink, 0, 255).astype(np.uint8), cv2.COLOR_GRAY2BGR)
    for image in (binary, crop):
        for scale in (1, 2):
            v = cv2.resize(image, None, fx=scale, fy=scale, interpolation=cv2.INTER_NEAREST) if scale > 1 else image
            yield cv2.copyMakeBorder(v, 10, 10, 10, 10, cv2.BORDER_REPLICATE)


# ─── Main ───────────────────────────────────────────────────────────────────

def _emit(phase, **data):
    """Một sự kiện tiến trình cho Unity (một dòng JSON). Chỉ gọi ở process chính."""
    print(PROGRESS_PREFIX + json.dumps({"phase": phase, **data}, ensure_ascii=False), flush=True)


def _collect_sprites(art_dirs, demo_path, recursive):
    demo_abs = os.path.abspath(demo_path)
    seen, files = set(), []
    for d in art_dirs:
        if os.path.isfile(d):
            candidates = [d]
        elif recursive:
            candidates = [os.path.join(r, f) for r, _, fs in os.walk(d) for f in fs]
        else:
            candidates = [os.path.join(d, f) for f in os.listdir(d)]
        for p in sorted(candidates):
            name = os.path.basename(p)
            if not name.lower().endswith(".png") or name.lower().lstrip("_").startswith("demo"):
                continue
            p_abs = os.path.abspath(p)
            if p_abs == demo_abs or p_abs in seen:
                continue
            seen.add(p_abs)
            files.append(p)
    return files


def main():
    ap = argparse.ArgumentParser(description="Định vị sprite trên ảnh demo UI.")
    ap.add_argument("--demo", required=True, help="Ảnh demo (PNG/JPG)")
    ap.add_argument("--art", action="append", required=True, help="Thư mục art hoặc file PNG; lặp lại được")
    ap.add_argument("--out", required=True, help="File JSON đầu ra")
    ap.add_argument("--recursive", action="store_true", help="Quét cả thư mục con")
    ap.add_argument("--workers", type=int, default=0, help="Số process (0 = theo số nhân CPU)")
    ap.add_argument("--cache", default="", help="Thư mục cache (bỏ trống = không cache)")
    ap.add_argument("--no-ocr", action="store_true", help="Không đọc nội dung chữ")
    ap.add_argument("--hint", action="append", default=[],
                    help="locate.json của demo tab khác cùng màn — đối chiếu phần chung; lặp lại được")
    args = ap.parse_args()

    started = time.time()
    demo = _load_rgb(args.demo)
    if demo is None:
        raise ValueError(f"Không đọc được ảnh demo {args.demo}")
    with open(args.demo, "rb") as f:
        demo_hash = hashlib.sha1(f.read()).hexdigest()

    sprites = _collect_sprites(args.art, args.demo, args.recursive)
    results, pending, keys = {}, [], {}
    if args.cache:
        os.makedirs(args.cache, exist_ok=True)
        global _ocr_cache_file
        _ocr_cache_file = os.path.join(args.cache, "ocr_" + hashlib.sha1(f"{ALGO_VERSION}|{demo_hash}".encode()).hexdigest() + ".json")
    for p in sprites:
        if args.cache:
            keys[p] = _cache_key(demo_hash, p)
            cached = os.path.join(args.cache, keys[p] + ".json")
            if os.path.exists(cached):
                with open(cached, "r", encoding="utf-8") as f:
                    results[p] = {**json.load(f), "sprite": p}
                continue
        pending.append(p)

    # Quá ~12 process chỉ tranh cache/băng thông bộ nhớ (đo trên i5-14600K: 12 và 19 process như nhau, 12 nhanh hơn
    # với 842 sprite) — mỗi sprite chạy song song đã chậm ~2.5 lần so với chạy một mình.
    workers = args.workers or max(1, min(MAX_WORKERS, (os.cpu_count() or 2) - 1))
    _emit("start", total=len(sprites), cachedCount=len(sprites) - len(pending), workers=min(workers, max(1, len(pending))),
          demoWidth=int(demo.shape[1]), demoHeight=int(demo.shape[0]))
    for done, p in enumerate((p for p in sprites if p in results), 1):
        _emit("sprite", done=done, total=len(sprites), cached=True, sprite=results[p])
    if pending:
        if workers == 1 or len(pending) == 1:
            _init_worker(args.demo)
            computed = ((p, _locate_sprite(p)) for p in pending)
        else:
            # Sprite lớn (nặng nhất) chạy trước để cuối lượt không còn một sprite nặng bắt cả nhóm chờ; nhận kết quả
            # theo thứ tự xong để tiến trình trên Unity chạy đều.
            pool = ProcessPoolExecutor(max_workers=min(workers, len(pending)),
                                       initializer=_init_worker, initargs=(args.demo,))
            futures = {pool.submit(_locate_sprite, p): p for p in sorted(pending, key=os.path.getsize, reverse=True)}
            computed = ((futures[f], f.result()) for f in as_completed(futures))
        for p, r in computed:
            results[p] = r
            if args.cache and r.get("status") != "error":
                with open(os.path.join(args.cache, keys[p] + ".json"), "w", encoding="utf-8") as f:
                    json.dump(r, f)
            _emit("sprite", done=len(results), total=len(sprites), cached=False, sprite=r)

    ordered = [results[p] for p in sprites]
    _init_worker(args.demo)
    if args.hint:
        _emit("filter", message="Đối chiếu vị trí đã tìm được ở demo các tab khác")
        _apply_hints(ordered, args.hint)
    dropped, dim_alpha = _run_filter(ordered, "Bỏ vật nằm sau lớp dim", "behind-dim", _split_dimmed)
    dropped += _run_filter(ordered, "Bỏ khớp yếu từ thư mục art của màn khác", "foreign-weak",
                           lambda rs: _filter_foreign_weak(rs, args.art, args.demo))[0]
    dropped += _run_filter(ordered, "Bỏ khớp nằm trọn trong sprite khác", "explained-by-other", _filter_explained)[0]
    dropped += _run_filter(ordered, "Hai sprite cùng một chỗ → giữ bản khớp hơn", "same-spot", _filter_same_spot)[0]
    dropped += _run_filter(ordered, "Panel một màu trong panel cùng màu", "flat-nested", _filter_flat_nested)[0]
    dropped += _run_filter(ordered, "Sprite một màu trượt trên vùng cùng màu", "ambiguous", _filter_flat_ambiguous)[0]
    _emit("filter", message="Dò vùng UI chính — bỏ phần nằm dưới lớp phủ tối")
    ui_region, dim_estimate, ui_boxes = _ui_region(ordered)
    dim_estimated = dim_alpha is None and dim_estimate is not None
    if dim_estimated:
        dim_alpha = dim_estimate
    texts, ocr_status = _find_texts(ordered, not args.no_ocr, ui_region)
    dropped += _run_filter(ordered, "", "inside-text", lambda rs: _filter_inside_text(rs, texts), announce=False)[0]
    out = {
        "version": 1,
        "demo": args.demo,
        "demoWidth": int(demo.shape[1]),
        "demoHeight": int(demo.shape[0]),
        "elapsedMs": int((time.time() - started) * 1000),
        "cachedCount": len(sprites) - len(pending),
        "sprites": ordered,
        "dropped": dropped,
        "texts": texts,
        "ocr": ocr_status,
        "dimAlpha": dim_alpha,
        "dimEstimated": dim_estimated,
        "uiRegions": ui_boxes,
    }
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    matched = sum(1 for r in ordered if r["status"] == "matched")
    print(f"DONE {matched}/{len(ordered)} sprite khớp, {len(texts)} vùng chữ trong {out['elapsedMs']} ms → {args.out}", flush=True)


if __name__ == "__main__":
    # Windows: stdout nối pipe dùng codepage hệ thống (cp1252) → in tên/nhãn tiếng Việt bị UnicodeEncodeError.
    for _stream in (sys.stdout, sys.stderr):
        _stream.reconfigure(encoding="utf-8", errors="replace")
    try:
        main()
    except Exception as e:  # Unity đọc exit code + dòng cuối để báo lỗi
        print(f"ERROR {type(e).__name__}: {e}", file=sys.stderr, flush=True)
        sys.exit(1)
