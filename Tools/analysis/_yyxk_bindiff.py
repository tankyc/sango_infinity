# -*- coding: utf-8 -*-
"""一次性：用备份定位「底图与地图不一致」的确切位置（= 部分形变）。

底图是 mod 作者对着「原始的 .bin」画的；我们后来改过 .bin（bak_repair / bak_skybox）。
所以：当前 .bin 与那份原始备份的逐格差异，就是需要转换的区域。
"""
import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
FILES = [
    ("当前 .bin", MOD / "Map/yyxk_2x.bin"),
    ("bak_repair", MOD / "Map/yyxk_2x.bin.bak_repair"),
    ("bak_skybox", MOD / "Map/yyxk_2x.bin.bak_skybox"),
]

parsed = {}
for nm, p in FILES:
    d = parse_bin(str(p))
    tt = d["gridRaw"][..., 0].astype(np.int32)
    aid = d["gridRaw"][..., 5].astype(np.int32) | (d["gridRaw"][..., 6].astype(np.int32) << 8)
    st = (d["gridRaw"][..., 1].astype(np.uint32) | (d["gridRaw"][..., 2].astype(np.uint32) << 8)
          | (d["gridRaw"][..., 3].astype(np.uint32) << 16)
          | (d["gridRaw"][..., 4].astype(np.uint32) << 24))
    parsed[nm] = dict(tt=tt, aid=aid, st=st, cells=(d["cellsX"], d["cellsY"]),
                      nmodels=len(d["tail"]["models"]))
    print(f"{nm:<12} {p.name:<26} 格 {d['cellsX']}x{d['cellsY']}  "
          f"地形种类 {len(set(tt.ravel().tolist()))}  mapModels {len(d['tail']['models'])}")

# 谁是原始状态？先互相比较
names = [n for n, _ in FILES]
print("\n=== 逐格差异（地形 / areaId / terrainState）===")
pairs = [(names[0], names[1]), (names[0], names[2]), (names[1], names[2])]
for a, b in pairs:
    A, B = parsed[a], parsed[b]
    dt = int((A["tt"] != B["tt"]).sum())
    da = int((A["aid"] != B["aid"]).sum())
    ds = int((A["st"] != B["st"]).sum())
    print(f"  {a} vs {b}: 地形不同 {dt:>7} 格   areaId 不同 {da:>7}   state 不同 {ds:>7}")

cur, rep = parsed["当前 .bin"], parsed["bak_repair"]
diff = cur["tt"] != rep["tt"]
print(f"\n=== 当前 .bin vs bak_repair（= 底图可能对不上的区域）===")
print(f"  地形不同的格数 {int(diff.sum())} / {diff.size} = {diff.mean():.4f}")
if diff.any():
    xs, ys = np.where(diff)
    print(f"  范围 x {xs.min()}..{xs.max()}   y {ys.min()}..{ys.max()}")
    print(f"  转换对（bak_repair -> 当前）: " +
          str(Counter(zip(rep["tt"][diff].tolist(), cur["tt"][diff].tolist())).most_common(12)))
    # 聚集度：按 32 格块统计，看是零散还是成片
    bx, by = xs // 32, ys // 32
    blocks = Counter(zip(bx.tolist(), by.tolist()))
    print(f"  涉及 32 格块 {len(blocks)} 个（共 {(512 // 32) ** 2} 块）；"
          f"最密的 5 块: {blocks.most_common(5)}")
    # 与地形种类的关系
    print(f"  变化后在各地形上的分布: "
          f"{Counter(cur['tt'][diff].tolist()).most_common(10)}")
    # 是否触及据点格 / 内政位
    print(f"  其中改到都市(17)格的: {int((cur['tt'][diff] == 17).sum())}"
          f"  改到内政位(state==4)的: {int((cur['st'][diff] == 4).sum())}")

sc = json.loads((MOD / "Scenario/Scenario1.json").read_text(encoding="utf-8"))["citySet"]
city_by_id = {int(v.get("Id", -1)): v for v in sc.values() if "Id" in v}
aid_diff = cur["aid"] != rep["aid"]
if aid_diff.any():
    cc = Counter(aid_diff & (cur["aid"] != 0) and cur["aid"][aid_diff].tolist())
    print(f"\n  areaId 也变化了 {int(aid_diff.sum())} 格，涉及的城: "
          f"{[city_by_id.get(a, {}).get('Name', a) for a in list(cc)[:12]]}")

# 差异掩膜存下来，供后续转换使用
mask = diff.T                                  # [y, x]，与底图同序
np.save(r"D:\sango_infinity\Temp\yyxk_diff_mask.npy", mask)
print(f"\n差异掩膜已存 Temp\\yyxk_diff_mask.npy  shape {mask.shape}  真值 {int(mask.sum())}")
