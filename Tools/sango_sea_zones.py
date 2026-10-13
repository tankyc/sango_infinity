"""海域分档 + 可行走孤岛处理。

在 sango_fix_roads.py 之后就运行（写回 terrain.npy / terrain.bin）。

背景：海(9) 在 TerrainTypes.json 里 moveable=true，全图 145 万可行走格里 88 万是海，
      单位可以直接横渡太平洋；但若把不可行走地形当墙，又会把地图切成上千碎块。

1) 海域分档（按「到最近陆地的六边形距离」把 海(9) 重新归类）
     d <= --near(12)  -> 海(9)     近海：可行走（船）
     d <= --deep(40)  -> 远海(21)  不可行走
     d  > --deep      -> 深海(22)  不可行走
   阈值依据实测：N=8 是「500 座城仍在同一连通域」的临界值，N<8 会切掉 6~9 座城；
   N=12 留 1.5 倍余量，屏蔽 61.8% 的开放海域。浅滩(14) 本身就是近岸浅水，保持原样。

2) 孤岛处理（分档后重算可行走连通块）
   - 主块 = 最大连通块（实测含全部 500 城）
   - 其余块 >= --bridge-min(200) 格 -> 从主块做一次最小代价 Dijkstra，
     把路径上的格子改成可行走地形（远海/深海->海9、崖->栈道12、岸->小径20、川->渡所13）
     这样的「桥接」让封闭海重新可航、大岛可达；代价超过 --bridge-max-cost 的放弃
   - 其余较小的碎点 -> 整块设为不可行走（水->深海22、陆->崖16），
     它们本来就不含城市、也无法到达，留着只会让单位/寻路踩坑。
"""
import argparse
import heapq
from collections import Counter, deque
from pathlib import Path

import numpy as np
from scipy import sparse
from scipy.sparse.csgraph import connected_components

ROOT = Path(__file__).resolve().parent

SEA = {7, 8, 9, 14}          # 川/河/海/浅滩：算「水」（分档时用它们判断陆地）
OPEN_SEA = 9                 # 唯一参与分档的开放海域
SHALLOW = 14
NEAR_SEA, FAR_SEA, DEEP_SEA = 9, 21, 22
CLIFF, SHORE, EMPTY, STREAM, PLANK, FERRY, TRAIL = 16, 15, 0, 7, 12, 13, 20
CITY_TERRAIN = {17, 18, 19}
MOVEABLE = {1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 17, 18, 19, 20}
# 桥接时各类不可行走地形的穿越代价（越大越不优先切）
CARVE_COST = {FAR_SEA: 1.0, DEEP_SEA: 1.0, SHORE: 3.0, CLIFF: 4.0, EMPTY: 2.0, STREAM: 3.5}
CARVE_TO = {FAR_SEA: NEAR_SEA, DEEP_SEA: NEAR_SEA, CLIFF: PLANK, SHORE: TRAIL,
            EMPTY: TRAIL, STREAM: FERRY}

NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}


def hex_dist_to_land(land):
    """到最近陆地的六边形距离（多源 BFS）。"""
    cx, cy = land.shape
    dist = np.full((cx, cy), -1, np.int32)
    q = deque()
    for x in range(cx):
        for y in np.flatnonzero(land[x]).tolist():
            dist[x, y] = 0
            q.append((x, y))
    while q:
        x, y = q.popleft()
        d = dist[x, y] + 1
        for dx, dy in NEIGH[x & 1]:
            nx, ny = x + dx, y + dy
            if 0 <= nx < cx and 0 <= ny < cy and dist[nx, ny] < 0:
                dist[nx, ny] = d
                q.append((nx, ny))
    return dist


