# -*- coding: utf-8 -*-
"""一次性：查 yyxk mod 的剧本(城市/州/区域) 与地图内已有物件，供植被分带设计。"""
import collections
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
SC = MOD / "Scenario/Scenario1.json"
BIN = MOD / "Map/yyxk_2x.bin"

sc = json.loads(SC.read_text(encoding="utf-8"))
cs = sc.get("citySet", {})
print("=" * 78)
print(f"{SC.name}  {SC.stat().st_size / 1048576:.2f} MB")
print("=" * 78)
print(f"  顶层键: {list(sc.keys())}")
print(f"  citySet {len(cs)} 个")
k0 = list(cs)[0]
print(f"  样例 city[{k0}] 字段: {sorted(cs[k0].keys())}")
prov = collections.Counter(v.get("province", v.get("ProvinceId")) for v in cs.values())
print(f"  province 分布: {dict(sorted(prov.items(), key=lambda x: str(x[0])))}")
bt = collections.Counter(v.get("BuildingType") for v in cs.values())
print(f"  BuildingType 分布: {dict(bt)}   (1=城 2=关 3=港)")
xs = [v["x"] for v in cs.values() if "x" in v]
ys = [v["y"] for v in cs.values() if "y" in v]
print(f"  城市格坐标 x {min(xs)}..{max(xs)}  y {min(ys)}..{max(ys)}")
print(f"  名称样例: {[v.get('Name') for v in list(cs.values())[:14]]}")
print(f"  派系 forceSet {len(sc.get('forceSet', {}))}  军团 {len(sc.get('corpsSet', {}))}"
      f"  武将 {len(sc.get('personSet', {}))}")

# 州/区域数据（基础内容）
for f in ("Provinces.json", "Regions.json"):
    p = Path(r"D:\sango_infinity\Build\Content\Data\Common") / f
    if p.exists():
        raw = json.loads(p.read_text(encoding="utf-8"))
        data = raw.get(f.replace(".json", ""), raw)
        print(f"\n  {f}: {len(data)} 项")
        for k, v in list(data.items())[:14]:
            if isinstance(v, dict):
                print(f"     {k}: Name={v.get('Name')!r} Region={v.get('Region', v.get('RegionId'))}")

# 地图内已有物件
d = parse_bin(str(BIN))
ms = d["tail"]["models"][2]
print(f"\n--- 地图内物件 {len(ms)} 条 ---")
print(f"  objType 分布: {dict(sorted(collections.Counter(m[1] for m in ms).items()))}")
print(f"  modelId 分布: {dict(sorted(collections.Counter(m[3] for m in ms).items()))}")
sc_ = [round(m[6][0], 3) for m in ms]
print(f"  缩放取值: {dict(sorted(collections.Counter(sc_).items())[:6])}")
lb = d["tail"]["labels"][2]
print(f"  标注 {len(lb)} 条，样例: {[(x[0], tuple(round(v, 1) for v in x[1])) for x in lb[:6]]}")
