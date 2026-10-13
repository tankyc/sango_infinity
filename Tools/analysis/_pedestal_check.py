# -*- coding: utf-8 -*-
"""核验「据点底座 = 1 个像素」：与 mod 原始美术逐像素比对，看每个据点邻域被改了几像素。"""
import json
from pathlib import Path

import numpy as np
from PIL import Image

TEX = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Assets\Map\yyxk\BaseTex")
SC = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Scenario\Scenario1.json")
sc = json.loads(SC.read_text(encoding="utf-8"))["citySet"]
sites = [(v.get("Name"), int(v["x"]), int(v["y"]), int(v.get("BuildingType", 1)))
         for v in sc.values() if "x" in v]
print(f"据点 {len(sites)} 个（城 {sum(1 for s in sites if s[3]==1)} / 关 "
      f"{sum(1 for s in sites if s[3]==2)} / 港 {sum(1 for s in sites if s[3]==3)}）")

for si in range(4):
    cur = np.asarray(Image.open(TEX / f"BaseMap{si}.png").convert("RGB"), np.int16)
    pri = np.asarray(Image.open(TEX / f"BaseMap{si}.png.bak_before_repatch").convert("RGB"), np.int16)
    H, W = cur.shape[:2]
    px, py = W / 512.0, H / 512.0
    diff = np.abs(cur - pri).sum(2) > 6
    cnts, col = [], []
    for nm, x, y, bt in sites:
        cx, cy = int((x + 0.5) * px), int((y + 0.5) * py)
        w = diff[max(0, cy - 1):cy + 2, max(0, cx - 1):cx + 2]   # 只 ±1 像素，避开山体明暗
        cnts.append(int(w.sum()))
        if w.sum():
            col.append(tuple(int(v) for v in cur[cy, cx]))
    u, c = np.unique(np.array(cnts), return_counts=True)
    print(f"\nBaseMap{si} ({W}x{H}，{px:.0f} 像素/格)")
    print(f"  每个据点 ±4 像素窗内被改的像素数: {dict(zip(u.tolist(), c.tolist()))}")
    print(f"  有改动者 {len(col)}/{len(sites)}；该像素颜色样例 {col[:4]}")
    print(f"  全图被改像素合计 {int(diff.sum())}"
          f"（其中据点底座约 {len(col)} 像素，其余是山体明暗）")

# 放大对照：左=原始美术 右=1 像素底座
big = [s for s in sites if s[3] == 1][:3]
cur = Image.open(TEX / "BaseMap0.png").convert("RGB")
pri = Image.open(TEX / "BaseMap0.png.bak_before_repatch").convert("RGB")
R, Z = 10, 14
tile = R * 2 * Z
sheet = Image.new("RGB", (tile * 2 + 24, len(big) * (tile + 8)), (20, 20, 24))
for k, (nm, x, y, bt) in enumerate(big):
    cx, cy = int((x + 0.5) * 4), int((y + 0.5) * 4)
    box = (cx - R, cy - R, cx + R, cy + R)
    sheet.paste(pri.crop(box).resize((tile, tile), Image.Resampling.NEAREST), (0, k * (tile + 8)))
    sheet.paste(cur.crop(box).resize((tile, tile), Image.Resampling.NEAREST),
                (tile + 16, k * (tile + 8)))
    print(f"放大窗口: {nm} 格({x},{y}) -> 像素({cx},{cy})")
k = min(1.0, 900.0 / max(sheet.size))
sheet.resize((int(sheet.size[0] * k), int(sheet.size[1] * k)),
             Image.Resampling.LANCZOS).save(r"D:\sango_infinity\Temp\pedestal_1px.jpg", quality=92)
print("已写出 Temp\\pedestal_1px.jpg（左=原始美术 / 右=1 像素底座，14 倍最近邻放大）")
