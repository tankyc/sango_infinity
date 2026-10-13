# -*- coding: utf-8 -*-
"""sango_map_check.py —— 地图 .bin 的数据体检查错器。

检查项（按严重程度分级 FAIL / WARN / INFO）：
  1  地形 id 合法性（必须在 TerrainTypes.json 里，否则引擎 TerrainTypes.Get 返回 null）
  2  可行走格无 areaId（引擎会报「无归属」）
  3  areaId 越界 / 与剧本据点对不上
  4  据点落点：坐标格的地形、地形归属、高度（城应为都市/关所/港，且在陆地）
  5  都市格数与据点数量是否自洽（每城 N 格足迹）
  6  内政位（terrainState=4）：地形必须可建造、必须归属城市
  7  连通性：仅陆地 / 含水域两种口径下，据点是否连通、孤立簇多大
  8  高度：陆地格高度为 0、水面格高度异常等
  9  水陆结构：岸/川 是否与水域相邻、1 格孤岛
 10  物件/标注：objType、modelId 合法性、落点地形
 11  资源完整性：Assets/Map/<workContent>/ 下的底图与季节图层是否存在

用法：
    python sango_map_check.py --bin <地图.bin> [--scenario <剧本.json>] [--assets <mod根目录>]
"""
import argparse
import collections
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np

NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}

SEASONS = ("Autumn", "Spring", "Summer", "Winter")   # MapRender.SeasonNames 顺序

FAIL, WARN, INFO = [], [], []


def f(msg):
    FAIL.append(msg)
    print(f"  [FAIL] {msg}")


def w(msg):
    WARN.append(msg)
    print(f"  [WARN] {msg}")


def i(msg):
    INFO.append(msg)
    print(f"  [info] {msg}")


def load_terrains(path):
    raw = json.loads(Path(path).read_text(encoding="utf-8"))
    data = raw.get("TerrainTypes", raw)
    out = {}
    for k, v in data.items():
        if str(k).isdigit() and isinstance(v, dict):
            out[int(k)] = v
    return out


def neighbors_np(mask, x, y):
    for dx, dy in NEIGH[x & 1]:
        nx, ny = x + dx, y + dy
        if 0 <= nx < mask.shape[0] and 0 <= ny < mask.shape[1]:
            yield nx, ny


