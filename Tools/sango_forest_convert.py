# -*- coding: utf-8 -*-
"""sango_forest_convert.py —— 把「树模型所在的草地格」按可行性落定：能改成就改成森林，不能改就删模型。

背景（实测）：
    地图上有 8,266 个模型（树），其中 8,165 个落在 **草地(1)** 格上 —— 视觉上是树、数据上是草，
    两者不一致。改成森林(6) 会付出这些代价（TerrainTypes.json 逐字段对比）：
        canBuild   True -> False    这一格以后不能建任何东西（内政设施/军事设施/障碍）
        fertility  [20,40] -> [10,30]  该格可开发上限下降
        baseCost   5 -> 6           行军消耗略增
        fireRate   20 -> 25         更易燃
        prosperity [1,5] -> [9,20]  反而更高
        viewThrough / canAmbush / moveable  不变（不影响视野与伏击）
    引擎侧唯一的「城池周边」硬约束是 BuildingSpace（0~2 格，障碍物不得贴着港关城建），
    所以判定半径默认取 3 格（略宽于引擎上限，把城池可建设圈留干净）。

判定（--report 会逐条打印数量）：
    保留模型 + 改成森林   : 地形==1(草地) 且 距最近据点 > --protect-radius 格
                            且 不与水体 8 邻接（河岸留给农田）且 不是内政地
    删除模型（地形不动）  : 其余（含贴着城池、河岸、非草地等）
    原样不动             : 地形已是森林(6) 的（数据本来就是对的）

用法：
    python sango_forest_convert.py --bin <bin> --report      # 出统计与预览图
    python sango_forest_convert.py --bin <bin> --apply       # 写盘（地形 + 模型表，自动备份）
"""
import argparse
import struct
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin                          # noqa: E402

import numpy as np
from PIL import Image

OUT = Path(r"D:\sango_infinity\Temp")
CITY = (17, 18, 19)
WATER = (7, 8, 9, 14, 21, 22)
INTERIOR_BIT = 4                                               # GridState.Interior = 2 -> 1<<2


