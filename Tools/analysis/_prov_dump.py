# -*- coding: utf-8 -*-
"""一次性：打印游戏的州/区域表（A2 对照表要按这套 id 落）。"""
import json
from pathlib import Path

D = Path(r"D:\sango_infinity\Build\Content\Data\Common")
pj = json.loads((D / "Provinces.json").read_text(encoding="utf-8"))["Provinces"]
rj = json.loads((D / "Regions.json").read_text(encoding="utf-8"))["Regions"]
print("=== Regions ===")
for k, v in sorted(rj.items(), key=lambda kv: int(kv[0])):
    print(f"  {k}: {v.get('Name')}")
print("=== Provinces ===")
for k, v in sorted(pj.items(), key=lambda kv: int(kv[0])):
    print(f"  {k}: {v.get('Name')}  Region={v.get('Region')}  neighbors={v.get('neighbors')}")
