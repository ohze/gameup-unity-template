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
from concurrent.futures import ProcessPoolExecutor

import cv2
import numpy as np

ALGO_VERSION = "5"
ALPHA_OPAQUE = 200          # pixel có alpha > ngưỡng mới dùng để so khớp
MIN_OPAQUE_PIXELS = 64      # sprite gần như trong suốt (glow/vfx) → không dò được bằng hình
ROBUST_KEEP = 0.75          # sai lệch màu tính trên 75% pixel khớp nhất → chịu được bị che ~25%
ACCEPT_ZNCC = 0.80          # tương quan cấu trúc tối thiểu (khớp thật ≥ 0.9, khớp nhầm thường < 0.7)
ACCEPT_DIFF = 16.0          # sai lệch màu tối đa (0-255) trên phần không bị che
STRONG_ZNCC = 0.95          # khớp gần tuyệt đối ở 1:1 → bỏ qua dò scale / 9-slice
LOW_TEXTURE_STD = 6.0       # template gần như một màu → ZNCC vô nghĩa
LOW_TEXTURE_DIFF = 3.0      # ... nên chỉ nhận khi trùng gần tuyệt đối, và chỉ ở tỉ lệ 1:1
EXACT_TOLERANCE = 6         # pixel "trùng tuyệt đối" (demo ghép từ chính art này) — sai lệch màu ≤ ngưỡng
OCCLUDED_INLIER = 0.50      # sprite lớn bị che nhiều (nền popup dưới chữ/icon/nút): ≥ 50% pixel trùng tuyệt đối...
OCCLUDED_MIN_PIXELS = 5000  # ... và đủ nhiều pixel để không thể trùng ngẫu nhiên
FLAT_BOUNDARY = 0.60        # sprite một màu: ≥ 60% dải viền trong trùng màu (phần còn lại có thể bị che)...
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

_demo = None
_demo_gray = None
_pyramid = {}
_edge_pyramid = {}


def _init_worker(demo_path):
    global _demo, _demo_gray
    cv2.setNumThreads(1)
    _demo = _load_rgb(demo_path)
    _demo_gray = cv2.cvtColor(_demo, cv2.COLOR_BGR2GRAY)
    _pyramid.clear()
    _edge_pyramid.clear()


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
    def low_texture(self):
        return self.texture_std < LOW_TEXTURE_STD

    def fits(self, image):
        return self.w <= image.shape[1] and self.h <= image.shape[0] and self.opaque >= MIN_OPAQUE_PIXELS


