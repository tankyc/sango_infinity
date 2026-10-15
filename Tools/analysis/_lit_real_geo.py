# -*- coding: utf-8 -*-
"""真实地域示意图：等距圆柱 + 各向同性比例（不做任何人为变形）。

原则：
  * 全图单一比例、各向同性 —— 1 像素在经/纬方向代表同样地面距离（取窗口中心纬度 φ0）；
  * 没有任何「陆地密度加权 / 压缩带 / 纬度加权 / 中国核固定 320x200」之类的人为缩放；
  * 经纬框在该投影下就是矩形（经纬各自线性映射）。

用法：
  1) 全图：南切线 = 印度半岛最南端 Cape Comorin (8.078N) 之下留一丝海洋；
     东西 -15~160E，南北跨度由画面比例 900x500 反推。
       python Tools/analysis/_lit_real_geo.py
  2) 指定窗口（例如中国区域 73~135E / 18~54N）：
       python Tools/analysis/_lit_real_geo.py --west 73 --east 135 --south 18 --north 54 \
              --nx 1280 --hmax 5500 --name china

只给 --nx 时，--ny 会自动按各向同性反推，保证画面不被拉伸。
"""
import argparse
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

ETOPO = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
OUT = Path(r"D:\sango_infinity\Temp")
CAPE = (77.55, 8.078)                       # 印度半岛最南端 Cape Comorin
CN_LON, CN_LAT = (73.0, 135.0), (18.0, 54.0)
LM = [("Beijing", 116.4, 39.9), ("Shanghai", 121.5, 31.2), ("Chengdu", 104.1, 30.6),
      ("Lhasa", 91.1, 29.7), ("Harbin", 126.6, 45.8), ("Urumqi", 87.6, 43.8),
      ("Kunming", 102.7, 25.0), ("Wuhan", 114.3, 30.6), ("Xian", 108.9, 34.3),
      ("HongKong", 114.2, 22.3), ("Taipei", 121.5, 25.0), ("Delhi", 77.2, 28.6),
      ("Hanoi", 105.8, 21.0), ("Seoul", 127.0, 37.6), ("Tokyo", 139.7, 35.7),
      ("Mumbai", 72.9, 19.1), ("Bangkok", 100.5, 13.8), ("Singapore", 103.8, 1.35)]

ap = argparse.ArgumentParser()
ap.add_argument("--west", type=float, default=-15.0)
ap.add_argument("--east", type=float, default=160.0)
ap.add_argument("--south", type=float, default=7.50)
ap.add_argument("--north", type=float, default=None,
                help="不给则由画面比例 + 各向同性反推（需要 --ny 或不给 --ny 走默认比例）")
ap.add_argument("--nx", type=int, default=900)
ap.add_argument("--ny", type=int, default=None,
                help="不给则按各向同性反推（保证画面不被拉伸）")
ap.add_argument("--grid", type=float, default=None, help="经纬网间隔(度)，默认 5/10")
ap.add_argument("--hmax", type=float, default=4000.0, help="高程配色上限(m)")
ap.add_argument("--no-box", action="store_true", help="不画中国红框及其标注")
ap.add_argument("--name", default="", help="输出文件名后缀（空=lit_real.jpg）")
a = ap.parse_args()

dlon = a.east - a.west
NX = a.nx

# ---------- 取景（窗口）与画面高度：各向同性 ----------
if a.north is None:
    NY0 = a.ny if a.ny else int(round(NX * 500.0 / 900.0))
    lo_, hi_ = a.south + 1e-6, a.south + 250.0
    for _ in range(90):
        mid = (lo_ + hi_) / 2.0
        need = dlon * (NY0 / NX) * np.cos(np.deg2rad((a.south + mid) / 2.0))
        if (mid - a.south) < need:
            lo_ = mid
        else:
            hi_ = mid
    north = (lo_ + hi_) / 2.0
else:
    north = a.north
dlat = north - a.south
phi0 = (a.south + north) / 2.0
NY = a.ny if a.ny else max(25, int(round(NX * dlat / (dlon * np.cos(np.deg2rad(phi0))))))
kmx = dlon / NX * 111.32 * np.cos(np.deg2rad(phi0))
kmy = dlat / NY * 111.32
print(f"窗口 经 {a.west:g}~{a.east:g}E  纬 {a.south:g}~{north:.4f}N   画面 {NX}x{NY}")
print(f"比例（各向同性 @{phi0:.2f}N）：{kmx:.3f} km/px 经向 vs {kmy:.3f} km/px 纬向"
      f"   误差 {abs(kmx - kmy) / kmy * 100:.2f}%")
