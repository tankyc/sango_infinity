# -*- coding: utf-8 -*-
"""一次性：把 yyxk_2x.bin 画成俯视图（地形原色 + 高度晕渲），用于评估山体形状与沿岸。"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw

BIN = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Map\yyxk_2x.bin")
TT = Path(r"D:\sango_infinity\Build\Content\Data\Common\TerrainTypes.json")
OUT = Path(r"D:\sango_infinity\Temp\yyxk_preview.png")
SCALE = 0.98

raw = json.loads(TT.read_text(encoding="utf-8"))
ttj = raw.get("TerrainTypes", raw)


def parse_color(v):
    if isinstance(v, str):
        parts = [p for p in v.replace("(", "").replace(")", "").split(",") if p.strip()]
        try:
            n = [int(float(p)) for p in parts[:3]]
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
    if not str(k).isdigit() or not isinstance(v, dict):
        continue
    c = parse_color(v.get("color"))
    colors[int(k)] = c
print("地形颜色:", {k: colors[k] for k in sorted(colors)[:12]})

d = parse_bin(str(BIN))
cx, cy = d["cellsX"], d["cellsY"]
tt = d["gridRaw"][..., 0].astype(np.int32)
verts = d["verts"]
G, QUAD = float(d["gridSize"]), float(d["quadSize"])
VS = int(G // QUAD)

W, H = int(cx * SCALE), int(cy * SCALE)
img = Image.new("RGB", (W, H), (0, 0, 0))
px = img.load()

# 每格高度
xs = np.arange(cx, dtype=np.int64)[:, None]
ys = np.arange(cy, dtype=np.int64)[None, :]
vx = np.clip(VS * xs + VS // 2, 0, verts.shape[0] - 1)
vy = np.clip(VS * ys + (xs & 1) * (VS // 2) + VS // 2, 0, verts.shape[1] - 1)
hc = verts[vx, vy, 0].astype(np.float32)

# 晕渲：高度梯度做明暗
gy, gx = np.gradient(hc)
slope = np.sqrt(gx ** 2 + gy ** 2)
shade = np.clip(1.0 - slope * 0.12, 0.35, 1.25)

for x in range(cx):
    X = int(x * SCALE)
    for y in range(cy):
        t = int(tt[x, y])
        c = colors.get(t) or (90, 90, 90)
        s = float(shade[x, y])
        px[X, int(y * SCALE)] = (min(255, int(c[0] * s)), min(255, int(c[1] * s)), min(255, int(c[2] * s)))

# 叠加城市/关/港与已有植被
G_W = float(d["gridSize"])


def cell_to_px(col, row):
    wz = col * G_W + G_W * 0.5
    wx = row * G_W + (col & 1) * G_W * 0.5 + G_W * 0.5
    return int((wz / G_W) * SCALE), int((wx / G_W) * SCALE)


dr = ImageDraw.Draw(img)
ms = d["tail"]["models"][2]
for m in ms:
    z, _, xw = m[4]
    X, Y = int((z / G_W) * SCALE), int((xw / G_W) * SCALE)
    if 0 <= X < W and 0 <= Y < H:
        dr.point((X, Y), fill=(255, 255, 0))

sc = json.loads(Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Scenario\Scenario1.json")
                .read_text(encoding="utf-8"))
for v in sc["citySet"].values():
    if "x" not in v:
        continue
    X, Y = cell_to_px(int(v["x"]), int(v["y"]))
    if not (0 <= X < W and 0 <= Y < H):
        continue
    bt = v.get("BuildingType")
    col = (255, 40, 40) if bt == 1 else ((80, 160, 255) if bt == 3 else (255, 160, 0))
    dr.rectangle([X - 1, Y - 1, X + 1, Y + 1], fill=col)

img.save(OUT)
print(f"已写出 {OUT}  {W}x{H}")
print("配色: 地形用 TerrainTypes.json 原色 + 高度晕渲 | 红=城 橙=关 蓝=港 黄=已有植被")
