# -*- coding: utf-8 -*-
"""一次性：底图修正的前后对比（城市底座 + 外圈虚空）。"""
import json
from pathlib import Path

from PIL import Image, ImageDraw

MOD = Path(r"D:\sango_infinity\Build\Mods\m6dwbcwk")
BASE = MOD / "Assets/Map/xworld/BaseTex/BaseMap0.png"
NEW = Path(r"D:\sango_infinity\Temp\basemap_patch_0_Autumn.png")
SC = MOD / "Scenario/Scenario_world_full.json"
CX, CY = 1800, 1000
OUT = Path(r"D:\sango_infinity\Temp\basemap_before_after.png")

old = Image.open(BASE).convert("RGB")
new = Image.open(NEW).convert("RGB")
px, py = old.size[0] / CX, old.size[1] / CY
sc = json.loads(SC.read_text(encoding="utf-8"))["citySet"]
cities = [(v.get("Name"), int(v["x"]), int(v["y"])) for v in sc.values()
          if int(v.get("BuildingType", 1)) == 1 and "x" in v]
picks = [cities[60], cities[len(cities) // 2], cities[-60]]

W = 30          # 窗口半径（格）
Z = 4
rows = []
for nm, x, y in picks:
    box = (int((x - W) * px), int((y - W) * py), int((x + W) * px), int((y + W) * py))
    a = old.crop(box)
    b = new.crop(box)
    z = (a.size[0] * Z, a.size[1] * Z)
    a = a.resize(z, Image.Resampling.NEAREST)
    b = b.resize(z, Image.Resampling.NEAREST)
    pair = Image.new("RGB", (z[0] * 2 + 8, z[1]), (30, 30, 34))
    pair.paste(a, (0, 0))
    pair.paste(b, (z[0] + 8, 0))
    dr = ImageDraw.Draw(pair)
    mx = int(W * px * Z)
    my = int(W * py * Z)
    for im_x in (mx, mx + z[0] + 8):
        dr.line([im_x - 10, my, im_x + 10, my], fill=(255, 70, 70), width=1)
        dr.line([im_x, my - 10, im_x, my + 10], fill=(255, 70, 70), width=1)
    dr.text((4, 2), f"{nm} 格({x},{y})    左=修改前   右=修改后", fill=(255, 255, 0))
    rows.append(pair)

# 外圈：取地图左下角一块
box = (0, int((CY - 70) * py), int(70 * px), int(CY * py))
a = old.crop(box).resize((box[2] * 2, (box[3] - box[1]) * 2), Image.Resampling.NEAREST)
b = new.crop(box).resize((box[2] * 2, (box[3] - box[1]) * 2), Image.Resampling.NEAREST)
pair = Image.new("RGB", (a.size[0] * 2 + 8, a.size[1]), (30, 30, 34))
pair.paste(a, (0, 0))
pair.paste(b, (a.size[0] + 8, 0))
ImageDraw.Draw(pair).text((4, 2), "地图左下角外圈   左=修改前   右=修改后", fill=(255, 255, 0))
rows.append(pair)

wid = max(r.size[0] for r in rows)
hgt = sum(r.size[1] for r in rows) + 8 * (len(rows) - 1)
sheet = Image.new("RGB", (wid, hgt), (18, 18, 22))
yy = 0
for r in rows:
    sheet.paste(r, (0, yy))
    yy += r.size[1] + 8
if max(sheet.size) > 1500:
    s = 1500 / float(max(sheet.size))
    sheet = sheet.resize((int(sheet.size[0] * s), int(sheet.size[1] * s)), Image.Resampling.LANCZOS)
sheet.save(OUT)
print(f"已写出 {OUT}  {sheet.size[0]}x{sheet.size[1]}")
print(f"窗口城市: {picks}")
