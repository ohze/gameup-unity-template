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

ALGO_VERSION = "4"
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
LOW_TEXTURE_INLIER = 0.70   # sprite một màu bị che: ≥ 70% pixel trùng + mép tương phản với xung quanh
OCCLUDED_EDGE_INLIER = 0.85 # ... và viền ngoài của sprite phải lộ ra gần đủ (khúc giữa của nút kéo giãn không có viền)
EDGE_RING = 4               # độ dày vành kiểm tra quanh mép sprite (px)
EDGE_CONTRAST = 0.60        # tỉ lệ pixel vành khác màu sprite tối thiểu
COARSE_SCALES = [0.9, 0.8, 0.75, 0.7, 0.6, 0.5, 1.1, 1.25, 1.5]
MAX_INSTANCES = 12
PEAK_CANDIDATES = 16

_demo = None
_demo_gray = None
_pyramid = {}


def _init_worker(demo_path):
    global _demo, _demo_gray
    cv2.setNumThreads(1)
    _demo = _load_rgb(demo_path)
    _demo_gray = cv2.cvtColor(_demo, cv2.COLOR_BGR2GRAY)
    _pyramid.clear()


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
        eroded = cv2.erode(self.mask, np.ones((7, 7), np.uint8))
        self.boundary = (self.mask > 0) & (eroded == 0)

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

    if t.low_texture:
        mean = float(diff.mean())
        if t.scale != 1.0:
            return None
        if mean <= LOW_TEXTURE_DIFF:
            return mean, 0.0, inlier
        if inlier >= LOW_TEXTURE_INLIER and exact.sum() >= OCCLUDED_MIN_PIXELS and _edge_contrast(x, y, t):
            return mean, 0.0, inlier
        return None

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
    if (inlier >= OCCLUDED_INLIER and exact.sum() >= OCCLUDED_MIN_PIXELS
            and t.gray_masked[exact].std() >= LOW_TEXTURE_STD
            and float((diff2d[t.boundary] <= EXACT_TOLERANCE).mean()) >= OCCLUDED_EDGE_INLIER):
        return robust, zncc, inlier
    return None


def _edge_contrast(x, y, t):
    """Vành ngay ngoài mép sprite trên demo phải khác màu sprite — chốt đúng vị trí của mảng một màu."""
    r = EDGE_RING
    H, W = _demo.shape[:2]
    x0, y0, x1, y1 = max(0, x - r), max(0, y - r), min(W, x + t.w + r), min(H, y + t.h + r)
    padded = np.zeros((y1 - y0, x1 - x0), np.uint8)
    padded[y - y0:y - y0 + t.h, x - x0:x - x0 + t.w] = t.mask
    ring = (cv2.dilate(padded, np.ones((2 * r + 1, 2 * r + 1), np.uint8)) > 0) & (padded == 0)
    if ring.sum() < 16:
        return False
    color = t.bgr[t.mask > 0].mean(axis=0)
    ring_diff = np.abs(_demo[y0:y1, x0:x1][ring].astype(np.float32) - color).mean(axis=1)
    return float((ring_diff > 20).mean()) >= EDGE_CONTRAST


def _search(t, max_instances):
    """Dò thô → dò mịn quanh ứng viên. Trả về [(x, y, diff, zncc, inlier)] đã lọc, tốt nhất trước."""
    if not t.fits(_demo):
        return []
    img = _gray_at(t.f)
    if t.small_gray.shape[0] > img.shape[0] or t.small_gray.shape[1] > img.shape[1]:
        return []
    score = _masked_score_map(img, t.small_gray, t.small_mask)
    min_dist = max(2, int(min(t.small_gray.shape[:2]) * 0.5))
    peaks = _top_peaks(score, PEAK_CANDIDATES, min_dist)

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
        if verdict is not None:
            results.append((x, y) + verdict)

    results.sort(key=lambda r: (-max(r[3], r[4]), r[2]))
    kept = []
    for r in results:
        if all(abs(r[0] - k[0]) > t.w * 0.5 or abs(r[1] - k[1]) > t.h * 0.5 for k in kept):
            kept.append(r)
    return kept[:max_instances]


# ─── Scale đều ───────────────────────────────────────────────────────────────

def _find_uniform(sprite):
    """Thử 1:1 trước (art bàn giao 1:1). Chưa chắc thì chọn tỉ lệ bằng điểm dò thô, tinh chỉnh ±0.04.
    Sprite một màu không dò scale — ở tỉ lệ khác nó khớp với mọi mảng cùng màu."""
    base = _Template(sprite, 1.0)
    found = _search(base, MAX_INSTANCES)
    if base.low_texture or (found and _is_strong(found[0])):
        return base, found

    ranked = sorted(((_coarse_best(_Template(sprite, s)), s) for s in COARSE_SCALES))
    best_s = ranked[0][1]
    fine = [(ranked[0][0], best_s)]
    for d in (-0.04, -0.02, 0.02, 0.04):
        s = round(best_s + d, 2)
        if s > 0:
            fine.append((_coarse_best(_Template(sprite, s)), s))
    fine.sort()

    best_t, best_found = (base, found) if found else (None, [])
    for _, s in fine[:2]:
        t = _Template(sprite, s)
        f2 = _search(t, MAX_INSTANCES)
        if f2 and (not best_found or max(f2[0][3], f2[0][4]) > max(best_found[0][3], best_found[0][4])):
            best_t, best_found = t, f2
    return best_t, best_found


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
        corners = (tl, tr, bl, brs[0])
        diff = float(np.mean([c[2] for c in corners]))
        zncc = float(np.mean([c[3] for c in corners]))
        inlier = float(np.mean([c[4] for c in corners]))
        rects.append((tl[0], tl[1], rw, rh, diff, zncc, inlier))
    return rects


