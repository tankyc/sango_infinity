# -*- coding: utf-8 -*-
"""lit_world 覆盖范围示意图：标出 900x500 框（lon -15~160 / lat -13~77）、中国 320x200 核心、
以及建议裁掉的三个海多区域（非洲下半 / 印度 / 东南亚），并给出裁前裁后的陆地占比。"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

ETOPO = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
OUT = Path(r"D:\sango_infinity\Temp")

LON = (-15.0, 160.0)
LAT = (-13.0, 77.0)
CN_LON, CN_LAT = (73.0, 135.0), (18.0, 54.0)
CUT = [("非洲下半部分", (-20.0, 52.0), (-13.0, 5.0)),
       ("印度", (66.0, 98.0), (5.0, 37.0)),
       ("东南亚", (95.0, 142.0), (-13.0, 22.0))]

step = 0.5
lat = np.arange(LAT[0] + step / 2, LAT[1], step)
lon = np.arange(LON[0] + step / 2, LON[1], step)
dem = np.load(ETOPO, mmap_mode="r")
rows = np.clip(((90.0 - lat) * 60).astype(int), 0, dem.shape[0] - 1)
cols = np.clip(((lon + 180.0) * 60).astype(int), 0, dem.shape[1] - 1)
E = np.array(dem[np.ix_(rows, cols)], np.float32)          # (nlat, nlon) [行=lat 递减]
land = E > 0
NY, NX = E.shape
print(f"采样 {NX}x{NY}（0.5 度）")

in_cn = ((lon[None, :] >= CN_LON[0]) & (lon[None, :] <= CN_LON[1]) &
         (lat[:, None] >= CN_LAT[0]) & (lat[:, None] <= CN_LAT[1]))
cut = np.zeros_like(land)
for nm, (x0, x1), (y0, y1) in CUT:
    m = ((lon[None, :] >= x0) & (lon[None, :] <= x1) &
         (lat[:, None] >= y0) & (lat[:, None] <= y1))
    cut |= m
    print(f"  裁掉 {nm:<12} lon {x0:>6.1f}~{x1:<6.1f} lat {y0:>5.1f}~{y1:<5.1f}"
          f"  面积占比 {m.mean():.3f}  其中陆地 {land[m].mean():.3f}")

keep = ~cut
print(f"\n整框陆地占比      {land.mean():.3f}")
print(f"裁掉区陆地占比    {land[cut].mean():.3f}")
print(f"保留区陆地占比    {land[keep].mean():.3f}")
print(f"保留区内 中国核心陆地 {land[in_cn].mean():.3f}")
print(f"裁掉的陆地占全框陆地 {(land & cut).sum() / max(1,(land).sum()):.3f}"
      f"（＝放弃这点陆地，换取画面里海更少）")

# --- 画图 ---
img = np.zeros((NY, NX, 3), np.uint8)
img[..., :] = (52, 92, 132)                      # 海
img[land] = (150, 170, 130)                      # 陆
img[land & in_cn] = (196, 176, 108)              # 中国核心
img[cut] = (img[cut] * 0.35).astype(np.uint8)    # 裁掉区压暗
im = Image.fromarray(img).resize((900, 500), Image.Resampling.LANCZOS)
d = ImageDraw.Draw(im)

def xy(lo, la):
    return ((lo - LON[0]) / (LON[1] - LON[0]) * 900, (LAT[1] - la) / (LAT[1] - LAT[0]) * 500)

p0 = xy(CN_LON[0], CN_LAT[1])          # 左上（西北角）
p1 = xy(CN_LON[1], CN_LAT[0])          # 右下（东南角）
d.rectangle([p0[0], p0[1], p1[0], p1[1]], outline=(255, 90, 90), width=3)
for nm, (x0, x1), (y0, y1) in CUT:
    p0 = xy(x0, y1); p1 = xy(x1, y0)
    d.rectangle([p0[0], p0[1], p1[0], p1[1]], outline=(255, 220, 60), width=2)
    d.text((p0[0] + 4, p0[1] + 4), nm, fill=(255, 240, 120))
im.save(OUT / "lit_coverage.jpg", quality=90)
print(f"\n已写出 {OUT / 'lit_coverage.jpg'}"
      f"  红框=中国 320x200 核心  黄框=建议裁掉  压暗=裁掉区")
