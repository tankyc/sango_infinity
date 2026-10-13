# -*- coding: utf-8 -*-
"""一次性：用美术里作者自画的据点图标当锚点，量测底图与地图的错位。

比颜色判据可靠得多：图标是离散、明确的特征点。
"""
import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
OUT = Path(r"D:\sango_infinity\Temp")
SEASONS = ("Autumn", "Spring", "Summer", "Winter")

d = parse_bin(str(MOD / "Map/yyxk_2x.bin"))
GX, GY = d["cellsX"], d["cellsY"]
sc = json.loads((MOD / "Scenario/Scenario1.json").read_text(encoding="utf-8"))["citySet"]
sites = [(v.get("Name"), int(v["x"]), int(v["y"]), int(v.get("BuildingType", 1)))
         for v in sc.values() if "x" in v]
print(f"格 {GX}x{GY}  据点 {len(sites)}  {dict(Counter(b for *_, b in sites))}")

img = Image.open(MOD / "Assets/Map/yyxk/BaseTex/BaseMap0.png").convert("RGB")
W, H = img.size
px, py = W / float(GX), H / float(GY)
a = np.asarray(img, np.float32)
luma = a @ [0.299, 0.587, 0.114]
print(f"底图 {W}x{H}  {px:.3f} 像素/格   全图 luma 中位 {np.median(luma):.1f}  "
      f"p1 {np.percentile(luma, 1):.1f}  p5 {np.percentile(luma, 5):.1f}")

# 暗色小连通域 = 疑似据点图标
for k in (1, 2, 3):
    thr = np.percentile(luma, k)
    m = luma < thr
    lab, n = ndimage.label(m)
    sizes = np.bincount(lab.ravel())
    # 图标：面积在 8~600 像素（4px/格 下约 1~6 格见方）
    ok = [i for i in range(1, n + 1) if 8 <= sizes[i] <= 600]
    cents = ndimage.center_of_mass(m, lab, ok) if ok else []
    print(f"\n阈值 p{k} ({thr:.0f}): 暗域 {int(m.sum())} 像素, 连通域 {n}, "
          f"符合图标尺寸的 {len(ok)} 个")
    if not ok:
        continue
    # 与据点配对：每个据点找最近的图标
    pts = np.array([(cy, cx) for cy, cx in cents])          # (row, col)
    gpts = np.array([(y * py + py / 2, x * px + px / 2) for _, x, y, _ in sites])
    offs = []
    for gy, gx in gpts:
        dd = np.sqrt(((pts - [gy, gx]) ** 2).sum(1))
        i = int(np.argmin(dd))
        offs.append((pts[i, 1] - gx, pts[i, 0] - gy, float(dd[i])))
    offs = np.array(offs)
    near = offs[:, 2] <= 1.2 * max(px, py) * 3              # 3 格内算对上了
    print(f"  3 格内找到对应图标的据点 {int(near.sum())}/{len(sites)} = {near.mean():.3f}")
    if near.sum() > 20:
        o = offs[near]
        print(f"  偏移(像素) dx 中位 {np.median(o[:, 0]):+.1f}  dy 中位 {np.median(o[:, 1]):+.1f}")
        print(f"  偏移(格)   dx 中位 {np.median(o[:, 0]) / px:+.2f}  "
              f"dy 中位 {np.median(o[:, 1]) / py:+.2f}")
        print(f"  |偏移|(格) 中位 {np.median(np.hypot(o[:, 0] / px, o[:, 1] / py)):.2f}  "
              f"p90 {np.percentile(np.hypot(o[:, 0] / px, o[:, 1] / py), 90):.2f}")
        print(f"  dx 分布(格) {Counter(np.round(o[:, 0] / px).astype(int).tolist()).most_common(5)}")
        print(f"  dy 分布(格) {Counter(np.round(o[:, 1] / py).astype(int).tolist()).most_common(5)}")

# 可视化：图标点(青) vs 据点格(红)
thr = np.percentile(luma, 2)
m = luma < thr
lab, n = ndimage.label(m)
sizes = np.bincount(lab.ravel())
ok = [i for i in range(1, n + 1) if 8 <= sizes[i] <= 600]
cents = ndimage.center_of_mass(m, lab, ok) if ok else []
vis = np.asarray(img.convert("L").convert("RGB"), np.uint8).copy()
for cy, cx in cents:
    y, x = int(cy), int(cx)
    vis[max(0, y - 2):y + 3, max(0, x - 2):x + 3] = (0, 255, 255)
for _, x, y, _ in sites:
    cy, cx = int(y * py), int(x * px)
    vis[max(0, cy - 2):cy + 3, max(0, cx - 2):cx + 3] = (255, 40, 40)
Image.fromarray(vis).resize((1400, 1400), Image.Resampling.LANCZOS).save(OUT / "yyxk_anchor.png")
print(f"\n已写出 {OUT / 'yyxk_anchor.png'}  青=美术图标 红=据点格")

# 顺带：把每个据点在底图上的局部最暗点找出来（图标的精确位置）
loc = []
w = int(round(2.5 * px))
for _, x, y, _ in sites[:200]:
    c0, r0 = int(x * px), int(y * py)
    p = luma[max(0, r0 - w):r0 + w + 1, max(0, c0 - w):c0 + w + 1]
    if p.size == 0:
        continue
    iy, ix = np.unravel_index(int(np.argmin(p)), p.shape)
    loc.append((ix - w, iy - w, float(p.min() - np.median(p))))
loc = np.array(loc)
print(f"\n每据点邻域(±2.5格)最暗点的相对位置: dx 中位 {np.median(loc[:, 0]):+.1f}px "
      f"({np.median(loc[:, 0]) / px:+.2f}格)  dy 中位 {np.median(loc[:, 1]):+.1f}px "
      f"({np.median(loc[:, 1]) / py:+.2f}格)")
print(f"  局部对比度(最暗-中位) 中位 {np.median(loc[:, 2]):.1f}  "
      f"（越大说明该处确实有暗图标）")
