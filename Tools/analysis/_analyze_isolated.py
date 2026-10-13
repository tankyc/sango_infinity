"""临时分析：在选定的海域阈值下，看可走图的孤岛构成 + 港口是否还有可泊水域。"""
import json
from collections import Counter
from pathlib import Path

import numpy as np
from scipy import sparse
from scipy.sparse.csgraph import connected_components

D = Path("data/sango_full")
SCEN = Path(r"d:/sango_infinity/Build/Content/Scenario/Scenario_world_full.json")
SEA_ALL = {7, 8, 9, 14}
SEA_OPEN = {9, 14}
WALK_TYPES = {1, 2, 3, 4, 5, 6, 8, 10, 11, 12, 13, 17, 18, 19, 20}   # 去掉 9/14，由阈值决定
NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}


def comps(mask):
    cx, cy = mask.shape
    idx = np.full((cx, cy), -1, np.int64)
    flat = np.flatnonzero(mask.ravel())
    idx.ravel()[flat] = np.arange(flat.size)
    rows, cols = [], []
    for x in range(cx):
        ys = np.flatnonzero(mask[x])
        if ys.size == 0:
            continue
        for dx, dy in NEIGH[x & 1]:
            nx = np.full(ys.shape, x + dx, np.int64)
            ny = ys + dy
            ok = (nx >= 0) & (nx < cx) & (ny >= 0) & (ny < cy)
            if not ok.any():
                continue
            nxc, nyc = nx[ok], ny[ok]
            good = mask[nxc, nyc]
            if not good.any():
                continue
            rows.append(idx[x, ys[ok][good]])
            cols.append(idx[nxc[good], nyc[good]])
    if rows:
        r = np.concatenate(rows)
        c = np.concatenate(cols)
        g = sparse.coo_matrix((np.ones(r.size, np.int8), (r, c)), shape=(flat.size, flat.size))
    else:
        g = sparse.coo_matrix((flat.size, flat.size))
    n, lab = connected_components(g, directed=False)
    L = np.full((cx, cy), -1, np.int32)
    L.ravel()[flat] = lab
    return L, n


def main():
    t = np.load(D / "terrain.npy")
    dist = np.load("_sea_dist.npy")
    cities = json.loads((D / "cities.json").read_text(encoding="utf-8"))
    scen = json.loads(SCEN.read_text(encoding="utf-8"))["citySet"]
    cx, cy = t.shape
    for N in (12, 20):
        is_open = np.isin(t, list(SEA_OPEN))
        walk = np.isin(t, list(WALK_TYPES)) | (is_open & (dist <= N))
        L, n = comps(walk)
        sz = np.bincount(L[L >= 0])
        order = np.argsort(-sz)
        cc = Counter()
        for c in cities:
            cc[int(L[c["x"], c["y"]])] += 1
        big = int(order[0])
        frs = [(int(sz[i]), i) for i in order[1:]]
        print(f"=== N={N}: 可走 {int(walk.sum())} 格, 连通块 {n}, 最大块 {int(sz[big])} 格含 {cc.get(big,0)} 城")
        buckets = Counter()
        for s, _ in frs:
            buckets[min(s, 10) if s < 10 else (50 if s < 50 else (200 if s < 200 else 1000))] += 1
        print(f"    其余 {len(frs)} 块, 按大小分档(<=档位): "
              + " ".join(f"{k}格:{v}" for k, v in sorted(buckets.items())))
        print(f"    其余块格数合计 {sum(s for s, _ in frs)}")
        top = frs[:6]
        for s, i in top:
            m = L == i
            tt, cn = np.unique(t[m], return_counts=True)
            ys, xs = np.where(m)
            print(f"      块{i}: {s} 格, 地形 {dict(zip(tt.tolist(), cn.tolist()))}, x[{xs.min()},{xs.max()}] y[{ys.min()},{ys.max()}], 城数={cc.get(i,0)}")
        # 港口/关是否还有可泊的可走水域
        bad = []
        for k, v in scen.items():
            cid = int(k)
            if int(v.get("BuildingType", 1)) != 3:      # 只查港：关在山口，本来就不靠水
                continue
            x, y = int(v["x"]), int(v["y"])
            nb = [(x + dx, y + dy) for dx, dy in NEIGH[x & 1]]
            nb = [(a, b) for a, b in nb if 0 <= a < cx and 0 <= b < cy]
            wat = sum(1 for a, b in nb if walk[a, b] and t[a, b] in (8, 9, 14))
            if wat == 0:
                bad.append((cid, v.get("Name"), int(v.get("BuildingType", 1)), x, y))
        print(f"    港/关六邻中没有「可走水域」的: {len(bad)} 个 {bad[:6]}")


if __name__ == "__main__":
    main()
