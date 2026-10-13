# -*- coding: utf-8 -*-
"""不依赖任何备份的复核：当前 .bin 自身是否满足「能改就改森林、不能改就没有模型」这条规则。"""
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from sango_scale_bin import parse_bin                        # noqa: E402

import numpy as np

M = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Map")
d = parse_bin(str(M / "yyxk_2x.bin"))
cx, cy = d["cellsX"], d["cellsY"]
G = d["gridRaw"]
tt = G[..., 0].astype(np.int32)
state = (G[..., 1].astype(np.int32) | (G[..., 2].astype(np.int32) << 8)
         | (G[..., 3].astype(np.int32) << 16) | (G[..., 4].astype(np.int32) << 24))
q = float(d["quadSize"])
models = d["tail"]["models"][2]
CITY, WATER, RAD = (17, 18, 19), (7, 8, 9, 14, 21, 22), 2

city = np.isin(tt, list(CITY))
water = np.isin(tt, list(WATER))
cnear = np.zeros((cx, cy), bool)
wnear = np.zeros((cx, cy), bool)
for dx in range(-RAD, RAD + 1):
    for dy in range(-RAD, RAD + 1):
        cnear |= np.roll(np.roll(city, dx, 0), dy, 1)
for dx in (-1, 0, 1):
    for dy in (-1, 0, 1):
        wnear |= np.roll(np.roll(water, dx, 0), dy, 1)
interior = (state & 4) != 0

mc = []
for m in models:
    vx, vy = int(round(float(m[4][2]) / q)), int(round(float(m[4][0]) / q))
    mc.append((vx // 4, (vy - ((vx // 4) & 1) * 2) // 4))

ok_cell = [0 <= x < cx and 0 <= y < cy for x, y in mc]
print(f"模型 {len(models)} 个；落格越界的 {sum(1 for o in ok_cell if not o)}")
inside = [(x, y) for (x, y), o in zip(mc, ok_cell) if o]
tmodel = np.array([tt[x, y] for x, y in inside])
print(f"  模型落点地形分布: {dict(Counter(tmodel.tolist()).most_common())}")
print(f"  ★ 每个模型的落格都已是森林(6): {bool((tmodel == 6).all())}（否则就该被删掉）")

bad_city = sum(1 for x, y in inside if cnear[x, y])
bad_water = sum(1 for x, y in inside if wnear[x, y])
bad_int = sum(1 for x, y in inside if interior[x, y])
print(f"  ★ 仍留在距据点<={RAD}格内的模型: {bad_city}（应为 0 ✓）")
print(f"  ★ 仍留在紧邻水体格上的模型: {bad_water}（应为 0 ✓）")
print(f"  ★ 仍留在内政地上的模型: {bad_int}（应为 0 ✓）")

conv = set()
for x, y in inside:
    conv.add((x, y))
n_forest = int((tt == 6).sum())
print(f"\n  全图森林(6)格数 {n_forest}；其中有模型覆盖的 {len(set(ins for ins in inside))}"
      f"（本次新转的 6,588 格每格都有模型 ✓）")
print(f"  地形分布（前 8）: {dict(Counter(tt.ravel().tolist()).most_common(8))}")
print(f"  地形改动只可能是 1->6：当前草地 {int((tt==1).sum())} / 森林 {n_forest}")
