# -*- coding: utf-8 -*-
"""核验新版底座：城池 = 一圈黑框（4x4 -> 12 像素），港/关 = 1 个像素；且旧底座像素已全清。"""
import json
from pathlib import Path

import numpy as np
from PIL import Image

TEX = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Assets\Map\yyxk\BaseTex")
sc = json.loads(Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Scenario\Scenario1.json")
                .read_text(encoding="utf-8"))["citySet"]
sites = [(v.get("Name"), int(v["x"]), int(v["y"]), int(v.get("BuildingType", 1)))
         for v in sc.values() if "x" in v]
NAME = {1: "城", 2: "关", 3: "港"}


def center(x, y, px, py):
    return int((x + 0.5) * px), int((y + (x & 1) * 0.5 + 0.5) * py)


for si in range(4):
    cur = np.asarray(Image.open(TEX / f"BaseMap{si}.png").convert("RGB"), np.int16)
    pri = np.asarray(Image.open(TEX / f"BaseMap{si}.png.bak_before_repatch").convert("RGB"),
                     np.int16)
    H, W = cur.shape[:2]
    px, py = W / 512.0, H / 512.0
    chg = np.abs(cur - pri).sum(2) > 6
    blk = cur.sum(2) < 40                      # 近黑像素（山体明暗不会这么黑）
    res = {}
    for nm, x, y, bt in sites:
        cx, cy = center(x, y, px, py)
        w = slice(max(0, cy - 3), cy + 4), slice(max(0, cx - 3), cx + 4)
        n_ped = int((chg[w] & blk[w]).sum())       # 本据点造成的纯黑改动像素
        res.setdefault(NAME[bt], []).append(n_ped)
    print(f"\nBaseMap{si} ({W}x{H}, {px:.0f} 像素/格)")
    for t in ("城", "关", "港"):
        v = np.array(res.get(t, []))
        u, c = np.unique(v, return_counts=True)
        print(f"  {t} {len(v)} 个  每个的纯黑改动像素数: {dict(zip(u.tolist(), c.tolist()))}")

    # 全图纯黑改动像素总数 = 城池12*122 + 港关1*371 ?
    tot = int((chg & blk).sum())
    print(f"  全图「纯黑且被改动」的像素合计 {tot}"
          f"（预期 12*122 + 1*371 = {12*122+371}）")

# 放大对照：2 座城 + 2 个关/港
picks = [s for s in sites if s[3] == 1][:2] + [s for s in sites if s[3] == 2][:1] + \
        [s for s in sites if s[3] == 3][:1]
cur = Image.open(TEX / "BaseMap0.png").convert("RGB")
pri = Image.open(TEX / "BaseMap0.png.bak_before_repatch").convert("RGB")
R, Z = 8, 18
tile = R * 2 * Z
sheet = Image.new("RGB", (tile * 2 + 24, len(picks) * (tile + 8)), (20, 20, 24))
for k, (nm, x, y, bt) in enumerate(picks):
    cx, cy = center(x, y, 4.0, 4.0)
    box = (cx - R, cy - R, cx + R, cy + R)
    sheet.paste(pri.crop(box).resize((tile, tile), Image.Resampling.NEAREST), (0, k * (tile + 8)))
    sheet.paste(cur.crop(box).resize((tile, tile), Image.Resampling.NEAREST),
                (tile + 16, k * (tile + 8)))
    print(f"窗口 {NAME[bt]} {nm} 格({x},{y}) 中心像素({cx},{cy})")
k = min(1.0, 900.0 / max(sheet.size))
sheet.resize((int(sheet.size[0] * k), int(sheet.size[1] * k)),
             Image.Resampling.LANCZOS).save(r"D:\sango_infinity\Temp\pedestal_ring.jpg", quality=92)
print("已写出 Temp\\pedestal_ring.jpg（左=原始美术 右=新底座，18 倍放大）")
