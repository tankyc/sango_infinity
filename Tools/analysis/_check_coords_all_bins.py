"""临时脚本：把截图里报错的坐标拿到每个 mod 的地图 bin 上核对 areaId，
看哪一份 bin + 剧本 会真的出现「地块没有所属城池」。"""
import json
import struct
from pathlib import Path

import numpy as np

MODS = Path(r"D:\sango_infinity\Build\Mods")

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


def load_cells(bin_path):
    with open(bin_path, "rb") as f:
        struct.unpack("<i", f.read(4))[0]
        read_str(f)
        w, h = struct.unpack("<ii", f.read(8))
        struct.unpack("<i", f.read(4))[0]
        cx, cy = w // 4, h // 4
        raw = f.read(cx * cy * 7)
    cell = np.frombuffer(raw, dtype=np.uint8).reshape(cx, cy, 7)
    area = cell[..., 5:7].copy().view(np.uint16).reshape(cx, cy).astype(np.int32)
    terrain = cell[..., 0].astype(np.int32)
    return cx, cy, area, terrain


def main():
    bins = []
    for mod in sorted(p for p in MODS.iterdir() if p.is_dir()):
        for b in (mod / "Map").glob("*.bin") if (mod / "Map").exists() else []:
            bins.append((mod.name, b))
        for js in (mod / "Scenario").glob("*.json") if (mod / "Scenario").exists() else []:
            pass

    for mod_name, b in bins:
        cx, cy, area, terrain = load_cells(b)
        print(f"=== {mod_name}/{b.name}  格 {cx}x{cy} ===")
        jss = list((b.parent.parent / "Scenario").glob("*.json"))
        for js in jss:
            sd = json.loads(js.read_text(encoding="utf-8"))
            cities = {int(k) for k in sd.get("citySet", {}) if int(k) > 0}
            info = sd.get("Info", {})
            bad = 0
            detail = []
            for x, y in COORDS:
                if not (0 <= x < cx and 0 <= y < cy):
                    detail.append(f"({x},{y})越界")
                    bad += 1
                    continue
                a = int(area[x, y])
                t = int(terrain[x, y])
                ok = a != 0 and a in cities
                if not ok:
                    bad += 1
                detail.append(f"({x},{y})t{t}a{a}{'' if ok else '<==无城'}")
            print(f"  剧本 {js.name} (mapType={info.get('mapType')}, 城 {len(cities)}): "
                  f"21 个坐标里 {bad} 个会报错")
            if bad:
                print("    ", " ".join(detail))


if __name__ == "__main__":
    main()
