# -*- coding: utf-8 -*-
"""评估「草地(1) -> 森林(6)」的可行性：

1) 逐字段对比 id 1 与 id 6 的地形属性（哪些字段会变、影响什么）；
2) 盘查 8,166 个树模型所在格的上下文：归属(areaId)、状态、与据点/道路/水体的邻接、
   到最近据点的距离 —— 决定「能改」还是「必须删模型」。
"""
import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from sango_scale_bin import parse_bin                        # noqa: E402

import numpy as np

TT_JSON = r"D:\sango_infinity\Build\Content\Data\Common\TerrainTypes.json"
BIN = r"D:\sango_infinity\Build\Mods\nftfwxpx\Map\yyxk_2x.bin"

# ---------- 1) 字段对比 ----------
d = json.load(open(TT_JSON, encoding="utf-8"))["TerrainTypes"]
a, b = d["1"], d["6"]
print("=== 地形属性对比：id 1 草地  ->  id 6 森 ===")
keys = [k for k in a.keys() if k not in ("Id", "Name")]
for k in keys:
    va, vb = a.get(k), b.get(k)
    flag = "" if va == vb else "   <<< 变化"
    print(f"  {k:<18} {str(va):<22} -> {str(vb):<22}{flag}")
print(f"  Name               {a['Name']} -> {b['Name']}")

# ---------- 2) 模型所在格上下文 ----------
info = parse_bin(BIN)
cx, cy = info["cellsX"], info["cellsY"]
G = info["gridRaw"]
tt = G[..., 0].astype(np.int32)
st = G[..., 1].astype(np.int32) | (G[..., 2].astype(np.int32) << 8) | \
     (G[..., 3].astype(np.int32) << 16) | (G[..., 4].astype(np.int32) << 24)
aid = G[..., 5].astype(np.int32) | (G[..., 6].astype(np.int32) << 8)
q = float(info["quadSize"])
models = info["tail"]["models"][2]

CITY = (17, 18, 19)
ROAD = (11, 12, 13, 20)
WATER = (7, 8, 9, 14, 21, 22)

mc = Counter()
for m in models:
    vx, vy = int(round(float(m[4][2]) / q)), int(round(float(m[4][0]) / q))
    cxx, cyy = vx // 4, (vy - ((vx // 4) & 1) * 2) // 4
    if 0 <= cxx < cx and 0 <= cyy < cy:
        mc[(cxx, cyy)] += 1

print(f"\n=== 模型落格 {len(mc)} 个（模型 {len(models)} 个）===")
print("  地形分布:", dict(Counter(int(tt[x, y]) for x, y in mc).most_common()))
print("  状态分布:", dict(Counter(int(st[x, y]) for x, y in mc).most_common(5)))

grass = [(x, y) for (x, y) in mc if tt[x, y] == 1]
print(f"\n  其中落在草地(1)上的格 {len(grass)}")

rad = {}
for name, ids in (("据点", CITY), ("道路", ROAD), ("水体", WATER)):
    mask = np.isin(tt, list(ids))
    near = np.zeros((cx, cy), bool)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            near |= np.roll(np.roll(mask, dx, 0), dy, 1)
    rad[name] = int(sum(1 for (x, y) in grass if near[x, y]))
print("  这些草地里，8 邻域内出现的：", rad)

print("  归属 areaId 分布:", dict(Counter(int(aid[x, y]) for x, y in grass).most_common(6)))
n0 = sum(1 for (x, y) in grass if aid[x, y] == 0)
print(f"    areaId==0（无归属/荒野）{n0} / {len(grass)}")

citymask = np.isin(tt, list(CITY))
ys, xs = np.nonzero(citymask)
print(f"\n  据点格 {len(ys)} 个；到最近据点的格距离（切比雪夫）：")
if len(ys):
    gx = np.array([p[0] for p in grass])
    gy = np.array([p[1] for p in grass])
    dist = np.minimum.reduce([np.maximum(np.abs(gx - x0), np.abs(gy - y0)) for x0, y0 in zip(xs, ys)])
    for k in (1, 2, 3, 5, 8, 12, 20):
        print(f"    <= {k:>2} 格: {int((dist <= k).sum()):>6}")
    print(f"    中位 {np.median(dist):.0f}  最大 {dist.max()}")

# 道路/水体/据点之外，还有哪些既非草地也非森林的格被模型占了
print("\n  模型落格的地形（非 1/6）:", dict(Counter(int(tt[x, y]) for x, y in mc
                                              if tt[x, y] not in (1, 6)).most_common()))
