# -*- coding: utf-8 -*-
"""lit_world 陆地优先投影（中等强度）。

规则：
  * 中国核心 (73~135E, 18~54N) 权重强制 1.0 → 占据 320x200 格，**完全不变形**；
  * 核心之外按**陆地密度**给权重：陆地放大、海洋压缩（k 越大压得越狠）；
  * 覆盖窗口不预设，由「总宽=900、总高=500」从中国核向两侧扩展自动决定 ——
    海被压掉，框内自然只剩陆地密集区。
输出各强度下的覆盖范围与画面陆地占比，并渲染选中强度的一版预览。
"""
from pathlib import Path

import numpy as np
from PIL import Image

ETOPO = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
OUT = Path(r"D:\sango_infinity\Temp")
CN_LON, CN_LAT = (73.0, 135.0), (18.0, 54.0)
NX, NY = 900, 500
SC_LON = 320.0 / 62.0        # 5.161 格/度
SC_LAT = 200.0 / 36.0        # 5.556 格/度

SLON = np.arange(-70.0, 200.0, 0.5)
SLAT = np.arange(-45.0, 86.0, 0.5)          # 索引递增 = 由南向北
dem = np.load(ETOPO, mmap_mode="r")
rr = np.clip(((90.0 - SLAT) * 60).astype(int), 0, dem.shape[0] - 1)
cc = np.clip(((SLON + 180.0) * 60).astype(int), 0, dem.shape[1] - 1)
E = np.array(dem[np.ix_(rr, cc)], np.float32)            # (nlat, nlon)
land = E > 0

cn_c = (SLON >= CN_LON[0]) & (SLON <= CN_LON[1])
cn_r = (SLAT >= CN_LAT[0]) & (SLAT <= CN_LAT[1])
Lx, Ly = land.mean(0), land.mean(1)
mx = Lx[~cn_c].mean(); my = Ly[~cn_r].mean()
print(f"源窗 {SLON.size}x{SLAT.size}  非中国区平均陆地占比: 经向 {mx:.3f}  纬向 {my:.3f}")


def build(k):
    wx = 1.0 + k * (Lx - mx)
    wy = 1.0 + k * (Ly - my)
    wx[cn_c] = 1.0
    wy[cn_r] = 1.0
    wx = np.clip(wx, 0.12, None)
    wy = np.clip(wy, 0.12, None)
    dx = wx * SC_LON * 0.5                              # 每 0.5 度占的列数
    dy = wy * SC_LAT * 0.5

    def grow(dw, need, c0, c1):
        lo, hi = c0, c1
        tot = dw[lo:hi + 1].sum()
        while tot < need and (lo > 0 or hi + 1 < dw.size):
            if lo > 0:
                lo -= 1; tot += dw[lo]
            if tot < need and hi + 1 < dw.size:
                hi += 1; tot += dw[hi]
        return lo, hi, tot

    ci = np.nonzero(cn_c)[0]
    lo, hi, tot_x = grow(dx, NX, ci[0], ci[-1])
    ri = np.nonzero(cn_r)[0]
    r0, r1, tot_y = grow(dy, NY, ri[0], ri[-1])
    sub = land[r0:r1 + 1, lo:hi + 1]
    return dict(k=k, lo=lo, hi=hi, r0=r0, r1=r1, share=float(sub.mean()),
                lon=(float(SLON[lo]), float(SLON[hi])),
                lat=(float(SLAT[r0]), float(SLAT[r1])),
                dx=dx, dy=dy, tot_x=tot_x, tot_y=tot_y)


print("\n k     覆盖经度            覆盖纬度            画面陆地占比   中国核列/行")
cands = []
for k in (0.0, 0.6, 1.0, 1.4, 2.0, 2.8):
    r = build(k)
    cands.append(r)
    print(f" {k:<4}  {r['lon'][0]:7.1f}~{r['lon'][1]:<7.1f}  "
          f"{r['lat'][0]:7.1f}~{r['lat'][1]:<7.1f}   {r['share']:.3f}        "
          f"{r['dx'][np.nonzero(cn_c)[0]].sum():.0f}/{r['dy'][np.nonzero(cn_r)[0]].sum():.0f}")

pick = min(cands, key=lambda r: abs(r["share"] - 0.75))
print(f"\n选中 k={pick['k']}（画面陆地占比 {pick['share']:.3f}，最接近中等 ~75%）")
print(f"  覆盖经度 {pick['lon'][0]:.1f}~{pick['lon'][1]:.1f}"
      f"   纬度 {pick['lat'][0]:.1f}~{pick['lat'][1]:.1f}")

# ---- 渲染预览 ----
ux = np.concatenate([[0], np.cumsum(pick["dx"][pick["lo"]:pick["hi"] + 1])])
ux = ux / ux[-1] * NX
uy = np.concatenate([[0], np.cumsum(pick["dy"][pick["r0"]:pick["r1"] + 1])])
uy = uy / uy[-1] * NY
cols = np.clip(np.interp(np.arange(NX) + 0.5, ux, np.arange(pick["hi"] - pick["lo"] + 1)),
               0, pick["hi"] - pick["lo"]).astype(int)
# 行：源索引递增=向北；图上第 0 行=最北 → 用反向累积
rows = np.clip(np.interp(NY - (np.arange(NY) + 0.5), uy,
                         np.arange(pick["r1"] - pick["r0"] + 1)),
               0, pick["r1"] - pick["r0"]).astype(int)
sub_land = land[pick["r0"]:pick["r1"] + 1, pick["lo"]:pick["hi"] + 1]
sub_e = E[pick["r0"]:pick["r1"] + 1, pick["lo"]:pick["hi"] + 1]
sel = sub_land[np.ix_(rows, cols)]
sel_e = sub_e[np.ix_(rows, cols)]
img = np.zeros((NY, NX, 3), np.uint8)
img[~sel] = (58, 96, 138)
h = np.clip((sel_e - 0) / 4000.0, 0, 1)
img[sel] = (np.stack([120 + h * 110, 140 + h * 90, 90 + h * 110], -1)[sel]).astype(np.uint8)
im = Image.fromarray(img)
lo_col = int((CN_LON[0] - pick["lon"][0]) / (pick["lon"][1] - pick["lon"][0]) * NX)
hi_col = int((CN_LON[1] - pick["lon"][0]) / (pick["lon"][1] - pick["lon"][0]) * NX)
hi_row = int((pick["lat"][1] - CN_LAT[1]) / (pick["lat"][1] - pick["lat"][0]) * NY)
lo_row = int((pick["lat"][1] - CN_LAT[0]) / (pick["lat"][1] - pick["lat"][0]) * NY)
im.save(OUT / "lit_landbias.jpg", quality=90)
print(f"已写出 {OUT / 'lit_landbias.jpg'}（中国核心约在 列 {lo_col}~{hi_col} 行 {hi_row}~{lo_row}）")