def walkable(mask_src):
    """把布尔图拆成可行走连通块，返回 (标签图, 块数)。"""
    cx, cy = mask_src.shape
    idx = np.full((cx, cy), -1, np.int64)
    flat = np.flatnonzero(mask_src.ravel())
    idx.ravel()[flat] = np.arange(flat.size)
    rows, cols = [], []
    for x in range(cx):
        ys = np.flatnonzero(mask_src[x])
        if ys.size == 0:
            continue
        for dx, dy in NEIGH[x & 1]:
            nx = np.full(ys.shape, x + dx, np.int64)
            ny = ys + dy
            ok = (nx >= 0) & (nx < cx) & (ny >= 0) & (ny < cy)
            if not ok.any():
                continue
            nxc, nyc = nx[ok], ny[ok]
            good = mask_src[nxc, nyc]
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
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=str(ROOT / "data" / "sango_full"))
    ap.add_argument("--near", type=int, default=12, help="近海半径：距陆地<=该值仍可行走")
    ap.add_argument("--deep", type=int, default=40, help="超过该距离记为深海")
    ap.add_argument("--bridge-min", type=int, default=200, help="孤块>=该格数则尝试桥接到主网")
    ap.add_argument("--bridge-max-cost", type=float, default=80.0, help="桥接最大代价，超过则放弃")
    ap.add_argument("--report", action="store_true", help="只体检不写回")
    args = ap.parse_args()

    src = Path(args.src)
    terrain = np.load(src / "terrain.npy").copy()
    cx, cy = terrain.shape
    print(f"格 {cx}x{cy}", flush=True)

    # ---------------------------------------------------------- 1) 海域分档
    land = ~np.isin(terrain, list(SEA))
    dist = hex_dist_to_land(land)
    open_sea = terrain == OPEN_SEA
    n_open = int(open_sea.sum())
    far = open_sea & (dist > args.near)
    deep = open_sea & (dist > args.deep)
    terrain[far] = FAR_SEA
    terrain[deep] = DEEP_SEA
    print(f"海域分档: 开放海域 {n_open} 格 -> 近海(9) {int((terrain == NEAR_SEA).sum())}、"
          f"远海(21) {int((terrain == FAR_SEA).sum())}、深海(22) {int((terrain == DEEP_SEA).sum())}"
          f"；屏蔽 {int(far.sum())} 格（{far.sum() / max(1, n_open):.3f}）", flush=True)

    # ---------------------------------------------------------- 2) 孤岛
    mask = np.isin(terrain, list(MOVEABLE))
    L, n = walkable(mask)
    sz = np.bincount(L[L >= 0])
    order = np.argsort(-sz)
    main = int(order[0])
    print(f"可行走 {int(mask.sum())} 格, 连通块 {n} 个; 主块 {int(sz[main])} 格", flush=True)

    others = [(int(sz[i]), int(i)) for i in order[1:]]
    to_bridge = [(s, i) for s, i in others if s >= args.bridge_min]
    to_block = [(s, i) for s, i in others if s < args.bridge_min]
    print(f"  孤块 {len(others)} 个: >= {args.bridge_min} 格的 {len(to_bridge)} 个"
          f"（合计 {sum(s for s, _ in to_bridge)} 格）-> 尝试桥接;"
          f" 更小的 {len(to_block)} 个（合计 {sum(s for s, _ in to_block)} 格）-> 设为不可行走", flush=True)

    # 各孤块的格子清单（一次分组，避免对每个块做一次全图 np.where）
    need = {i for _, i in others}
    cells_of = {}
    lx, ly = np.where(L >= 0)          # np.where 返回 (dim0, dim1) = (x, y)
    lv = L[lx, ly]
    for a, b, l in zip(lx.tolist(), ly.tolist(), lv.tolist()):
        if l in need:
            cells_of.setdefault(l, []).append((a, b))

    # ---- 从主块出发，按「切一格要付多少代价」做 Dijkstra
    BIG = 1e18
    cost = np.full((cx, cy), BIG, np.float64)
    prev = np.full((cx, cy), -1, np.int64)
    xs, ys = np.where(L == main)       # 注意顺序：dim0 = x, dim1 = y
    heap = [(0.0, x, y) for x, y in zip(xs.tolist(), ys.tolist())]
    for x, y in zip(xs.tolist(), ys.tolist()):
        cost[x, y] = 0.0
    heapq.heapify(heap)

    def step_cost(t):
        if t in MOVEABLE:
            return 0.0
        return CARVE_COST.get(int(t), 5.0)

    while heap:
        d, x, y = heapq.heappop(heap)
        if d > cost[x, y] + 1e-9:
            continue
        if d > args.bridge_max_cost:
            continue
        for dx, dy in NEIGH[x & 1]:
            nx, ny = x + dx, y + dy
            if nx < 0 or ny < 0 or nx >= cx or ny >= cy:
                continue
            nd = d + step_cost(int(terrain[nx, ny]))
            if nd < cost[nx, ny] - 1e-9:
                cost[nx, ny] = nd
                prev[nx, ny] = x * cy + y
                heapq.heappush(heap, (nd, nx, ny))

    carved = 0
    blocked = 0
    fail = 0
    for s, i in to_bridge:
        cl = cells_of.get(i, [])
        if not cl:
            fail += 1
            continue
        cvals = np.array([cost[a, b] for a, b in cl], np.float64)
        j = int(np.argmin(cvals))
        if cvals[j] > args.bridge_max_cost or not np.isfinite(cvals[j]):
            fail += 1
            continue
        # 回溯路径，把需要切的格子改掉
        a, b = cl[j]
        cur = a * cy + b
        while cur >= 0:
            x, y = int(cur // cy), int(cur % cy)
            t = int(terrain[x, y])
            if t not in MOVEABLE:
                terrain[x, y] = CARVE_TO.get(t, TRAIL)
                carved += 1
            cur = int(prev[x, y])
    print(f"桥接: 成功 {len(to_bridge) - fail} 个, 放弃 {fail} 个; 改写 {carved} 格", flush=True)

    # ---- 复检后，仍与主网不连通的可行走块一律设为不可行走。
    # 这样既覆盖了「小于 bridge-min 的碎点」，也覆盖了「桥接代价超上限的封闭海」，
    # 不会留下船进不去、单位可能搁浅的死水域。
    mask_b = np.isin(terrain, list(MOVEABLE))
    Lb, nb = walkable(mask_b)
    szb = np.bincount(Lb[Lb >= 0])
    ob = np.argsort(-szb)
    rest = [int(i) for i in ob[1:]]
    blocked = 0
    if rest:
        rx, ry = np.where(np.isin(Lb, rest))
        t = terrain[rx, ry]
        is_water = np.isin(t, [NEAR_SEA, SHALLOW, FAR_SEA, DEEP_SEA])
        terrain[rx[is_water], ry[is_water]] = DEEP_SEA
        terrain[rx[~is_water], ry[~is_water]] = CLIFF
        blocked = int(rx.size)
    print(f"残留封锁: {len(rest)} 个块 / {blocked} 格 -> 水变深海(22)、陆变崖(16)", flush=True)

    # ---------------------------------------------------------- 复检
    mask2 = np.isin(terrain, list(MOVEABLE))
    L2, n2 = walkable(mask2)
    sz2 = np.bincount(L2[L2 >= 0])
    o2 = np.argsort(-sz2)
    print(f"复检: 可行走 {int(mask2.sum())} 格, 连通块 {n2} 个, 主块 {int(sz2[o2[0]])} 格; "
          f"其余块最大 {int(sz2[o2[1]]) if len(o2) > 1 else 0} 格", flush=True)
    if len(o2) > 1:
        top = [(int(sz2[i]), int(i)) for i in o2[1:6]]
        for s, i in top:
            tt, cn = np.unique(terrain[L2 == i], return_counts=True)
            print(f"    残留块{i}: {s} 格 地形 {dict(zip(tt.tolist(), cn.tolist()))}", flush=True)

    if args.report:
        print("--report：未写回任何文件", flush=True)
        return

    bak = src / "terrain_orig2.npy"
    if not bak.exists():
        np.save(bak, np.load(src / "terrain.npy"))
        print(f"已备份原始地形 -> {bak.name}", flush=True)
    np.save(src / "terrain.npy", terrain)
    (src / "terrain.bin").write_bytes(terrain.tobytes())
    c = Counter(terrain.ravel().tolist())
    print(f"已写回 {src/'terrain.npy'}；地形分布 {dict(sorted(c.items()))}", flush=True)


if __name__ == "__main__":
    main()
