"""Sinh demo tổng hợp có đáp án (ground truth) cho ui_locate.py từ art thật của project — các case khó: scale (cả
bilinear/bicubic kiểu Photoshop), 9-slice (cả panel một màu), lớp dim + tint + bị che + chữ viền, danh sách lặp, JPEG,
sprite gần giống nhau / trùng art ở 2 thư mục / icon 2048 px thu nhỏ. Demo đặt trong thư mục art của "màn" tương ứng
(như dữ liệu thật) cùng ~120 sprite mồi ngẫu nhiên để đo khớp nhầm.

    python make_cases.py <thư mục art UI_v2 của project> <thư mục đầu ra>
    python evaluate.py <thư mục đầu ra> [tên case…] [--cache]

Cần art UI_v2 của dự án (Settings, Challenge Mode/popup_ranking, _Shared, Store…)."""
import glob
import hashlib
import json
import os
import random
import shutil
import sys

import cv2
import numpy as np

ART = os.path.abspath(sys.argv[1]) if len(sys.argv) > 2 else None
OUT = os.path.abspath(sys.argv[2]) if len(sys.argv) > 2 else None
W, H = 1080, 2160
rng = random.Random(7)
SCREEN = {"A": "Settings", "B": "Settings", "C": "Settings", "D": "Challenge Mode/popup_ranking",
          "E": "Challenge Mode/popup_ranking", "F": "Challenge Mode/popup_ranking", "G": "Challenge Mode/popup_ranking"}


def load(rel):
    img = cv2.imread(os.path.join(ART, rel), cv2.IMREAD_UNCHANGED)
    assert img is not None, rel
    if img.shape[2] == 3:
        img = cv2.cvtColor(img, cv2.COLOR_BGR2BGRA)
    return img


def scale(img, s, interp=cv2.INTER_AREA):
    h, w = img.shape[:2]
    return cv2.resize(img, (max(1, round(w * s)), max(1, round(h * s))), interpolation=interp)


def slice9(img, w, h, b):
    l, r, t, bt = b
    sh, sw = img.shape[:2]
    sx, dx = [0, l, sw - r, sw], [0, l, w - r, w]
    sy, dy = [0, t, sh - bt, sh], [0, t, h - bt, h]
    out = np.zeros((h, w, 4), np.uint8)
    for i in range(3):
        for j in range(3):
            piece = img[sy[i]:sy[i + 1], sx[j]:sx[j + 1]]
            size = (dx[j + 1] - dx[j], dy[i + 1] - dy[i])
            if piece.size and size[0] > 0 and size[1] > 0:
                out[dy[i]:dy[i + 1], dx[j]:dx[j + 1]] = cv2.resize(piece, size, interpolation=cv2.INTER_NEAREST)
    return out


