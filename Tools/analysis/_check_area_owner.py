"""临时排查脚本：检查 xworld_full.bin 中「地块没有所属城池」的格子。

用法: python _check_area_owner.py <bin> <scenario.json>
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
        ver = struct.unpack("<i", f.read(4))[0]
        work = read_str(f)
        w, h = struct.unpack("<ii", f.read(8))
        gs = struct.unpack("<i", f.read(4))[0]
        cx, cy = w // 4, h // 4
        print(f"版本 {ver} work={work} {w}x{h}px gridSize={gs} 格 {cx}x{cy}")
        raw = f.read(cx * cy * 7)

    cell = np.frombuffer(raw, dtype=np.uint8).reshape(cx, cy, 7)
    terrain = cell[..., 0].astype(np.int32)
    state = cell[..., 1:5].copy().view(np.int32).reshape(cx, cy)
    area = cell[..., 5:7].copy().view(np.uint16).reshape(cx, cy).astype(np.int32)

    sd = json.loads(scen_path.read_text(encoding="utf-8"))
    cities = {int(k): v for k, v in sd.get("citySet", {}).items() if int(k) > 0}
    print(f"剧本城池数 {len(cities)}，id 范围 {min(cities)}~{max(cities)}")

    used = np.unique(area)
    print(f"地图中出现的 areaId {len(used)} 个，范围 {used.min()}~{used.max()}")
    bad_ids = sorted(int(i) for i in used if i != 0 and i not in cities)
    print(f"areaId 在剧本中查不到城市的有 {len(bad_ids)} 个: {bad_ids[:40]}")
    unused_cities = sorted(i for i in cities if i not in set(used.tolist()))
    print(f"剧本中有但地图未使用的城 {len(unused_cities)} 个: {unused_cities[:40]}")

    zero = area == 0
    print(f"areaId=0 的格 {int(zero.sum())} / {area.size}")
    by_terrain = np.unique(terrain[zero], return_counts=True)
    print("  areaId=0 按地形统计:", dict(zip(by_terrain[0].tolist(), by_terrain[1].tolist())))

    # 截图里报错的坐标
    samples = [(593, 510), (599, 515), (490, 484), (854, 515), (851, 512), (367, 392),
               (860, 515), (342, 347), (366, 313), (413, 324), (836, 465), (835, 469),
               (414, 215), (707, 522), (505, 310), (188, 370), (187, 369), (503, 369),
               (455, 487), (522, 365), (361, 301)]
    print("截图坐标核对 (terrain, state, areaId, 是否在剧本城池中):")
    for x, y in samples:
        if 0 <= x < cx and 0 <= y < cy:
            a = int(area[x, y])
            print(f"  ({x},{y}) terrain={int(terrain[x, y])} state={int(state[x, y])} "
                  f"areaId={a} 城池={'有' if a in cities else '无'}")
        else:
            print(f"  ({x},{y}) 越界(cx={cx},cy={cy})")


if __name__ == "__main__":
    main()
