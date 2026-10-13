"""地图后处理：城市占地平整化 + 内政地生成 + 可通行格高度平滑。

在 sango_convert.py 之后、sango_basemap.py / sango_build_bin.py 之前运行
（和 sango_fix_roads.py 同一层）。就地写回 terrain.npy / vheight.npy / vwater.npy，
并新增 interior.npy（内政地掩码，供 sango_build_bin.py 写成 terrainState 的内政位）。

三件事的依据都来自引擎源码 + DefaultMap.bin 的实测：

1) 城市占地（平整化 + 陆地化）
   MapGrid.GridData 每格 7 字节 = terrainType(u8) + terrainState(i32) + areaId(u16)。
   DefaultMap.bin 实测：每座「都市」恰好是 7 格 —— 中心 + 周围 6 邻的六边形块
   （42 座城 x 7 = 294 格 terrainType=17 全部吻合）。
   本脚本把每座城也铺成同样 7 格：统一地形 都市(17)、一律陆地化（顶点 water=0）、
   并把该范围的顶点高度压成一个平台。

2) 内政地
   Cell.IsInterior => terrainState 的 Interior 位。
   MapGrid.SetGridState 用 1 << (int)state，而 GridState.Interior = 2
   => 内政位 = 1<<2 = **4**（Defence=2, Thief=8）。
   City.interiorCellList 从「本城 areaId 的格子」里筛出 IsInterior 的格，
   City.InteriorCellCount 就是该城可建建筑的槽位数。
   DefaultMap.bin 实测分布：53 座城有内政地，每城 2~22 格（中位 12），
   地形 100% 是 土(2)，位于城外 2~8 格、成小簇。
   本脚本按同一分布生成：每城在城外挑同等数量的可建造陆地格。

3) 可通行格高度平滑
   TerrainTypes.json 的 moveable 决定通行性。要求「可通行格自身的顶点高差不过大、
   相邻可通行格能平滑衔接」，故对可通行格做「斜率限制 + 松弛」：
     - 相邻可通行格高差 <= --max-step（默认 10 字节 = 5 世界单位）
     - 每格高度按格写回顶点（共享顶点取相邻格平均），保证格内也平坦
   不可通行地形（川 7 / 岸 15 / 崖 16 / 空 0）保持原高度，继续充当天然屏障。

用法：
  python sango_map_fixes.py --src data\\sango_full                 # 处理 1:1 数据
  python sango_map_fixes.py --src data\\sango_full --report        # 只体检不改
"""
import argparse
import json
import struct
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent
# 中国区内政地的来源（DefaultMap 是 311 中国区原图）
DEF_MAP = Path(r"d:/sango_infinity/Build/Content/Map/DefaultMap.bin")
WORLD_INFO = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\world\world_info.json")
CITIES_GEO = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\cities_georef.json")

# Sango 地形（与 TerrainTypes.json 对应）
S_SOIL, S_CITY = 2, 17
WATER = {7, 8, 9, 14, 21, 22}          # 川/河/海/浅滩/远海/深海：不可建造
# canBuild=true 的四种地形里，主径(11) 是道路 —— 内政地压在它上面会挡路，
# 所以实际可用的只有 草地/土/砂地。
BUILDABLE = {1, 2, 3}
ROAD = {11, 12, 13, 20}                # 主径/栈道/渡所/小径：道路，内政地和建筑工程都别占
CITY_TERRAIN = {17, 18, 19}            # 都市 / 港 / 关所：城市地形本身，不能被内政地占用
MOVEABLE = {1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 17, 18, 19, 20}

# odd-q 六边形邻接，下标 = x & 1（与引擎 / sango_fix_roads.py 一致）
NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}


def _read_bin_str(f):
    n, sh = 0, 0
    while True:
        v = f.read(1)[0]
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    return f.read(n).decode("utf-8", "replace")


