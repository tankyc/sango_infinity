# -*- coding: utf-8 -*-
"""一次性：把烘进 yyxk 的植被位置画在底图上，并给出「扩展到山地」的候选规模。"""
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
models = d["tail"]["models"][2]          # tail['models'] = (起始偏移, 结束偏移, 记录列表)
print(f"mapModels {len(models)} 条  gridSize {G}")

cells = []
ids = Counter()
print(f"  首条记录原始字段: {models[0]}")
for m in models:
    pos = m[4]                       # pos = (worldX, worldY, worldZ)
    wx, wz = float(pos[0]), float(pos[2])
    x = int(round((wz - G / 2) / G))
    y = int(round((wx - ((x & 1) * G / 2) - G / 2) / G))
    cells.append((x, y, 0))
print(f"  反解到格: 前 5 个 {cells[:5]}")
print(f"  modelId 分布 {dict(ids)}")
tt = d["gridRaw"][..., 0].astype(np.int32)
bad = [(x, y) for x, y, _ in cells if tt[x, y] != 6]
print(f"  落在森(6)之外的 {len(bad)} 个")

# 画在底图上
art = Image.open(MOD / "Assets/Map/yyxk/BaseTex/BaseMap0.png").convert("RGB")
W, H = art.size
px, py = W / cx, H / cy
dr = ImageDraw.Draw(art)
for x, y, mid in cells:
    c = (int((x + 0.5) * px), int((y + 0.5) * py))
    r = 4
    dr.ellipse([c[0] - r, c[1] - r, c[0] + r, c[1] + r], outline=(255, 30, 30), width=2)
    dr.ellipse([c[0] - 1, c[1] - 1, c[0] + 1, c[1] + 1], fill=(255, 200, 0))
art.resize((900, 900), Image.Resampling.LANCZOS).save(
    r"D:\sango_infinity\Temp\yyxk_veg_positions.png")
print("已写出 Temp/yyxk_veg_positions.png")

# 半径 26 格内有多少棵（看聚集程度）
import math
pts = [(x, y) for x, y, _ in cells]
iso = 0
for i, (x, y) in enumerate(pts):
    n = sum(1 for j, (a, b) in enumerate(pts) if j != i and abs(a - x) <= 26 and abs(b - y) <= 26)
    if n == 0:
        iso += 1
print(f"  26 格范围内没有任何同伴的孤植树 {iso}/{len(pts)}")

# 扩展到山地的候选规模
cnt = Counter(tt.ravel().tolist())
print("\n扩展到其它地形的规模（供选择）:")
for t, name in ((6, "森"), (16, "崖/山"), (3, "砂地"), (1, "草地")):
    n = cnt.get(t, 0)
    for d_ in (0.05, 0.1, 0.2):
        print(f"  地形 {t}({name}) {n} 格  × 密度 {d_} -> 约 {int(n * d_)} 棵"
              f"  （预算 10000，单模型 1000）")
