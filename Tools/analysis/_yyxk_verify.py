# -*- coding: utf-8 -*-
"""一次性：核验 yyxk 底图改动（只动了该动的像素）+ 出前后对比图。"""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
TEX = MOD / "Assets/Map/yyxk/BaseTex"
OUT = Path(r"D:\sango_infinity\Temp")
SEASONS = ("Autumn", "Spring", "Summer", "Winter")
CX, CY = 512, 512
# 13 个数据差异格 (y, x)
DIFF = [(97, 31), (186, 354), (209, 89), (209, 91), (228, 268), (228, 269), (273, 237),
        (301, 266), (307, 196), (312, 342), (390, 309), (469, 346), (486, 115)]
sc = json.loads((MOD / "Scenario/Scenario1.json").read_text(encoding="utf-8"))["citySet"]
cities = [(v.get("Name"), int(v["x"]), int(v["y"])) for v in sc.values() if "x" in v]

print("=" * 74)
for si, nm in enumerate(SEASONS):
    p = TEX / f"BaseMap{si}.png"
    bak = TEX / f"BaseMap{si}.png.bak_before_repatch"
    a = np.asarray(Image.open(p).convert("RGB"), np.int32)
    b = np.asarray(Image.open(bak).convert("RGB"), np.int32)
    H, W = a.shape[:2]
    px, py = W / CX, H / CY
    d = np.abs(a - b).sum(2)
    ys, xs = np.nonzero(d > 6)
    gx = np.clip((xs / px).astype(int), 0, CX - 1)
    gy = np.clip((ys / py).astype(int), 0, CY - 1)
    # 归属：13 格附近 2 格内 = 补正；据点邻域 1.5 格内 = 底座
    near_diff = np.zeros(len(xs), bool)
    for dy, dx in DIFF:
        near_diff |= (np.abs(gy - dy) <= 2) & (np.abs(gx - dx) <= 2)
    near_city = np.zeros(len(xs), bool)
    for _, cx, cy in cities:
        near_city |= (np.abs(gy - cy) <= 2) & (np.abs(gx - cx) <= 3)
    print(f"{nm:<7} {W}x{H}  {px:.2f} 像素/格")
    print(f"  改动像素 {len(xs):>7}   在 13 格差异处 {int(near_diff.sum()):>6}"
          f"   在据点处 {int(near_city.sum()):>7}   两者都不是 {int((~near_diff & ~near_city).sum()):>6}")
    print(f"  改动格数 {len(set(zip(gy.tolist(), gx.tolist())))}")

# 对比图：挑 2 个差异格 + 2 个据点
picks = [("数据差异格", DIFF[6], 14), ("数据差异格", DIFF[9], 14),
         ("据点", (cities[30][2], cities[30][1]), 12), ("据点", (cities[200][2], cities[200][1]), 12)]
rows = []
for si in (0, 2):
    p = TEX / f"BaseMap{si}.png"
    bak = TEX / f"BaseMap{si}.png.bak_before_repatch"
    A, B = Image.open(bak).convert("RGB"), Image.open(p).convert("RGB")
    W, H = A.size
    px, py = W / CX, H / CY
    for tag, (gy, gx), half in picks:
        x0, y0 = int((gx - half) * px), int((gy - half) * py)
        x1, y1 = int((gx + half) * px), int((gy + half) * py)
        if x0 < 0 or y0 < 0 or x1 > W or y1 > H:
            continue
        a = A.crop((x0, y0, x1, y1))
        b = B.crop((x0, y0, x1, y1))
        z = max(1, int(200 / max(1, a.size[0])))
        a = a.resize((a.size[0] * z, a.size[1] * z), Image.Resampling.NEAREST)
        b = b.resize((b.size[0] * z, b.size[1] * z), Image.Resampling.NEAREST)
        pair = Image.new("RGB", (a.size[0] * 2 + 8, a.size[1]), (25, 25, 30))
        pair.paste(a, (0, 0))
        pair.paste(b, (a.size[0] + 8, 0))
        dr = ImageDraw.Draw(pair)
        dr.text((4, 2), f"{SEASONS[si]} {tag} 格({gx},{gy})  左=原 右=改后", fill=(255, 255, 0))
        rows.append(pair)

wid = max(r.size[0] for r in rows)
hgt = sum(r.size[1] for r in rows) + 8 * (len(rows) - 1)
sheet = Image.new("RGB", (wid, hgt), (18, 18, 22))
yy = 0
for r in rows:
    sheet.paste(r, (0, yy))
    yy += r.size[1] + 8
if max(sheet.size) > 1500:
    k = 1500 / float(max(sheet.size))
    sheet = sheet.resize((int(sheet.size[0] * k), int(sheet.size[1] * k)), Image.Resampling.LANCZOS)
sheet.save(OUT / "yyxk_repatch_compare.png")
print(f"\n已写出 {OUT / 'yyxk_repatch_compare.png'}  {sheet.size[0]}x{sheet.size[1]}")
