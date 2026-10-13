# -*- coding: utf-8 -*-
"""独立复核「草地->森林 + 删模型」这一版改动：与备份逐格逐顶点比对。"""
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from sango_scale_bin import parse_bin                       # noqa: E402

import numpy as np

M = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Map")
cur = parse_bin(str(M / "yyxk_2x.bin"))
old = parse_bin(str(M / "yyxk_2x.bin.bak_before_forest"))

tc, to = cur["gridRaw"][..., 0].astype(np.int32), old["gridRaw"][..., 0].astype(np.int32)
diff = np.argwhere(tc != to)
pair = Counter((int(to[a, b]), int(tc[a, b])) for a, b in diff)
print(f"地格地形改动 {len(diff)} 格   改动类型: {dict(pair)}")

# 每一处改动，都必须是「原草地、且是本次要改的那批（模型所在格）」
q = float(cur["quadSize"])
keep_models = cur["tail"]["models"][2]
old_models = old["tail"]["models"][2]
mcells = set()
for m in old_models:
    vx, vy = int(round(float(m[4][2]) / q)), int(round(float(m[4][0]) / q))
    mcells.add((vx // 4, (vy - ((vx // 4) & 1) * 2) // 4))
print(f"  改动格全部属于「原模型所在格」: {all((int(a), int(b)) in mcells for a, b in diff)}")
print(f"  改动格全部是 1->6: {bool(((to[diff[:,0], diff[:,1]] == 1) & (tc[diff[:,0], diff[:,1]] == 6)).all())}")

print(f"\n模型: {len(old_models)} -> {len(keep_models)}（删除 {len(old_models)-len(keep_models)}）")
# 保留的模型必须逐字段等于原表里的对应项（顺序不变）
kept_ok = True
j = 0
deleted_cells = set()
for m in old_models:
    vx, vy = int(round(float(m[4][2]) / q)), int(round(float(m[4][0]) / q))
    c = (vx // 4, (vy - ((vx // 4) & 1) * 2) // 4)
    if j < len(keep_models) and tuple(keep_models[j]) == tuple(m):
        j += 1
    else:
        deleted_cells.add(c)
        kept_ok = False
print(f"  保留模型与原始记录逐条一致（顺序不变）: {kept_ok}  {j}/{len(keep_models)}")

# 被删的模型：其格子的地形**没有**被改成森林（即「不能改就删模型」）
bad = [c for c in deleted_cells if 0 <= c[0] < to.shape[0] and 0 <= c[1] < to.shape[1]
       and tc[c] == 6 and to[c] == 1]
print(f"  被删模型所在格里，却把地形改成森林的: {len(bad)}（应为 0 ✓）")
print(f"  被删模型的落点地形: {dict(Counter(int(to[c]) for c in deleted_cells if 0<=c[0]<to.shape[0] and 0<=c[1]<to.shape[1]).most_common(5))}")

print(f"\n别的都没动:")
print(f"  areaId/state 未动: {bool(np.array_equal(cur['gridRaw'][...,1:], old['gridRaw'][...,1:]))}")
print(f"  顶点(高度/贴图/水面)未动: {bool(np.array_equal(cur['verts'], old['verts']))}")
print(f"  标注 {len(old['tail']['labels'][2])} -> {len(cur['tail']['labels'][2])}")
print(f"  mapHeader/其它尾部段长度: {cur['mapWidth']==old['mapWidth']} {cur['mapHeight']==old['mapHeight']}")
