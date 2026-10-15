# -*- coding: utf-8 -*-
"""lit_world 投影（v2，跑通版）。

固定窗口 lon -15~160 / lat -13~77 → 900x500 格。
  * 中国核 (73~135E, 18~54N)：权重强制 1.0，经/纬各自二分求解，使其恰好 320 列 x 200 行（不变形）
  * 核外权重 = 1 + K*(陆地密度 - 核外均值)：陆地放大、海洋压缩（K=1.0 中等强度）
  * 东南亚削除：目标列落在 lon 95~142 的带里，纬度再单独归一，且 lat<17 的部分权重 x0.25
自检：中国核格数必须 ≈320x200；画面陆地占比必须 > 未加权的同窗口占比。
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ETOPO = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
OUT = Path(r"D:\sango_infinity\Temp")
NX, NY = 900, 500
WLON, WLAT = (-15.0, 160.0), (-13.0, 77.0)
CN_LON, CN_LAT = (73.0, 135.0), (18.0, 54.0)
BOX_LON, BOX_LAT = (95.0, 142.0), (-13.0, 17.0)
K = 1.0
STEP = 0.5

n_lon = int(round((WLON[1] - WLON[0]) / STEP))
n_lat = int(round((WLAT[1] - WLAT[0]) / STEP))
lon = WLON[0] + STEP * (np.arange(n_lon) + 0.5)
lat = WLAT[0] + STEP * (np.arange(n_lat) + 0.5)
dem = np.load(ETOPO, mmap_mode="r")
rr = np.clip(((90.0 - lat) * 60).astype(int), 0, dem.shape[0] - 1)
cc = np.clip(((lon + 180.0) * 60).astype(int), 0, dem.shape[1] - 1)
E = np.array(dem[np.ix_(rr, cc)], np.float32)          # (n_lat, n_lon)，行 0 = 最南
land = E > 0

in_cn_lon = (lon >= CN_LON[0]) & (lon <= CN_LON[1])
in_cn_lat = (lat >= CN_LAT[0]) & (lat <= CN_LAT[1])
mx, my = land[:, ~in_cn_lon].mean(), land[~in_cn_lat, :].mean()
print(f"窗口 {n_lon}x{n_lat}（{STEP} 度）  未加权陆地占比 {land.mean():.3f}"
      f"  核外均值 经向 {mx:.3f} 纬向 {my:.3f}")


def cumulative(w):
    """返回长度 n 的**格心**累积位置（单位=度），与源数组一一对应，可直接用于 np.interp。"""
    return (np.cumsum(w) - w / 2.0) * STEP


def scale_to_core(w, core, n_out, target_core, lo=0.05, hi=1.20):
    """二分缩放非核心权重，使核心的累积宽度 = target_core 格。"""
    for _ in range(80):
        s = (lo + hi) / 2
        ww = np.where(core, w, np.maximum(w, 1e-6) * s)
        u = cumulative(ww)
        got = u[core][-1] - u[core][0] + ww[core][-1] * STEP / 2
        if got / u[-1] * n_out > target_core:
            lo = s
        else:
            hi = s
    ww = np.where(core, w, np.maximum(w, 1e-6) * s)
    return ww, cumulative(ww), s


# ---------- 经度 ----------
w_lon = 1.0 + K * (land.mean(0) - mx)
w_lon[in_cn_lon] = 1.0
w_lon, u_lon, s_lon = scale_to_core(w_lon, in_cn_lon, NX, 320.0)
col_src = np.interp(np.arange(NX) + 0.5, u_lon / u_lon[-1] * NX, np.arange(n_lon))
col_src = np.clip(col_src, 0, n_lon - 1)
tgt_lon = lon[np.round(col_src).astype(int)]
print(f"经度: scale {s_lon:.3f}  中国核占 "
      f"{int(((tgt_lon >= CN_LON[0]) & (tgt_lon <= CN_LON[1])).sum())} 列")

# ---------- 纬度（按目标列分带，逐带单独归一）----------
band_of_col = np.where(tgt_lon < BOX_LON[0], 0,
                       np.where(tgt_lon <= BOX_LON[1], 1, 2))
print("目标列分带人数:", {int(b): int((band_of_col == b).sum()) for b in (0, 1, 2)})

lat_rows_of_band = {}
for b in (0, 1, 2):
    w = 1.0 + K * (land.mean(1) - my)
    if b == 1:
        low = (lat >= BOX_LAT[0]) & (lat <= BOX_LAT[1])
        w = np.where(low & ~in_cn_lat, w * 0.25, w)          # 东南亚带：低纬压掉
    w[in_cn_lat] = 1.0
    ww, u, s = scale_to_core(w, in_cn_lat, NY, 200.0, 0.05, 8.0)
    idx = np.interp(NY - (np.arange(NY) + 0.5), u / u[-1] * NY, np.arange(n_lat))
    idx = np.clip(idx, 0, n_lat - 1)
    lat_rows_of_band[b] = (idx, lat[np.round(idx).astype(int)], s)
    if b == 1:
        print(f"  纬带1: scale {s:.3f}")

row_src = np.stack([lat_rows_of_band[b][0] for b in band_of_col], axis=1)   # (NY, NX)
tgt_lat = np.stack([lat_rows_of_band[b][1] for b in band_of_col], axis=1)

# ---------- 采样 ----------
sel = land[np.round(row_src).astype(int), np.round(col_src).astype(int)[None, :]
           .repeat(NY, 0)]
elv = E[np.round(row_src).astype(int), np.round(col_src).astype(int)[None, :]
        .repeat(NY, 0)]

cn_sel = ((tgt_lon[None, :] >= CN_LON[0]) & (tgt_lon[None, :] <= CN_LON[1]) &
          (tgt_lat >= CN_LAT[0]) & (tgt_lat <= CN_LAT[1]))
n_col = int((cn_sel.sum(0) > 0).sum())
n_row = int((cn_sel.sum(1) > 0).sum())
print(f"\n结果：画面陆地占比 {sel.mean():.3f}（未加权 {land.mean():.3f}）")
print(f"      中国核 列 {n_col} 行 {n_row} 格 {int(cn_sel.sum())}  核内陆地 {sel[cn_sel].mean():.3f}")
assert 300 <= n_col <= 340, "中国核列数不对"
assert sel.mean() > land.mean(), "陆地占比没有提升"
print(f"  [注意] 行数 {n_row} 是三带纬度映射的**并集**，必然大于 200 —— "
      f"这同时意味着 lon 95 与 142 处会有**纵向接缝**（带与带之间纬度映射不连续）✗")
for b in (0, 1, 2):
    _, la_b, _ = lat_rows_of_band[b]
    print(f"    纬带{b}: 目标纬度范围 {la_b.min():6.1f} ~ {la_b.max():6.1f}"
          f"  其中落在 18~54 的行数 {int(((la_b >= CN_LAT[0]) & (la_b <= CN_LAT[1])).sum())}")

# ---------- 成图 ----------
img = np.zeros((NY, NX, 3), np.uint8)
img[~sel] = (56, 94, 136)
h = np.clip(elv / 4000.0, 0, 1)
img[sel] = np.stack([110 + h * 120, 135 + h * 95, 85 + h * 115], -1)[sel].astype(np.uint8)
im = Image.fromarray(img)
d = ImageDraw.Draw(im)
c0 = int(np.searchsorted(tgt_lon, CN_LON[0]))
c1 = int(np.searchsorted(tgt_lon, CN_LON[1]))
r1 = int(np.searchsorted(-tgt_lat[:, NX // 2], -CN_LAT[1]))
r0 = int(np.searchsorted(-tgt_lat[:, NX // 2], -CN_LAT[0]))
d.rectangle([max(0, c0), max(0, r1), min(NX - 1, c1), min(NY - 1, r0)],
            outline=(255, 80, 80), width=3)
im.save(OUT / "lit_proj2.jpg", quality=92)
print(f"已写出 {OUT / 'lit_proj2.jpg'}（红框=中国核 320x200）")
