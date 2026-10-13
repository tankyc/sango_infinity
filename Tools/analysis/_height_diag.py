# -*- coding: utf-8 -*-
"""一次性：诊断最高档（真实 >3000m）为什么没抬起来。

逐点看：格 DEM 高程 / 该格中心顶点的插值高程 / 曲线目标 / 文件里实际写入的高度。
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402
from sango_height_fix import CURVE, elev_to_byte   # noqa: E402

import numpy as np
from PIL import Image

M = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Map")
cur = parse_bin(str(M / "yyxk_2x.bin"))
bak = parse_bin(str(M / "yyxk_2x.bin.bak_before_height"))
cx, cy = cur["cellsX"], cur["cellsY"]
tt = cur["gridRaw"][..., 0].astype(int)
H1 = cur["verts"][..., 0].astype(np.float32)
H0 = bak["verts"][..., 0].astype(np.float32)

latc, lonc = (0.003708, -0.039995, 41.9144), (0.049694, 0.002809, 99.4265)
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
el = slab[rows - r0, cols - c0]                      # (cx, cy)

VY, VX = H0.shape
up = np.asarray(Image.fromarray(el.T.astype(np.float32)).resize((VX, VY), Image.Resampling.BILINEAR),
                np.float32)
tgt = elev_to_byte(up, CURVE)

sel = np.argwhere((el > 3000) & ~np.isin(tt, [7, 8, 9, 14, 21, 22]))
print(f"真实高程 >3000m 的陆地格 {len(sel)} 个；抽查 8 个：")
print(f"{'格(x,y)':<14}{'格DEM':>7}{'顶点插值':>9}{'曲线目标':>9}{'原高度':>8}{'写入高度':>9}")
for x, y in sel[:: max(1, len(sel) // 8)][:8]:
    vx, vy = 4 * int(x) + 2, 4 * int(y) + (int(x) & 1) * 2 + 2
    print(f"({int(x):>3},{int(y):>3})   {el[int(x),int(y)]:>7.0f}{up[vy,vx]:>9.0f}"
          f"{tgt[vy,vx]:>9.0f}{H0[vy,vx]:>8.0f}{H1[vy,vx]:>9.0f}")

print(f"\n顶点插值场 up: 中位 {np.median(up):.0f}  max {up.max():.0f}")
print(f"曲线目标 tgt: 中位 {np.median(tgt):.0f}  max {tgt.max():.0f}")
print(f"写入高度 H1: 中位 {np.median(H1):.0f}  max {H1.max():.0f}")
# 检查 tgt 与 H1 的差异分布（排除保护带：这里只按「water>H」近似）
W0 = bak["verts"][..., 1].astype(np.float32)
vis = W0 > H0
m = ~vis
d = H1 - tgt
print(f"\n非可见水面顶点上 (H1 - tgt): 中位 {np.median(d[m]):.0f}  "
      f"|d|<=1 的占比 {float((np.abs(d[m]) <= 1).mean()):.3f}  "
      f"|d|>20 的占比 {float((np.abs(d[m]) > 20).mean()):.3f}")
big = m & (np.abs(d) > 20)
ys_, xs_ = np.nonzero(big)
print(f"|d|>20 的顶点 {int(big.sum())} 个；它们所在格的 DEM 中位 {np.median(el[np.clip(xs_//4,0,cx-1), np.clip(ys_//4,0,cy-1)]):.0f} m")
print(f"   它们所在格的地形分布(前6): "
      f"{dict(sorted(__import__('collections').Counter(tt[np.clip(xs_//4,0,cx-1), np.clip(ys_//4,0,cy-1)].tolist()).items(), key=lambda kv:-kv[1])[:6])}")