def defaultmap_interior(ds=1):
    """把 DefaultMap(中国区) 的内政格坐标转到世界图，返回 {世界城 id: [(x, y), ...]}。

    映射链路（用 42 座三国城标定，平均误差 0.29 格、最大 0.29 格）：
        DefaultMap 格 (dx,dy) --(-28)--> 中国块局部坐标
        world_cell = floor((local + china_lo/hi + 0.5) / DS)
    （-28 就是 311 可玩区在 DefaultMap 里的偏移；china_lo/hi 取自 data/world/world_info.json。）

    DefaultMap 的 42 个「都市(17) 七格块」与 data/cities_georef.json 的 42 座城一一对应、
    顺序一致，而 cities.json 前 42 条正是它们（id = 序号+1），所以：
        georef 第 i 座  <->  世界图城 id (i+1)
    返回空 dict 表示标定失败（此时调用方回退到程序化生成）。
    """
    if not DEF_MAP.exists() or not WORLD_INFO.exists() or not CITIES_GEO.exists():
        return {}
    with open(DEF_MAP, "rb") as f:
        if struct.unpack("<i", f.read(4))[0] != 10:
            return {}
        _read_bin_str(f)
        w, h = struct.unpack("<ii", f.read(8))
        gs = struct.unpack("<i", f.read(4))[0]
        gx, gy = w * 5 // gs, h * 5 // gs
        buf = np.frombuffer(f.read(gx * gy * 7), np.uint8).reshape(gx, gy, 7)
    tt = buf[..., 0].astype(np.int32)
    st = (buf[..., 1].astype(np.uint32) | (buf[..., 2].astype(np.uint32) << 8)
          | (buf[..., 3].astype(np.uint32) << 16) | (buf[..., 4].astype(np.uint32) << 24))
    info = json.loads(WORLD_INFO.read_text(encoding="utf-8"))
    geo = json.loads(CITIES_GEO.read_text(encoding="utf-8"))
    clo, chi = info["china_lo"], info["china_hi"]

    # 都市(17) 连通块 -> 块中心
    seen = np.zeros((gx, gy), bool)
    centers = []
    for x, y in zip(*np.where(tt == 17)):
        if seen[x, y]:
            continue
        stack = [(int(x), int(y))]
        seen[x, y] = True
        cells = []
        while stack:
            a, b = stack.pop()
            cells.append((a, b))
            for dx, dy in NEIGH[a & 1]:
                na, nb = a + dx, b + dy
                if 0 <= na < gx and 0 <= nb < gy and not seen[na, nb] and tt[na, nb] == 17:
                    seen[na, nb] = True
                    stack.append((na, nb))
        if len(cells) >= 5:
            centers.append((float(np.mean([c[0] for c in cells])),
                            float(np.mean([c[1] for c in cells]))))
    if not centers or len(centers) != len(geo):
        return {}

    C = np.array(centers)
    G = np.array([[float(g["lo"]), float(g["hi"])] for g in geo])
    # centers 是按 x 行主序扫出来的（第一个是 x 最小的那座），与 geo 的文件顺序不同，
    # 必须先按「局部坐标 = 中心 - 28」把两者一一配对，否则 j+1 会整体错位。
    L = C - 28.0
    dm = ((L[:, None, :] - G[None, :, :]) ** 2).sum(-1)
    pair = dm.argmin(1)
    if len(set(pair.tolist())) != len(pair):
        return {}
    print(f"  DefaultMap 城市配对: 最大偏差 {float(np.sqrt(dm.min(1)).max()):.2f} 格", flush=True)
    gi_of_center = {j: int(gi) for j, gi in enumerate(pair.tolist())}

    out = {}
    for ddx, ddy in np.argwhere((st & 4) != 0):
        j = int(((C - np.array([ddx, ddy])) ** 2).sum(1).argmin())
        gi = gi_of_center[j]
        lx, ly = float(ddx) - 28.0, float(ddy) - 28.0
        wx = int(np.floor((lx + clo + 0.5) / ds))
        wy = int(np.floor((ly + chi + 0.5) / ds))
        out.setdefault(gi + 1, []).append((wx, wy))
    return out


def hex_dist(x1, y1, x2, y2):
    """odd-q 六边形距离。"""
    q1, r1 = x1, y1 - (x1 - (x1 & 1)) // 2
    q2, r2 = x2, y2 - (x2 - (x2 & 1)) // 2
    dq, dr = q1 - q2, r1 - r2
    return (abs(dq) + abs(dr) + abs(dq + dr)) // 2


