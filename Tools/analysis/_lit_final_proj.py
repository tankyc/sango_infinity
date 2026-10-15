# -*- coding: utf-8 -*-
"""lit_world 最终投影（中等强度 / 只裁东南亚）。

固定窗口 lon -15~160, lat -13~77 → 900x500 格。
  * 中国核 (73~135E, 18~54N) 权重强制 1.0 → 恰好 320x200 格，不变形（经、纬各自二分求解归一）；
  * 核外权重 = 1 + k*(陆地密度 - 均值)：陆地放大、海洋压缩；
  * 东南亚削除：经度带 95~142 内、纬度 < 17 的部分权重 x0.25（把南海/印尼海压成窄条），
    该带纬度方向单独归一，中国（>=18N）不受影响。
"""
from pathlib import Path

import numpy as np
from PIL import Image

ETOPO = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
OUT = Path(r"D:\sango_infinity\Temp")
NX, NY = 900, 500
WLON, WLAT = (-15.0, 160.0), (-13.0, 77.0)
CN = ((73.0, 135.0), (18.0, 54.0))
BOX_LON, BOX_LAT = (95.0, 142.0), (-13.0, 17.0)      # 东南亚削除区
K = 1.0                                              # 中等强度

lon = np.arange(WLON[0] + 0.25, WLON[1], 0.5)
lat = np.arange(WLAT[0] + 0.25, WLAT[1], 0.5)
dem = np.load(ETOPO, mmap_mode="r")
rr = np.clip(((90.0 - lat) * 60).astype(int), 0, dem.shape[0] - 1)
cc = np.clip(((lon + 180.0) * 60).astype(int), 0, dem.shape[1] - 1)
land = np.array(dem[np.ix_(rr, cc)], np.float32) > 0            # (nlat, nlon)
Lx, Ly = land.mean(0), land.mean(1)
in_c = (lon >= CN[0][0]) & (lon <= CN[0][1])
in_r = (lat >= CN[1][0]) & (lat <= CN[1][1])
mx, my = Lx[~in_c].mean(), Ly[~in_r].mean()
print(f"窗口 {lon.size}x{lat.size}  核外平均陆地 经向 {mx:.3f} 纬向 {my:.3f}")

# ---- 经度：解 scale_s 使中国核占 320 列 ----
wl = 1.0 + K * (Lx - mx)
wl[in_c] = 1.0


def share_core(w, step, core, n_out, scale_out=True):
    for s in np.linspace(0.2, 3.0, 4000):
        ww = w.copy()
        ww[~core] = np.clip(ww[~core], 0.05, None) * (s if scale_out else 1.0)
        u = np.cumsum(ww) * step
        got = u[core][-1] - u[core][0] + ww[core][-1] * step
        if got / u[-1] * n_out <= 320.0:
            return ww, u
    return ww, u


# 经度带（用于纬度分带）
band_w = lon < BOX_LON[0]
band_b = (lon >= BOX_LON[0]) & (lon <= BOX_LON[1])
band_e = lon > BOX_LON[1]
band_of = np.where(band_w, 0, np.where(band_b, 1, 2))

# ---- 纬度：每带单独权重（东南亚带把 <17N 压缩），每带归一，且中国核 = 200 行 ----
wl_ = 1.0 + K * (Ly - my)
wl_[in_r] = 1.0
wl_box = wl_.copy()
box_r = (lat >= BOX_LAT[0]) & (lat <= BOX_LAT[1])
wl_box[box_r & ~in_r] *= 0.25
wl_box[in_r] = 1.0

# 逐带解归一（让该带 500 行里中国核占 200）
def solve_band(wb, target_core_rows):
    for s in np.linspace(0.2, 3.0, 4000):
        ww = wb.copy()
        ww[~in_r] = np.clip(ww[~in_r], 0.05, None) * s
        u = np.cumsum(ww) * 0.5
        got = u[in_r][-1] - u[in_r][0] + ww[in_r][-1] * 0.5
        if got / u[-1] * NY <= target_core_rows:
            return ww, u
    return ww, u


w_lat_band = {}
for b, wb in ((0, wl_), (1, wl_box), (2, wl_)):
    ww, u = solve_band(wb, 200.0)
    w_lat_band[b] = (ww, u)
    print(f"  纬带 {b}（lon {['<95','95~142','>142'][b]}）"
          f"  权重均值 {ww.mean():.3f}  核内行数 "
          f"{(u[in_r][-1] - u[in_r][0]) / u[-1] * NY:.0f}")

# 经度归一（让中国核 320 列）
res = share_core(wl, 0.5, in_c, NX)
w_lon, u_lon = res

# ---- 采样到 900x500 ----
tgt_lon = np.interp(np.arange(NX) + 0.5, u_lon / u_lon[-1] * NX, lon)
tgt_col = np.clip(((tgt_lon - lon[0]) / 0.5).astype(int), 0, lon.size - 1)
tgt_lat = np.zeros((NY, NX), np.float32)
for b in (0, 1, 2):
    ww, u = w_lat_band[b]
    idx = np.nonzero(band_of == b)[0]
    if idx.size == 0:
        continue
    uu = u / u[-1] * NY
    la = np.interp(NY - (np.arange(NY) + 0.5), uu, lat)          # 图上行=北
    tgt_lat[:, idx] = la[:, None]
tgt_row = np.clip(np.round((tgt_lat - (WLAT[0] + 0.25)) / 0.5), 0,
                  lat.size - 1).astype(int)

sel = land[tgt_row, np.broadcast_to(tgt_col, (NY, NX))]        # 两个都是 (NY,NX) 的索引
print(f"\n目标 900x500：陆地占比 {sel.mean():.3f}")
cn_sel = ((tgt_lon[None, :] >= CN[0][0]) & (tgt_lon[None, :] <= CN[0][1]) &
          (tgt_lat >= CN[1][0]) & (tgt_lat <= CN[1][1]))
print(f"  中国核：列 {int((cn_sel.sum(0) > 0).sum())} 行 {int((cn_sel.sum(1) > 0).sum())}"
      f"  格数 {int(cn_sel.sum())}  核内陆地 {sel[cn_sel].mean():.3f}")

img = np.zeros((NY, NX, 3), np.uint8)
img[~sel] = (58, 96, 138)
img[sel] = (150, 165, 120)
img[cn_sel] = (205, 180, 110)
Image.fromarray(img).save(OUT / "lit_final.jpg", quality=90)
print(f"已写出 {OUT / 'lit_final.jpg'}（蓝=海 绿=陆 黄=中国核）")
