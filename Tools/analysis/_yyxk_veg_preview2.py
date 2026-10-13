# -*- coding: utf-8 -*-
"""一次性：把全部植被位置按地形着色画在底图上（森林=深绿、山地=橙色），并统计。"""
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
d = parse_bin(str(MOD / "Map/yyxk_2x.bin"))
cx, cy = d["cellsX"], d["cellsY"]
G = float(d["gridSize"])
models = d["tail"]["models"][2]
tt = d["gridRaw"][..., 0].astype(np.int32)
print(f"mapModels {len(models)} 条")

cells = []
for m in models:
    pos = m[4]
    wx, wz = float(pos[0]), float(pos[2])
    x = int(round((wz - G / 2) / G))
    y = int(round((wx - ((x & 1) * G / 2) - G / 2) / G))
    cells.append((x, y))

by_t = Counter(tt[x, y].tolist() for x, y in cells)
print(f"  按所在地形统计: {dict(by_t)}")
mins = Counter()
for mid in range(15):
    n = sum(1 for m in models if len(m) > 3 and int(m[3]) == mid)
    if n:
        mins[mid] = n
print(f"  按 modelId 统计: {dict(sorted(mins.items()))}")
over = {k: v for k, v in mins.items() if v > 1023}
print(f"  超过 DrawMeshInstanced 上限 1023 的: {over if over else '无 ✓'}")

art = Image.open(MOD / "Assets/Map/yyxk/BaseTex/BaseMap0.png").convert("RGB")
W, H = art.size
px, py = W / cx, H / cy
layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
dr = ImageDraw.Draw(layer)
for x, y in cells:
    t = tt[x, y]
    c = (40, 255, 60, 235) if t == 6 else ((255, 150, 20, 190) if t == 16 else (0, 200, 255, 220))
    p = (int((x + 0.5) * px), int((y + 0.5) * py))
    r = max(2.5, 0.55 * px)
    dr.ellipse([p[0] - r, p[1] - r, p[0] + r, p[1] + r], fill=c)
base = art.convert("RGBA")
base.alpha_composite(layer)
out = base.convert("RGB")
out.resize((900, 900), Image.Resampling.LANCZOS).save(
    r"D:\sango_infinity\Temp\yyxk_veg_positions2.png")
# 局部放大两处：一个山地密集区、一个森林区
crops = []
fc = next(((x, y) for x, y in cells if tt[x, y] == 6), (236, 205))
mc = Counter((x // 32, y // 32) for x, y in cells if tt[x, y] == 16).most_common(1)
mx, my = (mc[0][0] * 32 + 16, mc[0][1] * 32 + 16) if mc else (256, 256)
for cxy, tag in ((fc, "森林(6) 一带"), ((mx, my), "山地(16) 最密块")):
    x0, y0 = cxy
    box = (int((x0 - 40) * px), int((y0 - 40) * py), int((x0 + 40) * px), int((y0 + 40) * py))
    if box[0] < 0 or box[1] < 0 or box[2] > W or box[3] > H:
        continue
    c = out.crop(box)
    c = c.resize((c.size[0] * 2, c.size[1] * 2), Image.Resampling.NEAREST)
    crops.append((tag, c))
if crops:
    sheet = Image.new("RGB", (sum(c.size[0] for _, c in crops) + 10, max(c.size[1] for _, c in crops)), (20, 20, 24))
    xx = 0
    for tag, c in crops:
        sheet.paste(c, (xx, 0))
        ImageDraw.Draw(sheet).text((xx + 6, 4), tag, fill=(255, 255, 0))
        xx += c.size[0] + 10
    if max(sheet.size) > 1500:
        k = 1500 / float(max(sheet.size))
        sheet = sheet.resize((int(sheet.size[0] * k), int(sheet.size[1] * k)), Image.Resampling.LANCZOS)
    sheet.save(r"D:\sango_infinity\Temp\yyxk_veg_zoom.png")
    print(f"已写出 Temp/yyxk_veg_zoom.png  {sheet.size[0]}x{sheet.size[1]}")
print("已写出 Temp/yyxk_veg_positions2.png")
