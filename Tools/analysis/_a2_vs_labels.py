# -*- coding: utf-8 -*-
"""一次性：用地图自带的州-郡标签交叉校验 A2 归属（独立于剧本 province 字段的第二份证据）。"""
import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
# 历史州 -> 本游戏 12 州 id
ZHOU2ID = {"幽州": {1}, "冀州": {2}, "并州": {2}, "青州": {3}, "徐州": {3},
           "兖州": {4}, "豫州": {4},
           # 司隶(三辅) 在本游戏里落到 6 京兆，所以 5 与 6 都算对
           "司隶": {5, 6},
           "雍州": {6}, "京兆": {6}, "右扶风": {6}, "左冯翊": {6},
           "凉州": {7}, "扬州": {8},
           # 荆州在本游戏拆成 9 荆北 / 10 荆南，两者都算对
           "荆州": {9, 10}, "荆南": {10},
           "益州": {11}, "交州": {10}, "南中": {12}}
ID2NAME = {1: "幽州", 2: "冀州", 3: "青徐", 4: "兖予", 5: "司隶", 6: "京兆", 7: "凉州",
           8: "扬州", 9: "荆北", 10: "荆南", 11: "益州", 12: "南中"}

d = parse_bin(str(MOD / "Map/yyxk_2x.bin"))
labels = d["tail"]["labels"][2]
print(f"地图标签 {len(labels)} 条")

pat = re.compile(r"^\s*([\u4e00-\u9fa5]{1,2}州|司隶|京兆|南中)\s*[-－—~～]\s*(.+?)\s*$")
pairs = []
print("\n含「州」的标签原文（前 80 条）:")
n_zhou = 0
for t, pos, col, sz in labels:
    s = str(t)
    if "州" in s or "司隶" in s:
        n_zhou += 1
        if n_zhou <= 80:
            print(f"   {s!r}  color={tuple(col)}")
    m = pat.match(s)
    if m:
        pairs.append((m.group(1), m.group(2), tuple(col)))
print(f"共 {n_zhou} 条含州字样")
print(f"其中「州-郡」形式 {len(pairs)} 条")

sc = json.loads((MOD / "Scenario/Scenario1.json").read_text(encoding="utf-8"))["citySet"]
by_name = {}
for k, v in sc.items():
    by_name.setdefault(v.get("Name", "?"), []).append((int(v.get("Id", k)),
                                                       int(v.get("province", 0)),
                                                       int(v.get("x", -1)), int(v.get("y", -1))))


def find_city(txt):
    """名字可能被截断（如 江夏郡北 -> 江夏郡北岸），用前缀双向匹配。"""
    if txt in by_name:
        return txt
    for nm in by_name:
        if nm.startswith(txt) or txt.startswith(nm):
            return nm
    return None


ok, bad, miss = [], [], []
for zhou, city, col in pairs:
    nm = find_city(city)
    if nm is None:
        miss.append((zhou, city))
        continue
    exp = ZHOU2ID.get(zhou)
    for cid, prov, x, y in by_name[nm]:
        if exper_ok := (prov in exp if isinstance(exp, set) else prov == exp):
            ok.append((zhou, nm, prov))
        else:
            bad.append((zhou, nm, prov, exp, x, y))

print(f"\n一致 {len(ok)} 条   不一致 {len(bad)} 条   标签里的名字在剧本中找不到 {len(miss)} 条")
if bad:
    print("\n=== 不一致清单（标签说一个州，剧本 province 字段是另一个）===")
    for zhou, nm, prov, exp, x, y in bad:
        e = "/".join(ID2NAME[z] for z in sorted(exp)) if isinstance(exp, set) else ID2NAME.get(exp, exp)
        print(f"  标签「{zhou}-{nm}」 剧本 province={prov}({ID2NAME.get(prov, '?')})"
              f"  标签期望={e}  格({x},{y})")
if miss:
    print("\n=== 标签名字未匹配到据点 ===")
    print("  " + " | ".join(f"{z}-{c}" for z, c in miss))

# 反向：剧本里有 province 但地图没有标签的
have = {nm for _, nm, _ in ok} | {nm for _, nm, _, _, _, _ in bad}
print(f"\n覆盖到的据点 {len(have)} 个；地图标签里没有州信息的据点 "
      f"{len(by_name) - len(have)} 个（属正常，标签只标了部分）")
print(f"\n州标签分布: " + str({z: sum(1 for a, _, _ in ok if a == z) +
                              sum(1 for a, _, _, _, _ in bad if a == z) for z in sorted(set(z for z, _, _ in pairs))}))