# ---------------------------------------------------------------- 向量化邻域
def shift(a, dx, dy):
    """返回 (b, valid)：b[x, y] = a[x + dx, y + dy]，越界处 valid=False。"""
    h, w = a.shape
    out = np.zeros_like(a)
    v = np.zeros((h, w), bool)
    sy0, sy1 = max(0, dy), min(w, w + dy)
    sx0, sx1 = max(0, dx), min(h, h + dx)
    dy0, dy1 = max(0, -dy), min(w, w - dy)
    dx0, dx1 = max(0, -dx), min(h, h - dx)
    if sx0 < sx1 and sy0 < sy1:
        out[dx0:dx1, dy0:dy1] = a[sx0:sx1, sy0:sy1]
        v[dx0:dx1, dy0:dy1] = True
    return out, v


def neighbor_val(field, k, par):
    """第 k 个六边形方向上的邻居值（按列奇偶分别取偏移）。

    par 为「奇数列」掩码：偶数列用 NEIGH[0]、奇数列用 NEIGH[1]。
    注意两个分支必须各用自己的掩码，否则偶数列会被奇数列的偏移覆盖/漏掉
    （曾因此让所有偶数列取不到邻居，松弛完全失效）。
    """
    out = np.zeros_like(field)
    valid = np.zeros(field.shape, bool)
    for p, offs in NEIGH.items():
        dx, dy = offs[k]
        v, ok = shift(field, dx, dy)
        sel = (par if p else ~par) & ok
        out[sel] = v[sel]
        valid[sel] = True
    return out, valid


def relax_slope(h, passable, max_step, iters, pinned=None):
    """把「相邻可通行格高差」限制到 max_step 内（同步区间投影）。

    每轮先用全部邻居算出本格允许的区间 [max(邻居)-M, min(邻居)+M]，再夹一次。
    必须是「同步」更新（一轮内 h 不改），不能按方向就地改：
    就地 Gauss-Seidel 在邻格要求互相矛盾时会进极限环（实测每轮稳定摆动 240、
    500 轮仍有超限），同步版在真实数据上 16 轮就完全收敛到 0 条超限。
    不可通行格不参与约束（继续当屏障），pinned 的格是硬边界（自己不动、只约束邻居）。
    """
    BIG = 1e9
    h = h.astype(np.float64)
    par = parity(*h.shape)
    free = passable.copy()
    if pinned is not None:
        free &= ~pinned
    pass_i = passable.astype(np.int32)

    for _ in range(iters):
        lo = np.full(h.shape, -BIG)
        hi = np.full(h.shape, BIG)
        for k in range(6):
            nv, ok = neighbor_val(h, k, par)
            np_, ok2 = neighbor_val(pass_i, k, par)
            m = free & ok & ok2 & (np_ > 0)
            if not m.any():
                continue
            lo[m] = np.maximum(lo[m], nv[m] - max_step)
            hi[m] = np.minimum(hi[m], nv[m] + max_step)
        m = free & (hi < BIG)
        if not m.any():
            break
        new = np.clip(h[m], lo[m], hi[m])
        changed = float(np.abs(new - h[m]).sum())
        h[m] = new
        if changed < 0.5:
            break
    return np.clip(np.round(h), 0, 255).astype(np.int32)


def parity(CX, CY):
    """列奇偶掩码（odd-q 邻接用）。"""
    return (np.arange(CX)[:, None] & 1).astype(bool) & np.ones((CX, CY), bool)


def count_over(hc, moveable, par, max_step):
    """统计「相邻可通行格高差 > max_step」的边数 / 总边数。"""
    over = edges = 0
    mi = moveable.astype(np.int32)
    for k in range(6):
        nv, ok = neighbor_val(hc, k, par)
        mv, ok2 = neighbor_val(mi, k, par)
        m = moveable & ok & ok2 & (mv > 0)
        if not m.any():
            continue
        d = np.abs(hc - nv)[m]
        edges += int(d.size)
        over += int((d > max_step).sum())
    return over, edges


