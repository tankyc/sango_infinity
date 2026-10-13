# -*- coding: utf-8 -*-
"""一次性：搞清 yyxk 这张图里 terrainType 各 id 的含义。

办法：把 mod 自带的地表贴图 Terrain/Autumn/layer_N.png（N=id）与地形分布并排看，
再统计各 id 的格数/连通域/与什么相邻 —— 一眼能看出哪个 id 是林地。
"""
import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
OUT = Path(r"D:\sango_infinity\Temp")
TT = json.loads(Path(r"D:\sango_infinity\Build\Content\Data\Common\TerrainTypes.json")
                .read_text(encoding="utf-8"))["TerrainTypes"]

d = parse_bin(str(MOD / "Map/yyxk_2x.bin"))
cx, cy = d["cellsX"], d["cellsY"]
terr = d["gridRaw"][..., 0].astype(np.int32)
cnt = Counter(terr.ravel().tolist())

print("=" * 78)
print(f"yyxk_2x.bin  格 {cx}x{cy}")
print("id   格数     占比    游戏表名   可通行 颜色         mod 地表贴图")
for tid in sorted(cnt, key=lambda t: -cnt[t]):
    info = TT.get(str(tid))
    name = info["Name"] if info else "**表外**"
    mv = ("是" if info["moveable"] else "否") if info else "?"
    col = tuple(info["color"]) if info else "?"
    lp = MOD / f"Assets/Map/yyxk/Terrain/Autumn/layer_{tid}.png"
    lsz = f"{lp.stat().st_size / 1024:.0f} KB" if lp.exists() else "无"
    print(f"{tid:>3}  {cnt[tid]:>7}  {cnt[tid] / terr.size:>6.3f}  {name:<8} {mv:^6} "
          f"{str(col):<12} {lsz}")

# 候选植被地形（森 6 / 崖 16 / 表外的 32）
cand = [6, 16, 32]
print("\n候选地形的连通域与邻居分析:")
for t in cand:
    m = terr == t
    if m.sum() == 0:
        continue
    from scipy import ndimage
    lab, n = ndimage.label(m)
    sizes = np.bincount(lab.ravel())[1:]
    nb = Counter()
    for dy, dx in ((0, 1), (1, 0)):
        a = m[max(0, dy):cx - dy or cx, max(0, dx):cy - dx or cy]
        b = terr[max(0, dy):cx - dy or cx, max(0, dx):cy - dx or cy]
        sel = a & ~m[max(0, dy):cx - dy or cx, max(0, dx):cy - dx or cy]
        nb.update(b[sel].tolist())
    print(f"  t{t}: 格 {int(m.sum())}  连通域 {n}  最大 {int(sizes.max()) if n else 0}"
          f"  中位 {int(np.median(sizes)) if n else 0}   邻居地形 {nb.most_common(6)}")

# 既有 mapModels
print(f"\n既有 mapModels {len(d['tail']['models'])} 条:")
for i, m in enumerate(d["tail"]["models"]):
    print(f"  #{i}: {m}")

# 拼图：地形分布（用 mod 的 layer 贴图取色）+ 各候选 layer 放大
sheet_items = []
timg = np.zeros((cy, cx, 3) if False else (cx, cy, 3), np.uint8)
palette = {}
for tid in sorted(cnt):
    lp = MOD / f"Assets/Map/yyxk/Terrain/Autumn/layer_{tid}.png"
    if lp.exists():
        a = np.asarray(Image.open(lp).convert("RGB").resize((32, 32)), np.float32)
        palette[tid] = a.reshape(-1, 3).mean(0)
    else:
        info = TT.get(str(tid))
        palette[tid] = np.array(info["color"], np.float32) if info else np.array([255, 0, 255], np.float32)
for tid, c in palette.items():
    timg[terr == tid] = c.astype(np.uint8)
mapimg = Image.fromarray(np.transpose(timg, (1, 0, 2)))            # 行=y
mapimg.resize((620, 620), Image.Resampling.NEAREST).save(OUT / "yyxk_terrain_ids_map.png")
print(f"\n已写出 {OUT / 'yyxk_terrain_ids_map.png'}")

# layer 贴图对比条
rows = []
for t in [1, 6, 16, 32, 9, 15]:
    lp = MOD / f"Assets/Map/yyxk/Terrain/Autumn/layer_{t}.png"
    if not lp.exists():
        continue
    im = Image.open(lp).convert("RGB")
    k = 160 / max(1, max(im.size))
    im = im.resize((max(1, int(im.size[0] * k)), max(1, int(im.size[1] * k))), Image.Resampling.NEAREST)
    cell = Image.new("RGB", (200, 190), (25, 25, 30))
    cell.paste(im, ((200 - im.size[0]) // 2, 22))
    dr = ImageDraw.Draw(cell)
    info = TT.get(str(t))
    dr.text((4, 4), f"layer_{t}  {info['Name'] if info else '表外'}", fill=(255, 255, 0))
    rows.append(cell)
sheet = Image.new("RGB", (len(rows) * 204, 190), (18, 18, 22))
for i, r in enumerate(rows):
    sheet.paste(r, (i * 204, 0))
sheet.save(OUT / "yyxk_layer_strip.png")
print(f"已写出 {OUT / 'yyxk_layer_strip.png'}  {sheet.size[0]}x{sheet.size[1]}")
