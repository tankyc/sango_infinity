# -*- coding: utf-8 -*-
"""证明图：在「当前底图」上把 122 座城的黑框位置标出来，并给 1:1 特写。
另外并排给出「原始美术」同一位置，看黑框是否落在格子上（而不是美术自己那个歪的图标）。"""
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

TEX = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Assets\Map\yyxk\BaseTex")
sc = json.loads(Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Scenario\Scenario1.json")
                .read_text(encoding="utf-8"))["citySet"]
cities = [(v.get("Name"), int(v["x"]), int(v["y"])) for v in sc.values()
          if "x" in v and int(v.get("BuildingType", 1)) == 1]


def center(x, y, px, py):
    return int((x + 0.5) * px), int((y + (x & 1) * 0.5 + 0.5) * py)


cur = Image.open(TEX / "BaseMap0.png").convert("RGB")
W, H = cur.size
d = ImageDraw.Draw(cur)
for nm, x, y in cities:
    cx, cy = center(x, y, W / 512.0, H / 512.0)
    d.ellipse([cx - 9, cy - 9, cx + 9, cy + 9], outline=(255, 40, 40), width=2)
cur.resize((900, 900), Image.Resampling.LANCZOS).save(
    r"D:\sango_infinity\Temp\proof_full.jpg", quality=88)
print(f"全图标出 122 座城（红圈）：Temp\\proof_full.jpg")

# 1:1 与 8 倍特写：三座相距较远的城
picks = cities[0], cities[len(cities) // 3], cities[len(cities) // 2]
R = 16
rows = []
for nm, x, y in picks:
    cx, cy = center(x, y, W / 512.0, H / 512.0)
    rows.append((nm, x, y, cur.crop((cx - R, cy - R, cx + R, cy + R)).resize((R * 16, R * 16),
                                                                              Image.Resampling.NEAREST)))
tile = R * 16
sheet = Image.new("RGB", (tile + 8, len(rows) * (tile + 8)), (20, 20, 24))
for k, (nm, x, y, im) in enumerate(rows):
    sheet.paste(im, (4, k * (tile + 8) + 4))
    print(f"特写 {nm} 格({x},{y})")
sheet.save(r"D:\sango_infinity\Temp\proof_zoom.jpg", quality=92)
print("已写出 Temp\\proof_zoom.jpg（8 倍特写，能看到那圈黑框）")
