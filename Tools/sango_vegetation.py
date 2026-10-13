# -*- coding: utf-8 -*-
"""sango_vegetation.py —— 按地格属性给「森林」地格烘焙植被模型，写进地图 .bin 的 mapModels 段。

为什么是烘焙而不是运行时生成（与原版一致）：
    原版 DefaultMap.bin 的 mapModels 段里就是 **2352 条 objType=6（植）** 的记录，
    即官方自己就把植被烘焙在地图数据里。本工具产出同一种数据，
    引擎侧走完全相同的通道（MapModels.OnLoad -> CreateModel -> AddStatic ->
    ModelInstanceData 实例化绘制 + 四季材质自动切换），因此不需要改任何 C# 代码。

地格属性怎么用：
    terrainType == 6（森）            -> 只有森林地格长树（terrains 可配）
    terrainState & 4（内政位）        -> 默认跳过（skipInterior）
    areaId -> 城市 -> 州/区域         -> 供 zones 的 provinces/regions/cities 规则使用
    格 (x,y)                          -> 经纬度（geo 的线性拟合）-> 供 zones 的经纬度带使用

世界坐标（已用原版 2352 棵树逐棵验证，残差 0.000 格）：
    worldZ = x*gridSize + gridSize/2
    worldX = y*gridSize + (x&1)*gridSize/2 + gridSize/2
    worldY = 顶点(4x+2, 4y+2*(x&1)+2).height * 0.5      （gridVertexCount = gridSize/quadSize）

硬约束（重要）：
    MapModels.Init 分配 modelInstanceDatas[14]，索引 = modelId-1，所以 **只能用 modelId 1..14**；
    而 ModelInstanceData.DrawMesh 用 Graphics.DrawMeshInstanced 一次画完，该 API **单次上限 1023**，
    超出会报错且整批不绘制。原版单模型最多 637 条也印证了这一点。
    因此本工具按 budget.maxPerModel（默认 1000）截断，超预算时随机丢弃（保证空间上均匀，不砍掉半张图）。

用法：
    python sango_vegetation.py --bin Map/world_full.bin --config VegetationTypes.json --report
    python sango_vegetation.py --bin Map/world_full.bin --config VegetationTypes.json --apply
"""
import argparse
import json
import random
import sys
from collections import Counter, defaultdict
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin, Writer          # noqa: E402  复用现成的 .bin 逐段解析/写入

OBJ_TYPE_PLANT = 6          # ModelBrush.objectTypeTitle: 城/关/港/内/军/植/其他 -> 6 = 植
INTERIOR_BIT = 4            # GridState.Interior = 2, SetGridState 用 1<<2
MODEL_ID_MIN, MODEL_ID_MAX = 1, 14   # modelInstanceDatas 长度 14 的硬限制


# --------------------------------------------------------------------------- #
# 地格 -> 经纬度
# --------------------------------------------------------------------------- #
def make_geo(cfg):
    g = cfg.get("geo", {})
    lat = g.get("lat", {})
    lon = g.get("lon", {})
    ay, ax, ab = lat.get("y", 0.0), lat.get("x", 0.0), lat.get("b", 0.0)
    bx, by, bb = lon.get("x", 0.0), lon.get("y", 0.0), lon.get("b", 0.0)

    def latlon(x, y):
        return ay * y + ax * x + ab, bx * x + by * y + bb
    return latlon


def hit_zone(zone, lat, lon, ctx):
    """zone 的条件全部满足才算命中；provinces/regions/cities 优先于经纬度带。"""
    for key in ("cities", "provinces", "regions"):
        want = zone.get(key)
        if want:
            got = ctx.get(key)
            if not got or got not in want:
                return False
    for key, val in (("latMin", lat), ("latMax", lat), ("lonMin", lon), ("lonMax", lon)):
        if key in zone:
            lim = zone[key]
            if key.endswith("Min") and val < lim:
                return False
            if key.endswith("Max") and val > lim:
                return False
    return True


def pick_models(zone):
    """models: [[id, weight], ...] 或 [id, ...]"""
    ids, ws = [], []
    for m in zone.get("models", []):
        if isinstance(m, (list, tuple)):
            ids.append(int(m[0]))
            ws.append(float(m[1]) if len(m) > 1 else 1.0)
        else:
            ids.append(int(m))
            ws.append(1.0)
    return ids, ws


