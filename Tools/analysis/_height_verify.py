# -*- coding: utf-8 -*-
"""一次性：核验高度修正的实际结果（与备份逐字节比）。"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np

M = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Map")
cur = parse_bin(str(M / "yyxk_2x.bin"))
bak = parse_bin(str(M / "yyxk_2x.bin.bak_before_height"))

g1, g0 = cur["gridRaw"], bak["gridRaw"]
print(f"地形/状态/归属 未动: {bool(np.array_equal(g1, g0))}")
V1, V0 = cur["verts"], bak["verts"]
print(f"顶点数组形状 {V0.shape} -> {V1.shape}")
for i in range(V0.shape[2]):
    d = np.abs(V1[..., i].astype(np.int32) - V0[..., i].astype(np.int32))
    same = int((d == 0).sum())
    print(f"  通道{i}: 改动的顶点 {int((d>0).sum()):>8}  未动 {same:>8}  "
          f"最大差 {int(d.max())}  原中位 {int(np.median(V0[..., i]))} -> 新中位 {int(np.median(V1[..., i]))}")

H0 = V0[..., 0].astype(np.float32)
H1 = V1[..., 0].astype(np.float32)
W0 = V0[..., 1].astype(np.float32)
print(f"\n水面可见顶点(water>height): {int((W0 > H0).sum())} -> {int((W0 > H1).sum())}（应完全相同）")
print(f"water 字节是否被动: "
      f"{'是 ✗' if not np.array_equal(V1[..., 1], V0[..., 1]) else '否 ✓'}")
print(f"贴图通道是否被动: "
      f"{'是 ✗' if not np.array_equal(V1[..., 2], V0[..., 2]) else '否 ✓'}")
prot = (W0 > H0)
m = ~prot
print(f"\n陆地(非保护)顶点高度: 原 中位 {np.median(H0[m]):.0f} p90 {np.percentile(H0[m],90):.0f}"
      f"  ->  新 中位 {np.median(H1[m]):.0f} p90 {np.percentile(H1[m],90):.0f}")
print(f"保护顶点是否原样: {bool(np.array_equal(H1[prot], H0[prot]))}")
# 与真实高程的相关
latc, lonc = (0.003708, -0.039995, 41.9144), (0.049694, 0.002809, 99.4265)
cx, cy = cur["cellsX"], cur["cellsY"]
xs = np.arange(cx, dtype=np.float64)[:, None]
ys = np.arange(cy, dtype=np.float64)[None, :]
lat = latc[0] * xs + latc[1] * ys + latc[2]
lon = lonc[0] * xs + lonc[1] * ys + lonc[2]
dem = np.load(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy",
              mmap_mode="r")
rows = np.clip(((90.0 - lat) * 60.0).astype(np.int32), 1, dem.shape[0] - 2)
cols = np.clip(((lon + 180.0) * 60.0).astype(np.int32), 1, dem.shape[1] - 2)
r0, c0 = int(rows.min()) - 1, int(cols.min()) - 1
slab = np.array(dem[r0:rows.max() + 2, c0:cols.max() + 2], np.float32)
el = slab[rows - r0, cols - c0]
h0 = H0[2::4, 2::4][:cx, :cy]
h1 = H1[2::4, 2::4][:cx, :cy]
tt = g1[..., 0].astype(int)
land = ~np.isin(tt, [7, 8, 9, 14, 21, 22])
print(f"\n陆地格中心高度 vs 真实高程 相关: 原 {np.corrcoef(h0[land], el[land])[0,1]:+.3f}"
      f"  ->  新 {np.corrcoef(h1[land], el[land])[0,1]:+.3f}")
for lo, hi, nm in ((0, 100, "0~100m 平原"), (100, 300, "100~300m"), (300, 800, "300~800m 丘陵"),
                   (800, 1500, "800~1500m"), (1500, 3000, "1500~3000m 山地"), (3000, 9999, ">3000m")):
    sel = land & (el >= lo) & (el < hi)
    if sel.sum() > 50:
        print(f"  真实高程 {nm:<14} {int(sel.sum()):>6} 格: 地图高度 原中位 {np.median(h0[sel]):.0f}"
              f" -> 新中位 {np.median(h1[sel]):.0f}")
