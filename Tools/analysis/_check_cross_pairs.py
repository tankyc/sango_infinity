"""临时脚本：所有「地图 bin × 剧本 citySet」交叉组合，看哪些组合会让截图坐标报错。"""
import json
import struct
from pathlib import Path

import numpy as np

ROOT = Path(r"D:\sango_infinity")
MODS = ROOT / "Build" / "Mods"
SAVES = ROOT / "Build" / "Save"

COORDS = [(593, 510), (599, 515), (490, 484), (854, 515), (851, 512), (367, 392),
          (860, 515), (342, 347), (366, 313), (413, 324), (836, 465), (835, 469),
          (414, 215), (707, 522), (505, 310), (188, 370), (187, 369), (503, 369),
          (455, 487), (522, 365), (361, 301)]


def read_str(f):
    n, sh = 0, 0
    while True:
        v = f.read(1)[0]
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    return f.read(n).decode("utf-8", "replace")


def load_area(bin_path):
    with open(bin_path, "rb") as f:
        struct.unpack("<i", f.read(4))[0]
        work = read_str(f)
        w, h = struct.unpack("<ii", f.read(8))
        struct.unpack("<i", f.read(4))[0]
        cx, cy = w // 4, h // 4
        raw = f.read(cx * cy * 7)
    cell = np.frombuffer(raw, dtype=np.uint8).reshape(cx, cy, 7)
    area = cell[..., 5:7].copy().view(np.uint16).reshape(cx, cy).astype(np.int32)
    return work, cx, cy, area


def main():
    bins = []
    for mod in sorted(p for p in MODS.iterdir() if p.is_dir()):
        for b in (mod / "Map").glob("*.bin") if (mod / "Map").exists() else []:
            bins.append((f"{mod.name}/{b.name}", b))
    if (ROOT / "Build" / "Content" / "Map").exists():
        for b in (ROOT / "Build" / "Content" / "Map").glob("*.bin"):
            bins.append((f"Content/{b.name}", b))

    sets = {}
    for mod in sorted(p for p in MODS.iterdir() if p.is_dir()):
        for js in (mod / "Scenario").glob("*.json") if (mod / "Scenario").exists() else []:
            d = json.loads(js.read_text(encoding="utf-8"))
            sets[f"{mod.name}/{js.name}({d.get('Info', {}).get('mapType')})"] = {
                int(k) for k in d.get("citySet", {}) if int(k) > 0}
    d = json.loads((ROOT / "Build" / "Content" / "Scenario" / "Scenario.json").read_text(encoding="utf-8"))
    sets["Content/Scenario.json"] = {int(k) for k in d.get("citySet", {}) if int(k) > 0}
    for sv in sorted(SAVES.glob("*.json")):
        try:
            d = json.loads(sv.read_text(encoding="utf-8"))
        except Exception:
            continue
        cs = d.get("citySet")
        if isinstance(cs, dict):
            mp = d.get("Map", {}) or {}
            sets[f"Save/{sv.name}({mp.get('Name')})"] = {int(k) for k in cs if int(k) > 0}

    for name, b in bins:
        work, cx, cy, area = load_area(b)
        row = []
        for sname, ids in sets.items():
            bad = 0
            for x, y in COORDS:
                if not (0 <= x < cx and 0 <= y < cy):
                    bad += 1
                    continue
                a = int(area[x, y])
                if a == 0 or a not in ids:
                    bad += 1
            row.append((bad, sname))
        row.sort()
        print(f"=== {name} work={work} {cx}x{cy} ===")
        for bad, sname in row[:6]:
            print(f"   报错 {bad:2d}/21  ←  {sname}")


if __name__ == "__main__":
    main()
