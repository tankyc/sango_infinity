# -*- coding: utf-8 -*-
"""一次性：像素级对齐测试 —— 同一地理窗口下，BaseTex 与「当前 .bin 真实地形」对照。

顺便标出我们的城市在底图上的像素位置，看黑色城市底座有没有落在正确位置。
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


COL = {int(k): parse_color(v.get("color")) for k, v in ttj.items()
       if str(k).isdigit() and isinstance(v, dict) and parse_color(v.get("color"))}

MAPS = [
    dict(name="yyxk", bin=r"D:\sango_infinity\Build\Mods\nftfwxpx\Map\yyxk_2x.bin",
         base=r"D:\sango_infinity\Build\Mods\nftfwxpx\Assets\Map\yyxk\BaseTex\BaseMap0.png",
         sc=r"D:\sango_infinity\Build\Mods\nftfwxpx\Scenario\Scenario1.json",
         cells=(512, 512), ppc=4.0),
    dict(name="xworld", bin=r"D:\sango_infinity\Build\Mods\m6dwbcwk\Map\xworld_full.bin",
         base=r"D:\sango_infinity\Build\Mods\m6dwbcwk\Assets\Map\xworld\BaseTex\BaseMap0.png",
         sc=r"D:\sango_infinity\Build\Mods\m6dwbcwk\Scenario\Scenario_world_full.json",
         cells=(1800, 1000), ppc=2048.0 / 7200.0),
]

WIN = 46          # 窗口半径（格）
Z = 3             # 放大倍数
sheets = []

for cfg in MAPS:
    bp = Path(cfg["base"])
    if not bp.exists():
        print(f"[跳过] 缺底图 {bp}")
        continue
    d = parse_bin(cfg["bin"])
    cx, cy = d["cellsX"], d["cellsY"]
    base = Image.open(bp).convert("RGB")
    print(f"\n=== {cfg['name']} ===")
    print(f"  底图 {base.size[0]}x{base.size[1]}   格 {cx}x{cy}   "
          f"实际 {base.size[0] / cx:.4f} x {base.size[1] / cy:.4f} 像素/格")

    # 当前地形图（行=y格, 列=x格）
    ttT = d["gridRaw"][..., 0].astype(np.int32).T
    timg = np.zeros((cy, cx, 3), np.uint8)
    for t, c in COL.items():
        timg[ttT == t] = c
    terr = Image.fromarray(timg)

    sc = json.loads(Path(cfg["sc"]).read_text(encoding="utf-8"))["citySet"]
    picks = []
    for v in sc.values():
        if int(v.get("BuildingType", 1)) != 1 or "x" not in v:
            continue
        x, y = int(v["x"]), int(v["y"])
        if WIN < x < cx - WIN and WIN < y < cy - WIN:
            picks.append((v.get("Name"), x, y))
    picks = picks[:1] + picks[len(picks) // 2:len(picks) // 2 + 1] + picks[-1:]

    sx, sy = base.size[0] / float(cx), base.size[1] / float(cy)
    rows = []
    for nm, x, y in picks:
        b = base.crop((int((x - WIN) * sx), int((y - WIN) * sy),
                       int((x + WIN) * sx), int((y + WIN) * sy)))
        t = terr.crop((x - WIN, y - WIN, x + WIN, y + WIN)).resize(b.size, Image.Resampling.NEAREST)
        b = b.resize((b.size[0] * Z, b.size[1] * Z), Image.Resampling.NEAREST)
        t = t.resize(b.size, Image.Resampling.NEAREST)
        pair = Image.new("RGB", (b.size[0] * 2 + 10, b.size[1]), (30, 30, 34))
        pair.paste(b, (0, 0))
        pair.paste(t, (b.size[0] + 10, 0))
        dr = ImageDraw.Draw(pair)
        # 标记城市格中心
        mx = int(WIN * sx * Z)
        my = int(WIN * sy * Z)
        dr.line([mx - 12, my, mx + 12, my], fill=(255, 0, 0), width=2)
        dr.line([mx, my - 12, mx, my + 12], fill=(255, 0, 0), width=2)
        dr.text((4, 2), f"{nm} 格({x},{y})  左=BaseTex 右=当前地形", fill=(255, 255, 0))
        rows.append(pair)
    if not rows:
        continue
    wid = max(r.size[0] for r in rows)
    hgt = sum(r.size[1] for r in rows) + 10 * (len(rows) - 1)
    sheet = Image.new("RGB", (wid, hgt), (20, 20, 24))
    yy = 0
    for r in rows:
        sheet.paste(r, (0, yy))
        yy += r.size[1] + 10
    p = Path(rf"D:\sango_infinity\Temp\pedestal_{cfg['name']}.png")
    sheet.save(p)
    print(f"  已写出 {p}  {sheet.size[0]}x{sheet.size[1]}  ({len(rows)} 个窗口)")
    print(f"  窗口城市: {[(n, x, y) for n, x, y in picks]}")
