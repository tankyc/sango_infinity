# -*- coding: utf-8 -*-
"""一次性：对比「当前底图 / 官方 Default 底图 / yyxk 底图 / 当前 .bin 真实地形」。

用途：判断底图与当前地形到底哪里形变、城市底座在哪些位置对不上。
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw

TT = Path(r"D:\sango_infinity\Build\Content\Data\Common\TerrainTypes.json")
OUT = Path(r"D:\sango_infinity\Temp\basemap_cmp.png")
PANEL = 640

raw = json.loads(TT.read_text(encoding="utf-8"))
ttj = raw.get("TerrainTypes", raw)


def parse_color(v):
    if isinstance(v, str):
        p = [x for x in v.replace("(", "").replace(")", "").split(",") if x.strip()]
        try:
            n = [int(float(x)) for x in p[:3]]
            return tuple(n) if len(n) == 3 else None
        except ValueError:
            return None
    if isinstance(v, (list, tuple)) and len(v) >= 3:
        try:
            return tuple(int(float(x)) for x in v[:3])
        except (ValueError, TypeError):
            return None
    return None


colors = {}
for k, v in ttj.items():
    if str(k).isdigit() and isinstance(v, dict):
        c = parse_color(v.get("color"))
        if c:
            colors[int(k)] = c

CAMPAIGNS = [
    ("xworld BaseTex (我们的)", Path(r"D:\sango_infinity\Build\Mods\m6dwbcwk\Assets\Map\xworld\BaseTex\BaseMap0.png")),
    ("Default BaseTex (官方)", Path(r"D:\sango_infinity\Build\Content\Assets\Map\Default\BaseTex\BaseMap0.png")),
    ("yyxk BaseTex (mod作者)", Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Assets\Map\yyxk\BaseTex\BaseMap0.png")),
]


def fit(im, size):
    im = im.convert("RGB")
    w, h = im.size
    s = size / float(max(w, h))
    return im.resize((max(1, int(w * s)), max(1, int(h * s))), Image.Resampling.LANCZOS)


panels = []
for label, p in CAMPAIGNS:
    if not p.exists():
        print(f"缺: {p}")
        continue
    im = Image.open(p)
    print(f"{label}: {im.size[0]}x{im.size[1]}  {p.stat().st_size / 1024:.0f} KB")
    panels.append((label, fit(im, PANEL)))

# 当前 .bin 的真实地形
d = parse_bin(r"D:\sango_infinity\Build\Mods\m6dwbcwk\Map\xworld_full.bin")
cx, cy = d["cellsX"], d["cellsY"]
# 底图的行 ↔ vy ↔ worldX ↔ 格 y；列 ↔ vx ↔ worldZ ↔ 格 x。所以按 [y, x] 取地形。
ttT = d["gridRaw"][..., 0].astype(np.int32).T          # (cy, cx)
img = np.zeros((cy, cx, 3), np.uint8)
for t, c in colors.items():
    img[ttT == t] = c
unknown = ~np.isin(ttT, list(colors))
img[unknown] = (255, 0, 255)
ter = Image.fromarray(img)
print(f"当前 .bin 地形: {cx}x{cy}（按 [y,x] 排布）  未知地形 id "
      f"{sorted(set(ttT[unknown].ravel().tolist()))[:8]}")
panels.append(("当前 .bin 真实地形", fit(ter, PANEL)))

pad = 24
wid = sum(p.size[0] for _, p in panels) + pad * (len(panels) + 1)
hgt = max(p.size[1] for _, p in panels) + pad * 2 + 18
sheet = Image.new("RGB", (wid, hgt), (24, 24, 28))
dr = ImageDraw.Draw(sheet)
x = pad
for label, p in panels:
    sheet.paste(p, (x, pad + 18))
    dr.text((x, 4), label, fill=(255, 255, 255))
    dr.rectangle([x - 1, pad + 17, x + p.size[0], pad + 18 + p.size[1]], outline=(90, 90, 100))
    x += p.size[0] + pad
sheet.save(OUT)
print(f"已写出 {OUT}  {sheet.size[0]}x{sheet.size[1]}")
