# -*- coding: utf-8 -*-
"""一次性：生成「圈区域」用的参考图 —— 原美术 + 坐标网格 + 城名 + 地形海岸线。

网格：每 64 格一条线（8x8 个小区，编号 A1~H8，A=列 1=行），线附近标格坐标。
城名：用剧本里的城名标在对应格上，方便口述位置。
海岸线：青色 = 当前 .bin 的地形海岸线（真值），用来对比美术画的海岸在哪。
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw, ImageFont

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
OUT = Path(r"D:\sango_infinity\Temp")
WATER = [7, 8, 9, 21, 22]

d = parse_bin(str(MOD / "Map/yyxk_2x.bin"))
GX, GY = d["cellsX"], d["cellsY"]
terr = d["gridRaw"][..., 0].astype(np.int32).T          # [y, x]
water = np.isin(terr, WATER)
nb = np.zeros_like(water)
nb[1:, :] |= water[1:, :] != water[:-1, :]
nb[:, 1:] |= water[:, 1:] != water[:, :-1]
coast = nb

sc = json.loads((MOD / "Scenario/Scenario1.json").read_text(encoding="utf-8"))["citySet"]
cities = [(v.get("Name", "?"), int(v["x"]), int(v["y"]), int(v.get("BuildingType", 1)))
          for v in sc.values() if "x" in v]
cities.sort(key=lambda c: -int(c[0] == "?"))
print(f"格 {GX}x{GY}  据点 {len(cities)}  海岸格 {int(coast.sum())}")


def font(sz):
    for p in (r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\simhei.ttf",
              r"C:\Windows\Fonts\msyhbd.ttc"):
        if Path(p).exists():
            try:
                return ImageFont.truetype(p, sz)
            except OSError:
                pass
    return ImageFont.load_default()


def build(art, scale, cell0, cell1, tag, off=(0, 0)):
    """在 art（已按 scale 放大的图）上画网格/城名/海岸线，裁出 cell0..cell1。

    off = 这张图左上角对应的全局格坐标（象限图不是从 0,0 开始，标注必须加上它）。
    """
    img = art.copy()
    dr = ImageDraw.Draw(img)
    S = scale
    step = 64
    NX, NY = int(art.size[0] / S), int(art.size[1] / S)
    ox, oy = off
    f_big, f_small = font(int(7 * S) + 12), font(int(5 * S) + 9)
    # 网格（只画落在本图范围内的全局 64 格线）
    for gx in range(0, GX + 1, step):
        i = gx - ox
        if 0 <= i <= NX:
            dr.line([(i * S, 0), (i * S, NY * S)], fill=(255, 255, 255), width=2)
    for gy in range(0, GY + 1, step):
        j = gy - oy
        if 0 <= j <= NY:
            dr.line([(0, j * S), (NX * S, j * S)], fill=(255, 255, 255), width=2)
    # 海岸线
    ys, xs = np.nonzero(coast)
    for y, x in zip(ys.tolist(), xs.tolist()):
        i, j = x - ox, y - oy
        if 0 <= i < NX and 0 <= j < NY:
            dr.rectangle([i * S, j * S, (i + 1) * S - 1, (j + 1) * S - 1], outline=(0, 255, 255))
    # 城名
    for nm, x, y, bt in cities:
        i, j = x - ox, y - oy
        if not (0 <= i < NX and 0 <= j < NY):
            continue
        cx, cy = (i + 0.5) * S, (j + 0.5) * S
        col = (255, 70, 70) if bt == 1 else ((255, 190, 60) if bt == 2 else (120, 200, 255))
        dr.ellipse([cx - 0.7 * S, cy - 0.7 * S, cx + 0.7 * S, cy + 0.7 * S], fill=col)
        if bt == 1:
            dr.text((cx + 1.0 * S, cy - 0.6 * S), nm, fill=(255, 255, 90), font=f_small,
                    stroke_width=2, stroke_fill=(0, 0, 0))
    # 小区编号 + 全局格坐标（只有本图的格线才标）
    for gx in range(0, GX + 1, step):
        i = gx - ox
        if not (0 <= i <= NX):
            continue
        for gy in range(0, GY + 1, step):
            j = gy - oy
            if not (0 <= j <= NY):
                continue
            code = f"{chr(ord('A') + gx // step)}{gy // step + 1}"
            dr.text((i * S + 5, j * S + 5), code, fill=(255, 255, 255),
                    font=f_big, stroke_width=2, stroke_fill=(0, 0, 0))
            dr.text((i * S + 5, j * S + 5 + f_big.size + 6),
                    f"格 x={gx} y={gy}", fill=(190, 255, 190),
                    font=f_small, stroke_width=2, stroke_fill=(0, 0, 0))
    dr.text((8, 4), "青框=当前 .bin 地形海岸线   黄字=城名  橙=关 蓝=港   白字=小区号 + 格坐标",
            fill=(255, 255, 255), font=f_big, stroke_width=2, stroke_fill=(0, 0, 0))
    x0, y0, x1, y1 = int(cell0[0] * S), int(cell0[1] * S), int(cell1[0] * S), int(cell1[1] * S)
    return img.crop((x0, y0, x1, y1)), tag


art0 = Image.open(MOD / "Assets/Map/yyxk/BaseTex/BaseMap0.png").convert("RGB")
assert art0.size == (GY * 4, GX * 4) or art0.size == (GX * 4, GY * 4), art0.size
# 美术是 2048x2048 对 512x512 格 -> 4 像素/格，已在网格坐标下
full, _ = build(art0, 4, (0, 0), (GX, GY), "full")
if max(full.size) > 2000:
    k = 2000 / float(max(full.size))
    full = full.resize((int(full.size[0] * k), int(full.size[1] * k)), Image.Resampling.LANCZOS)
full.save(OUT / "yyxk_guide_full.png")
print(f"已写出 {OUT / 'yyxk_guide_full.png'}  {full.size[0]}x{full.size[1]}")

# 4 个象限，各放大 2 倍（8 像素/格），标注用全局格坐标
H = GX // 2
for qi, (x0, y0) in enumerate([(0, 0), (H, 0), (0, H), (H, H)]):
    crop = art0.crop((x0 * 4, y0 * 4, (x0 + H) * 4, (y0 + H) * 4))
    up = crop.resize((H * 8, H * 8), Image.Resampling.LANCZOS)
    S, step = 8, 64
    pad = step * S                        # 留出 64 格边距，使标注范围外扩
    canvas = Image.new("RGB", (up.size[0] + pad, up.size[1] + pad), (30, 34, 40))
    canvas.paste(up, (pad // 2, pad // 2))
    q, _ = build(canvas, S, (0, 0), (canvas.size[0] // S, canvas.size[1] // S),
                 f"q{qi}", off=(x0 - step // 2, y0 - step // 2))
    q = q.crop((pad // 2, pad // 2, pad // 2 + up.size[0], pad // 2 + up.size[1]))
    dr = ImageDraw.Draw(q)
    dr.rectangle([0, 26, q.size[0], 66], fill=(0, 0, 0))
    dr.text((8, 30), f"本图覆盖 格 x {x0}~{x0 + H} / y {y0}~{y0 + H}"
                     f"（小区 {chr(ord('A') + x0 // 64)}{y0 // 64 + 1} ~ "
                     f"{chr(ord('A') + (x0 + H - 1) // 64)}{(y0 + H - 1) // 64 + 1}）",
            fill=(255, 255, 255), font=font(30), stroke_width=2, stroke_fill=(0, 0, 0))
    if max(q.size) > 2000:
        k = 2000 / float(max(q.size))
        q = q.resize((int(q.size[0] * k), int(q.size[1] * k)), Image.Resampling.LANCZOS)
    q.save(OUT / f"yyxk_guide_q{qi}_x{x0}y{y0}.png")
    print(f"已写出 {OUT / f'yyxk_guide_q{qi}_x{x0}y{y0}.png'}  {q.size[0]}x{q.size[1]}")
