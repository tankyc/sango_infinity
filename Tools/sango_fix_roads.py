"""检查并修复「相邻城市之间无通路」的问题。

背景：引擎 Map.GetDirectPath 是 BFS + 安全次数上限（ScenarioVariables.pathfindingSafeCount，
默认 10 万）。目标不可达时 BFS 会把整个连通域搜完 → 报「寻路超出安全次数」。

通行性取 Build/Content/Data/Common/TerrainTypes.json 的 moveable：
  可通行  1草地 2土 3砂地 4湿地 5毒泉 6森 8河 9海 10荒地 11主径 12栈道 13渡所 14浅滩 17都市 18港 19关 20小径
  不可通行 0无 7川 15岸 16崖 21..31地20..30

我们地图里真正造成阻隔的是 **川(7)** —— 一条条 1 格宽的河线像墙一样切断陆地；
以及 **崖(16)** 整片山块。修复方式是把挡路的那几格换成可通行地形：
  跨 川(7)  -> 渡所(13)   跨 崖(16) -> 栈道(12)   跨 岸(15)/其他 -> 小径(20)

用法：
  python sango_fix_roads.py --check                     # 只体检
  python sango_fix_roads.py --fix --out data\sango_fixed
"""
import argparse
import heapq
import json
from pathlib import Path

import numpy as np
from scipy import sparse
from scipy.sparse.csgraph import connected_components

ROOT = Path(__file__).resolve().parent
SCEN = Path(r"d:/sango_infinity/Build/Content/Scenario")

MOVEABLE = {1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 17, 18, 19, 20}
CROSS = {7: 13, 15: 20, 16: 12, 0: 20}      # 阻挡地形 -> 换成什么（渡所/小径/栈道）
CROSS_COST = {7: 6.0, 16: 3.0, 15: 4.0, 0: 2.0}   # 穿越代价（越大越不愿意切）

# odd-q 六边形邻接（与 sango_scenario.py / 引擎一致），下标 = x & 1
NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}


def neighbors(x, y):
    for dx, dy in NEIGH[x & 1]:
        yield x + dx, y + dy


def build_components(passable):
    """用稀疏图求连通分量，返回 (labels, n)。labels[x][y] = 分量号，不可通行处为 -1。"""
    cx, cy = passable.shape
    idx = np.full((cx, cy), -1, np.int64)
    flat = np.flatnonzero(passable.ravel())
    idx.ravel()[flat] = np.arange(flat.size)
    rows, cols = [], []
    for x in range(cx):
        ys = np.flatnonzero(passable[x])
        if ys.size == 0:
            continue
        for dx, dy in NEIGH[x & 1]:
            nx = np.full(ys.shape, x + dx, np.int64)
            ny = ys + dy
            ok = (nx >= 0) & (nx < cx) & (ny >= 0) & (ny < cy)
            if not ok.any():
                continue
            nx_c, ny_c = nx[ok], ny[ok]
            good = passable[nx_c, ny_c]
            if not good.any():
                continue
            rows.append(idx[x, ys[ok][good]])
            cols.append(idx[nx_c[good], ny_c[good]])
    if rows:
        rows = np.concatenate(rows)
        cols = np.concatenate(cols)
        g = sparse.coo_matrix((np.ones(rows.size, np.int8), (rows, cols)), shape=(flat.size, flat.size))
    else:
        g = sparse.coo_matrix((flat.size, flat.size))
    n, lab = connected_components(g, directed=False)
    labels = np.full((cx, cy), -1, np.int32)
    labels.ravel()[flat] = lab
    return labels, n


