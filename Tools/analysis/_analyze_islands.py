"""临时分析：陆地块结构 + 海域距陆距离（为「深海/远海设为不可行走」与「孤岛处理」提供依据）。"""
import json
from collections import Counter, deque
from pathlib import Path

import numpy as np
from scipy import sparse
from scipy.sparse.csgraph import connected_components

D = Path("data/sango_full")
SEA = {7, 8, 9, 14}
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
    cities = json.loads((D / "cities.json").read_text(encoding="utf-8"))
    cx, cy = t.shape
    land = ~np.isin(t, list(SEA))
    L, n = comps(land)
    sz = np.bincount(L[L >= 0])
    order = np.argsort(-sz)
    print(f"陆地格 {int(land.sum())}  连通块 {n} 个")
    print(f"  最大 8 块格数: {sz[order[:8]].tolist()}")

    cc = Counter()
    for c in cities:
        if 0 <= c["x"] < cx and 0 <= c["y"] < cy:
            cc[int(L[c["x"], c["y"]])] += 1
    print(f"  城市分布: 有城市的陆地块 {len(cc)} 个, 城市数 {sum(cc.values())}")
    print(f"  块大小 min/中位/max = {int(sz[order[-1]])}/{int(sz[order[len(order)//2]])}/{int(sz[order[0]])}")
    for th in (10, 50, 100, 500, 2000):
        big = [int(i) for i in order if sz[i] >= th]
        nocity = [i for i in big if i not in cc]
        print(f"  >= {th:5d} 格的陆地块 {len(big):5d} 个, 其中无城市 {len(nocity):5d} 个,"
              f" 这些块格数合计 {int(sum(sz[i] for i in nocity))}")
    print(f"  <10 格的陆地块 {int(sum(1 for s in sz if s < 10))} 个（碎点，格数合计 {int(sum(s for s in sz if s < 10))}）")
    np.save("_land_lab.npy", L)

    # ---- 海域到陆地的六边形距离（BFS）
    dist = np.full((cx, cy), 1 << 20, np.int32)
    q = deque()
    for x in range(cx):
        ys = np.flatnonzero(land[x])
        for y in ys:
            dist[x, y] = 0
            q.append((x, y))
    while q:
        x, y = q.popleft()
        for dx, dy in NEIGH[x & 1]:
            nx, ny = x + dx, y + dy
            if 0 <= nx < cx and 0 <= ny < cy and dist[nx, ny] == 1 << 20:
                dist[nx, ny] = dist[x, y] + 1
                q.append((nx, ny))
    sea = np.isin(t, list(SEA))
    ds = dist[sea]
    print(f"\n海域格 {int(sea.sum())}；到陆地距离 p50/p90/p99/max = "
          f"{int(np.percentile(ds,50))}/{int(np.percentile(ds,90))}/{int(np.percentile(ds,99))}/{int(ds.max())}")
    for th in (2, 3, 5, 8, 12, 20, 30):
        print(f"  距陆地 > {th:2d} 格的海域: {int((ds > th).sum()):8d} 格 ({(ds>th).mean():.3f})")
    np.save("_sea_dist.npy", dist)


if __name__ == "__main__":
    main()