# --------------------------------------------------------------------------- #
# 主流程
# --------------------------------------------------------------------------- #
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True, help="地图 .bin")
    ap.add_argument("--config", required=True, help="植被配置 json")
    ap.add_argument("--scenario", default=None, help="剧本 json（用于 city->province->region 规则）")
    ap.add_argument("--province-data", default=None, help="Provinces.json（province -> Region）")
    ap.add_argument("--region-data", default=None, help="Regions.json（Region 名字）")
    ap.add_argument("--out", default=None, help="输出 .bin（默认就地改写，并留 .bak_vegetation）")
    ap.add_argument("--keep-existing-plants", action="store_true",
                    help="保留原有的 objType=6 记录（默认替换：本工具生成的植被是派生物，重跑应幂等）")
    ap.add_argument("--mask", default=None,
                    help="可选：格掩膜 .npy（bool，形状 cx*cy）。给了就只在这些格上种（与 terrains 取交集）")
    ap.add_argument("--report", action="store_true", help="只体检不改写")
    ap.add_argument("--apply", action="store_true", help="真要写文件")
    args = ap.parse_args()

    cfg_path = Path(args.config)
    cfg = json.loads(cfg_path.read_text(encoding="utf-8"))
    src = Path(args.bin_path)
    if not src.exists():
        print(f"[错误] 找不到 {src}")
        return
    print(f"地图: {src}  {src.stat().st_size / 1048576:.1f} MB")
    print(f"配置: {cfg_path}")

    d = parse_bin(str(src))
    cx, cy = d["cellsX"], d["cellsY"]
    grid = d["gridRaw"]
    tt = grid[..., 0].astype(np.int32)
    st = (grid[..., 1].astype(np.uint32) | (grid[..., 2].astype(np.uint32) << 8)
          | (grid[..., 3].astype(np.uint32) << 16) | (grid[..., 4].astype(np.uint32) << 24))
    aid = grid[..., 5].astype(np.int32) | (grid[..., 6].astype(np.int32) << 8)
    verts = d["verts"]
    G = float(d["gridSize"])
    QUAD = float(d["quadSize"])
    VS = int(G // QUAD)                       # gridVertexCount，格中心所在顶点块边长
    print(f"  格 {cx}x{cy}  gridSize={d['gridSize']} quadSize={d['quadSize']}  "
          f"顶点 {verts.shape[0]}x{verts.shape[1]}  格中心顶点步长={VS}")

    m_start, m_end, old_models = d["tail"]["models"]
    n_plant_old = sum(1 for m in old_models if m[1] == OBJ_TYPE_PLANT)
    print(f"  原有物件 {len(old_models)} 条（其中 objType=6 植 {n_plant_old} 条）")
    if old_models:
        sc = Counter(round(m[6][0], 3) for m in old_models)
        print(f"  原有物件缩放分布(前5): {sc.most_common(5)}")

    # ---------------- 剧本侧：city -> province / region ----------------
    ctx_city = {}
    prov2region = {}
    region_name = {}
    if args.province_data and Path(args.province_data).exists():
        pd = json.loads(Path(args.province_data).read_text(encoding="utf-8"))
        for k, v in pd.items():
            prov2region[int(k)] = int(v.get("Region", v.get("region", 0)))
    if args.region_data and Path(args.region_data).exists():
        rd = json.loads(Path(args.region_data).read_text(encoding="utf-8"))
        for k, v in rd.items():
            region_name[int(k)] = v.get("Name", v.get("name", str(k)))
    if args.scenario and Path(args.scenario).exists():
        sc = json.loads(Path(args.scenario).read_text(encoding="utf-8"))
        for k, v in sc.get("citySet", {}).items():
            ctx_city[int(k)] = {
                "name": v.get("Name", ""),
                "province": v.get("province", v.get("ProvinceId", 0)),
            }
        print(f"  剧本: {len(ctx_city)} 个城市，province 取值 "
              f"{sorted(set(c['province'] for c in ctx_city.values()))[:15]}")

    latlon = make_geo(cfg)
    terrains = set(int(t) for t in cfg.get("terrains", [6]))
    skip_interior = bool(cfg.get("skipInterior", True))
    density = float(cfg.get("density", 1.0))
    jitter = float(cfg.get("jitter", 0.0))
    sscale = cfg.get("scale", [1.0, 1.0])
    seed = int(cfg.get("seed", 20260101))
    budget = cfg.get("budget", {})
    max_per_model = int(budget.get("maxPerModel", 1000))
    max_total = int(budget.get("maxTotal", 10000))
    zones = cfg.get("zones", [])

    # ---------------- 候选格 ----------------
    xs = np.arange(cx, dtype=np.int64)[:, None]
    ys = np.arange(cy, dtype=np.int64)[None, :]
    tm = np.isin(tt, list(terrains))
    if skip_interior:
        tm &= (st & INTERIOR_BIT) == 0
    if args.mask:
        mk = np.load(args.mask).astype(bool)
        if mk.shape != tm.shape:
            print(f"[错误] 掩膜形状 {mk.shape} 与候选格 {tm.shape} 不符")
            return
        before = int(tm.sum())
        tm &= mk
        print(f"  掩膜 {Path(args.mask).name}: {int(mk.sum())} 格   候选 {before} -> {int(tm.sum())}")
    cell_x, cell_y = np.where(tm)
    total_cand = cell_x.size
    by_terrain = Counter(tt[cell_x, cell_y].tolist())
    print(f"\n候选地格（地形 {sorted(terrains)}"
          f"{'，已排除内政位' if skip_interior else ''}）: {total_cand} 格  分布 {dict(by_terrain)}")

    rng = random.Random(seed)
    keep = [i for i in range(total_cand) if rng.random() < density]
    print(f"密度 {density} -> 计划种植 {len(keep)} 棵")

    # ---------------- 分派 zone / 选树种 ----------------
    zone_hit = Counter()
    picked = defaultdict(list)              # modelId -> [(x,y,zone)]
    unassigned = 0
    for i in keep:
        x, y = int(cell_x[i]), int(cell_y[i])
        lat, lon = latlon(x, y)
        a = int(aid[x, y])
        info = ctx_city.get(a, {}) if a else {}
        prov = info.get("province", 0)
        ctx = {
            "cities": a if a else None,
            "provinces": prov if prov else None,
            "regions": region_name.get(prov2region.get(prov, 0)) if prov else None,
            "provinceName": info.get("name", ""),
        }
        zone = None
        for z in zones:
            if hit_zone(z, lat, lon, ctx):
                zone = z
                break
        if zone is None:
            unassigned += 1
            continue
        ids, ws = pick_models(zone)
        if not ids:
            continue
        zone_hit[zone.get("name", "?")] += 1
        mid = rng.choices(ids, weights=ws, k=1)[0]
        if mid < MODEL_ID_MIN or mid > MODEL_ID_MAX:
            print(f"[警告] 配置里的 modelId {mid} 超出 {MODEL_ID_MIN}..{MODEL_ID_MAX}（"
                  f"MapModels.modelInstanceDatas 长度 14），已忽略该格")
            continue
        picked[mid].append((x, y, zone.get("name", "?")))

    print(f"\n各 zone 命中格数: {dict(zone_hit)}")
    if unassigned:
        print(f"  未命中任何 zone（被跳过）: {unassigned}")

    # ---------------- 预算截断（随机丢弃，保证空间均匀） ----------------
    trimmed = {}
    total = sum(len(v) for v in picked.values())
    for mid in sorted(picked):
        lst = picked[mid]
        if len(lst) > max_per_model:
            trimmed[mid] = len(lst) - max_per_model
            picked[mid] = rng.sample(lst, max_per_model)
    total2 = sum(len(v) for v in picked.values())
    if total2 > max_total:
        factor = max_total / float(total2)
        for mid in list(picked):
            lst = picked[mid]
            n = max(1, int(len(lst) * factor))
            if n < len(lst):
                trimmed[mid] = trimmed.get(mid, 0) + (len(lst) - n)
                picked[mid] = rng.sample(lst, n)
    if trimmed:
        print(f"  预算截断: {dict(sorted(trimmed.items()))}  "
              f"（maxPerModel={max_per_model}, maxTotal={max_total}）")
    print(f"\n最终种植: {sum(len(v) for v in picked.values())} 棵（截断前 {total}）")
    for mid in sorted(picked):
        print(f"  modelId {mid:>2}: {len(picked[mid]):>5} 棵")

    # ---------------- 生成模型记录 ----------------
    new_models = []
    if args.keep_existing_plants:
        new_models.extend(m for m in old_models if m[1] == OBJ_TYPE_PLANT)
    new_models.extend(m for m in old_models if m[1] != OBJ_TYPE_PLANT)

    def cell_world(x, y):
        wz = x * G + G * 0.5
        wx = y * G + (x & 1) * G * 0.5 + G * 0.5
        vx = VS * x + VS // 2
        vy = VS * y + (x & 1) * (VS // 2) + VS // 2
        vx = min(max(vx, 0), verts.shape[0] - 1)
        vy = min(max(vy, 0), verts.shape[1] - 1)
        return wx, verts[vx, vy, 0] * 0.5, wz

    oid = max([m[0] for m in new_models], default=-1) + 1
    placed_index = []
    for mid in sorted(picked):
        for (x, y, zname) in picked[mid]:
            wx, wy, wz = cell_world(x, y)
            if jitter > 0:
                wx += rng.uniform(-jitter, jitter) * G
                wz += rng.uniform(-jitter, jitter) * G
            rot = rng.uniform(0.0, 360.0)
            s = rng.uniform(sscale[0], sscale[1])
            new_models.append((oid, OBJ_TYPE_PLANT, 0, mid,
                               (wx, wy, wz), (0.0, rot, 0.0), (s, s, s)))
            placed_index.append((oid, x, y, mid, zname))
            oid += 1

    print(f"\n写入 mapModels: {len(new_models)} 条"
          f"（新增植被 {len(placed_index)}，保留原有 {len(new_models) - len(placed_index)}）")
    print(f"  objType 分布: {dict(Counter(m[1] for m in new_models))}")
    print(f"  modelId 分布: {dict(sorted(Counter(m[3] for m in new_models).items()))}")
    if placed_index:
        s_vals = [m[6][0] for m in new_models if m[1] == OBJ_TYPE_PLANT]
        print(f"  缩放 min/p50/max: {min(s_vals):.2f}/{sorted(s_vals)[len(s_vals)//2]:.2f}/{max(s_vals):.2f}")

    # ---------------- 校验：每棵树都落在应有的地格上 ----------------
    if placed_index:
        bad_terrain = bad_interior = bad_pos = 0
        for _oid2, x, y, _mid, _zn in placed_index:
            if tt[x, y] not in terrains:
                bad_terrain += 1
            if skip_interior and (st[x, y] & INTERIOR_BIT):
                bad_interior += 1
        # 用反向公式再从世界坐标解回格坐标，确认坐标公式自洽
        for m in new_models:
            if m[1] != OBJ_TYPE_PLANT or m[0] < 0:
                continue
            wx, _wy, wz = m[4]
            col = int(np.floor((wz - G * 0.5) / G))
            row = int(np.floor((wx - G * 0.5 - (col & 1) * G * 0.5) / G))
            if jitter > 0:
                continue
            if not (0 <= col < cx and 0 <= row < cy) or tt[col, row] not in terrains:
                bad_pos += 1
        print(f"\n校验: 地形不符 {bad_terrain}，内政位 {bad_interior}，"
              f"坐标反解不落在候选地形 {bad_pos}（应为 0，jitter>0 时不检查）")

    if not args.apply or args.report:
        print("\n--report：未改写文件。加 --apply 写盘。")
        return

    # ---------------- 写盘 ----------------
    w = Writer()
    w.i32(len(new_models))
    for (oid_, ot, bid, mid, pos, rot, scl) in new_models:
        w.i32(oid_); w.i32(ot); w.i32(bid); w.i32(mid)
        for v in pos:
            w.f32(v)
        for v in rot:
            w.f32(v)
        for v in scl:
            w.f32(v)
    blob = w.bytes()

    out = Path(args.out) if args.out else src
    if out == src:
        bak = src.with_suffix(src.suffix + ".bak_vegetation")
        if not bak.exists():
            bak.write_bytes(src.read_bytes())
            print(f"已备份 -> {bak.name}")
    data = d["buf"][:m_start] + blob + d["buf"][m_end:]
    out.write_bytes(data)
    print(f"已写出 {out}  {len(data) / 1048576:.1f} MB（原 {len(d['buf']) / 1048576:.1f} MB，"
          f"差 {len(data) - len(d['buf']):+d} 字节）")

    chk = parse_bin(str(out))
    print(f"复核: 解析通过，mapModels {len(chk['tail']['models'][2])} 条，"
          f"格 {chk['cellsX']}x{chk['cellsY']}，"
          f"mapLabelSet {len(chk['tail']['labels'][2])} 条")


if __name__ == "__main__":
    main()
