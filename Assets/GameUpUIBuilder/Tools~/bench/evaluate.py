"""Chạy ui_locate.py trên các case tổng hợp (make_cases.py) và chấm theo đáp án: recall, khớp thừa, lệch vị trí, chữ
đọc được, thời gian. Chạy bằng python của venv UI Builder:

    python evaluate.py <thư mục đầu ra của make_cases> [tên case…] [--cache]"""
import hashlib
import json
import os
import re
import subprocess
import sys
import time

HERE = os.path.abspath(sys.argv[1]) if len(sys.argv) > 1 else sys.exit(__doc__)
SCRIPT = os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "ui_locate.py")
PY = sys.executable


def content_key(path, cache={}):
    if path not in cache:
        with open(path, "rb") as f:
            cache[path] = hashlib.md5(f.read()).hexdigest()
    return cache[path]


def iou(a, b):
    ix = max(0, min(a["x"] + a["w"], b["x"] + b["w"]) - max(a["x"], b["x"]))
    iy = max(0, min(a["y"] + a["h"], b["y"] + b["h"]) - max(a["y"], b["y"]))
    inter = ix * iy
    return inter / float(a["w"] * a["h"] + b["w"] * b["h"] - inter) if inter else 0.0


def norm(s):
    return re.sub(r"[^a-z0-9.]", "", s.lower())


def evaluate(case, art, use_cache):
    out = os.path.join(HERE, "results", f"{case['name']}.json")
    os.makedirs(os.path.dirname(out), exist_ok=True)
    args = [PY, SCRIPT, "--demo", case["demo"], "--art", art, "--recursive", "--out", out]
    if use_cache:
        args += ["--cache", os.path.join(HERE, "cache")]
    t0 = time.time()
    proc = subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")
    elapsed = time.time() - t0
    if proc.returncode != 0:
        return {"name": case["name"], "error": proc.stderr.strip().splitlines()[-1:] or proc.stdout[-300:]}
    res = json.load(open(out, encoding="utf-8"))

    found = []  # (key, rect)
    for s in res["sprites"]:
        if s["status"] == "matched":
            for m in s["matches"]:
                found.append((content_key(s["sprite"]), m, s["name"]))
    dropped = [(d["name"], d) for d in res.get("dropped", [])]

    report = {"name": case["name"], "time": round(elapsed, 1), "sprites": len(res["sprites"]), "misses": [],
              "errors": [], "false": [], "dimOk": [], "texts": []}
    used = set()
    ui_gt = [g for g in case["gt"] if not g.get("behindDim")]
    for g in ui_gt:
        key = content_key(os.path.join(art, g["sprite"]))
        best = max(((iou(g, m), i) for i, (k, m, _) in enumerate(found) if k == key), default=(0, -1))
        if best[0] < 0.8:
            report["misses"].append(f"{g['sprite']} [{g['kind']}] @({g['x']},{g['y']}) {g['w']}x{g['h']}"
                                    + (f" (gần nhất IoU {best[0]:.2f})" if best[1] >= 0 else ""))
            continue
        used.add(best[1])
        m = found[best[1]][1]
        err = max(abs(m["x"] - g["x"]), abs(m["y"] - g["y"]), abs(m["w"] - g["w"]), abs(m["h"] - g["h"]))
        if err > 1:
            report["errors"].append(f"{g['sprite']} [{g['kind']}] lệch {err}px: dò {m['x']},{m['y']} {m['w']}x{m['h']}")
    for g in case["gt"]:
        if g.get("behindDim"):
            key = content_key(os.path.join(art, g["sprite"]))
            leaked = any(k == key and iou(g, m) > 0.5 for k, m, _ in found)
            report["dimOk"].append(f"{g['sprite']}: {'LỌT VÀO UI' if leaked else 'đã loại'}")
    gt_keys = {content_key(os.path.join(art, g["sprite"])) for g in case["gt"]}
    for i, (k, m, name) in enumerate(found):
        if i in used:
            continue
        # Khớp trùng vị trí với một GT khác nội dung = khớp nhầm; khớp đúng nội dung ở chỗ không có GT = thừa
        report["false"].append(f"{name} @({m['x']},{m['y']}) {m['w']}x{m['h']} s{m['scale']}{' 9s' if m['sliced'] else ''}"
                               + (" (sprite có trong demo)" if k in gt_keys else ""))

    texts = res.get("texts", [])
    for t in case["texts"]:
        hit = max(texts, key=lambda r: iou(t, r), default=None)
        ok = hit is not None and iou(t, hit) > 0.3
        read = hit.get("text", "") if ok else ""
        report["texts"].append(f"{'✔' if ok and norm(read) == norm(t['text']) else ('~' if ok else '✘')} "
                               f"{t['text']!r} → {read!r}")
    report["extraTexts"] = len(texts) - sum(1 for t in texts if any(iou(t, g) > 0.3 for g in case["texts"]))
    report["recall"] = f"{len(ui_gt) - len(report['misses'])}/{len(ui_gt)}"
    return report


def main():
    manifest = json.load(open(os.path.join(HERE, "manifest.json"), encoding="utf-8"))
    names = [a for a in sys.argv[2:] if not a.startswith("--")]
    use_cache = "--cache" in sys.argv
    for case in manifest["cases"]:
        if names and not any(case["name"].startswith(n) for n in names):
            continue
        r = evaluate(case, manifest["art"], use_cache)
        if "error" in r:
            print(f"== {r['name']}: LỖI {r['error']}")
            continue
        print(f"== {r['name']}  recall {r['recall']}  khớp thừa {len(r['false'])}  lệch {len(r['errors'])}  "
              f"chữ thừa {r['extraTexts']}  {r['time']}s ({r['sprites']} sprite)")
        for key in ("misses", "errors", "false", "dimOk", "texts"):
            for line in r[key]:
                print(f"   {key[:5]:5s} {line}")


if __name__ == "__main__":
    main()