def _boundary(mask):
    """Dải viền trong ~3 px của vùng đục. Ngoài mép ảnh coi là trong suốt — mặc định erode của OpenCV coi là đầy,
    khiến sprite kín tới mép ảnh chỉ còn viền ở góc bo."""
    eroded = cv2.erode(mask, np.ones((7, 7), np.uint8), borderType=cv2.BORDER_CONSTANT, borderValue=0)
    return (mask > 0) & (eroded == 0)


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
    if zncc >= ACCEPT_ZNCC and robust <= ACCEPT_DIFF:
        return robust, zncc, inlier
    # Bị che nhiều: phần lộ ra vẫn trùng tuyệt đối, có hoạ tiết (không phải mảng một màu trùng ngẫu nhiên),
    # và viền ngoài lộ gần đủ — loại các khúc của sprite khác trùng pixel một phần (nút kéo giãn, dải ruy băng).
    edge = float((diff2d[t.boundary] <= EXACT_TOLERANCE).mean())
    if (inlier >= OCCLUDED_INLIER and exact.sum() >= OCCLUDED_MIN_PIXELS and edge >= OCCLUDED_EDGE_INLIER
            and (t.gray_masked[exact].std() >= LOW_TEXTURE_STD or edge >= FLAT_EDGE_STRONG)):
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
    diff2d = np.abs(src * tint - patch.astype(np.float32)).mean(axis=2)
    exact = diff2d <= TINT_TOLERANCE
    inlier = float(exact[m].mean())
    edge = float(exact[t.boundary].mean())
    # Tint tối làm pixel tối (viền đen) khớp với mọi vùng tối → đòi cả phần sáng của sprite cũng trùng.
    if (inlier < OCCLUDED_INLIER or edge < OCCLUDED_EDGE_INLIER or float(exact[bright].mean()) < OCCLUDED_INLIER
            or exact[m].sum() < min(OCCLUDED_MIN_PIXELS, t.opaque * 0.3)):
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
    r = EDGE_RING
    color = np.median(t.bgr[t.mask > 0], axis=0).astype(np.int16)
    diff = np.abs(_demo.astype(np.int16) - color).max(axis=2)
    same = cv2.copyMakeBorder((diff <= EXACT_TOLERANCE).astype(np.float32), r, r, r, r, cv2.BORDER_CONSTANT, value=0)
    other = cv2.copyMakeBorder((diff > 20).astype(np.float32), r, r, r, r, cv2.BORDER_CONSTANT, value=1)

    mask = cv2.copyMakeBorder(t.mask, r, r, r, r, cv2.BORDER_CONSTANT, value=0)
    ring = ((cv2.dilate(mask, np.ones((2 * r + 1, 2 * r + 1), np.uint8)) > 0) & (mask == 0)).astype(np.float32)
    boundary = np.zeros(mask.shape, np.float32)
    boundary[r:r + t.h, r:r + t.w] = t.boundary
    if ring.sum() < 16 or boundary.sum() < 16:
        return []

    edge = cv2.matchTemplate(same, boundary, cv2.TM_CCORR) / boundary.sum()
    # Vành ngoài tách theo 4 cạnh: dải tím giữa 2 khối (trên/dưới tương phản, 2 đầu liền màu) không được nhận.
    sides = np.zeros(ring.shape, np.uint8)
    sides[:r, :], sides[r + t.h:, :] = 1, 2
    sides[r:r + t.h, :r], sides[r:r + t.h, r + t.w:] = 3, 4
    contrast = np.zeros(edge.shape, np.float32)
    passing = np.zeros(edge.shape, np.int32)
    for side in (1, 2, 3, 4):
        part = ring * (sides == side)
        if part.sum() < 4:
            passing += 1  # cạnh không có vành (sprite sát mép ảnh gốc) → không xét
            continue
        c = cv2.matchTemplate(other, part, cv2.TM_CCORR) / part.sum()
        passing += (c >= FLAT_RING).astype(np.int32)
        contrast += c * part.sum() / ring.sum()
    score = np.where((edge >= FLAT_BOUNDARY) & (passing >= FLAT_SIDES), edge + contrast, -1.0)
    results = []
    for py, px in zip(*np.unravel_index(np.argsort(score, axis=None)[::-1][:2000], score.shape)):
        if score[py, px] < 0:
            break
        if all(abs(px - k[0]) > t.w * 0.5 or abs(py - k[1]) > t.h * 0.5 for k in results):
            results.append((int(px), int(py), 0.0, 0.0, round(float(edge[py, px]), 3)))
            if len(results) >= max_instances:
                break
    return results


