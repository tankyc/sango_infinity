# -*- coding: utf-8 -*-
"""一次性：校核 A2 州归属对照表 —— 覆盖/缺失/多出/与锚点冲突。"""
import json
from collections import defaultdict
from pathlib import Path

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
sc = json.loads((MOD / "Scenario/Scenario1.json").read_text(encoding="utf-8"))["citySet"]
all_names = {v.get("Name", "?") for v in sc.values()}
anchor = {v.get("Name", "?"): int(v.get("province", -1)) for v in sc.values()
          if int(v.get("province", -1)) > 0}
tab = json.loads(Path(r"D:\sango_infinity\Tools\yyxk_provinces_A2.json").read_text(encoding="utf-8"))

name2prov = {}
dupes = defaultdict(list)
for pid, blk in tab["provinces"].items():
    for nm in list(blk.get("anchors", [])) + list(blk.get("members", [])):
        if nm in name2prov and name2prov[nm] != pid:
            dupes[nm].append((name2prov[nm], pid))
        name2prov[nm] = pid

print(f"剧本据点名 {len(all_names)} 个")
print(f"对照表收录 {len(name2prov)} 个（去重后）")
missing = sorted(all_names - set(name2prov))
extra = sorted(set(name2prov) - all_names)
print(f"\n缺失（剧本里有、表里没有）{len(missing)} 个：")
for i in range(0, len(missing), 12):
    print("   " + " ".join(missing[i:i + 12]))
print(f"\n多出（表里有、剧本里没有）{len(extra)} 个：")
for i in range(0, len(extra), 12):
    print("   " + " ".join(extra[i:i + 12]))
conf = [(n, anchor[n], name2prov[n]) for n in anchor if n in name2prov and name2prov[n] != str(anchor[n])]
print(f"\n与锚点冲突 {len(conf)} 个：")
for n, a, b in conf:
    print(f"   {n}: 剧本 province={a}  表里={b}")
print(f"\n重复归到多个州的名: {dict(dupes) if dupes else '无'}")
print(f"锚点覆盖 {sum(1 for n in anchor if n in name2prov)}/{len(anchor)}")
