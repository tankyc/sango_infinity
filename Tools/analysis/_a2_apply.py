# -*- coding: utf-8 -*-
"""一次性：把 A2 州归属表落到 yyxk 剧本里。

步骤：
  1) 读对照表（含锚点）；19 个认不出的名字按「最近邻已归属据点」推（用户指定）
  2) 输出 diff 预览：哪些城 旧 province -> 新 province，逐州统计
  3) --apply 时备份剧本、就地写入，并把补齐后的表回写 JSON
"""
import argparse
import json
import shutil
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
SC = MOD / "Scenario/Scenario1.json"
TAB = Path(r"D:\sango_infinity\Tools\yyxk_provinces_A2.json")

ap = argparse.ArgumentParser()
ap.add_argument("--apply", action="store_true")
args = ap.parse_args()

sc_raw = SC.read_text(encoding="utf-8")
sc = json.loads(sc_raw)
cs = sc["citySet"]
print(f"剧本 {SC.name}  {SC.stat().st_size / 1024:.0f} KB  citySet {len(cs)} 条")
head = sc_raw[:120].replace("\n", "\\n")
print(f"  原文件开头: {head}")
indent = 1 if sc_raw[:200].count("\n") > 3 else None
print(f"  检测缩进: {indent if indent else '紧凑'}")

tab = json.loads(TAB.read_text(encoding="utf-8"))
name2prov = {}
for pid, blk in tab["provinces"].items():
    for nm in list(blk.get("anchors", [])) + list(blk.get("members", [])):
        name2prov[nm] = int(pid)
pid2name = {int(k): v["name"] for k, v in tab["provinces"].items()}

# 已知归属的据点坐标，用于最近邻
known = {}
for k, v in cs.items():
    nm = v.get("Name", "?")
    if nm in name2prov and "x" in v:
        known[nm] = (int(v["x"]), int(v["y"]), name2prov[nm])
print(f"\n表内可定位的据点 {len(known)} 个")

missing = sorted({v.get("Name", "?") for v in cs.values()} - set(name2prov))
print(f"待最近邻补的名 {len(missing)} 个: {missing}")
nn = {}
kn = np.array([(x, y) for x, y, _ in known.values()], np.float32)
kp = [p for _, _, p in known.values()]
for nm in missing:
    row = next(v for v in cs.values() if v.get("Name") == nm)
    if "x" not in row:
        continue
    d = np.hypot(kn[:, 0] - int(row["x"]), kn[:, 1] - int(row["y"]))
    i = int(d.argmin())
    nn[nm] = (kp[i], float(d[i]), tuple(kn[i].astype(int)))
    print(f"  {nm:<6} 格({row['x']:>4},{row['y']:>4}) -> {pid2name[kp[i]]}"
          f"  最近「{list(known)[i]}」 距离 {d[i]:.1f} 格")

# diff
chg = []
for k, v in cs.items():
    nm = v.get("Name", "?")
    old = int(v.get("province", 0))
    new = name2prov.get(nm, nn.get(nm, (0,))[0] if nm in nn else 0)
    if new and new != old:
        chg.append((nm, k, old, new, int(v.get("x", -1)), int(v.get("y", -1))))
print(f"\n需要改动 province 的据点 {len(chg)} 个（含原为 0 的）")
print(f"  变更前后分布: 旧 {dict(Counter(c[2] for c in chg))}")
print(f"                新 {dict(Counter(c[3] for c in chg))}")
for nm, k, o, n, x, y in chg[:25]:
    print(f"    {nm:<8} id={k:<4} 格({x:>4},{y:>4})  {o} -> {n} ({pid2name.get(n, n)})")
if len(chg) > 25:
    print(f"    ... 共 {len(chg)} 条")

anchor_chg = [c for c in chg if c[2] > 0]
print(f"\n  ⚠ 锚点被改动: {len(anchor_chg)} 个（应为 0）")
for nm, k, o, n, x, y in anchor_chg[:10]:
    print(f"    {nm} 剧本={o} 表={n}")

if args.apply:
    bak = SC.with_suffix(".json.bak_before_A2")
    if not bak.exists():
        shutil.copy2(SC, bak)
    for k, v in cs.items():
        nm = v.get("Name", "?")
        new = name2prov.get(nm)
        if new is None and nm in nn:
            new = nn[nm][0]
        if new:
            v["province"] = int(new)
    SC.write_text(json.dumps(sc, ensure_ascii=False, indent=indent), encoding="utf-8")
    print(f"\n已写入 {SC}  {SC.stat().st_size / 1024:.0f} KB（备份 {bak.name}）")
    # 回写对照表：补上最近邻结果
    tab.setdefault("_最近邻补名", {})["说明"] = "以下名字按最近邻已归属据点推得（用户指定）"
    for nm, (p, d, coord) in nn.items():
        tab["_最近邻补名"][nm] = {"province": p, "name": pid2name[p],
                                  "dist_cells": round(d, 1), "nearest_at": list(coord)}
    tab["统计"] = {"剧本据点名": 492, "锚点": 52, "已归 12 州": 492, "待复核": 0,
                   "最近邻补名": len(nn)}
    TAB.write_text(json.dumps(tab, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"已回写对照表 {TAB.name}")
else:
    print("\n（未加 --apply，未写盘）")