def _search(t, max_instances):
    """Dò thô → dò mịn quanh ứng viên. Trả về [(x, y, diff, zncc, inlier)] đã lọc, tốt nhất trước."""
    if t.low_texture:
        return _search_flat(t, max_instances)
    if not t.fits(_demo):
        return []
    img = _gray_at(t.f)
    if t.small_gray.shape[0] > img.shape[0] or t.small_gray.shape[1] > img.shape[1]:
        return []
    score = _masked_score_map(img, t.small_gray, t.small_mask)
    min_dist = max(2, int(min(t.small_gray.shape[:2]) * 0.5))
    peaks = _top_peaks(score, PEAK_CANDIDATES, min_dist)
    # Ứng viên thứ 2 theo đường nét: chữ/icon đè lên làm sai điểm màu nhưng chỉ thêm nét, viền sprite vẫn trùng.
    t_edges = _edges(t.small_gray) * (t.small_mask > 0)
    if t_edges.sum() >= 16:
        edge_score = cv2.matchTemplate(_edges_at(t.f), t_edges.astype(np.float32), cv2.TM_CCORR)
        peaks += [p for p in _top_peaks(-edge_score, PEAK_CANDIDATES // 2, min_dist) if p not in peaks]
    # Ứng viên thứ 3 theo hình bóng ngoài: ruột bị chữ che và bị tint, nhưng viền quanh sprite vẫn nguyên.
    silhouette = (cv2.Canny(t.small_mask, 50, 150) > 0).astype(np.float32)
    if silhouette.sum() >= 16:
        shape_score = cv2.matchTemplate(_edges_at(t.f), silhouette, cv2.TM_CCORR)
        peaks += [p for p in _top_peaks(-shape_score, PEAK_CANDIDATES // 2, min_dist) if p not in peaks]

    H, W = _demo.shape[:2]
    pad = int(np.ceil(1.0 / t.f)) + 2
    results = []
    for cx, cy in peaks:
        x0, y0 = max(0, int(cx / t.f) - pad), max(0, int(cy / t.f) - pad)
        x1, y1 = min(W, int(cx / t.f) + pad + t.w), min(H, int(cy / t.f) + pad + t.h)
        window = _demo[y0:y1, x0:x1]
        if window.shape[0] < t.h or window.shape[1] < t.w:
            continue
        fine = _masked_score_map(window, t.bgr, t.mask)
        _, _, loc, _ = cv2.minMaxLoc(fine)
        x, y = x0 + loc[0], y0 + loc[1]
        verdict = _verify(x, y, t)
        if verdict is None and t.scale == 1.0:
            # Sprite bị tint: điểm màu lệch khỏi chỗ đúng vài px → dò mịn lại bằng tương quan chuẩn hoá (bất biến với tint).
            corr = cv2.matchTemplate(cv2.cvtColor(window, cv2.COLOR_BGR2GRAY), t.gray, cv2.TM_CCOEFF_NORMED, mask=t.mask)
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


def _find_uniform(sprite):
    """[(template, hits)] theo từng tỉ lệ tìm thấy. Thử 1:1 trước (art bàn giao 1:1); chưa chắc thì chọn tới 3 tỉ lệ
    tốt nhất theo điểm dò thô (cách nhau ≥ 0.08), tinh chỉnh ±0.04 quanh mỗi tỉ lệ rồi dò mịn.
    Sprite một màu không dò scale — ở tỉ lệ khác nó khớp với mọi mảng cùng màu."""
    base = _Template(sprite, 1.0)
    found = _search(base, MAX_INSTANCES)
    if base.low_texture or (found and _is_strong(found[0])):
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
    crops = {"tl": sprite[:ch, :cw], "tr": sprite[:ch, w - cw:],
             "bl": sprite[h - ch:, :cw], "br": sprite[h - ch:, w - cw:]}
    found = {}
    for key, crop in crops.items():
        t = _Template(crop, 1.0)
        if t.opaque < 16:
            return []
        t.opaque = max(t.opaque, MIN_OPAQUE_PIXELS)  # góc nhỏ vẫn được dò
        found[key] = _search(t, MAX_INSTANCES * 2)
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
        # 4 góc khớp chưa đủ (khung thẻ rỗng ruột khớp góc của khung khác) → kiểm tra cả sprite sau khi kéo giãn.
        rendered = _render_match(sprite, {"w": rw, "h": rh, "sliced": True, "scale": 1.0}, border)
        inlier, edge = _pixel_agreement(rendered, tl[0], tl[1])
        if inlier < OCCLUDED_INLIER or edge < OCCLUDED_EDGE_INLIER:
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

    groups = _find_uniform(sprite)
    matches = [_match_entry(x, y, t.w, t.h, t.scale, False, d, z, i, *extra)
               for t, found in groups for x, y, d, z, i, *extra in found]
    found = [hit for _, hits in groups for hit in hits]
    low_texture = _Template(sprite, 1.0).low_texture

    border = _estimate_border(sprite)
    if border is not None and not (found and _is_strong(found[0])):
        matches += [_match_entry(x, y, rw, rh, 1.0, True, d, z, i) for x, y, rw, rh, d, z, i in _find_sliced(sprite, border)]

    matches = _complete_repeats(sprite, _dedupe_matches(matches))
    if not matches:
        return {**base, "status": "unmatched", "reason": "no-match"}
    result = {**base, "status": "matched", "lowTexture": low_texture, "matches": matches}
    if low_texture:
        result["flatColor"] = [int(c) for c in np.median(sprite[:, :, :3][sprite[:, :, 3] > ALPHA_OPAQUE], axis=0)]
    if any(m["sliced"] for m in matches):
        result["suggestedBorder"] = border
    return result


def _dedupe_matches(matches):
    """Một vị trí chỉ giữ 1 kết quả (ưu tiên cấu trúc giống nhất); bỏ bản thường nằm trong bản 9-slice của chính nó."""
    sliced = [m for m in matches if m["sliced"]]
    matches = [m for m in matches if m["sliced"] or not any(_contains(k, m) for k in sliced)]
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


def _inside_ratio(inner, outer):
    ix = max(0, min(inner["x"] + inner["w"], outer["x"] + outer["w"]) - max(inner["x"], outer["x"]))
    iy = max(0, min(inner["y"] + inner["h"], outer["y"] + outer["h"]) - max(inner["y"], outer["y"]))
    return ix * iy / float(inner["w"] * inner["h"])


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
            if m.get("tint") and _is_dim_tint(m["tint"]):
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
            loser = mb if max(ma["zncc"], ma["inlier"]) >= max(mb["zncc"], mb["inlier"]) else ma
            dropped.add(id(loser))
    for r in results:
        if r.get("status") != "matched":
            continue
        r["matches"] = [m for m in r["matches"] if id(m) not in dropped]
        if not r["matches"]:
            r["status"], r["reason"] = "unmatched", "explained-by-other"
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


def _dominant_color(pixels):
    """Màu chữ (ruột nét): màu phổ biến nhất sau khi lượng tử hoá; bỏ viền tối nếu phần sáng chiếm ≥ 15% — chữ trắng viền
    đen dày của font game có viền nhiều pixel hơn ruột."""
    bright = pixels[pixels.astype(np.int32).sum(axis=1) >= 150]
    if len(bright) >= len(pixels) * 0.15:
        pixels = bright
    keys = (pixels // 32).astype(np.int32)
    codes = keys[:, 0] * 64 + keys[:, 1] * 8 + keys[:, 2]
    mode = np.bincount(codes).argmax()
    b, g, r = pixels[codes == mode].mean(axis=0)
    return "#{:02X}{:02X}{:02X}".format(int(r), int(g), int(b))


# ─── Đọc chữ (OCR) ─────────────────────────────────────────────────────────

OCR_COLOR_TOLERANCE = 60    # pixel gần màu chữ → đen, còn lại trắng: bỏ viền/nền để OCR đọc font pixel chuẩn hơn


OCR_DET_LIMIT = 1600         # cạnh dài tối đa khi phát hiện chữ — đủ giữ chữ nhỏ trên ảnh 1080x2160
OCR_MIN_COVER = 0.5          # dòng chữ phải nằm ≥ 50% trên sprite đã khớp (bỏ chữ của nền gameplay phía sau)
OCR_MIN_RESIDUAL = 0.08      # chữ trùng pixel sprite (logo "ADS" vẽ sẵn trong art) → không phải text cần dựng
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


def _find_texts(results, use_ocr):
    """(danh sách dòng chữ, trạng thái OCR). Có OCR: model phát hiện chữ trên vùng UI (bỏ qua avatar/panel/ô vật phẩm),
    đọc lại từng dòng với ảnh tách theo màu chữ. Không có OCR: tìm theo vùng lạ, nội dung để trống."""
    covered, diff = _residual(results)
    engine = _ocr_engine() if use_ocr else None
    if engine is None:
        return _detect_texts(diff > TEXT_RESIDUAL), ("skipped" if not use_ocr else "unavailable")
    texts = _detect_texts_ocr(engine, covered, diff)
    for t in texts:
        _refine_text(engine, t)
    return texts, ("ok" if texts else "none")


def _detect_texts_ocr(engine, covered, diff):
    ys, xs = np.nonzero(covered)
    if len(xs) == 0:
        return []
    x0, y0, x1, y1 = int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1
    result = engine(_demo[y0:y1, x0:x1], use_det=True, use_cls=False, use_rec=True)
    if result.boxes is None:
        return []

    words = []
    for box, text, score in zip(result.boxes, result.txts, result.scores):
        bx, by, bw, bh = cv2.boundingRect((np.array(box) + [x0, y0]).astype(np.int32))
        if bw < 4 or bh < 4 or covered[by:by + bh, bx:bx + bw].mean() < OCR_MIN_COVER:
            continue
        if (diff[by:by + bh, bx:bx + bw] > TEXT_RESIDUAL).mean() < OCR_MIN_RESIDUAL:
            continue
        words.append({"x": bx, "y": by, "w": bw, "h": bh, "text": text.strip(), "confidence": float(score)})

    texts = []
    for line in _merge_ocr_words(words):
        bx, by, bw, bh = line["x"], line["y"], line["w"], line["h"]
        region = diff[by:by + bh, bx:bx + bw] > TEXT_COLOR_RESIDUAL
        pixels = _demo[by:by + bh, bx:bx + bw][region] if region.sum() >= 10 else _demo[by:by + bh, bx:bx + bw].reshape(-1, 3)
        color = _dominant_color(pixels)
        x, y, w, h = _ink_box(bx, by, bw, bh, color)
        entry = {"x": x, "y": y, "w": w, "h": h, "color": color,
                 "text": line["text"], "confidence": round(line["confidence"], 3)}
        entry.update(_text_outline(x, y, w, h, color))
        texts.append(entry)
    texts.sort(key=lambda t: (t["y"], t["x"]))
    return texts


def _merge_ocr_words(words):
    """Model phát hiện đôi khi tách từng từ ("Remove" | "Ads") → nối các khung cùng dòng, sát nhau thành một nhãn."""
    words.sort(key=lambda w: (w["y"], w["x"]))
    lines = []
    for word in sorted(words, key=lambda w: w["x"]):
        for line in lines:
            overlap = min(line["y"] + line["h"], word["y"] + word["h"]) - max(line["y"], word["y"])
            gap = word["x"] - (line["x"] + line["w"])
            # Khung phát hiện có đệm nên 2 từ liền nhau có thể chồng lên nhau (gap âm).
            if overlap >= 0.6 * min(line["h"], word["h"]) and -0.6 * max(line["h"], word["h"]) <= gap <= TEXT_WORD_GAP * max(line["h"], word["h"]):
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
    args = ap.parse_args()

    started = time.time()
    demo = _load_rgb(args.demo)
    with open(args.demo, "rb") as f:
        demo_hash = hashlib.sha1(f.read()).hexdigest()

    sprites = _collect_sprites(args.art, args.demo, args.recursive)
    results, pending, keys = {}, [], {}
    if args.cache:
        os.makedirs(args.cache, exist_ok=True)
    for p in sprites:
        if args.cache:
            keys[p] = _cache_key(demo_hash, p)
            cached = os.path.join(args.cache, keys[p] + ".json")
            if os.path.exists(cached):
                with open(cached, "r", encoding="utf-8") as f:
                    results[p] = {**json.load(f), "sprite": p}
                continue
        pending.append(p)

    workers = args.workers or max(1, (os.cpu_count() or 2) - 1)
    if pending:
        if workers == 1 or len(pending) == 1:
            _init_worker(args.demo)
            computed = map(_locate_sprite, pending)
        else:
            pool = ProcessPoolExecutor(max_workers=min(workers, len(pending)),
                                       initializer=_init_worker, initargs=(args.demo,))
            computed = pool.map(_locate_sprite, pending, chunksize=1)
        for p, r in zip(pending, computed):
            results[p] = r
            if args.cache and r.get("status") != "error":
                with open(os.path.join(args.cache, keys[p] + ".json"), "w", encoding="utf-8") as f:
                    json.dump(r, f)
            print(f"[{len(results)}/{len(sprites)}] {r['name']}: {r['status']}", flush=True)

    ordered = [results[p] for p in sprites]
    _init_worker(args.demo)
    dim_alpha = _split_dimmed(ordered)
    _filter_explained(ordered)
    _filter_same_spot(ordered)
    _filter_flat_nested(ordered)
    _filter_flat_ambiguous(ordered)
    texts, ocr_status = _find_texts(ordered, not args.no_ocr)
    out = {
        "version": 1,
        "demo": args.demo,
        "demoWidth": int(demo.shape[1]),
        "demoHeight": int(demo.shape[0]),
        "elapsedMs": int((time.time() - started) * 1000),
        "cachedCount": len(sprites) - len(pending),
        "sprites": ordered,
        "texts": texts,
        "ocr": ocr_status,
        "dimAlpha": dim_alpha,
    }
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False, indent=1)
    matched = sum(1 for r in ordered if r["status"] == "matched")
    print(f"DONE {matched}/{len(ordered)} sprite khớp, {len(texts)} vùng chữ trong {out['elapsedMs']} ms → {args.out}", flush=True)


if __name__ == "__main__":
    try:
        main()
    except Exception as e:  # Unity đọc exit code + dòng cuối để báo lỗi
        print(f"ERROR {type(e).__name__}: {e}", file=sys.stderr, flush=True)
        sys.exit(1)
