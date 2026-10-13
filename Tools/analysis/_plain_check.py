# -*- coding: utf-8 -*-
"""盘查「大片平地」：各地形的格数/高度平整度、内政地与据点的分布、模型落在哪些格。"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from sango_scale_bin import parse_bin                       # noqa: E402

import numpy as np

BIN = r"D:\sango_infinity\Build\Mods\nftfwxpx\Map\yyxk_2x.bin"
d = parse_bin(BIN)
cx, cy = d["cellsX"], d["cellsY"]
G = d["gridRaw"]
tt = G[..., 0].astype(np.int32)
st = G[..., 2].astype(np.int32) | (G[..., 3].astype(np.int32) << 8)   # terrainState int32
V = d["verts"]
VX, VY = V.shape[0], V.shape[1]
H = V[..., 0].astype(np.int32)
quest = float(d["quadSize"])

print(f"格 {cx}x{cy}   顶点 {VX}x{VY}")
print("\n=== 地格地形分布（格数 / 占比 / 该地形内顶点高度 中位·p95·唯一值数）===")
for t in sorted(np.unique(tt).tolist()):
    m = tt == t
    n = int(m.sum())
    if n < 200:
        continue
    xs, ys = np.nonzero(m)
    vs = H[4 * xs, 4 * ys + (xs & 1) * 2]
    print(f"  id {t:<3d} 格 {n:>7d} ({n/(cx*cy):.4f})   顶点高度 中位 {np.median(vs):4.0f}"
          f"  p95 {np.percentile(vs,95):4.0f}  唯一值 {len(np.unique(vs))}")

print("\n=== terrainState 分布 ===")
u, c = np.unique(st, return_counts=True)
for v, n in zip(u.tolist(), c.tolist()):
    if n > 100:
        print(f"  state {v:<3d} 格 {n:>7d}")

print("\n=== 据点格（都市17/关19/港18 由剧本另定，这里看地形）===")
for t in (17, 18, 19):
    print(f"  地形 {t}: {int((tt==t).sum())} 格")

print("\n=== 模型落点 ===")
ms = d["tail"]["models"][2]
q = quest
cells = {}
for m in ms:
    vx, vy = int(round(float(m[4][2]) / q)), int(round(float(m[4][0]) / q))
    cxx, cyy = vx // 4, (vy - ((vx // 4) & 1) * 2) // 4
    if 0 <= cxx < cx and 0 <= cyy < cy:
        key = (cxx, cyy)
        cells[key] = cells.get(key, 0) + 1
print(f"  模型 {len(ms)} 个，落在 {len(cells)} 个不同格上")
from collections import Counter
cnt = Counter(int(tt[x, y]) for x, y in cells)
print("  这些格的地形分布:", dict(cnt.most_common(8)))
stc = Counter(int(st[x, y]) for x, y in cells)
print("  这些格的状态分布:", dict(stc.most_common(8)))
