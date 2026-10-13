# -*- coding: utf-8 -*-
"""一次性：用 DefaultMap.bin 的 2352 棵官方树，校准「世界坐标 <-> 格坐标」公式。

候选（gridSize=g=20, quadSize=5）：
  A: wz = col*g + g/2 ;  wx = row*g + (col&1)*g/2 + g/2      （sango_scale_bin 的写法）
  B: wz = col*g + g/2 ;  wx = row*g - (col&1)*g/2 + g/2
  C: wz = col*g       ;  wx = row*g + (col&1)*g/2
"""
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(".").resolve()))
from sango_scale_bin import parse_bin          # noqa: E402

g = 20.0


def cand_A(wz, wx):
    col = np.floor((wz - g * 0.5) / g).astype(np.int64)
    row = np.floor((wx - g * 0.5 - (col & 1) * g * 0.5) / g).astype(np.int64)
    return col, row, (col * g + g * 0.5, row * g + (col & 1) * g * 0.5 + g * 0.5)


def cand_B(wz, wx):
    col = np.floor((wz - g * 0.5) / g).astype(np.int64)
    row = np.floor((wx - g * 0.5 + (col & 1) * g * 0.5) / g).astype(np.int64)
    return col, row, (col * g + g * 0.5, row * g - (col & 1) * g * 0.5 + g * 0.5)


def cand_C(wz, wx):
    col = np.floor(wz / g).astype(np.int64)
    row = np.floor((wx - (col & 1) * g * 0.5) / g).astype(np.int64)
    return col, row, (col * g, row * g + (col & 1) * g * 0.5)


p = Path(r"d:\sango_infinity\Build\Content\Map\DefaultMap.bin")
d = parse_bin(p)
cx, cy = d["cellsX"], d["cellsY"]
tt = d["gridRaw"][..., 0]
models = d["tail"]["models"][2]
trees = [m for m in models if 5 <= m[3] <= 14]
pos = np.array([m[4] for m in trees], float)
wz, wx = pos[:, 2], pos[:, 0]
print(f"DefaultMap {cx}x{cy}  树 {len(trees)} 棵")

for name, fn in (("A (+lean)", cand_A), ("B (-lean)", cand_B), ("C (no +g/2)", cand_C)):
    col, row, (ccz, ccx) = fn(wz, wx)
    ok = (col >= 0) & (col < cx) & (row >= 0) & (row < cy)
    terrain = np.full(len(trees), -1)
    terrain[ok] = tt[col[ok], row[ok]]
    n6 = int((terrain == 6).sum())
    # 到格中心的残差（格为单位）
    dz = (wz - ccz) / g
    dx = (wx - ccx) / g
    print(f"\n{name}: 出界 {int((~ok).sum())}  落在 森(6) 的 {n6}/{len(trees)} = {n6/len(trees):.3%}")
    print(f"   残差(格) z: p10={np.percentile(dz,10):+.3f} p50={np.median(dz):+.3f} p90={np.percentile(dz,90):+.3f}")
    print(f"   残差(格) x: p10={np.percentile(dx,10):+.3f} p50={np.median(dx):+.3f} p90={np.percentile(dx,90):+.3f}")
    # 残差的小数部分分布（看是否有系统性半格偏移）
    fz = np.round((dz - np.floor(dz)) * 4) / 4
    fx = np.round((dx - np.floor(dx)) * 4) / 4
    import collections
    print(f"   小数部分 z: {dict(sorted(collections.Counter(fz.tolist()).items()))}")
    print(f"   小数部分 x: {dict(sorted(collections.Counter(fx.tolist()).items()))}")
