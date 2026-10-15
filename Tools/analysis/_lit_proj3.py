# -*- coding: utf-8 -*-
"""lit_world 投影 v3。

窗口 lon -15~160 / lat -13~77 → 900x500。
  * 中国核 (73~135E, 18~54N) 权重 1.0，经/纬各自二分 → 恰好 320 列 x 200 行，不变形；
  * 核外按陆地密度加权（K=1.0 中等）：陆地放大、海洋压缩；
  * **压缩带** lon 66~142（含印度 + 东南亚）、lat < 17：权重 x0.25，把这一带低纬海面压窄，
    让印度与东南亚的纬度节奏与中国对齐；
  * 压缩带内外用 8 度 smoothstep **平滑混合**（避免纵向接缝）。
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ETOPO = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
OUT = Path(r"D:\sango_infinity\Temp")
NX, NY = 900, 500
WLON, WLAT = (-15.0, 160.0), (-13.0, 77.0)
CN_LON, CN_LAT = (73.0, 135.0), (18.0, 54.0)
BOX_LON, BOX_LAT = (66.0, 142.0), (-13.0, 17.0)      # 压缩带（含印度）
FEATH = 8.0
K = 1.0
STEP = 0.5

n_lon = int(round((WLON[1] - WLON[0]) / STEP))
n_lat = int(round((WLAT[1] - WLAT[0]) / STEP))
lon = WLON[0] + STEP * (np.arange(n_lon) + 0.5)
lat = WLAT[0] + STEP * (np.arange(n_lat) + 0.5)
dem = np.load(ETOPO, mmap_mode="r")
rr = np.clip(((90.0 - lat) * 60).astype(int), 0, dem.shape[0] - 1)
cc = np.clip(((lon + 180.0) * 60).astype(int), 0, dem.shape[1] - 1)
E = np.array(dem[np.ix_(rr, cc)], np.float32)
land = E > 0

in_c = (lon >= CN_LON[0]) & (lon <= CN_LON[1])
in_r = (lat >= CN_LAT[0]) & (lat <= CN_LAT[1])
mx, my = land[:, ~in_c].mean(), land[~in_r, :].mean()
print(f"窗口 {n_lon}x{n_lat}  未加权陆地 {land.mean():.3f}  核外均值 经 {mx:.3f} 纬 {my:.3f}")


def cumulative(w):
    return (np.cumsum(w) - w / 2.0) * STEP


def scale_to_core(w, core, n_out, target, lo=0.05, hi=8.0):
    s = lo
    for _ in range(80):
        s = (lo + hi) / 2
        ww = np.where(core, w, np.maximum(w, 1e-6) * s)
        u = cumulative(ww)
        got = u[core][-1] - u[core][0] + ww[core][-1] * STEP / 2
        if got / u[-1] * n_out > target:
            lo = s
        else:
            hi = s
    ww = np.where(core, w, np.maximum(w, 1e-6) * s)
    return ww, cumulative(ww), s


# 经度
w_lon = 1.0 + K * (land.mean(0) - mx)
w_lon[in_c] = 1.0
w_lon, u_lon, s_lon = scale_to_core(w_lon, in_c, NX, 320.0)
col_src = np.clip(np.interp(np.arange(NX) + 0.5, u_lon / u_lon[-1] * NX, np.arange(n_lon)),
                  0, n_lon - 1)
tgt_lon = lon[np.round(col_src).astype(int)]
print(f"经度 scale {s_lon:.3f}  中国核占 "
      f"{int(((tgt_lon >= CN_LON[0]) & (tgt_lon <= CN_LON[1])).sum())} 列")


SIB = 0.35          # 西伯利亚（中国以北，lat > 54）压缩权重：地广人稀，压掉给别的纬度


def lat_map(compress):
    w = 1.0 + K * (land.mean(1) - my)
    sib = np.clip((lat - CN_LAT[1]) / 6.0, 0, 1)      # 54 度以上 6 度内平滑压缩，无横向接缝
    sib = sib * sib * (3 - 2 * sib)
    w = w * (1 - (1 - SIB) * sib)
    if compress:
        w = np.where((lat >= BOX_LAT[0]) & (lat <= BOX_LAT[1]) & ~in_r, w * 0.25, w)
    w[in_r] = 1.0
    ww, u, s = scale_to_core(w, in_r, NY, 200.0)
    idx = np.clip(np.interp(NY - (np.arange(NY) + 0.5), u / u[-1] * NY, np.arange(n_lat)),
                  0, n_lat - 1)
    return idx, lat[np.round(idx).astype(int)], s


row_free, lat_free, s_f = lat_map(False)
row_box, lat_box, s_b = lat_map(True)
print(f"纬度 自由 scale {s_f:.3f} / 压缩(含印度, 低纬 x0.25) scale {s_b:.3f}   "
      f"中国核行数 自由 {int(((lat_free>=CN_LAT[0])&(lat_free<=CN_LAT[1])).sum())}"
      f" / 压缩 {int(((lat_box>=CN_LAT[0])&(lat_box<=CN_LAT[1])).sum())}")

t1 = np.clip((tgt_lon - BOX_LON[0]) / FEATH, 0, 1); t1 = t1 * t1 * (3 - 2 * t1)
t2 = np.clip((BOX_LON[1] - tgt_lon) / FEATH, 0, 1); t2 = t2 * t2 * (3 - 2 * t2)
tt = np.minimum(t1, t2)[None, :]                       # 0=自由映射 1=压缩映射
tgt_lat = (1 - tt) * lat_free[:, None] + tt * lat_box[:, None]
row_src = (1 - tt) * row_free[:, None] + tt * row_box[:, None]

sel = land[np.round(row_src).astype(int), np.round(col_src).astype(int)[None, :].repeat(NY, 0)]
elv = E[np.round(row_src).astype(int), np.round(col_src).astype(int)[None, :].repeat(NY, 0)]
cn_sel = ((tgt_lon[None, :] >= CN_LON[0]) & (tgt_lon[None, :] <= CN_LON[1]) &
          (tgt_lat >= CN_LAT[0]) & (tgt_lat <= CN_LAT[1]))
n_col = int((cn_sel.sum(0) > 0).sum())
print(f"\n结果 画面陆地 {sel.mean():.3f}（未加权 {land.mean():.3f}）"
      f"  中国核 列 {n_col} 格 {int(cn_sel.sum())} 核内陆地 {sel[cn_sel].mean():.3f}"
      f"  压缩带权重过渡 {tt.min():.2f}~{tt.max():.2f}")
assert 300 <= n_col <= 340, "中国核列数不对"
assert sel.mean() > land.mean(), "陆地占比没有提升"

img = np.zeros((NY, NX, 3), np.uint8)
img[~sel] = (56, 94, 136)
h = np.clip(elv / 4000.0, 0, 1)
img[sel] = np.stack([110 + h * 120, 135 + h * 95, 85 + h * 115], -1)[sel].astype(np.uint8)
im = Image.fromarray(img)
d = ImageDraw.Draw(im)
in_cn_col = (tgt_lon >= CN_LON[0]) & (tgt_lon <= CN_LON[1])
cols = np.nonzero(in_cn_col)[0]
tops, bots = [], []
for j in cols:
    m = (tgt_lat[:, j] >= CN_LAT[0]) & (tgt_lat[:, j] <= CN_LAT[1])
    if not m.any():
        tops.append(np.nan); bots.append(np.nan); continue
    r = np.nonzero(m)[0]
    tops.append(r.min()); bots.append(r.max())
tops, bots = np.array(tops, float), np.array(bots, float)
n = len(tops)
print(f"  红框逐列行范围: 左端 {tops[0]:.0f}~{bots[0]:.0f}  中间 {tops[n//2]:.0f}~{bots[n//2]:.0f}"
      f"  右端 {tops[-1]:.0f}~{bots[-1]:.0f}")
print(f"  列跨度 {cols[0]}~{cols[-1]}（{n} 列，应 ~321）")
ok = ~np.isnan(tops)
poly = [(int(cols[i]), int(tops[i])) for i in np.nonzero(ok)[0]]
poly += [(int(cols[i]), int(bots[i])) for i in np.nonzero(ok)[0][::-1]]
if poly:
    d.line(poly + [poly[0]], fill=(255, 80, 80), width=3)
for nm, lo, la in (("Beijing", 116.4, 39.9), ("Shanghai", 121.5, 31.2), ("Chengdu", 104.1, 30.6),
                   ("Lhasa", 91.1, 29.7), ("Harbin", 126.6, 45.8)):
    j = min(max(int(np.searchsorted(tgt_lon, lo)), 0), NX - 1)
    m = np.nonzero((tgt_lat[:, j] >= la - 0.3) & (tgt_lat[:, j] <= la + 0.3))[0]
    if len(m):
        y = int(m[0])
        d.ellipse([j - 3, y - 3, j + 3, y + 3], fill=(0, 255, 255))
        d.text((j + 5, y - 8), nm, fill=(0, 255, 255))
        print(f"    {nm}({lo}E,{la}N) -> 列 {j} 行 {y}")
im.save(OUT / "lit_proj4.jpg", quality=92)
print(f"已写出 {OUT / 'lit_proj4.jpg'}（红框=逐列真实求出的中国 18~54N；青点=地标）")
