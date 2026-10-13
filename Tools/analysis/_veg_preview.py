# -*- coding: utf-8 -*-
"""一次性：把烘焙好的植被画成俯视图，核对地域偏向（树种是否按气候带分布）。"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.path.insert(0, str(Path(".").resolve()))
from sango_scale_bin import parse_bin          # noqa: E402

BIN = r"D:\sango_infinity\Build\Mods\m6dwbcwk\Map\xworld_full.bin"
SCALE = 0.5                                    # 1800x1000 -> 900x500

d = parse_bin(BIN)
cx, cy = d["cellsX"], d["cellsY"]
tt = d["gridRaw"][..., 0]
models = d["tail"]["models"][2]
trees = [m for m in models if m[1] == 6]

W, H = int(cx * SCALE), int(cy * SCALE)
img = Image.new("RGB", (W, H), (12, 20, 32))
px = img.load()

# 地形底色（按地形表近似色）
LAND = {1: (96, 118, 66), 2: (150, 126, 88), 3: (198, 176, 120), 4: (120, 140, 92),
        5: (110, 130, 100), 6: (58, 88, 52), 7: (58, 96, 128), 8: (170, 160, 140),
        10: (80, 110, 80), 11: (168, 148, 104), 12: (150, 130, 96), 13: (140, 120, 90),
        14: (130, 130, 130), 15: (176, 164, 130), 16: (110, 98, 84), 17: (200, 120, 110),
        18: (150, 150, 170), 19: (170, 140, 120), 20: (190, 180, 150)}
for y in range(cy):
    yy = int(y * SCALE)
    col = tt[:, y]
    for x in range(cx):
        c = LAND.get(int(col[x]))
        if c is not None:
            px[int(x * SCALE), yy] = c

# 树种颜色（冷暖顺序：泰加->温带->亚热带->热带）
COLOR = {14: (196, 220, 240), 7: (150, 200, 235), 8: (120, 180, 225),
         13: (120, 190, 120), 6: (170, 210, 130), 9: (235, 220, 120),
         10: (240, 180, 90), 5: (200, 160, 110), 11: (235, 105, 105), 12: (235, 60, 130)}

dr = ImageDraw.Draw(img)
G = float(d["gridSize"])
for m in trees:
    wx, _wy, wz = m[4]
    x = (wz - G * 0.5) / G
    y = (wx - G * 0.5 - (int(round(x)) & 1) * G * 0.5) / G
    X, Y = int(round(x * SCALE)), int(round(y * SCALE))
    if 0 <= X < W and 0 <= Y < H:
        c = COLOR.get(m[3], (255, 255, 255))
        dr.point((X, Y), fill=c)
        # 放大一点便于观察
        for dx2, dy2 in ((1, 0), (0, 1), (1, 1)):
            if X + dx2 < W and Y + dy2 < H:
                px[X + dx2, Y + dy2] = c
img.save("_veg_preview.png")
print(f"已写出 _veg_preview.png  {W}x{H}  树 {len(trees)} 棵")
print("配色: 14高松/7松树1/8松树2 冷蓝  13矮植3/6矮植2 绿  9扇叶1 黄  10扇枝丛 橙  "
      "5矮植1 褐  11芭叶1/12芭叶2 红/洋红")
