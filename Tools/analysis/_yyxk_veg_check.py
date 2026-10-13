# -*- coding: utf-8 -*-
"""一次性：yyxk 的植被地块现状检查 —— 森林格分布、既有 mapModels、城市结构、地理拟合。"""
import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
d = parse_bin(str(MOD / "Map/yyxk_2x.bin"))
cx, cy = d["cellsX"], d["cellsY"]
raw = d["gridRaw"]
terr = raw[..., 0].astype(np.int32)
st = (raw[..., 1].astype(np.uint32) | (raw[..., 2].astype(np.uint32) << 8)
      | (raw[..., 3].astype(np.uint32) << 16) | (raw[..., 4].astype(np.uint32) << 24))
area = raw[..., 5].astype(np.int32) | (raw[..., 6].astype(np.int32) << 8)

print("=" * 76)
print(f"yyxk_2x.bin  格 {cx}x{cy}   mapModels {len(d['tail']['models'])} 条")
print("地形分布:", dict(sorted(Counter(terr.ravel().tolist()).items())))

forest = terr == 6
print(f"\n[植被地块] 森(6) 格数 {int(forest.sum())}  占全图 {forest.mean():.4f}")
print(f"  内政位(state&4) 中的森林格 {int((forest & ((st & 4) != 0)).sum())}")
print(f"  有 areaId 的森林格 {int((forest & (area != 0)).sum())}"
      f"   无 areaId {int((forest & (area == 0)).sum())}")
cnt = Counter(area[forest].tolist())
print(f"  森林格最多的 areaId: {cnt.most_common(10)}")
print(f"  有森林的 areaId 个数 {len(cnt)}")

# 既有 mapModels 记录
print(f"\n[既有 mapModels] {len(d['tail']['models'])} 条")
for i, m in enumerate(d["tail"]["models"]):
    print(f"  #{i}: {m}")

# 城市结构
sc = json.loads((MOD / "Scenario/Scenario1.json").read_text(encoding="utf-8"))["citySet"]
keys = Counter()
for v in sc.values():
    for k in v:
        keys[k] += 1
print(f"\n[剧本] 据点 {len(sc)}  字段: {dict(keys)}")
bt = Counter(int(v.get("BuildingType", 1)) for v in sc.values())
print(f"  BuildingType 分布 {dict(bt)}（1城 2关 3港）")
for f in ("province", "ProvinceId", "region", "RegionId", "StateId"):
    if f in keys:
        vals = Counter(int(v.get(f, -1)) for v in sc.values())
        print(f"  {f}: {dict(sorted(vals.items())[:20])}")
print("  样例:", json.dumps(list(sc.values())[0], ensure_ascii=False)[:400])

# 地理拟合
gp = Path(r"D:\sango_infinity\Temp\yyxk_geofit.json")
if gp.exists():
    g = json.loads(gp.read_text(encoding="utf-8"))
    print(f"\n[地理拟合] {gp.name}")
    print("  顶层键:", list(g.keys())[:12])
    for k in list(g.keys())[:12]:
        v = g[k]
        if isinstance(v, (dict, list)):
            print(f"    {k}: {json.dumps(v, ensure_ascii=False)[:220]}")
        else:
            print(f"    {k}: {v}")
