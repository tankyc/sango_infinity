# -*- coding: utf-8 -*-
"""把剧本裁剪到「只含地图上真实存在的城市」。

背景：sango_scenario.py 是以 DefaultMap 的 87 城剧本为底，把新地图的城替换进前 N 个 id，
其余的城/关/港仍留在剧本里（坐标是光荣中国图的，在本图里没有对应地格与领地），
地图上 city 数很少时（例如只放两座测试城）就会产生一堆空壳城市。
本脚本按 `<src>/cities.json` 的城数只保留前 N 座，并把势力/军团/武将裁到实际被引用的部分。

用法：
  python Tools/sango_scenario_trim.py --src <data\lit_world> \
         --scenario D:\sango_infinity\Build\Mods\lit_world\Scenario\Scenario1.json
  （--single-force 可把全部保留的城合并给 1 号势力）
"""
import argparse
import json
from pathlib import Path

ap = argparse.ArgumentParser()
ap.add_argument("--src", required=True, help="含 cities.json 的地图数据目录")
ap.add_argument("--scenario", required=True)
ap.add_argument("--out", default="", help="默认就地覆盖")
ap.add_argument("--single-force", action="store_true",
                help="所有保留的城归 1 号势力 + 1 号军团，只留该势力的武将")
args = ap.parse_args()

src = Path(args.src)
sp = Path(args.scenario)
d = json.loads(sp.read_text(encoding="utf-8"))
ours = json.loads((src / "cities.json").read_text(encoding="utf-8"))
n = len(ours)
keep = [str(i) for i in range(1, n + 1)]

cities = {k: dict(v) for k, v in d["citySet"].items() if k in keep}
# 底剧本里 BuildingType==3 的条目就是「港」模板（含 p_port 模型与港口字段）
port_tpl = next((dict(v) for v in d["citySet"].values()
                 if int(v.get("BuildingType", 1)) == 3), None)
city_ids = [int(k) for k in cities]
# 以 cities.json 为准同步名字/坐标/类型：type=port 的套用港模板，其余确保是都市(BuildingType=1)
for i, c in enumerate(ours):
    k = str(i + 1)
    if k not in cities:
        continue
    e = cities[k]
    if str(c.get("type", "city")) == "port" and port_tpl is not None:
        pid = int(k)
        e.clear()
        e.update({kk: vv for kk, vv in port_tpl.items() if kk != "Id"})
        e["Id"] = pid
        e["Name"] = c["name"]
        e["x"], e["y"] = int(c["x"]), int(c["y"])
        e["CityLevelType"] = 1
        e["BuildingType"] = 3
        e["BelongForce"] = 0
        e["BelongCorps"] = 0
        e["NeighborList"] = []
        near = min(((j, (cities[str(j)]["x"] - e["x"]) ** 2 + (cities[str(j)]["y"] - e["y"]) ** 2)
                    for j in city_ids
                    if ours[j - 1].get("type", "city") == "city"), key=lambda t: t[1],
                   default=(0, 0))
        e["BelongCity"] = near[0]
    else:
        e["Name"] = c["name"]
        e["x"], e["y"] = int(c["x"]), int(c["y"])
        if int(e.get("BuildingType", 1)) != 1:
            e["BuildingType"] = 1
            e["model"] = "Assets/Model/Prefab/p_city_1.prefab"
if not cities:
    raise SystemExit("剧本里没有与 cities.json 对应的城（id 必须从 1 开始连续）")
forces_used = {int(c.get("BelongForce", 0)) for c in cities.values()} - {0}
corps_used = {int(c.get("BelongCorps", 0)) for c in cities.values()} - {0}
keep_ids = {int(x) for x in keep}

if args.single_force:
    forces_used, corps_used = {1}, {1}
    for c in cities.values():
        c["BelongForce"] = 1
        c["BelongCorps"] = 1
    d["forceSet"] = {"1": d["forceSet"]["1"]}
    d["corpsSet"] = {"1": d["corpsSet"]["1"]}
else:
    d["forceSet"] = {k: v for k, v in d["forceSet"].items() if int(k) in forces_used}
    d["corpsSet"] = {k: v for k, v in d["corpsSet"].items() if int(k) in corps_used}

# 武将：只留属于保留势力的；军团/城池引用落到保留集合内
first_corps = sorted(corps_used)[0]
first_city = keep_ids and min(keep_ids)
persons = {}
for k, p in d["personSet"].items():
    if int(p.get("BelongForce", 0)) not in forces_used:
        continue
    q = dict(p)
    if int(q.get("BelongCorps", 0)) not in corps_used:
        q["BelongCorps"] = first_corps
    if int(q.get("BelongCity", 0)) not in keep_ids:
        q["BelongCity"] = first_city
    if int(q.get("CurrentCity", 0)) not in keep_ids:
        q["CurrentCity"] = first_city
    persons[k] = q
d["personSet"] = persons
pid = {int(k) for k in persons}

# 势力/军团的武将引用修正
for f in d["forceSet"].values():
    for fld in ("Counsellor", "Governor"):
        if int(f.get(fld, -1)) not in pid:
            f[fld] = sorted(pid)[0] if pid else -1
    f["allies"] = [a for a in f.get("allies", []) if int(a) in forces_used]
for k, c in d["corpsSet"].items():
    c["BelongForce"] = int(c.get("BelongForce", 1)) \
        if int(c.get("BelongForce", 1)) in forces_used else sorted(forces_used)[0]
    if int(c.get("Comander", -1)) not in pid:
        c["Comander"] = sorted(pid)[0] if pid else -1
    c["Id"] = int(k)

# 城的相邻表只留保留的城
for c in cities.values():
    c["NeighborList"] = [x for x in c.get("NeighborList", []) if str(x) in cities]
d["citySet"] = cities

d["Name"] = "lit_world 测试（两城）" if n == 2 else f"lit_world 测试（{n} 城）"
d["Info"]["name"] = d["Name"]
d["Info"]["description"] = (f"lit_world 世界图测试剧本：{len(cities)} 座城、"
                            f"{len(d['forceSet'])} 势力、{len(persons)} 名武将。")
out = Path(args.out) if args.out else sp
out.write_text(json.dumps(d, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
print(f"已写 {out}")
print(f"  城 {len(cities)}  {[(c['Id'], c['Name'], c['x'], c['y'], c['BelongForce']) for c in cities.values()]}")
print(f"  势力 {len(d['forceSet'])}  军团 {len(d['corpsSet'])}  武将 {len(persons)}  "
      f"mapType={d['Info'].get('mapType')}  id={d['Info'].get('id')}")