if abs(kmx - kmy) / kmy > 0.02:
    print("  [警告] 画面比例与各向同性不一致：画面被拉伸了（去掉 --ny 可自动修正）")

lon = a.west + (np.arange(NX) + 0.5) / NX * dlon
lat = north - (np.arange(NY) + 0.5) / NY * dlat


def px(lonv):
    return (lonv - a.west) / dlon * NX


def py(latv):
    return (north - latv) / dlat * NY


# ---------- 采样 ETOPO 60s（细比例时做 2x2 平均，防锯齿）----------
dem = np.load(ETOPO, mmap_mode="r")
R0 = (90.0 - lat) * 60.0
C0 = (lon + 180.0) * 60.0
b = 1 if min(kmx, kmy) > 6.0 else 2
offs = (np.arange(b) - (b - 1) / 2.0) / b
ri = np.clip(np.round(R0[:, None] + offs[None, :]).astype(int), 0, dem.shape[0] - 1)
ci = np.clip(np.round(C0[:, None] + offs[None, :]).astype(int), 0, dem.shape[1] - 1)
r0, r1 = int(ri.min()) - 1, int(ri.max()) + 2
c0, c1 = int(ci.min()) - 1, int(ci.max()) + 2
sub = dem[r0:r1, c0:c1]
E = np.zeros((NY, NX), np.float32)
for k in range(b * b):
    E += np.array(sub[ri[:, k // b] - r0][:, ci[:, k % b] - c0], np.float32)
E /= float(b * b)
land = E > 0.0
print(f"  采样 {E.shape}（每像素 {b}x{b} 平均）  陆地 {land.mean():.3f}"
      f"  高程 {E.min():.0f}~{E.max():.0f} m")

# ---------- 渲染 ----------
img = np.zeros((NY, NX, 3), np.float32)
deep = np.array([16, 42, 84], np.float32)
shallow = np.array([78, 132, 176], np.float32)
od = np.clip(-E / 5500.0, 0, 1)[..., None]
img[:] = deep + (shallow - deep) * (1 - od)
h = np.clip(E / a.hmax, 0, 1)[..., None]
img = np.where(land[..., None],
               np.array([102, 130, 80], np.float32) + h * np.array([124, 98, 118], np.float32),
               img)
coast = land & ~ndimage.binary_erosion(land, np.ones((3, 3), bool))
img[coast] = (244, 238, 206)
im = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))

gs = a.grid if a.grid else (5.0 if dlon <= 90.0 else 10.0)
ov = im.copy()
do = ImageDraw.Draw(ov)
for la in np.arange(np.ceil(a.south / gs) * gs, north, gs):
    do.line([(0, py(la)), (NX, py(la))], fill=(255, 255, 255), width=1)
for lo in np.arange(np.ceil(a.west / gs) * gs, a.east, gs):
    do.line([(px(lo), 0), (px(lo), NY)], fill=(255, 255, 255), width=1)
im = Image.blend(im, ov, 0.22)
d = ImageDraw.Draw(im)


def font(sz):
    for p in (r"C:\Windows\Fonts\segoeui.ttf", r"C:\Windows\Fonts\arial.ttf"):
        if Path(p).exists():
            return ImageFont.truetype(p, sz)
    return ImageFont.load_default()


f10, f13 = font(max(11, NX // 80)), font(max(14, NX // 62))


def plate(xy, lines, fnts, pad=6, bg=(10, 14, 20), fg=(255, 255, 255), alpha=0.70):
    """半透明说明版（自动贴边；底图仍可透出）。"""
    x, y = xy
    ws, hs = [], []
    for t, f in zip(lines, fnts):
        bb = d.textbbox((0, 0), t, font=f)
        ws.append(bb[2] - bb[0])
        hs.append(bb[3] - bb[1] + 5)
    w, hh = int(max(ws) + pad * 2), int(sum(hs) + pad * 2)
    x, y = int(min(max(0, x), NX - w)), int(min(max(0, y), NY - hh))
    box = im.crop((x, y, x + w, y + hh))
    im.paste(Image.blend(box, Image.new("RGB", box.size, bg), alpha), (x, y))
    d.rectangle([x, y, x + w, y + hh], outline=(96, 106, 126), width=1)
    yy = y + pad
    for t, f, h1 in zip(lines, fnts, hs):
        d.text((x + pad, yy), t, font=f, fill=fg)
        yy += h1
    return x, y, w, hh


for la in np.arange(np.ceil(a.south / (2 * gs)) * 2 * gs, north, 2 * gs):
    d.text((5, py(la) + 3), f"{la:g}N", font=f10, fill=(238, 238, 238))
for lo in np.arange(np.ceil(a.west / (2 * gs)) * 2 * gs, a.east, 2 * gs):
    d.text((px(lo) + 4, 5), f"{abs(lo):g}{'E' if lo >= 0 else 'W'}", font=f10,
           fill=(238, 238, 238))

# 中国真实框
box_in = (a.west < CN_LON[0] - 0.5 or a.east > CN_LON[1] + 0.5 or
          a.south < CN_LAT[0] - 0.5 or north > CN_LAT[1] + 0.5)
if box_in and not a.no_box:
    x0, x1, y0, y1 = px(CN_LON[0]), px(CN_LON[1]), py(CN_LAT[1]), py(CN_LAT[0])
    d.rectangle([x0, y0, x1, y1], outline=(255, 88, 80), width=3)
    plate((x0 + 10, y0 - 48),
          ["China 73-135E / 18-54N  (red box)",
           f"real size {x1 - x0:.0f} x {y1 - y0:.0f} px",
           f"ground {abs(x1 - x0) * kmx:.0f} x {(y1 - y0) * kmy:.0f} km"
           f"   aspect {(x1 - x0) / (y1 - y0):.2f}"],
          [f13, f10, f10], fg=(255, 172, 166))

# 南切线（印度半岛最南端）
if a.south < CAPE[1] < north and a.west < CAPE[0] < a.east:
    cx, cy = px(CAPE[0]), py(CAPE[1])
    d.ellipse([cx - 5, cy - 5, cx + 5, cy + 5], outline=(255, 80, 220), width=3)
    bx, by, bw, bh = plate((cx - 300, min(NY - 96, cy + 40)),
                           [f"Cape Comorin {CAPE[1]}N  (India south tip)",
                            f"south cut = {a.south:g}N  ->  "
                            f"{(CAPE[1] - a.south) * 111.32:.0f} km ocean",
                            f"tip sits {NY - cy:.0f} px above the map edge"],
                           [f13, f10, f10], fg=(255, 170, 235))
    d.line([(cx, cy), (bx + bw, by + 6)], fill=(255, 80, 220), width=2)

# 说明版
plate((8, 30),
      ["Equirectangular - real geography, no artificial squeeze",
       f"isotropic @{phi0:.1f}N:  {kmx:.2f} km/px  (x = y)",
       f"window  {a.west:g}~{a.east:g}E  /  {a.south:g}~{north:.2f}N   {NX}x{NY}",
       f"land {land.mean():.3f}   ETOPO 60s   hmax {a.hmax:g} m"],
      [f13, f10, f10, f10], fg=(230, 235, 245))

for nm, lo, la in LM:
    if not (a.west + 0.4 < lo < a.east - 0.4 and a.south + 0.4 < la < north - 0.4):
        continue
    xx, yy = px(lo), py(la)
    d.ellipse([xx - 3, yy - 3, xx + 3, yy + 3], fill=(0, 255, 255))
    tx = xx + 6 if xx < NX - 95 else xx - 8 - d.textlength(nm, font=f10)
    d.text((tx, yy - 7), nm, font=f10, fill=(0, 255, 255))

tag = f"_{a.name}" if a.name else ""
f1 = OUT / f"lit_real{tag}.jpg"
f2 = OUT / f"lit_real{tag}_view.jpg"
im.save(f1, quality=93)
k = min(1.0, 900.0 / NX)
im.resize((int(NX * k), int(NY * k)), Image.Resampling.LANCZOS).save(f2, quality=88)
print(f"已写出 {f1}  {f2}")
