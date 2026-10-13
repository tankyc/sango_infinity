# -*- coding: utf-8 -*-
"""一次性：把地形的海岸线 / 都市格 / 内政位 直接叠在 mod 原底图上，
用肉眼确认「部分形变」到底发生在哪里。"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
OUT = Path(r"D:\sango_infinity\Temp")
WATER_IDS = [9, 21, 22]

d = parse_bin(str(MOD / "Map/yyxk_2x.bin"))
GX, GY = d["cellsX"], d["cellsY"]
terr = d["gridRaw"][..., 0].astype(np.int32).T          # [y, x]
area = (d["gridRaw"][..., 5].astype(np.int32)
        | (d["gridRaw"][..., 6].astype(np.int32) << 8)).T
water = np.isin(terr, WATER_IDS)
city = terr == 17

# 海岸线 = 水与陆相邻的格
nb = np.zeros_like(water)
nb[1:, :] |= water[1:, :] != water[:-1, :]
nb[:, 1:] |= water[:, 1:] != water[:, :-1]
coast = nb

art = Image.open(MOD / "Assets/Map/yyxk/BaseTex/BaseMap0.png").convert("RGB")
W, H = art.size
sc = W / float(GX)
print(f"底图 {W}x{H}  格 {GX}x{GY}  {sc:.3f} 像素/格")
print(f"水格 {int(water.sum())}  海岸格 {int(coast.sum())}  都市格 {int(city.sum())}")

# 叠线：海岸=青，都市=红，areaId 分界=黄（细）
ov = np.zeros((GY, GX, 4), np.uint8)
ov[coast] = (0, 255, 255, 210)
ov[city] = (255, 40, 40, 230)
b_a = np.zeros((GY, GX), bool)
b_a[1:, :] |= (area[1:, :] != area[:-1, :]) & (area[1:, :] > 0) & (area[:-1, :] > 0)
b_a[:, 1:] |= (area[:, 1:] != area[:, :-1]) & (area[:, 1:] > 0) & (area[:, :-1] > 0)
ov[b_a & ~coast & ~city] = (255, 230, 0, 90)
ovi = Image.fromarray(ov).resize(art.size, Image.Resampling.NEAREST)
base = art.convert("RGBA")
base.alpha_composite(ovi)

# 拼三块放大窗口看细节（选海岸线密集处）
coords = np.argwhere(coast)
dens = []
for y0 in range(0, GY - 64, 64):
    for x0 in range(0, GX - 64, 64):
        dens.append((int(coast[y0:y0 + 64, x0:x0 + 64].sum()), x0, y0))
dens.sort(reverse=True)
picks = []
for _, x0, y0 in dens:
    if all(abs(x0 - p[1]) + abs(y0 - p[2]) > 80 for p in picks):
        picks.append((None, x0, y0))
    if len(picks) >= 3:
        break
Z = 3
crops = []
for _, x0, y0 in picks:
    box = (int(x0 * sc), int(y0 * sc), int((x0 + 64) * sc), int((y0 + 64) * sc))
    c = base.crop(box).convert("RGB")
    c = c.resize((c.size[0] * Z, c.size[1] * Z), Image.Resampling.NEAREST)
    dr = ImageDraw.Draw(c)
    dr.text((4, 2), f"格({x0},{y0}) 青=地形海岸线 红=都市格", fill=(255, 255, 0))
    crops.append(c)

full = base.convert("RGB").resize((900, 900), Image.Resampling.LANCZOS)
top = Image.new("RGB", (sum(c.size[0] for c in crops) + 8 * (len(crops) - 1), crops[0].size[1]),
                (20, 20, 24))
xx = 0
for c in crops:
    top.paste(c, (xx, 0))
    xx += c.size[0] + 8
sheet = Image.new("RGB", (max(top.size[0], 900), top.size[1] + 900 + 8), (20, 20, 24))
sheet.paste(top, (0, 0))
sheet.paste(full, (0, top.size[1] + 8))
if max(sheet.size) > 1500:
    s = 1500 / float(max(sheet.size))
    sheet = sheet.resize((int(sheet.size[0] * s), int(sheet.size[1] * s)), Image.Resampling.LANCZOS)
sheet.save(OUT / "yyxk_overlay.png")
print(f"已写出 {OUT / 'yyxk_overlay.png'}  {sheet.size[0]}x{sheet.size[1]}")

# 量化：海岸线与底图水体度的贴合
sa = np.asarray(art.resize((GX, GY), Image.Resampling.BOX), np.float32)
sw = sa[..., 2] - sa[..., 0]
print(f"\n海岸格的水体度 {sw[coast].mean():+.1f}（应接近水陆之间）")
print(f"  海岸格判为水的比例 {(sw[coast] > 10).mean():.3f}（理想 ~0.5）")
print(f"  水格判为水的比例 {(sw[water] > 10).mean():.3f}  陆格 {(sw[~water] > 10).mean():.3f}")

# 每个 areaId 的水/陆判错率，找形变集中的城
bad = []
for aid in sorted(set(area.T.ravel().tolist())):
    if aid == 0:
        continue
    m = area == aid
    if m.sum() < 200:
        continue
    e = water[m]
    o = sw[m] > 10
    bad.append((float((e != o).mean()), int(aid), int(m.sum())))
bad.sort(reverse=True)
print("\n水/陆判错率最高的城（可能底图与地图不符）:")
for r, aid, n in bad[:10]:
    print(f"    areaId {aid:>3}  格数 {n:>5}  判错 {r:.3f}")
print(f"  122 城平均判错 {np.mean([b[0] for b in bad]):.3f}")
