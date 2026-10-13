"""以 Sango Infinity 的 Scenario.json 为底，生成指向世界地图 world.bin 的新剧本。

规则：
  * 原 42 座三国城池 Id 1..42 保持不变（坐标换成世界图的新坐标），其余字段照抄；
  * 原 45 座关所/港（Id 43..87）重新编号为 501..545，坐标按「中国图 lo/hi -> 世界图」换算；
  * 新世界的 458 座程序化城市接在 Id 43..500（与地图里的 areaId 一一对应：Id = 城市序号+1）；
  * NeighborList 全部重算（按领地相邻 + 六边形邻接）。
"""
import argparse
import json
from pathlib import Path

import numpy as np

ROOT = Path(r"d:/sango_infinity/Build/Content/Scenario")
DEF_SNG = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\sango")
BASE = ROOT / "Scenario.json"
CHINA_OFF = 28                     # 原版中国图在 256x256 世界里的偏移
CHINA_LO, CHINA_HI = 1234, 436     # san11-world 世界图里中国块的起点（源格坐标）
GATE_PORT_SHIFT = 500              # 关所/港新编号 = 旧编号 + 500（43..87 -> 501..545）
SWAP = 42                          # 我们城市列表里前 42 个是三国城池

NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}
WATER = (7, 8, 9, 14)


def china_to_world(lo, hi, ds):
    """中国图内相对格 (lo,hi) -> 世界图格 (x,y)。

    严格取 sango_convert.py 里「目标格 -> 源 hex」映射的逆：
        源 hex = (ds * x, ds * y + (x & 1))
    =>  x = floor((lo_abs + 0.5) / ds),  y = floor((hi_abs + 0.5) / ds)
    （用 floor(v+0.5) 而不是 py 的 round，避开 banker's rounding 在 .5 上的偏移）
    """
    x = int(np.floor((lo + CHINA_LO + 0.5) / ds))
    y = int(np.floor((hi + CHINA_HI + 0.5) / ds))
    return x, y


def build_links(area):
    """按领地相邻（六边形邻接且 areaId 不同）统计每座城的相邻城。"""
    cx_n, cy_n = area.shape
    links = {}
    for x in range(cx_n):
        for y in range(cy_n):
            a = int(area[x, y])
            if a == 0:
                continue
            for dx, dy in NEIGH[x & 1]:
                nx, ny = x + dx, y + dy
                if 0 <= nx < cx_n and 0 <= ny < cy_n:
                    b = int(area[nx, ny])
                    if b != 0 and b != a:
                        links.setdefault(a, set()).add(b)
    return links


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=str(DEF_SNG), help="sango_convert.py 的输出目录")
    ap.add_argument("--map-key", default="world", help="地图标识，需与 Map/<key>.bin 对应")
    ap.add_argument("--out", default=str(ROOT / "Scenario_world.json"))
    args = ap.parse_args()
    sng = Path(args.src)

    base = json.loads(BASE.read_text(encoding="utf-8"))
    ours = json.loads((sng / "cities.json").read_text(encoding="utf-8"))
    area = np.load(sng / "area.npy")
    meta = json.loads((sng / "meta.json").read_text(encoding="utf-8"))
    ds = meta.get("ds", 2)
    cells_x, cells_y = meta["cellsX"], meta["cellsY"]
    print(f"底剧本城市 {len(base['citySet'])}  我方城市 {len(ours)}  领地 {area.shape}  "
          f"ds={ds} 图 {cells_x}x{cells_y} 标识={args.map_key}", flush=True)

    old_cities = base["citySet"]
    template = json.loads(json.dumps(old_cities["1"]))       # 新城字段模板（下面会被第 1 号城覆盖）

    new_cities = {}

    # ---- 1) 原 42 城：保留，坐标用世界图新坐标
    for cid in range(1, SWAP + 1):
        key = str(cid)
        if key not in old_cities:
            continue
        c = json.loads(json.dumps(old_cities[key]))
        if cid - 1 < len(ours):
            c["x"], c["y"] = ours[cid - 1]["x"], ours[cid - 1]["y"]
        c["Id"] = cid
        new_cities[key] = c
        if cid == 1:
            template = json.loads(json.dumps(c))

    # ---- 2) 原关所/港：重编号 + 坐标换算
    for cid in range(SWAP + 1, len(old_cities) + 1):
        key = str(cid)
        if key not in old_cities:
            continue
        c = json.loads(json.dumps(old_cities[key]))
        lo, hi = c["x"] - CHINA_OFF, c["y"] - CHINA_OFF
        c["x"], c["y"] = china_to_world(lo, hi, ds)
        c["Id"] = cid + GATE_PORT_SHIFT
        new_cities[str(c["Id"])] = c

    # ---- 3) 新世界的 458 座程序化城市：Id 43..500
    for i in range(SWAP, len(ours)):
        cid = i + 1
        c = json.loads(json.dumps(template))
        rank = ours[i].get("rank", 3)
        c["Id"] = cid
        c["Name"] = ours[i]["name"]
        c["x"], c["y"] = ours[i]["x"], ours[i]["y"]
        c["BelongForce"] = 0
        c["BelongCorps"] = 0
        c["BuildingType"] = 1
        c["CityLevelType"] = 1 if rank == 1 else (2 if rank == 2 else 3)
        c["NeighborList"] = []
        new_cities[str(cid)] = c

    # ---- 4) 相邻表：领地相邻优先，按距离排序取前 6
    links = build_links(area)
    ids = sorted(int(k) for k in new_cities.keys())
    pos = {i: (new_cities[str(i)]["x"], new_cities[str(i)]["y"]) for i in ids}

    def dist(a, b):
        """六边形距离（odd-q，列奇偶决定偏移），与引擎 HexNeighbors.Distance 一致。"""
        (ax, ay), (bx, by) = pos[a], pos[b]
        aq, ar = ax, ay - (ax - (ax & 1)) // 2
        bq, br = bx, by - (bx - (bx & 1)) // 2
        dq, dr = bq - aq, br - ar
        return max(abs(dq), abs(dr), abs(dq + dr))

    n_link = 0
    for i in ids:
        cand = sorted(links.get(i, set()))
        cand = [j for j in cand if j in pos]
        cand.sort(key=lambda j: dist(i, j))
        new_cities[str(i)]["NeighborList"] = cand[:6]
        n_link += len(cand[:6])
    print(f"相邻关系: 共 {n_link} 条", flush=True)

    # ---- 5) Info 指向新地图
    info = dict(base["Info"])
    info["mapType"] = args.map_key
    info["id"] = 1000 if ds >= 2 else 1001
    info["name"] = "世界地圖-英雄集結" if ds >= 2 else "世界地圖-英雄集結(全尺寸)"
    info["type"] = 1
    info["description"] = ("以 ETOPO 全球高程與 Natural Earth 河湖生成的歐亞大陸世界地圖，"
                           "光榮的中國地圖原樣嵌在東亞。全圖 %dx%d 格、共 %d 座城池。"
                           % (cells_x, cells_y, len(new_cities)))
    out = {
        "Id": info["id"],
        "Name": info["name"],
        "Info": info,
        "citySet": new_cities,
        "corpsSet": base["corpsSet"],
        "forceSet": base["forceSet"],
        "personSet": base["personSet"],
    }
    out_path = Path(args.out)
    out_path.write_text(json.dumps(out, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
    print(f"已写入 {out_path}（{out_path.stat().st_size / 1048576:.1f} MB，城市 {len(new_cities)} 座，"
          f"势力 {len(out['forceSet'])}，武将 {len(out['personSet'])}）", flush=True)


if __name__ == "__main__":
    main()