# ─── Xử lý 1 sprite ─────────────────────────────────────────────────────────

def _cache_key(demo_hash, sprite_path):
    with open(sprite_path, "rb") as f:
        sprite_hash = hashlib.sha1(f.read()).hexdigest()
    return hashlib.sha1(f"{ALGO_VERSION}|{demo_hash}|{sprite_hash}".encode()).hexdigest()


def _match_entry(x, y, w, h, scale, sliced, diff, zncc, inlier):
    return {"x": int(x), "y": int(y), "w": int(w), "h": int(h), "scale": scale, "sliced": sliced,
            "diff": round(float(diff), 1), "zncc": round(float(zncc), 3), "inlier": round(float(inlier), 3)}


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

    t, found = _find_uniform(sprite)
    matches = [_match_entry(x, y, t.w, t.h, t.scale, False, d, z, i) for x, y, d, z, i in found]
    low_texture = _Template(sprite, 1.0).low_texture

    border = _estimate_border(sprite)
    if border is not None and not (found and _is_strong(found[0])):
        matches += [_match_entry(x, y, rw, rh, 1.0, True, d, z, i) for x, y, rw, rh, d, z, i in _find_sliced(sprite, border)]

    matches = _dedupe_matches(matches)
    if not matches:
        return {**base, "status": "unmatched", "reason": "no-match"}
    result = {**base, "status": "matched", "lowTexture": low_texture, "matches": matches}
    if any(m["sliced"] for m in matches):
        result["suggestedBorder"] = border
    return result


def _dedupe_matches(matches):
    """Một vị trí chỉ giữ 1 kết quả (ưu tiên cấu trúc giống nhất); bỏ bản thường nằm trong bản 9-slice của chính nó."""
    sliced = [m for m in matches if m["sliced"]]
    matches = [m for m in matches if m["sliced"] or not any(_contains(k, m) for k in sliced)]
    matches.sort(key=lambda m: (-max(m["zncc"], m["inlier"]), m["diff"]))
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
        items += [(m["w"] * m["h"], sprite, m, border) for m in r["matches"]]

    for _, sprite, m, border in sorted(items, key=lambda it: -it[0]):
        img = _render_match(sprite, m, border)
        x0, y0 = max(0, m["x"]), max(0, m["y"])
        x1, y1 = min(W, m["x"] + img.shape[1]), min(H, m["y"] + img.shape[0])
        if x1 <= x0 or y1 <= y0:
            continue
        part = img[y0 - m["y"]:y1 - m["y"], x0 - m["x"]:x1 - m["x"]].astype(np.float32)
        alpha = part[:, :, 3:4] / 255.0
        predicted[y0:y1, x0:x1] = part[:, :, :3] * alpha + predicted[y0:y1, x0:x1] * (1 - alpha)
        covered[y0:y1, x0:x1] |= part[:, :, 3] > ALPHA_OPAQUE
    return predicted, covered


def _detect_texts(results):
    """Vùng nằm trên sprite đã khớp nhưng pixel khác sprite → gần như chắc chắn là chữ vẽ đè.
    Gom chữ cái thành dòng, trả về khung + màu chữ chủ đạo. Nội dung chữ để AI/người điền."""
    predicted, covered = _compose(results)
    residual = (np.abs(_demo.astype(np.float32) - predicted).max(axis=2) > TEXT_RESIDUAL) & covered
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
    """Màu chữ: màu phổ biến nhất sau khi lượng tử hoá, bỏ viền tối nếu phần sáng đủ nhiều."""
    bright = pixels[pixels.astype(np.int32).sum(axis=1) >= 150]
    if len(bright) >= len(pixels) * 0.3:
        pixels = bright
    keys = (pixels // 32).astype(np.int32)
    codes = keys[:, 0] * 64 + keys[:, 1] * 8 + keys[:, 2]
    mode = np.bincount(codes).argmax()
    b, g, r = pixels[codes == mode].mean(axis=0)
    return "#{:02X}{:02X}{:02X}".format(int(r), int(g), int(b))


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
            if not name.lower().endswith(".png") or name.startswith("_demo"):
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
    _filter_explained(ordered)
    texts = _detect_texts(ordered)
    out = {
        "version": 1,
        "demo": args.demo,
        "demoWidth": int(demo.shape[1]),
        "demoHeight": int(demo.shape[0]),
        "elapsedMs": int((time.time() - started) * 1000),
        "cachedCount": len(sprites) - len(pending),
        "sprites": ordered,
        "texts": texts,
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