# ---------------------------------------------------------------- 顶点合成
def compose_vertices(vh, hc, passable, radius=2):
    """把「每格高度 hc」按格写回顶点场。

    顶点 (vx, vy) 由覆盖它的若干格共同决定，取这些格的 hc 平均；
    没有被任何可通行格覆盖的顶点保留原高度（保持山体/河谷原貌）。
    格 (x,y) 覆盖顶点 vx∈[4x, 4x+2r]，vy∈[4y+2(x&1), 4y+2(x&1)+2r]。
    """
    VX, VY = vh.shape
    CX, CY = hc.shape
    xs = np.arange(CX, dtype=np.int32)[:, None]
    ys = np.arange(CY, dtype=np.int32)[None, :]
    par = ((xs & 1) * 2)
    acc = np.zeros(VX * VY, np.float64)
    cnt = np.zeros(VX * VY, np.float64)
    sel = passable
    if not sel.any():
        return vh
    w = hc[sel].astype(np.float64)
    for dx in range(2 * radius + 1):
        ax = np.clip(4 * xs + dx, 0, VX - 1)
        for dy in range(2 * radius + 1):
            ay = np.clip(4 * ys + par + dy, 0, VY - 1)
            idx = (ax * VY + ay)[sel]
            acc += np.bincount(idx, weights=w, minlength=VX * VY)
            cnt += np.bincount(idx, minlength=VX * VY)
    cnt = cnt.reshape(VX, VY)
    out = vh.astype(np.float64).copy()
    ok = cnt > 0
    out[ok] = acc.reshape(VX, VY)[ok] / cnt[ok]
    return np.clip(np.round(out), 0, 255).astype(np.uint8)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=str(ROOT / "data" / "sango_full"))
    ap.add_argument("--radius", type=int, default=1, help="城市占地半径(格)，1 => 7 格")
    ap.add_argument("--interior-base", type=int, default=16, help="内政地基准数量(最大 rank)")
    ap.add_argument("--interior-min", type=int, default=6)
    ap.add_argument("--interior-max-dist", type=int, default=8, help="内政地离城最大格距")
    ap.add_argument("--interior-min-dist", type=int, default=3,
                    help="内政地离城最小格距（旧版 2 太贴城墙）")
    ap.add_argument("--max-step", type=int, default=10, help="相邻可通行格最大高差(高度字节)")
    ap.add_argument("--iters", type=int, default=200, help="斜率松弛迭代上限（收敛即提前结束）")
    ap.add_argument("--report", action="store_true", help="只体检不改")
    args = ap.parse_args()

    src = Path(args.src)
    terrain = np.load(src / "terrain.npy").copy()
    vh = np.load(src / "vheight.npy").copy()
    vw = np.load(src / "vwater.npy").copy()
    cities = json.loads((src / "cities.json").read_text(encoding="utf-8"))
    CX, CY = terrain.shape
    VX, VY = vh.shape
    city0 = int((terrain == S_CITY).sum())
    print(f"格 {CX}x{CY}  顶点 {VX}x{VY}  城市 {len(cities)}  原都市格 {city0}", flush=True)

    moveable = np.isin(terrain, list(MOVEABLE))
    land = ~np.isin(terrain, list(WATER)) & (terrain != 0)

    # ------------------------------------------------ 每格高度（与引擎 GetGridHeight 一致）
    xs = np.arange(CX, dtype=np.int32)[:, None]
    ys = np.arange(CY, dtype=np.int32)[None, :]
    sxy = (np.clip(4 * xs + 2, 0, VX - 1), np.clip(4 * ys + 2 * (xs & 1) + 2, 0, VY - 1))
    hc = vh[sxy].astype(np.int32)

    # ================================================ 1) 城市占地：7 格 + 压平 + 陆地化
    r = args.radius
    city_mask = np.zeros((CX, CY), bool)
    # 归属图：0 = 无主，>0 = 城市 id。城市占地与内政地都登记在这里，
    # 用于保证不同城市的「顶点块」不互相覆盖（相邻格会共享 1 列顶点，
    # 两个城压到不同高度时必定有一格不平）。
    owner_map = np.zeros((CX, CY), np.int32)
    city_base = {}
    # 下标 = areaId（城 id 从 1 起，与 sango_build_bin.py 的 assign_areas 一致）
    city_base_arr = np.zeros(len(cities) + 1, np.int32)
    out_of_range = 0
    for ci, c in enumerate(cities):
        x, y = c["x"], c["y"]
        if not (0 <= x < CX and 0 <= y < CY):
            out_of_range += 1
            continue
        cells = [(x, y)]
        for dx, dy in NEIGH[x & 1]:
            nx, ny = x + dx, y + dy
            if 0 <= nx < CX and 0 <= ny < CY:
                cells.append((nx, ny))
        city_mask[tuple(np.array(cells).T)] = True
        owner_map[tuple(np.array(cells).T)] = ci + 1
        # 平台高度：取该范围顶点高度的中位，夹到合理陆地范围
        base = int(np.median([hc[a, b] for a, b in cells]))
        b = int(np.clip(base if base > 2 else 5, 4, 80))
        city_base[(x, y)] = b
        city_base_arr[ci + 1] = b
    if out_of_range:
        print(f"  [警告] {out_of_range} 座城坐标越界，已跳过", flush=True)

    print(f"城市占地: {int(city_mask.sum())} 格（{len(cities)} 城 x {int(city_mask.sum()) // max(1, len(cities))} 格/城）", flush=True)

    # 陆地化：占地一律 都市(17)（陆地、可通行）。必须在平滑之前写入，
    # 否则落在海/崖上的城市格不算可通行，第 3 步的约束会把它们漏掉。
    terrain[np.where(city_mask)] = S_CITY

    # ================================================ 2) 内政地
    # 归属判定：areaId 在 sango_build_bin.py 里按「到最近城的代价」分派，
    # 这里要求内政格离本城严格最近，否则将来不会落进这座城的 interiorCellList。
    CXS = np.array([c["x"] for c in cities], np.int64)
    CYS = np.array([c["y"] for c in cities], np.int64)
    CQ = CXS
    CR = CYS - (CXS - (CXS & 1)) // 2

    base_n = args.interior_base
    # 值 = 所属城市 id（从 1 起），0 = 不是内政地。
    # 一张表同时承载「内政位」和「归属城市」：build_bin 直接拿它写 areaId，
    # 保证每块内政地都有归属城市（否则 City.interiorCellList 收不到它、拿不到槽位）。
    interior = np.zeros((CX, CY), np.int32)
    # 中国区：直接采用 DefaultMap 转换过来的内政地坐标（那才是原版的摆放位置）。
    # 其余区域仍按统计规律程序化生成。
    from_def = defaultmap_interior(ds=1 if CX >= 1800 else 2)
    tot_def = sum(len(v) for v in from_def.values())
    print(f"DefaultMap 内政地: 标定到 {len(from_def)} 座中国城 / {tot_def} 格" if from_def
          else "DefaultMap 内政地: 标定失败，全部走程序化生成", flush=True)
    per_city = []
    n_def_used = n_def_skip = 0
    for ci, c in enumerate(cities):
        x, y = c["x"], c["y"]
        if not (0 <= x < CX and 0 <= y < CY):
            continue
        cid = ci + 1
        want = int(np.clip(base_n - 2 * (int(c.get("rank", 3)) - 1),
                           args.interior_min, args.interior_max_dist + 10))
        R = args.interior_max_dist
        got = 0
        # 1) 中国城先用 DefaultMap 转换来的坐标；被占/是水/是城市地形/与别城顶块相邻的跳过
        for (wx, wy) in from_def.get(cid, []):
            if not (0 <= wx < CX and 0 <= wy < CY):
                n_def_skip += 1
                continue
            if owner_map[wx, wy] or terrain[wx, wy] in CITY_TERRAIN or terrain[wx, wy] in WATER:
                n_def_skip += 1
                continue
            if any(0 < owner_map[a, b] != cid
                   for a, b in [(wx + u, wy + v) for u, v in NEIGH[wx & 1]]
                   if 0 <= a < CX and 0 <= b < CY):
                n_def_skip += 1
                continue
            interior[wx, wy] = cid
            owner_map[wx, wy] = cid
            if terrain[wx, wy] not in BUILDABLE:
                terrain[wx, wy] = S_SOIL
            got += 1
            n_def_used += 1
        # 2) 不足则用程序化候选补齐（非中国城全部走这一步）
        cands = []
        for dx in range(-R - 1, R + 2):
            nx = x + dx
            if nx < 0 or nx >= CX:
                continue
            for dy in range(-R - 1, R + 2):
                ny = y + dy
                if ny < 0 or ny >= CY or owner_map[nx, ny] or not land[nx, ny]:
                    continue
                # 内政地不能落在 都市(17)/港(18)/关所(19) 上：
                # 它们不是 canBuild 地形，会被下面改成 土(2) 而毁掉一座港/关。
                if terrain[nx, ny] in CITY_TERRAIN:
                    continue
                # 道路(主径/小径/栈道/渡所)上一律不摆内政地：它们是可建造地形里唯一的
                # 交通设施，压上去就把路堵了（旧版 6698 格里有 2380 格压在主径上）。
                if terrain[nx, ny] in ROAD:
                    continue
                d = hex_dist(nx, ny, x, y)
                # 不能离城太近：旧版从 2 格起、紧贴城墙；改成 --interior-min-dist（默认 3）
                if d < args.interior_min_dist or d > R:
                    continue
                nb = [(nx + u, ny + v) for u, v in NEIGH[nx & 1]]
                nb = [(a, b) for a, b in nb if 0 <= a < CX and 0 <= b < CY]
                # 六邻里若已有别城的占地/内政地就跳过：相邻格的顶点块会共享 1 列顶点，
                # 两个城压到各自平台高度时，必有一格变得不平。
                if any(0 < owner_map[a, b] != ci + 1 for a, b in nb):
                    continue
                # 贴着道路的格子也不优先（建筑半径可能盖住路面）
                near_road = 1 if any(terrain[a, b] in ROAD for a, b in nb) else 0
                cands.append((0 if terrain[nx, ny] in BUILDABLE else 1, near_road, d, nx, ny))
        if not cands:
            per_city.append(got)
            continue
        cands.sort()
        arr = np.array([(t[3], t[4]) for t in cands], np.int64)
        qz = arr[:, 0]
        rz = arr[:, 1] - (arr[:, 0] - (arr[:, 0] & 1)) // 2
        dq = qz[:, None] - CQ[None, :]
        dr = rz[:, None] - CR[None, :]
        owner = ((np.abs(dq) + np.abs(dr) + np.abs(dq + dr)) // 2).argmin(1)   # (n, 城数)
        for i, (_b, _nr, _d, nx, ny) in enumerate(cands):
            if got >= want:
                break
            if owner[i] != ci:
                continue
            interior[nx, ny] = cid
            owner_map[nx, ny] = cid
            if terrain[nx, ny] not in BUILDABLE:
                terrain[nx, ny] = S_SOIL       # 与 DefaultMap 一致：内政地是 土(2)
            got += 1
        per_city.append(got)

    inter_mask = interior > 0
    per_city = np.array(per_city, dtype=np.int32)
    print(f"内政地: {int(inter_mask.sum())} 格，{int((per_city > 0).sum())} 座城有内政地；"
          f"每城 min/p50/max = {per_city.min()}/{int(np.median(per_city))}/{per_city.max()}"
          f"（DefaultMap: 2/12/22）", flush=True)
    print(f"  来自 DefaultMap 的中国城: {n_def_used} 格（跳过 {n_def_skip} 格）；"
          f"地形非可建造 {int((inter_mask & ~np.isin(terrain, list(BUILDABLE))).sum())} 格；"
          f"归属城市 id 缺失(0) 的 {int(((interior == 0) & inter_mask).sum())} 格", flush=True)

    # 第 2 项优化的验收：不压在道路上、不离城太近
    road_hit = int((inter_mask & np.isin(terrain, list(ROAD))).sum())
    cxs = np.array([cc["x"] for cc in cities], np.int64)
    cys = np.array([cc["y"] for cc in cities], np.int64)
    _ix2, _iy2 = np.where(inter_mask)      # np.where 返回 (dim0, dim1) = (x, y)
    _oid = np.clip(interior[_ix2, _iy2] - 1, 0, len(cities) - 1)
    q1, r1 = _ix2, _iy2 - (_ix2 - (_ix2 & 1)) // 2
    q2 = cxs[_oid]
    r2 = cys[_oid] - (cxs[_oid] - (cxs[_oid] & 1)) // 2
    dd = (np.abs(q1 - q2) + np.abs(r1 - r2) + np.abs((q1 + r1) - (q2 + r2))) // 2
    print(f"  验收: 压道路 {road_hit} 格；离所属城 格距 min/p50/max = "
          f"{int(dd.min())}/{int(np.median(dd))}/{int(dd.max())}；"
          f"小于 {args.interior_min_dist} 格的 {int((dd < args.interior_min_dist).sum())} 格", flush=True)
    # 放宽到 16 格：DefaultMap 原版的内政格本身就在离城 2~16 格之间
    far = np.where(dd > 16)[0]
    if far.size:
        print(f"  [异常] 离所属城超过 16 格（DefaultMap 原版上限）的有 {far.size} 格，样例：", flush=True)
        for w in far[:6]:
            a, b = int(_ix2[w]), int(_iy2[w])
            o = int(interior[a, b]) - 1
            print(f"      内政格({a},{b}) 归属 id={o + 1}({cities[o].get('name')}@{cxs[o]},{cys[o]})"
                  f" 格距={int(dd[w])} 来自默认图={'是' if o + 1 in from_def else '否'}", flush=True)

    # 每城内政格清单（后面按城压平/陆地化要按归属取）
    city_interior = {}
    _iy, _ix = np.where(inter_mask)
    for _a, _b in zip(_iy.tolist(), _ix.tolist()):
        city_interior.setdefault(int(interior[_a, _b]), []).append((_a, _b))

    # ================================================ 3) 可通行格高度平滑
    moveable = np.isin(terrain, list(MOVEABLE))          # 内政地/城市格改地形后重算
    water = np.isin(terrain, list(WATER))                # 海/河/浅滩：可通行，但海床不能动
    print(f"可通行格 {int(moveable.sum())} 格（{moveable.mean():.3f}），"
          f"其中陆地 {int((moveable & ~water).sum())} 格", flush=True)

    par = parity(CX, CY)

    # 城市平台：7 格压成同一高度，作为硬边界把周边拉平
    for c in cities:
        x, y = c["x"], c["y"]
        if not (0 <= x < CX and 0 <= y < CY):
            continue
        b = city_base[(x, y)]
        hc[x, y] = b
        for dx, dy in NEIGH[x & 1]:
            nx, ny = x + dx, y + dy
            if 0 <= nx < CX and 0 <= ny < CY:
                hc[nx, ny] = b

    # 内政地：与所属城的平台同高 —— 城市 + 内政地是一整片平地，
    # 而不是每块内政地各按自己的局部地形平滑。
    hc = np.where(inter_mask, city_base_arr[np.where(inter_mask, interior, 0)], hc).astype(np.int32)

    before = hc.copy()
    o0, e0 = count_over(hc, moveable, par, args.max_step)

    # 只有「陆地可通行格」会移动：海/河保持海床高度当锚点，
    # 城市格与内政地是硬边界。这样海岸自然形成缓坡，也不会把海床抬起来
    # （抬高会顶穿水面，水面就消失了）。
    pinned = city_mask | inter_mask | water
    hc = relax_slope(hc, moveable, args.max_step, args.iters, pinned=pinned)

    o1, e1 = count_over(hc, moveable, par, args.max_step)
    print(f"平滑: 相邻可通行格超 {args.max_step} 字节的边 {o0}/{e0} ({o0 / max(1, e0):.3%})"
          f" -> {o1}/{e1} ({o1 / max(1, e1):.4%})", flush=True)
    print(f"  格高度改动: 变化格 {int((hc != before).sum())}，"
          f"平均 |Δ| {np.abs(hc - before).mean():.2f} 字节，最大 {int(np.abs(hc - before).max())}", flush=True)

    # ================================================ 写回顶点
    new_vh = compose_vertices(vh, hc, moveable, radius=2)

    # 城市 + 内政地：每格的顶点块都压成所属城的平台高度，并清掉顶点水面（陆地化）。
    # 「中心 + 周围一圈」共 7 格必须逐格压 —— 只压中心格的话外围一圈仍是原高度。
    n_district = 0
    for ci, c in enumerate(cities):
        x, y = c["x"], c["y"]
        if not (0 <= x < CX and 0 <= y < CY):
            continue
        b = city_base[(x, y)]
        cells = [(x, y)] + [(x + dx, y + dy) for dx, dy in NEIGH[x & 1]]
        cells += city_interior.get(ci + 1, [])
        for cx_, cy_ in cells:
            if not (0 <= cx_ < CX and 0 <= cy_ < CY):
                continue
            n_district += 1
            vxs = np.clip(np.arange(4 * cx_, 4 * cx_ + 5), 0, VX - 1)
            vys = np.clip(np.arange(4 * cy_ + 2 * (cx_ & 1),
                                    4 * cy_ + 2 * (cx_ & 1) + 5), 0, VY - 1)
            new_vh[np.ix_(vxs, vys)] = b
            vw[np.ix_(vxs, vys)] = 0
    print(f"压平+陆地化: 城市 7 格 + 内政地，共 {n_district} 个顶点块", flush=True)

    changed_v = int((new_vh != vh).sum())
    print(f"顶点: 改动 {changed_v} / {vh.size} ({changed_v / vh.size:.3%})；"
          f"高度 mean {vh.mean():.1f} -> {new_vh.mean():.1f}", flush=True)
    print(f"地形: 都市(17) {city0} -> {int((terrain == S_CITY).sum())} 格", flush=True)

    # 复检：内政地必须「平整 + 陆地 + 有归属城市」
    flat_bad = water_bad = 0
    for ci, c in enumerate(cities):
        b = city_base.get((c["x"], c["y"]))
        if b is None:
            continue
        for a, e in city_interior.get(ci + 1, []):
            vs = np.clip(np.arange(4 * a, 4 * a + 5), 0, VX - 1)
            ws = np.clip(np.arange(4 * e + 2 * (a & 1), 4 * e + 2 * (a & 1) + 5), 0, VY - 1)
            blk = new_vh[np.ix_(vs, ws)]
            if blk.min() != b or blk.max() != b:
                flat_bad += 1
            if vw[np.ix_(vs, ws)].max() != 0:
                water_bad += 1
    print(f"内政地复检: 顶点不平 {flat_bad} 格，仍含水 {water_bad} 格，"
          f"地形非可建造 {int((inter_mask & ~np.isin(terrain, list(BUILDABLE))).sum())} 格，"
          f"归属城市 id 为 0 的 {int(((interior == 0) & inter_mask).sum())} 格", flush=True)

    if args.report:
        print("--report：未写回任何文件", flush=True)
        return

    # 首次运行留一份原始副本，便于回退/对比
    for name, arr in (("terrain_orig.npy", np.load(src / "terrain.npy")),
                      ("vheight_orig.npy", vh),
                      ("vwater_orig.npy", np.load(src / "vwater.npy"))):
        bak = src / name
        if not bak.exists():
            np.save(bak, arr)
            print(f"已备份 {bak.name}", flush=True)

    np.save(src / "terrain.npy", terrain)
    np.save(src / "vheight.npy", new_vh)
    np.save(src / "vwater.npy", vw)
    # uint16：0 = 非内政地，>0 = 所属城市 id。build_bin 用它同时写
    # terrainState 的内政位和强制 areaId，保证内政格一定有归属城市。
    np.save(src / "interior.npy", interior.astype(np.uint16))
    (src / "terrain.bin").write_bytes(terrain.tobytes())
    (src / "vheight.bin").write_bytes(new_vh.tobytes())
    (src / "vwater.bin").write_bytes(vw.tobytes())
    print(f"已写回 {src}/ terrain.npy vheight.npy vwater.npy + interior.npy", flush=True)


if __name__ == "__main__":
    main()
