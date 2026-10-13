# -*- coding: utf-8 -*-
"""一次性：
  A) 量化 xworld 底图与当前 .bin 地形的差异（水域掩膜 IoU + 外圈虚空框）
  B) 从官方 Default 底图上学习「城市阴影底座」的画法（按都市格定位裁剪放大）
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw

TT = Path(r"D:\sango_infinity\Build\Content\Data\Common\TerrainTypes.json")
raw = json.loads(TT.read_text(encoding="utf-8"))
ttj = raw.get("TerrainTypes", raw)
WATER = {int(k) for k, v in ttj.items()
         if str(k).isdigit() and isinstance(v, dict) and v.get("isWater")}
CITY_T = 17
print("水域地形 id:", sorted(WATER))

# ---------------- A) xworld 差异量化 ----------------
XW = Path(r"D:\sango_infinity\Build\Mods\m6dwbcwk")
d = parse_bin(str(XW / "Map/xworld_full.bin"))
cx, cy = d["cellsX"], d["cellsY"]
tt = d["gridRaw"][..., 0].astype(np.int32)          # [x, y]
base = Image.open(XW / "Assets/Map/xworld/BaseTex/BaseMap0.png").convert("RGB")
bw, bh = base.size
print(f"\n=== A) xworld 底图 {bw}x{bh} vs 地形 {cx}x{cy} ===")

ba = np.asarray(base.resize((cx, cy), Image.Resampling.BOX), np.int32)
# 底图的水：蓝通道明显高于红（我们 gamut 调色下水色偏蓝）
water_img = (ba[..., 2] > ba[..., 0] + 12)
water_ter = np.isin(tt, list(WATER))
inter = int((water_img & water_ter.T).sum())
union = int((water_img | water_ter.T).sum())
print(f"  水域掩膜 IoU = {inter / max(1, union):.3f}   "
      f"底图判为水 {water_img.mean():.3f} / 地形判为水 {water_ter.mean():.3f}")

# 外圈 20 格：当前 .bin 已置 terrainType=0，看底图在那里是什么
M = 20
E = np.zeros((cx, cy), bool)
E[:M] = E[-M:] = True
E[:, :M] = E[:, -M:] = True
print(f"  外圈 20 格: 地形里 terrainType==0 的比例 {float((tt[E] == 0).mean()):.3f}"
      f"（当前 .bin 应接近 1.0）")
print(f"              底图在那里的水域比例 {float(water_img[E.T].mean()):.3f}"
      f"（若明显 >0 说明底图还是改动前的地形）")

# 城市底座：检查城市格附近像素的暗度（底座应是暗色）
sc = json.loads((XW / "Scenario/Scenario_world_full.json").read_text(encoding="utf-8"))["citySet"]
lum = (ba @ [0.299, 0.587, 0.114])
# 底图行=格 y, 列=格 x  -> lum[y, x]
pts = []
for v in sc.values():
    if int(v.get("BuildingType", 1)) != 1 or "x" not in v:
        continue
    x, y = int(v["x"]), int(v["y"])
    if 8 < x < cx - 8 and 8 < y < cy - 8:
        pts.append((v.get("Name"), x, y))
print(f"  取样 {len(pts)} 座城：城市格亮度 vs 全图亮度")
vals = np.array([lum[y, x] for _, x, y in pts])
print(f"    城市格 mean {vals.mean():.1f}  p10 {np.percentile(vals, 10):.1f}"
      f"   全图 mean {lum.mean():.1f}  p10 {np.percentile(lum, 10):.1f}")
print(f"    -> 城市格如果明显更暗，说明底图原本就画了底座；接近则说明没画")

# ---------------- B) 官方底图的底座画法 ----------------
DD = Path(r"D:\sango_infinity\Build\Content\Assets\Map\Default")
dm = parse_bin(str(DD.parent.parent.parent / "Map/DefaultMap.bin"))
dtt = dm["gridRaw"][..., 0].astype(np.int32)
dcx, dcy = dm["cellsX"], dm["cellsY"]
dcity = np.argwhere(dtt == CITY_T)              # [x, y] 列表
print(f"\n=== B) 官方 Default 底图 ===")
print(f"  DefaultMap 格 {dcx}x{dcy}  都市(17) 格 {len(dcity)}")

dimg = Image.open(DD / "BaseTex/BaseMap0.png").convert("RGB")
print(f"  底图 {dimg.size[0]}x{dimg.size[1]}   {dimg.size[0] / dcx:.3f} 像素/格")

# 取几座互相远离的城，放大裁剪
sel = []
for x, y in dcity[::max(1, len(dcity) // 40)]:
    if all((abs(x - sx) + abs(y - sy)) > 40 for sx, sy, _ in sel):
        sel.append((int(x), int(y), len(sel)))
    if len(sel) >= 3:
        break
sx_scale = dimg.size[0] / float(dcx)
sy_scale = dimg.size[1] / float(dcy)
W = 26
Z = 5
crops = []
for x, y, i in sel:
    c = dimg.crop((int((x - W) * sx_scale), int((y - W) * sy_scale),
                   int((x + W) * sx_scale), int((y + W) * sy_scale)))
    c = c.resize((c.size[0] * Z, c.size[1] * Z), Image.Resampling.NEAREST)
    dr = ImageDraw.Draw(c)
    mx, my = int(W * sx_scale * Z), int(W * sy_scale * Z)
    dr.line([mx - 14, my, mx + 14, my], fill=(255, 60, 60), width=1)
    dr.line([mx, my - 14, mx, my + 14], fill=(255, 60, 60), width=1)
    dr.text((4, 2), f"都市格({x},{y})", fill=(255, 255, 0))
    crops.append(c)
if crops:
    wid = sum(c.size[0] for c in crops) + 8 * (len(crops) - 1)
    hgt = max(c.size[1] for c in crops)
    sheet = Image.new("RGB", (wid, hgt), (20, 20, 24))
    xx = 0
    for c in crops:
        sheet.paste(c, (xx, 0))
        xx += c.size[0] + 8
    if max(sheet.size) > 1400:
        s = 1400 / float(max(sheet.size))
        sheet = sheet.resize((int(sheet.size[0] * s), int(sheet.size[1] * s)),
                             Image.Resampling.LANCZOS)
    p = Path(r"D:\sango_infinity\Temp\pedestal_official.png")
    sheet.save(p)
    print(f"  已写出 {p}  {sheet.size[0]}x{sheet.size[1]}")

# 官方底图中城市格的暗度
dba = np.asarray(dimg.resize((dcx, dcy), Image.Resampling.BOX), np.int32)
dlum = dba @ [0.299, 0.587, 0.114]
dv = np.array([dlum[y, x] for x, y in dcity])
print(f"  城市格亮度 mean {dv.mean():.1f} p10 {np.percentile(dv, 10):.1f}"
      f"   全图 mean {dlum.mean():.1f} p10 {np.percentile(dlum, 10):.1f}")
