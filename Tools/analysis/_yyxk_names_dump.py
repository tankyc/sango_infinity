# -*- coding: utf-8 -*-
"""一次性：把 yyxk 剧本的据点名与 province 归属全量导出来，供 A2 州归属对照表使用。"""
import json
from collections import Counter, defaultdict
from pathlib import Path

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
sc = json.loads((MOD / "Scenario/Scenario1.json").read_text(encoding="utf-8"))["citySet"]
rows = []
for k, v in sc.items():
    rows.append((int(v.get("Id", k)), v.get("Name", "?"), int(v.get("province", -1)),
                 int(v.get("BuildingType", 1)), int(v.get("x", -1)), int(v.get("y", -1))))
print(f"据点总数 {len(rows)}   BuildingType {dict(Counter(r[3] for r in rows))}")
has = [r for r in rows if r[2] > 0]
print(f"带 province 的 {len(has)} 个，province 取值 {sorted(set(r[2] for r in has))}")

print("\n=== 已带 province 的据点（按 province 分组）===")
g = defaultdict(list)
for i, nm, p, bt, x, y in has:
    g[p].append(f"{nm}({i},{x},{y})")
for p in sorted(g):
    print(f"  province {p}: {' '.join(g[p])}")

print("\n=== 未带 province 的据点名（共 %d 个，按名字排序）===" % (len(rows) - len(has)))
miss = sorted({r[1] for r in rows if r[2] <= 0})
print("  " + " ".join(miss))

# 游戏数据里的省/区域表
for cand in ("Provinces.json", "Regions.json", "Cities.json"):
    for root in (Path(r"D:\sango_infinity\Build\Content\Data"), MOD):
        for p in root.rglob(cand):
            try:
                j = json.loads(p.read_text(encoding="utf-8"))
            except Exception as e:
                print(f"\n[{p}] 读取失败 {e}")
                continue
            print(f"\n=== {p} ===")
            if isinstance(j, dict):
                items = list(j.items())[:30]
                for k, v in items:
                    s = json.dumps(v, ensure_ascii=False)
                    print(f"  {k}: {s[:110]}")
                print(f"  ...共 {len(j)} 条")
            break