def carve_path(terrain, passable, start, goal, pad=140):
    """在「允许破开阻挡格」的前提下找最省路径，返回需要改写的格子 [(x,y,新地形)]。

    代价：可通行格 1.0；阻挡格 = CROSS_COST（越硬越贵），走过就换成可通行地形。
    只在两城外扩 pad 格的窗口内搜索 —— 全局 Dijkstra 在 180 万格上太慢，
    而这些不通的城对都很近（< 40 格），窗口足够。
    """
    cx, cy = terrain.shape
    sx, sy = start
    gx, gy = goal
    x0 = max(0, min(sx, gx) - pad)
    x1 = min(cx - 1, max(sx, gx) + pad)
    y0 = max(0, min(sy, gy) - pad)
    y1 = min(cy - 1, max(sy, gy) + pad)
    dist = {}
    prev = {}
    dist[(sx, sy)] = 0.0
    pq = [(0.0, sx, sy)]
    while pq:
        d, x, y = heapq.heappop(pq)
        if d > dist.get((x, y), 1e18) + 1e-9:
            continue
        if (x, y) == (gx, gy):
            break
        for nx, ny in neighbors(x, y):
            if nx < x0 or nx > x1 or ny < y0 or ny > y1:
                continue
            t = int(terrain[nx, ny])
            c = 1.0 if passable[nx, ny] else CROSS_COST.get(t, 5.0)
            nd = d + c
            if nd < dist.get((nx, ny), 1e18):
                dist[(nx, ny)] = nd
                prev[(nx, ny)] = (x, y)
                heapq.heappush(pq, (nd, nx, ny))
    if (gx, gy) not in dist:
        return None
    # 回溯，收集需要破开的格子
    out = []
    cur = (gx, gy)
    while cur != (sx, sy):
        p = prev.get(cur)
        if p is None:
            return None
        x, y = cur
        if not passable[x, y]:
            t = int(terrain[x, y])
            out.append((x, y, CROSS.get(t, 20)))
        cur = p
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=str(ROOT / "data" / "sango_full"))
    ap.add_argument("--scenario", default=str(SCEN / "Scenario_world_full.json"))
    ap.add_argument("--check", action="store_true", help="只体检")
    ap.add_argument("--fix", action="store_true", help="执行修复并写出 terrain_fixed.npy")
    ap.add_argument("--out", default=str(ROOT / "data" / "sango_fixed"))
    ap.add_argument("--max-pairs", type=int, default=99999)
    args = ap.parse_args()

    src = Path(args.src)
    terrain = np.load(src / "terrain.npy")
    cx, cy = terrain.shape
    scen = json.loads(Path(args.scenario).read_text(encoding="utf-8"))
    cities = {int(k): v for k, v in scen["citySet"].items()}
    print(f"图 {cx}x{cy}  城市 {len(cities)}", flush=True)

    def repass(t):
        p = np.zeros(cx * cy, bool)
        p.ravel()[:] = np.isin(t.ravel(), list(MOVEABLE))
        return p.reshape(cx, cy)

    passable = repass(terrain)
    b_qc = int((terrain == 7).sum())
    b_yc = int((terrain == 16).sum())
    b_an = int((terrain == 15).sum())
    b_other = int((~passable).sum()) - b_qc - b_yc - b_an
    print("可通行格占比 %.3f  阻挡: 川=%d 崖=%d 岸=%d 其他=%d"
          % (passable.mean(), b_qc, b_yc, b_an, b_other), flush=True)

    labels, ncomp = build_components(passable)
    print(f"连通分量 {ncomp} 个", flush=True)

    # 城市所在格（城市格已被写成都市 17，本身可通行）
    bad = []
    total_pairs = 0
    for cid, c in sorted(cities.items()):
        x, y = c["x"], c["y"]
        if not (0 <= x < cx and 0 <= y < cy):
            continue
        a = labels[x, y]
        for nb in c.get("NeighborList", []):
            if nb not in cities or nb < cid:
                continue
            nx, ny = cities[nb]["x"], cities[nb]["y"]
            if not (0 <= nx < cx and 0 <= ny < cy):
                continue
            total_pairs += 1
            b = labels[nx, ny]
            if a < 0 or b < 0 or a != b:
                bad.append((cid, nb, c["Name"], cities[nb]["Name"], (x, y), (nx, ny)))
    print(f"相邻城市对: 共 {total_pairs} 对，不通 {len(bad)} 对", flush=True)
    for b in bad[:25]:
        print(f"  [{b[0]}]{b[2]}{(b[4])} <-> [{b[1]}]{b[3]}{(b[5])}", flush=True)
    if len(bad) > 25:
        print(f"  … 其余 {len(bad) - 25} 对", flush=True)

    if args.check or not args.fix:
        return

    # ---- 修复：对每对不通的城市，切一条最省的路
    fixed = terrain.copy()
    done = set()
    carved_total = 0
    for (cid, nb, n1, n2, p1, p2) in bad[:args.max_pairs]:
        key = (min(cid, nb), max(cid, nb))
        if key in done:
            continue
        done.add(key)
        p = repass(fixed)
        if p[p1[0], p1[1]] and p[p2[0], p2[1]] and labels[p1[0], p1[1]] == labels[p2[0], p2[1]]:
            continue                                    # 前面的修复已连带打通
        cells = carve_path(fixed, p, p1, p2)
        if not cells:
            print(f"  跳过（找不到可切路径）{n1}<->{n2}", flush=True)
            continue
        for x, y, t in cells:
            fixed[x, y] = t
        carved_total += len(cells)

    # 复检
    labels2, n2c = build_components(repass(fixed))
    still = 0
    for cid, c in sorted(cities.items()):
        x, y = c["x"], c["y"]
        if not (0 <= x < cx and 0 <= y < cy):
            continue
        a = labels2[x, y]
        for nbi in c.get("NeighborList", []):
            if nbi not in cities or nbi < cid:
                continue
            nx, ny = cities[nbi]["x"], cities[nbi]["y"]
            if not (0 <= nx < cx and 0 <= ny < cy):
                continue
            if a < 0 or labels2[nx, ny] < 0 or a != labels2[nx, ny]:
                still += 1
    print(f"修复：切开 {len(done)} 对、改写 {carved_total} 格；复检剩余不通 {still} 对，分量 {n2c} 个", flush=True)

    # 就地写回源目录（先备份原始 terrain），这样 sango_build_bin.py / sango_basemap.py 能直接沿用
    bak = src / "terrain_orig.npy"
    if not bak.exists():
        np.save(bak, terrain)
        print(f"已备份原始地形 -> {bak}", flush=True)
    np.save(src / "terrain.npy", fixed)
    (src / "terrain.bin").write_bytes(fixed.tobytes())
    changed = int((fixed != terrain).sum())
    print(f"已写回 {src/'terrain.npy'}（改动 {changed} 格）", flush=True)


if __name__ == "__main__":
    main()
