"""临时分析：把「距陆地 <= N 格的海域」保留为可行走，试出合适的 N。

关注：500 座城市是否仍连成一片、哪些陆块会被切出去、被切出去的块有多大/多少城。
"""
import json
from collections import Counter
from pathlib import Path

import numpy as np
from scipy import sparse
from scipy.sparse.csgraph import connected_components

D = Path("data/sango_full")
SEA_ALL = {7, 8, 9, 14}
SEA_OPEN = {9, 14}          # 可调档的只有海/浅滩；川(7)/河(8) 是河道，不在本议题
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
    cx, cy = t.shape
    is_land = ~np.isin(t, list(SEA_ALL))
    is_open = np.isin(t, list(SEA_OPEN))
    print(f"陆地 {int(is_land.sum())}  开放海域(9/14) {int(is_open.sum())}")
    print(f"{'N':>4} {'可走格':>9} {'块数':>6} {'最大块':>8} {'最大块城数':>9} {'含城块':>7} "
          f"{'被切出的含城块(格数/城数)':>0}")
    for N in (4, 6, 8, 10, 12, 16, 20, 30, 40, 60, 99):
        walk = is_land | (is_open & (dist <= N))
        L, n = comps(walk)
        sz = np.bincount(L[L >= 0])
        order = np.argsort(-sz)
        big = int(order[0])
        cc = Counter()
        for c in cities:
            cc[int(L[c["x"], c["y"]])] += 1
        main_cities = cc.get(big, 0)
        others = sorted(((int(sz[i]), cc.get(i, 0)) for i in cc if i != big and cc.get(i, 0) > 0),
                        reverse=True)
        detail = " ".join(f"{s}格/{k}城" for s, k in others[:5])
        print(f"{N:>4} {int(walk.sum()):>9} {n:>6} {int(sz[big]):>8} {main_cities:>9} "
              f"{len(cc):>7}  {detail}")


if __name__ == "__main__":
    main()