class Case:
    def __init__(self, name, bg=None):
        self.name = name
        self.img = np.zeros((H, W, 3), np.float32) if bg is None else bg.astype(np.float32).copy()
        self.gt = []
        self.texts = []
        self.art = set()

    def paste(self, rel, x, y, img=None, kind="1:1", tint=None, record=True):
        src = load(rel) if img is None else img
        part = src.astype(np.float32)
        if tint is not None:
            part[:, :, :3] *= np.array(tint[::-1], np.float32) / 255.0
        h, w = part.shape[:2]
        x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + w), min(H, y + h)
        p = part[y0 - y:y1 - y, x0 - x:x1 - x]
        a = p[:, :, 3:4] / 255.0
        self.img[y0:y1, x0:x1] = p[:, :, :3] * a + self.img[y0:y1, x0:x1] * (1 - a)
        self.art.add(rel)
        if record:
            self.gt.append({"sprite": rel, "x": x, "y": y, "w": w, "h": h, "kind": kind})

    def dim(self, alpha):
        self.img *= (1 - alpha)
        for g in self.gt:
            g["behindDim"] = True

    def text(self, s, x, y, size=1.4, color=(255, 255, 255), outline=4):
        font = cv2.FONT_HERSHEY_DUPLEX
        (tw, th), base = cv2.getTextSize(s, font, size, 2)
        img = np.clip(self.img, 0, 255).astype(np.uint8)
        if outline:
            k = max(1, outline // 2)
            for dx in (-k, 0, k):
                for dy in (-k, 0, k):
                    if dx or dy:
                        cv2.putText(img, s, (x + dx, y + dy), font, size, (0, 0, 0), 2, cv2.LINE_AA)
        cv2.putText(img, s, (x, y), font, size, color[::-1], 2, cv2.LINE_AA)
        self.img = img.astype(np.float32)
        self.texts.append({"text": s, "x": x, "y": y - th, "w": tw, "h": th + base})

    def save(self, art_dir, jpeg=None):
        img = np.clip(self.img, 0, 255).astype(np.uint8)
        # Demo nằm trong thư mục art của màn (như dữ liệu thật) → mô phỏng người dùng chọn cả thư mục art gốc.
        path = os.path.join(art_dir, SCREEN[self.name[0]], f"demo_{self.name}.png")
        if jpeg:
            ok, enc = cv2.imencode(".jpg", img, [cv2.IMWRITE_JPEG_QUALITY, jpeg])
            img = cv2.imdecode(enc, cv2.IMREAD_COLOR)
        cv2.imwrite(path, img)
        return path


def noise_bg():
    """Nền kiểu gameplay: gradient + khối màu ngẫu nhiên."""
    bg = np.zeros((H, W, 3), np.float32)
    bg[:] = np.linspace(40, 110, H)[:, None, None] * np.array([1.0, 0.8, 0.6])
    r = np.random.RandomState(3)
    for _ in range(60):
        x, y = r.randint(0, W), r.randint(0, H)
        cv2.circle(bg, (x, y), int(r.randint(20, 120)), [float(c) for c in r.randint(30, 200, 3)], -1)
    return cv2.GaussianBlur(bg, (0, 0), 6)


def build():
    os.makedirs(os.path.join(OUT, "cases"), exist_ok=True)
    cases = []

    # A — popup Settings 1:1 trên nền phẳng
    a = Case("A_clean", np.full((H, W, 3), (40, 30, 30), np.float32))
    a.paste("Settings/popup.png", 103, 800)
    a.paste("Settings/title.png", 294, 740)
    a.paste("Settings/icon_music.png", 180, 900)
    a.paste("Settings/icon_sound.png", 178, 990)
    a.paste("Settings/switch_on.png", 760, 915)
    a.paste("Settings/switch_off.png", 760, 1005)
    a.paste("Settings/icon_x.png", 890, 780)
    a.paste("Settings/frame_name.png", 166, 1080)
    a.paste("Settings/icon_changeName.png", 830, 1105)
    a.text("Music", 280, 945)
    a.text("Sound", 280, 1035)
    cases.append(a)

    # B — scale đều, có bản bilinear (Photoshop)
    b = Case("B_scaled", np.full((H, W, 3), (60, 45, 35), np.float32))
    b.paste("Settings/popup.png", 103, 300)
    for i, (s, interp) in enumerate([(0.75, cv2.INTER_AREA), (0.6, cv2.INTER_AREA), (1.25, cv2.INTER_NEAREST),
                                     (0.8, cv2.INTER_LINEAR), (0.5, cv2.INTER_CUBIC)]):
        rel = f"_Shared/Avatar/hero/avatar_00{i + 1}.png"
        img = scale(load(rel), s, interp)
        b.paste(rel, 120 + i * 180, 1000, img, kind=f"scale {s} {['area','area','nearest','linear','cubic'][i]}")
    b.paste("Settings/icon_music.png", 200, 400, scale(load("Settings/icon_music.png"), 0.7), kind="scale 0.7")
    b.paste("Settings/icon_sound.png", 400, 400, scale(load("Settings/icon_sound.png"), 1.5, cv2.INTER_LINEAR), kind="scale 1.5 linear")
    b.paste("Store/tag_bonus.png", 600, 400, scale(load("Store/tag_bonus.png"), 0.66), kind="scale 0.66")
    cases.append(b)

    # C — 9-slice
    c = Case("C_nine_slice", np.full((H, W, 3), (35, 35, 50), np.float32))
    frame = load("Settings/frame.png")
    c.paste("Settings/frame.png", 170, 300, slice9(frame, 900, 104, (40, 40, 40, 40)), kind="9-slice 900x104")
    c.paste("Settings/frame.png", 170, 450, slice9(frame, 740, 220, (40, 40, 40, 40)), kind="9-slice 740x220")
    c.paste("Settings/frame.png", 170, 720, kind="1:1")
    popup = load("Settings/popup.png")
    c.paste("Settings/popup.png", 60, 900, slice9(popup, 960, 900, (80, 80, 120, 80)), kind="9-slice 960x900")
    btn = load("Store/dungeon_pack/btn_buy.png")
    c.paste("Store/dungeon_pack/btn_buy.png", 300, 1900, slice9(btn, 480, 96, (48, 48, 30, 30)), kind="9-slice 480x96")
    c.text("BUY", 480, 1965)
    cases.append(c)

    # D — nền gameplay + HUD sau lớp dim, tint, bị che, chữ viền
    d = Case("D_dim_tint_occl", noise_bg())
    d.paste("_Shared/bottom_tab.png", 0, 1946, kind="HUD")
    d.paste("_Shared/bottom_tab_selected.png", 200, 1946, kind="HUD")
    d.dim(0.6)
    d.paste("Challenge Mode/popup_ranking/border_popup.png", 24, 250)
    d.paste("Challenge Mode/popup_ranking/banner_title.png", 28, 190)
    d.paste("Challenge Mode/popup_ranking/btn_tab_1.png", 80, 1760)
    d.paste("Challenge Mode/popup_ranking/btn_tab_2.png", 600, 1760, tint=(160, 146, 175), kind="tint")
    d.paste("Challenge Mode/popup_ranking/boder_rankslot.png", 56, 500)
    d.paste("Challenge Mode/popup_ranking/crown_top1.png", 90, 520, kind="1:1")
    # avatar bị che ~35% bởi cờ
    d.paste("_Shared/Avatar/hero/avatar_003.png", 820, 960, kind="occluded ~33%")
    d.paste("Challenge Mode/popup_ranking/flag_top2.png", 560, 780)
    d.paste("_Shared/Avatar/hero/avatar_004.png", 300, 1200, kind="occluded by text")
    d.text("Season Leaderboard", 170, 265, 1.6, (248, 254, 78), 3)
    d.text("Player Name", 320, 580)
    d.text("999.99B", 700, 580, 1.0, outline=0)
    d.text("OVERLAP TEXT", 250, 1290, 1.6)
    d.text("Weekly", 180, 1820)
    d.text("Season", 700, 1820)
    cases.append(d)

    # E — danh sách lặp
    e = Case("E_list_rows", np.full((H, W, 3), (50, 35, 30), np.float32))
    e.paste("Challenge Mode/popup_ranking/border_popup.png", 24, 250)
    for i in range(6):
        y = 400 + i * 190
        e.paste("Challenge Mode/popup_ranking/boder_rankslot.png", 56, y, kind="row")
        av = f"_Shared/Avatar/enemy/avatar_0{10 + i}.png"
        e.paste(av, 90, y + 30, scale(load(av), 0.66), kind="scale 0.66 in row")
        e.text(f"Player {i + 1}", 300, y + 95)
        if i == 4:  # hàng bị ô vật phẩm che nhiều
            for k in range(4):
                e.paste("Store/dungeon_pack/boder_item_char.png", 560 + k * 100, y + 36, kind="item")
                e.paste("Store/dungeon_pack/icon_value+200.png", 560 + k * 100, y + 20,
                        scale(load("Store/dungeon_pack/icon_value+200.png"), 0.8), kind="scale 0.8")
    e.paste("Challenge Mode/popup_ranking/boder_playerrank.png", 40, 1600)
    e.text("You", 300, 1700)
    cases.append(e)

    # F — D nén JPEG
    f = Case("F_jpeg", d.img)
    f.gt, f.texts, f.art = [dict(g) for g in d.gt], list(d.texts), set(d.art)
    f.jpeg = 85
    cases.append(f)

    # G — gần giống nhau + trùng art + icon 2048 thu nhỏ
    g = Case("G_distractors", np.full((H, W, 3), (30, 40, 45), np.float32))
    g.paste("Challenge Mode/popup_ranking/btn_tab_1.png", 80, 300)
    g.paste("Challenge Mode/popup_ranking/btn_tab_2.png", 600, 300)
    g.paste("Challenge Mode/popup_ranking/crown_top2.png", 100, 600)
    g.paste("Challenge Mode/popup_ranking/crown_top3.png", 300, 600)
    g.paste("_Shared/Avatar/hero/avatar_001.png", 500, 600)   # trùng tên với enemy/avatar_001
    g.paste("Store/icon_gem_3.png", 700, 600, scale(load("Store/icon_gem_3.png"), 0.07), kind="scale 0.07 (2048px art)")
    g.paste("Store/icon_coin_2.png", 100, 900, scale(load("Store/icon_coin_2.png"), 0.1), kind="scale 0.1 (2048px art)")
    g.paste("Settings/switch_on.png", 500, 900)
    g.paste("Settings/switch_off.png", 700, 900)
    cases.append(g)
    return cases


def main():
    if not ART:
        sys.exit(__doc__)
    cases = build()
    # Thư mục art: sprite đã dùng + ~120 sprite mồi ngẫu nhiên (cùng art project) để đo khớp nhầm.
    art_dir = os.path.join(OUT, "art")
    if os.path.isdir(art_dir):
        shutil.rmtree(art_dir)
    used = sorted(set().union(*[c.art for c in cases]))
    pool = sorted(p for p in glob.glob(os.path.join(ART, "**", "*.png"), recursive=True)
                  if "demo" not in os.path.basename(p).lower())
    decoys = rng.sample([os.path.relpath(p, ART).replace("\\", "/") for p in pool], 120)
    for rel in sorted(set(used) | set(decoys)):
        dst = os.path.join(art_dir, rel)
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copyfile(os.path.join(ART, rel), dst)

    manifest = []
    for c in cases:
        path = c.save(art_dir, getattr(c, "jpeg", None))
        manifest.append({"name": c.name, "demo": path, "gt": c.gt, "texts": c.texts})
    with open(os.path.join(OUT, "manifest.json"), "w", encoding="utf-8") as fp:
        json.dump({"art": art_dir, "cases": manifest}, fp, indent=1, ensure_ascii=False)
    print(f"{len(cases)} case, {len(used)} sprite dùng, art {len(set(used) | set(decoys))} file")


if __name__ == "__main__":
    main()