def components(mask):
    """返回每个连通块的 (size, 代表点) 列表（六边形邻接）。"""
    seen = np.zeros(mask.shape, bool)
    out = []
    H, W = mask.shape
    for x0 in range(H):
        for y0 in range(W):
            if not mask[x0, y0] or seen[x0, y0]:
                continue
            stack = [(x0, y0)]
            seen[x0, y0] = True
            n = 0
            rep = (x0, y0)
            while stack:
                x, y = stack.pop()
                n += 1
                for nx, ny in neighbors_np(mask, x, y):
                    if mask[nx, ny] and not seen[nx, ny]:
                        seen[nx, ny] = True
                        stack.append((nx, ny))
            out.append((n, rep))
    out.sort(reverse=True)
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--scenario", default=None)
    ap.add_argument("--terrain-types", default=r"D:\sango_infinity\Build\Content\Data\Common\TerrainTypes.json")
    ap.add_argument("--assets", default=None, help="mod 根目录（含 Assets/Map/<wc>/）")
    args = ap.parse_args()

    p = Path(args.bin_path)
    print("=" * 78)
    print(f"地图体检: {p}")
    print("=" * 78)
    d = parse_bin(str(p))
    cx, cy = d["cellsX"], d["cellsY"]
    tt = d["gridRaw"][..., 0].astype(np.int32)
    st = (d["gridRaw"][..., 1].astype(np.uint32) | (d["gridRaw"][..., 2].astype(np.uint32) << 8)
          | (d["gridRaw"][..., 3].astype(np.uint32) << 16) | (d["gridRaw"][..., 4].astype(np.uint32) << 24))
    aid = d["gridRaw"][..., 5].astype(np.int32) | (d["gridRaw"][..., 6].astype(np.int32) << 8)
    verts = d["verts"]
    G, QUAD = float(d["gridSize"]), float(d["quadSize"])
    VS = int(G // QUAD)
    print(f"  格 {cx}x{cy}  work={d['workContent']!r}  世界 {d['mapWidth'] * d['quadSize']}"
          f" x {d['mapHeight'] * d['quadSize']}  顶点 {verts.shape[0]}x{verts.shape[1]}")

    T = load_terrains(args.terrain_types)
    moveable = np.zeros(tt.shape, bool)
    water = np.zeros(tt.shape, bool)
    for k, v in T.items():
        m = tt == k
        if v.get("moveable"):
            moveable |= m
        if v.get("isWater"):
            water |= m

    # 逐格高度
    xs = np.arange(cx, dtype=np.int64)[:, None]
    ys = np.arange(cy, dtype=np.int64)[None, :]
    vx = np.clip(VS * xs + VS // 2, 0, verts.shape[0] - 1)
    vy = np.clip(VS * ys + (xs & 1) * (VS // 2) + VS // 2, 0, verts.shape[1] - 1)
    hc = verts[vx, vy, 0].astype(np.int32)

    # ---------- 1 地形 id 合法性 ----------
    print("\n[1] 地形 id 合法性")
    bad = [int(t) for t in np.unique(tt) if int(t) not in T]
    if bad:
        for t in bad:
            cnt = int((tt == t).sum())
            yy, xx = np.where(tt == t)
            bd = (xx.min(), xx.max(), yy.min(), yy.max())
            f(f"地形 id {t} 不在 TerrainTypes.json 里：{cnt} 格（{cnt / tt.size:.2%}）"
              f"，范围 x{bd[0]}..{bd[1]} y{bd[2]}..{bd[3]}")
    else:
        i("全部地形 id 均在表中")

    # ---------- 2/3 领地 ----------
    print("\n[2] 可行走格归属")
    m = moveable & (aid == 0)
    if m.any():
        n = int(m.sum())
        pts = np.where(m)
        f(f"可行走但 areaId=0 的格 {n} 个，例如 "
          f"{[(int(a), int(b)) for a, b in list(zip(*pts))[:6]]}")
    else:
        i("可行走格全部有 areaId")

    print("\n[3] areaId 与剧本对照")
    city_by_id = {}
    if args.scenario and Path(args.scenario).exists():
        sc = json.loads(Path(args.scenario).read_text(encoding="utf-8"))
        city_by_id = {int(v["Id"]): v for v in sc["citySet"].values() if "Id" in v}
        ids = set(city_by_id)
        used = set(int(x) for x in np.unique(aid) if x)
        extra = sorted(used - ids)
        i(f"剧本据点 {len(ids)} 个，地图上出现的 areaId {len(used)} 个")
        if extra:
            f(f"地图上出现剧本里没有的 areaId: {extra[:12]}")
        unused = sorted(ids - used)
        if unused:
            w(f"剧本里有但地图上没有领地的据点 {len(unused)} 个: {unused[:12]}")
    else:
        w("未指定 --scenario，跳过据点对照")

    # ---------- 4/5 据点落点 ----------
    if city_by_id:
        print("\n[4] 据点落点校验")
        # 各地图惯例不同（本图 321 个关里大部分坐落在都市格上），所以允许集合而非单一值：
        #   城(1) 应在 都市17；关(2) 允许 关所19 或 都市17；港(3) 允许 港18 或 渡所13
        expect = {1: {17}, 2: {19, 17}, 3: {18, 13}}
        wrong_terrain, no_area, on_water, oob = [], [], [], []
        for cid, c in city_by_id.items():
            x, y = c.get("x"), c.get("y")
            if x is None or y is None:
                continue
            x, y = int(x), int(y)
            if not (0 <= x < cx and 0 <= y < cy):
                oob.append((cid, c.get("Name"), x, y))
                continue
            bt = c.get("BuildingType", 1)
            t = int(tt[x, y])
            if bt in expect and t not in expect[bt]:
                wrong_terrain.append((cid, c.get("Name"), bt, t, x, y))
            # 只有城池要求「所在格属于自己」；关/港常挂母城名下（areaId 指向母城是正常的）
            if bt == 1 and aid[x, y] != cid:
                no_area.append((cid, c.get("Name"), int(aid[x, y]), x, y))
            if water[x, y]:
                on_water.append((cid, c.get("Name"), x, y))
        for cid, nm, bt, t, x, y in wrong_terrain[:10]:
            want = "/".join(str(v) for v in sorted(expect[bt]))
            f(f"据点 {cid} {nm} (BuildingType={bt}) 落在 {t}({T.get(t, {}).get('Name', '?')})"
              f" 而非 {want}，格({x},{y})")
        if len(wrong_terrain) > 10:
            f(f"... 落点地形不符共 {len(wrong_terrain)} 个")
        if not wrong_terrain:
            i("所有据点落点地形在其允许集合内")
        for cid, nm, a2, x, y in no_area[:8]:
            f(f"城池 {cid} {nm} 所在格 areaId={a2}（应为 {cid}），格({x},{y})")
        if len(no_area) > 8:
            f(f"... 城池归属不符共 {len(no_area)} 个")
        if not no_area:
            i("所有城池所在格归属正确")
        if on_water:
            w(f"据点落在水面格 {len(on_water)} 个: {[(a, b) for a, b, _, _ in on_water[:6]]}")
        if oob:
            f(f"据点坐标越界 {len(oob)} 个: {oob[:6]}")

        print("\n[5] 都市/关所/港 格数自洽")
        for t, name in ((17, "都市"), (19, "关所"), (18, "港")):
            n = int((tt == t).sum())
            want = sum(1 for c in city_by_id.values() if c.get("BuildingType") == {17: 1, 19: 2, 18: 3}[t])
            ratio = n / want if want else 0
            i(f"{name}({t}): {n} 格 / {want} 个据点 = {ratio:.2f} 格/据点"
              + ("   ← 不是整数比，可能有重叠或缺口" if abs(ratio - round(ratio)) > 0.01 else ""))

    # ---------- 6 内政地 ----------
    print("\n[6] 内政位")
    im = (st & 4) != 0
    ni = int(im.sum())
    i(f"内政位 {ni} 格")
    if ni:
        buildable = np.zeros(tt.shape, bool)
        for k, v in T.items():
            if v.get("canBuild"):
                buildable |= tt == k
        bad_t = im & ~buildable
        if bad_t.any():
            cnt = int(bad_t.sum())
            dist = collections.Counter(tt[bad_t].tolist())
            f(f"内政位中地形不可建造的 {cnt} 格，地形分布 "
              f"{ {T.get(k, {}).get('Name', k): v for k, v in dist.items()} }")
        else:
            i("内政位地形全部可建造")
        no_own = im & (aid == 0)
        if no_own.any():
            f(f"内政位中无归属的 {int(no_own.sum())} 格")
        else:
            i("内政位全部有归属")
        per = collections.Counter(aid[im].tolist())
        vals = sorted(per.values())
        i(f"有内政位的城市 {len(per)} 个，每城格数 min/p50/max = "
          f"{vals[0]}/{vals[len(vals) // 2]}/{vals[-1]}")
        other = (st != 0) & ~im
        if other.any():
            w(f"terrainState 非 0 且非内政位的格 {int(other.sum())}")

    # ---------- 7 连通性 ----------
    if city_by_id:
        print("\n[7] 据点连通性")
        land = moveable & ~water
        for label, mask in (("仅陆地(不含海/河)", land), ("陆地+水域", moveable)):
            comps = components(mask)
            big = comps[0] if comps else (0, None)
            i(f"{label}: 连通块 {len(comps)} 个，最大 {big[0]} 格（占可通行 "
              f"{big[0] / max(1, int(mask.sum())):.1%}）")
            if len(comps) > 1 and big[0] < 0.98 * int(mask.sum()):
                w(f"{label} 下存在规模可观的孤立区域: "
                  f"{[(n, rep) for n, rep in comps[:5]]}")
            # 据点落在哪个块
            lab = np.full(mask.shape, -1, np.int32)
            cur = 0
            for x0 in range(cx):
                for y0 in range(cy):
                    if not mask[x0, y0] or lab[x0, y0] >= 0:
                        continue
                    stack = [(x0, y0)]
                    lab[x0, y0] = cur
                    while stack:
                        x, y = stack.pop()
                        for nx, ny in neighbors_np(mask, x, y):
                            if mask[nx, ny] and lab[nx, ny] < 0:
                                lab[nx, ny] = cur
                                stack.append((nx, ny))
                    cur += 1
            iso = []
            for cid, c in city_by_id.items():
                x, y = int(c["x"]), int(c["y"])
                if 0 <= x < cx and 0 <= y < cy and lab[x, y] != (
                        lab[big[1][0], big[1][1]] if big[1] else -1):
                    iso.append((cid, c.get("Name"), int(lab[x, y])))
            if iso:
                w(f"{label} 下不在最大连通块上的据点 {len(iso)} 个，例如 {iso[:8]}")
            else:
                i(f"{label} 下所有据点都在最大连通块上")

    # ---------- 8 高度 ----------
    print("\n[8] 高度合理性")
    land_h0 = (~water) & moveable & (hc == 0)
    if land_h0.any():
        w(f"陆地可通行格高度为 0 的 {int(land_h0.sum())} 格")
    else:
        i("陆地可通行格高度均 > 0")
    zw = water & (hc > 0)
    i(f"水面格高度 >0 的 {int(zw.sum())} 格（水面高度用于水位线，非错误）")
    i(f"高度字节 min/max/p50 = {int(verts[..., 0].min())}/{int(verts[..., 0].max())}/"
      f"{int(np.median(verts[..., 0]))}")

    # ---------- 9 水陆结构 ----------
    print("\n[9] 水陆结构")
    if water.any():
        water_comp = components(water)
        i(f"水域连通块 {len(water_comp)} 个，最大 {water_comp[0][0]} 格")
        small = [c for c in water_comp if c[0] <= 3]
        if small:
            w(f"≤3 格的小水塘/孤岛水 {len(small)} 处，例如 {[(n, r) for n, r in small[:6]]}")
    t7 = tt == 7
    if t7.any():
        c7 = components(t7)
        i(f"川(7) 连通块 {len(c7)} 个（应≈河流条数），最大 {c7[0][0]} 格")

    # ---------- 10 物件/标注 ----------
    print("\n[10] 物件与标注")
    ms = d["tail"]["models"][2]
    ot = collections.Counter(m[1] for m in ms)
    mid = collections.Counter(m[3] for m in ms)
    i(f"物件 {len(ms)} 条  objType {dict(ot)}  modelId {dict(sorted(mid.items()))}")
    bad_id = [m[3] for m in ms if not (1 <= m[3] <= MAX_MODEL_ID)]
    if bad_id:
        f(f"modelId 超出 1..{MAX_MODEL_ID}（MapModels.modelInstanceDatas 长度）的物件 "
          f"{len(bad_id)} 条: {sorted(set(bad_id))}")
    else:
        i(f"所有 modelId 都在 1..{MAX_MODEL_ID} 内（引擎实例化数组上限）")
    off = 0
    for m in ms:
        wx, _wy, wz = m[4]
        col = int((wz - G * 0.5) / G)
        row = int((wx - G * 0.5 - (col & 1) * G * 0.5) / G)
        if not (0 <= col < cx and 0 <= row < cy) or not moveable[col, row]:
            off += 1
    if off:
        # 植被长在山坡/崖上属正常（物件只是装饰，引擎不要求落在可通行格）
        i(f"物件落点不在可行走格上的 {off} / {len(ms)} 条（多为山坡/崖上的植被，正常）")
    else:
        i("所有物件落点都在可行走格上")
    lb = d["tail"]["labels"][2]
    i(f"标注 {len(lb)} 条")

    # ---------- 11 资源完整性 ----------
    print("\n[11] 资源完整性")
    root = Path(args.assets) if args.assets else p.parent.parent
    wc = d["workContent"]
    adir = root / "Assets" / "Map" / wc
    if not adir.is_dir():
        w(f"资源目录不存在: {adir}")
    else:
        i(f"资源目录: {adir}")
        mb = adir / "map_bounds.jpg"
        i(f"map_bounds.jpg {'存在' if mb.exists() else '**缺失**'}")
        bt = adir / "BaseTex"
        miss_base = [f"BaseMap{n}.png" for n in range(4) if not (bt / f"BaseMap{n}.png").exists()]
        if miss_base:
            w(f"BaseTex 缺失: {miss_base}")
        else:
            i("BaseTex 四季底图齐全（BaseMap0..3.png）")
        # 图层：从 .bin 的 mapLayer 段取贴图名，与季节目录对照
        s, e = d["tail"]["layer"]
        from sango_scale_bin import Reader
        r = Reader(d["buf"]); r.o = s
        lc = r.i32()
        names = set()
        for _ in range(lc):
            r.u8(); r.f32(); r.f32()
            for _j in range(4):
                for _k in range(3):
                    nm = r.string()
                    if nm:
                        names.add(nm)
        # 引擎按搜索路径回落：mod 的 Terrain 找不到时会有基础内容的 Default 兜底，
        # 所以判缺图必须两处都查，否则会把「靠 Default 提供」的层误报成缺失。
        base = Path(args.terrain_types).parent.parent.parent / "Assets" / "Map" / "Default" / "Terrain"
        miss_by_season = {}
        for season in SEASONS:
            sd = adir / "Terrain" / season
            bd = base / season
            miss = sorted(nm for nm in names
                          if not (sd / f"{nm}.png").exists() and not (sd / nm).exists()
                          and not (bd / f"{nm}.png").exists() and not (bd / nm).exists())
            miss_by_season[season] = miss
        i(f"mapLayer 引用 {lc} 层、{len(names)} 个不同贴图名")
        for season, miss in miss_by_season.items():
            if miss:
                w(f"{season} 季缺失图层 {len(miss)} 个（前 8: {miss[:8]}）")
            else:
                i(f"{season} 季图层齐全")

    # 天空盒
    s, e = d["tail"]["skyBox"]
    from sango_scale_bin import Reader as R2
    r = R2(d["buf"]); r.o = s
    r.f32(); r.f32()
    ac = r.i32()
    rects = []
    for _ in range(ac):
        rects.append((r.f32(), r.f32(), r.f32(), r.f32()))
        for _j in range(4):
            r.string()
    if rects:
        wz, wx = d["mapWidth"] * d["quadSize"], d["mapHeight"] * d["quadSize"]
        cz = max(a[0] for a in rects) + rects[0][2]
        cxn = max(a[1] for a in rects) + rects[0][3]
        (i if cz >= wz - 1 and cxn >= wx - 1 else w)(
            f"天空盒 {ac} 个区域，覆盖 {cz:.0f}/{wz} ({cz / wz:.1%}) x "
            f"{cxn:.0f}/{wx} ({cxn / wx:.1%})")
    else:
        w("天空盒 0 个区域（无天空贴图）")

    print("\n" + "=" * 78)
    print(f"汇总: FAIL {len(FAIL)}  WARN {len(WARN)}  INFO {len(INFO)}")
    print("=" * 78)
    for x in FAIL:
        print(f"  FAIL: {x}")


MAX_MODEL_ID = 14        # MapModels.modelInit 里 modelInstanceDatas 的长度

if __name__ == "__main__":
    main()
