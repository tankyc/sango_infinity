# -*- coding: utf-8 -*-
"""sango_map_repair.py —— 地图数据修补（只做可回退的数据操作）。

已实现两项：
  1) 崖(等不可建造地形)上的内政位
     规则（按用户指定）：
       先做孤岛检测 —— 该城是否在主连通块上（陆地+水域口径）；
       再看该城是否已有其它内政地；
         · 不在孤岛  且  该城没有其它内政地  -> 归还：把该格地形改成 土(2)（可建造），保留内政位
         · 其它情况                          -> 取消该格的内政位数据（terrainState 去掉 bit2）
  2) 清理植被模型：删除 mapModels 里 objType=6（植）的记录

用法：
    python sango_map_repair.py --bin <地图.bin> --scenario <剧本.json> --report
    python sango_map_repair.py --bin <地图.bin> --scenario <剧本.json> --apply
    （加 --only-plants / --only-interior 可只做其中一项）
"""
import argparse
import collections
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin, Reader, Writer   # noqa: E402

import numpy as np

NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}
INTERIOR_BIT = 4
T_SOIL = 2
OBJ_TYPE_PLANT = 6


def load_terrains(path):
    raw = json.loads(Path(path).read_text(encoding="utf-8"))
    return {int(k): v for k, v in raw.get("TerrainTypes", raw).items()
            if str(k).isdigit() and isinstance(v, dict)}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--scenario", required=True)
    ap.add_argument("--terrain-types",
                    default=r"D:\sango_infinity\Build\Content\Data\Common\TerrainTypes.json")
    ap.add_argument("--only-interior", action="store_true")
    ap.add_argument("--only-plants", action="store_true")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    p = Path(args.bin_path)
    d = parse_bin(str(p))
    cx, cy = d["cellsX"], d["cellsY"]
    grid = d["gridRaw"].copy()
    tt = grid[..., 0].astype(np.int32)
    st = (grid[..., 1].astype(np.uint32) | (grid[..., 2].astype(np.uint32) << 8)
          | (grid[..., 3].astype(np.uint32) << 16) | (grid[..., 4].astype(np.uint32) << 24))
    aid = grid[..., 5].astype(np.int32) | (grid[..., 6].astype(np.int32) << 8)
    G = float(d["gridSize"])
    QUAD = float(d["quadSize"])

    T = load_terrains(args.terrain_types)
    moveable = np.zeros(tt.shape, bool)
    water = np.zeros(tt.shape, bool)
    buildable = np.zeros(tt.shape, bool)
    for k, v in T.items():
        m = tt == k
        if v.get("moveable"):
            moveable |= m
        if v.get("isWater"):
            water |= m
        if v.get("canBuild"):
            buildable |= m

    print("=" * 78)
    print(f"修补: {p}")
    print(f"  格 {cx}x{cy}  work={d['workContent']!r}  mapModels {len(d['tail']['models'][2])} 条")
    print("=" * 78)

    # ---------------- 主连通块（陆地+水域，和引擎里城市能否互相到达同口径） ----------------
    mask = moveable
    lab = np.full(mask.shape, -1, np.int32)
    sizes = []
    cur = 0
    for x0 in range(cx):
        for y0 in range(cy):
            if not mask[x0, y0] or lab[x0, y0] >= 0:
                continue
            stack = [(x0, y0)]
            lab[x0, y0] = cur
            n = 0
            while stack:
                x, y = stack.pop()
                n += 1
                for dx, dy in NEIGH[x & 1]:
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < cx and 0 <= ny < cy and mask[nx, ny] and lab[nx, ny] < 0:
                        lab[nx, ny] = cur
                        stack.append((nx, ny))
            sizes.append((n, cur))
            cur += 1
    sizes.sort(reverse=True)
    main_lab = sizes[0][1] if sizes else -1
    print(f"  可通行连通块 {len(sizes)} 个，最大 {sizes[0][0] if sizes else 0} 格")
    print(f"  注意：崖上的内政位本身不可通行，孤岛检测用「它所属城池是否在主连通块上」来判断")

    sc = json.loads(Path(args.scenario).read_text(encoding="utf-8"))
    cities = {int(v["Id"]): v for v in sc["citySet"].values() if "Id" in v and "x" in v}

    im = (st & INTERIOR_BIT) != 0
    changes = []

    if not args.only_plants:
        print("\n[1] 崖（不可建造地形）上的内政位")
        bad = im & ~buildable
        n_bad = int(bad.sum())
        print(f"  需要处理的格 {n_bad}")
        pts = list(zip(*np.where(bad)))
        # 每城的「其它内政地」数量
        own_interior = collections.Counter(aid[im].tolist())
        for x, y in pts:
            cid = int(aid[x, y])
            c = cities.get(cid)
            cname = c.get("Name") if c else "?"
            # 其它内政地（不含自己）
            other = own_interior.get(cid, 0) - 1
            # 孤岛检测：城池所在格是否在主连通块
            on_main = False
            if c:
                ccx, ccy = int(c["x"]), int(c["y"])
                if 0 <= ccx < cx and 0 <= ccy < cy:
                    on_main = bool(mask[ccx, ccy] and lab[ccx, ccy] == main_lab)
            terrain_name = T.get(int(tt[x, y]), {}).get("Name", "?")
            if on_main and other <= 0:
                changes.append(("give_back", x, y, cid, cname, terrain_name, other))
                print(f"  格({x},{y}) 城{cid} {cname} 地形{terrain_name} —— 非孤岛且该城无其它内政地"
                      f" -> 归还：地形改 土({T_SOIL})")
            else:
                why = "处于孤岛" if not on_main else f"该城已有 {other} 个内政地"
                changes.append(("remove_bit", x, y, cid, cname, terrain_name, other))
                print(f"  格({x},{y}) 城{cid} {cname} 地形{terrain_name} —— {why}"
                      f" -> 取消内政位数据")

    # ---------------- 植被清理 ----------------
    ms = d["tail"]["models"][2]
    plants = [m for m in ms if m[1] == OBJ_TYPE_PLANT]
    keep = [m for m in ms if m[1] != OBJ_TYPE_PLANT]
    if not args.only_interior:
        print(f"\n[2] 清理植被模型：删除 objType={OBJ_TYPE_PLANT} 共 {len(plants)} 条，"
              f"保留其它 {len(keep)} 条")

    # ---------------- A1 领地空归属 ----------------
    print("\n[3] 可行走但无归属的格")
    a1_fix = []
    for x, y in zip(*np.where(moveable & (aid == 0))):
        cnt = collections.Counter()
        for dx, dy in NEIGH[int(x) & 1]:
            nx, ny = int(x) + dx, int(y) + dy
            if 0 <= nx < cx and 0 <= ny < cy and aid[nx, ny] > 0:
                cnt[int(aid[nx, ny])] += 1
        if cnt:
            pick = cnt.most_common(1)[0][0]
            a1_fix.append((int(x), int(y), pick))
            print(f"  格({x},{y}) 邻居归属 {dict(cnt)} -> 取最多的城 {pick}"
                  f"（{cities.get(pick, {}).get('Name', '?')}）")
        else:
            print(f"  格({x},{y}) 邻居也没有归属，跳过")
    if not a1_fix:
        print("  无需处理")

    # ---------------- A3 陆地高度为 0 ----------------
    VS = int(G // QUAD)
    xs2 = np.arange(cx, dtype=np.int64)[:, None]
    ys2 = np.arange(cy, dtype=np.int64)[None, :]
    vx_all = np.clip(VS * xs2 + VS // 2, 0, d["verts"].shape[0] - 1)
    vy_all = np.clip(VS * ys2 + (xs2 & 1) * (VS // 2) + VS // 2, 0, d["verts"].shape[1] - 1)
    hc = d["verts"][vx_all, vy_all, 0].astype(np.int32)
    print("\n[4] 陆地可通行格高度为 0")
    a3_fix = []
    for x, y in zip(*np.where((~water) & moveable & (hc == 0))):
        nb = []
        for dx, dy in NEIGH[int(x) & 1]:
            nx, ny = int(x) + dx, int(y) + dy
            if 0 <= nx < cx and 0 <= ny < cy and hc[nx, ny] > 0:
                nb.append(int(hc[nx, ny]))
        if nb:
            h = int(np.median(nb))
            a3_fix.append((int(x), int(y), h))
            print(f"  格({x},{y}) 邻格高度中位数 {h} -> 抬高到 {h}")
        else:
            print(f"  格({x},{y}) 邻格也为 0，跳过")
    if not a3_fix:
        print("  无需处理")

    # ---------------- A4 据点落点地形 ----------------
    print("\n[5] 据点落点地形")
    a4_fix = []
    for cid, c in cities.items():
        x, y = int(c["x"]), int(c["y"])
        if not (0 <= x < cx and 0 <= y < cy):
            continue
        bt = c.get("BuildingType", 1)
        t = int(tt[x, y])
        # 城/关 -> 都市17（本图惯例：321 个关大多坐落在都市格）；港 -> 港18（渡所13 亦可）
        want = 17 if bt in (1, 2) else 18
        ok = (t == want) or (bt == 2 and t == 19) or (bt == 3 and t == 13)
        if not ok:
            a4_fix.append((x, y, t, want, cid, c.get("Name")))
            print(f"  据点 {cid} {c.get('Name')} (类型{bt}) 格({x},{y}) "
                  f"{T.get(t, {}).get('Name', t)} -> 改 {T.get(want, {}).get('Name', want)}")
    if not a4_fix:
        print("  无需处理")

    if args.report or not args.apply:
        print("\n--report/未加 --apply：未写盘。")
        return

    # ---------------- 写盘 ----------------
    # 定位各段偏移（version>=6：header -> mapGrid -> 贴图名 -> quadSize -> 顶点 -> 尾部）
    r = Reader(d["buf"])
    r.i32()                       # version
    r.string()                    # WorkContent
    r.i32(); r.i32()              # mapWidth/mapHeight
    r.i32()                       # gridSize
    grid_start = r.o
    grid_end = grid_start + cx * cy * 7
    r.o = grid_end
    r.string()                    # gridTextureName
    vert_start = r.o + 4          # 跳过 quadSize
    vert_end = vert_start + d["verts"].size
    tail_start = vert_end

    new_grid = grid.copy()
    for kind, x, y, cid, cname, tname, other in changes:
        if kind == "give_back":
            new_grid[x, y, 0] = T_SOIL                 # 地形改土，内政位保留
        else:
            v = int(new_grid[x, y, 1]) | (int(new_grid[x, y, 2]) << 8) \
                | (int(new_grid[x, y, 3]) << 16) | (int(new_grid[x, y, 4]) << 24)
            v &= ~INTERIOR_BIT
            new_grid[x, y, 1] = v & 0xFF
            new_grid[x, y, 2] = (v >> 8) & 0xFF
            new_grid[x, y, 3] = (v >> 16) & 0xFF
            new_grid[x, y, 4] = (v >> 24) & 0xFF
    # A1 领地空归属
    for (x, y, pick) in a1_fix:
        new_grid[x, y, 5] = pick & 0xFF
        new_grid[x, y, 6] = (pick >> 8) & 0xFF
    # A4 据点落点地形
    for (x, y, t_old, t_new, cid, nm) in a4_fix:
        new_grid[x, y, 0] = t_new

    # A3 高度：连同周边 3x3 顶点一起抬，避免出现单顶点尖刺
    new_verts = d["verts"].copy()
    for (x, y, h) in a3_fix:
        ax = VS * x + VS // 2
        ay = VS * y + (x & 1) * (VS // 2) + VS // 2
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                ia = min(max(ax + dx, 0), new_verts.shape[0] - 1)
                ib = min(max(ay + dy, 0), new_verts.shape[1] - 1)
                new_verts[ia, ib, 0] = h

    w = Writer()
    w.i32(len(keep))
    for (oid, ot, bid, mid, pos, rot, scl) in keep:
        w.i32(oid); w.i32(ot); w.i32(bid); w.i32(mid)
        for v in pos:
            w.f32(v)
        for v in rot:
            w.f32(v)
        for v in scl:
            w.f32(v)
    models_blob = w.bytes()
    m_start, m_end = d["tail"]["models"][0], d["tail"]["models"][1]

    data = (d["buf"][:grid_start] + new_grid.tobytes()
            + d["buf"][grid_end:vert_start] + new_verts.tobytes()
            + d["buf"][vert_end:tail_start]
            + d["buf"][tail_start:m_start] + models_blob + d["buf"][m_end:])
    bak = p.with_suffix(p.suffix + ".bak_repair")
    if not bak.exists():
        bak.write_bytes(d["buf"])
        print(f"\n已备份 -> {bak.name}")
    p.write_bytes(data)
    print(f"已写出 {p}  {len(data)} 字节（原 {len(d['buf'])}，{len(data) - len(d['buf']):+d}）")

    # 复核
    d2 = parse_bin(str(p))
    g2 = d2["gridRaw"]
    t2 = g2[..., 0].astype(np.int32)
    s2 = (g2[..., 1].astype(np.uint32) | (g2[..., 2].astype(np.uint32) << 8)
          | (g2[..., 3].astype(np.uint32) << 16) | (g2[..., 4].astype(np.uint32) << 24))
    print(f"复核: 内政位 {int(((s2 & INTERIOR_BIT) != 0).sum())} 格（原 {int(im.sum())}）")
    print(f"      其中不可建造地形的 {int((((s2 & INTERIOR_BIT) != 0) & ~np.isin(t2, [k for k, v in T.items() if v.get('canBuild')])).sum())} 格")
    print(f"      mapModels {len(d2['tail']['models'][2])} 条  格 {d2['cellsX']}x{d2['cellsY']}")
    print(f"      归还(改土) {sum(1 for c in changes if c[0] == 'give_back')} 格，"
          f"取消内政位 {sum(1 for c in changes if c[0] == 'remove_bit')} 格")


if __name__ == "__main__":
    main()
