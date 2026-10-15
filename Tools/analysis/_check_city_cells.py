"""临时脚本：核对 xworld_full.bin 的 areaId 与 Scenario_world_full.json 城池是否一一对应。

检查项：
  1) 每座城的中心格 (city.x, city.y) 的 areaId 是否等于该城 Id；
  2) 每座城在地图上占多少格（有没有 0 格的城）；
  3) 地图上出现的 areaId 是否都在剧本城池表里。
"""
import json
import struct
import sys
from pathlib import Path

import numpy as np


def read_str(f):
    n, sh = 0, 0
    while True:
        v = f.read(1)[0]
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    return f.read(n).decode("utf-8", "replace")


def main():
    bin_path = Path(sys.argv[1])
    scen_path = Path(sys.argv[2])
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

    sd = json.loads(scen_path.read_text(encoding="utf-8"))
    cities = {int(k): v for k, v in sd["citySet"].items() if int(k) > 0}

    counts = {i: int((area == i).sum()) for i in cities}
    zero_cells = [i for i, c in counts.items() if c == 0]
    print(f"城池 {len(cities)} 座；地图上没有占到任何格的城 {len(zero_cells)} 座: {zero_cells[:20]}")

    mismatch = []
    out_of_range = []
    for i, v in cities.items():
        x, y = int(v.get("x", -1)), int(v.get("y", -1))
        if not (0 <= x < cx and 0 <= y < cy):
            out_of_range.append((i, x, y))
            continue
        a = int(area[x, y])
        if a != i:
            mismatch.append((i, v.get("Name"), x, y, a))
    print(f"中心格越界的城 {len(out_of_range)}: {out_of_range[:10]}")
    print(f"中心格 areaId != 城 Id 的城 {len(mismatch)}: {mismatch[:20]}")

    below = {i: c for i, c in counts.items() if 0 < c < 5}
    print(f"占格 <5 的城 {len(below)}: {list(below.items())[:20]}")

    # 空城（BelongForce=0）的领地统计
    empty = [i for i, v in cities.items() if int(v.get("BelongForce", 0)) == 0]
    owned = [i for i in cities if i not in set(empty)]
    e_cells = sum(counts[i] for i in empty)
    print(f"空城 {len(empty)} 座，占格 {e_cells}（{e_cells / area.size * 100:.1f}%）；"
          f"有主城 {len(owned)} 座，占格 {area.size - e_cells - int((area == 0).sum())}")
    uniq, cnt = np.unique(terrain[area == 0], return_counts=True)
    print("areaId=0 的地形分布:", dict(zip(uniq.tolist(), cnt.tolist())))
    ys, xs = np.where(area == 0)
    print(f"areaId=0 的坐标范围 x {xs.min()}~{xs.max()} y {ys.min()}~{ys.max()}")


if __name__ == "__main__":
    main()