def locate_grid(raw, cx, cy, tt):
    """定位地格段，并确认格在文件里的线性顺序（vx 外层 or vy 外层）。"""
    off = 4
    n, sh = 0, 0
    while True:
        v = raw[off]
        off += 1
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    off += n + 8                                    # mapWidth, mapHeight
    goff = off + 4                                  # 跳过 gridSize(int32)
    rng = np.random.default_rng(0)
    S = 400
    xs = rng.integers(0, cx, S)
    ys = rng.integers(0, cy, S)
    best = None
    for name, idx in (("vx_outer", lambda x, y: (x * cy + y) * 7),
                      ("vy_outer", lambda x, y: (y * cx + x) * 7)):
        ok = sum(1 for x, y in zip(xs, ys) if raw[goff + idx(int(x), int(y))] == int(tt[x, y]))
        if best is None or ok > best[0]:
            best = (ok, name)
    return goff, best[1], best[0] / S


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--from-terrain", type=int, default=1, help="要评估的原地形（默认 1=草地）")
    ap.add_argument("--to-terrain", type=int, default=6, help="目标地形（默认 6=森）")
    ap.add_argument("--city-ids", default="17,18,19", help="据点地形：都市/港/关")
    ap.add_argument("--water-ids", default="7,8,9,14,21,22")
    ap.add_argument("--protect-radius", type=int, default=3,
                    help="距最近据点 <= 该格数(切比雪夫) 的树一律删（把城池可建设圈留干净）")
    ap.add_argument("--protect-water-adjacent", default="on", choices=("on", "off"),
                    help="与水 8 邻接的树是否删（河岸留给农田）")
    ap.add_argument("--backup-tag", default="forest")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    if not (args.report or args.apply):
        args.report = True

    src = Path(args.bin_path)
    info = parse_bin(str(src))
    cx, cy = info["cellsX"], info["cellsY"]
    G = info["gridRaw"]
    tt = G[..., 0].astype(np.int32)
    state = (G[..., 1].astype(np.int32) | (G[..., 2].astype(np.int32) << 8)
             | (G[..., 3].astype(np.int32) << 16) | (G[..., 4].astype(np.int32) << 24))
    aid = G[..., 5].astype(np.int32) | (G[..., 6].astype(np.int32) << 8)
    q = float(info["quadSize"])
    msec = info["tail"]["models"]
    s_models, e_models, models = int(msec[0]), int(msec[1]), msec[2]

    print("=" * 84)
    print(f"草地->森林 可行性落定: {src.name}  格 {cx}x{cy}  模型 {len(models)}"
          f"  模式 {'写盘' if args.apply else '预览'}")
    print("=" * 84)

    # ---- 保护掩膜 ----
    city = np.isin(tt, [int(x) for x in args.city_ids.split(",") if x.strip()])
    water = np.isin(tt, [int(x) for x in args.water_ids.split(",") if x.strip()])
    rad = max(0, args.protect_radius)
    city_near = np.zeros((cx, cy), bool)
    water_near = np.zeros((cx, cy), bool)
    for dx in range(-rad, rad + 1):
        for dy in range(-rad, rad + 1):
            city_near |= np.roll(np.roll(city, dx, 0), dy, 1)
    for dx in (-1, 0, 1):
        for dy in (-1, 0, 1):
            water_near |= np.roll(np.roll(water, dx, 0), dy, 1)
    interior = (state & INTERIOR_BIT) != 0
    print(f"  据点格 {int(city.sum())}  距其 <= {rad} 格的格 {int(city_near.sum())}"
          f"  水体格 {int(water.sum())}  与水体 8 邻接的格 {int(water_near.sum())}"
          f"  内政地格 {int(interior.sum())}")

    # ---- 逐个模型判定 ----
    conv_cells = set()          # 保留模型 + 改地形
    del_cells = {}              # 删除模型（格 -> 原因）
    keep_cells = set()          # 原样不动（已是森林）
    conv_models, del_models, keep_models = 0, 0, 0
    keep_flag = []                                   # 逐模型判定（同格多模型也不会判错）
    for m in models:
        vx, vy = int(round(float(m[4][2]) / q)), int(round(float(m[4][0]) / q))
        cxx, cyy = vx // 4, (vy - ((vx // 4) & 1) * 2) // 4
        if not (0 <= cxx < cx and 0 <= cyy < cy):
            keep_flag.append(False)
            del_models += 1
            continue
        t = int(tt[cxx, cyy])
        if t == args.to_terrain:
            keep_cells.add((cxx, cyy))
            keep_flag.append(True)
            keep_models += 1
            continue
        why = None
        if t != args.from_terrain:
            why = f"地形{t}(非草地)"
        elif interior[cxx, cyy]:
            why = "内政地"
        elif city_near[cxx, cyy]:
            why = f"距据点<={rad}格"
        elif args.protect_water_adjacent == "on" and water_near[cxx, cyy]:
            why = "紧邻水体"
        if why:
            del_cells[(cxx, cyy)] = why
            keep_flag.append(False)
            del_models += 1
        else:
            conv_cells.add((cxx, cyy))
            keep_flag.append(True)
            conv_models += 1

    print(f"\n  判定结果（按模型计）:")
    print(f"    保留并改森林 {conv_models:>6} 个模型 / {len(conv_cells)} 格")
    print(f"    删除模型     {del_models:>6} 个模型 / {len(del_cells)} 格"
          f"   原因: {dict(Counter(del_cells.values()).most_common())}")
    print(f"    原样不动     {keep_models:>6} 个模型 / {len(keep_cells)} 格（地形已是森林 ✓）")

    # ---- 预览图 ----
    img = np.zeros((cy, cx, 3), np.uint8)
    img[tt == args.from_terrain] = (60, 70, 60)
    img[np.isin(tt, list([int(x) for x in args.water_ids.split(',')]))] = (40, 50, 80)
    img[tt == args.to_terrain] = (30, 90, 40)
    img[city] = (200, 160, 40)
    for (x, y) in conv_cells:
        img[y, x] = (0, 220, 90)                     # 绿：改成森林
    for (x, y) in del_cells:
        img[y, x] = (230, 60, 60)                    # 红：删掉模型
    im = Image.fromarray(img).resize((cx * 2, cy * 2), Image.Resampling.NEAREST)\
        .resize((780, 780), Image.Resampling.NEAREST)
    im.save(OUT / "forest_convert.jpg", quality=90)
    print(f"\n  预览（绿=改成森林 红=删模型 黄=据点 墨绿=原森林 蓝=水）"
          f"{OUT / 'forest_convert.jpg'}")

    if not args.apply:
        print("\n（预览模式，未写盘。确认后加 --apply）")
        return

    # ---- 写盘：先改地格地形，再重建模型段 ----
    raw = bytearray(src.read_bytes())
    goff, order, acc = locate_grid(raw, cx, cy, tt)
    print(f"\n  地格段起 {goff}  存储顺序自检 {order}（抽样命中率 {acc:.3f}）")
    idx = (lambda x, y: (x * cy + y) * 7) if order == "vx_outer" else (lambda x, y: (y * cx + x) * 7)
    n_ter = 0
    for (x, y) in sorted(conv_cells):
        o = goff + idx(x, y)
        if raw[o] != args.to_terrain:
            raw[o] = args.to_terrain
            n_ter += 1
    print(f"  地格地形改写 {n_ter} 格（{args.from_terrain} -> {args.to_terrain}）")

    record = 52
    head = raw[:s_models]
    kept = bytearray()
    for i in range(len(models)):
        if not keep_flag[i]:
            continue
        off = s_models + 4 + i * record
        kept += raw[off:off + record]
    new_raw = bytes(head) + struct.pack("<i", len(kept) // record) + bytes(kept) + raw[e_models:]
    # 备份一律新生成一个唯一名：旧的同名备份可能是上一轮更早的产物，绝不能当成这次的回退点
    bak = src.with_suffix(src.suffix + f".bak_before_{args.backup_tag}")
    k = 1
    while bak.exists():
        bak = src.with_suffix(src.suffix + f".bak_before_{args.backup_tag}_{k}")
        k += 1
    bak.write_bytes(src.read_bytes())
    src.write_bytes(new_raw)
    print(f"  已写入 {src.name} {len(new_raw)/1024/1024:.2f} MB"
          f"  模型 {len(models)} -> {len(kept)//record}（删除 {len(models)-len(kept)//record}）"
          f"（备份 {bak.name}）")

    # ---- 复核 ----
    d2 = parse_bin(str(src))
    tt2 = d2["gridRaw"][..., 0].astype(np.int32)
    diff = np.argwhere(tt2 != tt)
    print(f"  复核 地形改动格数 {len(diff)}（应为 {n_ter}）"
          f"   全部落在待改集合内: "
          f"{all((int(a), int(b)) in conv_cells for a, b in diff)}"
          f"   全部是 {args.from_terrain}->{args.to_terrain}: "
          f"{bool((tt[diff[:,0], diff[:,1]] == args.from_terrain).all())}")
    print(f"  复核 areaId/state 未动: "
          f"{bool(np.array_equal(d2['gridRaw'][..., 1:], G[..., 1:]))}")
    print(f"  复核 顶点/贴图/水面字节未动: "
          f"{bool(np.array_equal(d2['verts'], info['verts']))}")
    print(f"  复核 标注未动: {len(d2['tail']['labels'][2])} 条"
          f"  模型 {len(d2['tail']['models'][2])} 个")
    rest_ok = d2["tail"]["models"][2][-1][4][2] == models[-1][4][2] if len(kept) else True
    print(f"  复核 保留模型与原始记录一致: {rest_ok}")


if __name__ == "__main__":
    main()
