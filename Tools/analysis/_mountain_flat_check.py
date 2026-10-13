# -*- coding: utf-8 -*-
"""量测崖（不可行走山体）区的高度平整度与安全边界，为「做出起伏」定参数。"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from sango_scale_bin import parse_bin                      # noqa: E402
from sango_mountain_beautify import vertex_cell_index      # noqa: E402

import numpy as np

BIN = r"D:\sango_infinity\Build\Mods\nftfwxpx\Map\yyxk_2x.bin"
d = parse_bin(BIN)
cx, cy = d["cellsX"], d["cellsY"]
tt = d["gridRaw"][..., 0].astype(np.int32)
V = d["verts"]
VX, VY = V.shape[0], V.shape[1]
H = V[..., 0].astype(np.int32)
TI = V[..., 1].astype(int)
WB = V[..., 2].astype(int)
cxx, cyy, ok = vertex_cell_index(VX, VY, cx, cy)
ct = np.where(ok, tt[cxx, cyy], -1)
cliff = ok & (ct == 16)

print(f"顶点 {VX}x{VY}   崖顶点 {int(cliff.sum())} ({cliff.mean():.3f})")
print("\n--- 崖顶点高度分布（越集中越平）---")
h = H[cliff]
print(f"  中位 {np.median(h):.0f}  均值 {h.mean():.1f}  标准差 {h.std():.1f}"
      f"  p5 {np.percentile(h,5):.0f}  p95 {np.percentile(h,95):.0f}"
      f"  min {h.min()} max {h.max()}")
u, c = np.unique(h, return_counts=True)
o = np.argsort(-c)[:6]
print("  最常见高度: " + ", ".join(f"{u[i]}({100*c[i]/h.size:.1f}%)" for i in o))
print(f"  唯一高度值 {len(u)} 个")

print("\n--- 各主要地形顶点高度 ---")
for t in (1, 2, 3, 6, 9, 12, 15, 16, 17, 18, 19, 32):
    m = ok & (ct == t)
    if m.sum() < 100:
        continue
    print(f"  id {t:<3d} 顶点 {int(m.sum()):>8d}  中位 {np.median(H[m]):4.0f}"
          f"  p95 {np.percentile(H[m],95):4.0f}  max {H[m].max():4d}")

walk = ok & (ct > 0) & ~np.isin(ct, [0, 7, 15, 16, 21, 22])
print("\n--- 可走地表高度（山体地板线参考）---")
print(f"  可走顶点 {int(walk.sum())}  中位 {np.median(H[walk]):.0f}"
      f"  p95 {np.percentile(H[walk],95):.0f}  p99 {np.percentile(H[walk],99):.0f}"
      f"  max {H[walk].max()}")

print("\n--- 水面字节 water(ch2) ---")
print(f"  全图 中位 {np.median(WB):.0f} max {WB.max()}   崖区 中位 {np.median(WB[cliff]):.0f}"
      f" max {WB[cliff].max()}")
print(f"  当前可见水面顶点 (water>height): {int((WB > H).sum())}")

u, c = np.unique(TI[cliff], return_counts=True)
o = np.argsort(-c)[:6]
print("\n--- 崖区 textureIndex top6 ---")
print("  " + ", ".join(f"{u[i]}({100*c[i]/c.sum():.1f}%)" for i in o))

# 崖区「安全顶点」规模：±4 顶点邻域都在崖内者，才允许改高度（保证不动可走格）
k = 4
m = cliff
for s in range(1, k + 1):
    m = (m & np.roll(cliff, s, 0) & np.roll(cliff, -s, 0)
         & np.roll(cliff, s, 1) & np.roll(cliff, -s, 1))
print(f"\n--- 可改高度的「安全崖顶点」（±{k} 顶点邻域全在崖内）---")
print(f"  {int(m.sum())} / {int(cliff.sum())} = {m.sum()/max(1,cliff.sum()):.3f}")
hh = H[m]
print(f"  这些顶点的高度: 中位 {np.median(hh):.0f} 标准差 {hh.std():.1f}"
      f" p5 {np.percentile(hh,5):.0f} p95 {np.percentile(hh,95):.0f}")
