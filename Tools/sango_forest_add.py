# -*- coding: utf-8 -*-
"""sango_forest_add.py —— 按真实高程把「草地/湿地」改成「森」，用于增加森林格。

约束（按需求）：
  1) 不改通行性：只改 moveable=true 的 草地(1)/湿地(4) -> 森(6)（森也是 moveable=true），
     所以「能不能走」不变；但 baseCost 5->6、canBuild true->false 是地形属性决定的，
     脚本会把受影响格数逐项报出来。
  2) 不改道路格：道路地形 主径(11)/栈道(12)/渡所(13)/小径(20) 一律排除，
     并且写盘后复核这些格的字节完全没动。
  3) 不碰内政位（terrainState & 4）与 areaId；顶点贴图(textureIndex)不动 -> 地表外观不变。

判据：真实高程（ETOPO）落在 [--elev-min, --elev-max) 且坡度 <= --slope-max
      且当前地形在 --from-terrains 里。

用法：
    python sango_forest_add.py --bin <bin> --report
    python sango_forest_add.py --bin <bin> --apply
"""
import argparse
import json
import shutil
import struct
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np

DEM_DEFAULT = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
LAT_DEFAULT = "0.003708,-0.039995,41.9144"
LON_DEFAULT = "0.049694,0.002809,99.4265"
ROAD_T = (11, 12, 13, 20)          # 主径/栈道/渡所/小径
INTERIOR_BIT = 4


def grid_offset(raw):
    off = 4
    n, sh = 0, 0
    while True:
        v = raw[off]
        off += 1
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    off += n
    off += 8                       # mapWidth/Height
    off += 4                       # gridSize
    return off


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--dem", default=DEM_DEFAULT)
    ap.add_argument("--lat", default=LAT_DEFAULT)
    ap.add_argument("--lon", default=LON_DEFAULT)
    ap.add_argument("--elev-min", type=float, default=200.0)
    ap.add_argument("--elev-max", type=float, default=1000.0)
    ap.add_argument("--slope-max", type=float, default=12.0)
    ap.add_argument("--from-terrains", default="1,4", help="草地/湿地")
    ap.add_argument("--to-terrain", type=int, default=6, help="森")
    ap.add_argument("--skip-interior", action="store_true", default=True)
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    src = Path(args.bin_path)
    d = parse_bin(str(src))
    cx, cy = d["cellsX"], d["cellsY"]
    raw = src.read_bytes()
    goff = grid_offset(raw)
    g = np.frombuffer(raw, np.uint8, cx * cy * 7, goff).reshape(cx, cy, 7)
    tt = g[..., 0].astype(np.int32)
    st = (g[..., 1].astype(np.uint32) | (g[..., 2].astype(np.uint32) << 8)
          | (g[..., 3].astype(np.uint32) << 16) | (g[..., 4].astype(np.uint32) << 24))
    area = g[..., 5].astype(np.int32) | (g[..., 6].astype(np.int32) << 8)
    frm = {int(x) for x in args.from_terrains.split(",") if x.strip()}

    latc = [float(x) for x in args.lat.split(",")]
    lonc = [float(x) for x in args.lon.split(",")]
    xs = np.arange(cx)[:, None]
    ys = np.arange(cy)[None, :]
    lat = latc[0] * xs + latc[1] * ys + latc[2]
    lon = lonc[0] * xs + lonc[1] * ys + lonc[2]
    dem = np.load(args.dem, mmap_mode="r")
    rows = np.clip(((90.0 - lat) * 60.0).astype(np.int32), 1, dem.shape[0] - 2)
    cols = np.clip(((lon + 180.0) * 60.0).astype(np.int32), 1, dem.shape[1] - 2)
    r0, c0 = int(rows.min()) - 2, int(cols.min()) - 2
    slab = np.array(dem[r0:rows.max() + 3, c0:cols.max() + 3], np.float32)
    er, ec = rows - r0, cols - c0
    elev = slab[er, ec]
    km = 1852.0
    slope = np.degrees(np.arctan(np.hypot(
        (slab[er, ec + 1] - slab[er, ec - 1]) / (2 * km),
        (slab[er + 1, ec] - slab[er - 1, ec]) / (2 * km))))

    base = np.isin(tt, list(frm))
    band = (elev >= args.elev_min) & (elev < args.elev_max) & (slope <= args.slope_max)
    cand = base & band
    interior = (st & INTERIOR_BIT) != 0
    if args.skip_interior:
        cand &= ~interior
    road = np.isin(tt, list(ROAD_T))

    print("=" * 78)
    print(f"增加森林格: {src.name}  格 {cx}x{cy}")
    print(f"  真实高程带 [{args.elev_min:.0f}, {args.elev_max:.0f}) m  坡度 <= {args.slope_max:.0f}°")
    print(f"  来源地形 {sorted(frm)} -> {args.to_terrain}")
    print(f"  当前地形分布: {dict(sorted(Counter(tt.ravel().tolist()).items()))}")
    print(f"\n  基础候选（来源地形）: {int(base.sum())} 格")
    print(f"  交集（高程带+坡度）: {int((base & band).sum())} 格")
    print(f"  排除内政位后: {int(cand.sum())} 格")
    print(f"  其中按来源地形: {dict(Counter(tt[cand].tolist()))}")
    for lo, hi in ((200, 500), (500, 1000)):
        m = cand & (elev >= lo) & (elev < hi)
        print(f"    高程 {lo}~{hi} m: {int(m.sum())} 格")

    # 影响提示
    buildable = np.array([True, True, True, True, False, False, False, False, False, False, False, False,
                          False, False, False, False, False, False, False, False, False, False, False, False])
    print(f"\n  通行性影响: moveable 不变（1 与 4 都是 true，6 也是 true）✓")
    n_build = int((cand & (tt == 1)).sum())
    print(f"            canBuild true->false 的格: {n_build}（来自草地(1)；湿地(4)本来就是 false）")
    print(f"            baseCost 5->6 的格: {int(cand.sum())}（行军消耗 +1，均为可通行）")
    print(f"  道路格保护: 道路地形 {list(ROAD_T)} 当前 {int(road.sum())} 格，"
          f"候选与道路交集 {int((cand & road).sum())} 格（应为 0 ✓）")
    print(f"  areaId 0 的候选格: {int((cand & (area == 0)).sum())}（不改 areaId）")

    if not args.apply:
        print("\n（未加 --apply，未写盘）")
        return

    bak = src.with_suffix(src.suffix + ".bak_before_forest")
    if not bak.exists():
        shutil.copy2(src, bak)
    buf = bytearray(raw)
    n = 0
    xs_, ys_ = np.nonzero(cand)
    for x, y in zip(xs_.tolist(), ys_.tolist()):
        o = goff + (x * cy + y) * 7
        if buf[o] in frm:
            buf[o] = args.to_terrain
            n += 1
    src.write_bytes(bytes(buf))
    print(f"\n已写入 {src}  {src.stat().st_size / 1024 / 1024:.2f} MB（备份 {bak.name}）")
    print(f"  改写格数 {n}")

    # 复核
    raw2 = src.read_bytes()
    g2 = np.frombuffer(raw2, np.uint8, cx * cy * 7, goff).reshape(cx, cy, 7)
    tt2 = g2[..., 0].astype(np.int32)
    st2 = (g2[..., 1].astype(np.uint32) | (g2[..., 2].astype(np.uint32) << 8)
           | (g2[..., 3].astype(np.uint32) << 16) | (g2[..., 4].astype(np.uint32) << 24))
    print(f"  复核 森(6): {int((tt == 6).sum())} -> {int((tt2 == 6).sum())}")
    print(f"  复核 道路格 {int(np.isin(tt2, list(ROAD_T)).sum())}（原 {int(road.sum())}）"
          f"  道路字节是否被动: "
          f"{'是 ✗' if not np.array_equal(tt[road], tt2[road]) else '否 ✓'}")
    print(f"  复核 内政位 {int(interior.sum())} -> {int(((st2 & INTERIOR_BIT) != 0).sum())}")
    print(f"  复核 areaId 是否被动: "
          f"{'是 ✗' if not np.array_equal(area, g2[..., 5].astype(np.int32) | (g2[..., 6].astype(np.int32) << 8)) else '否 ✓'}")
    changed = int((tt != tt2).sum())
    print(f"  复核 总改动格 {changed}（应等于改写格数 {n}）")
    mv = {1: True, 4: True, 6: True}
    print(f"  复核 可通行格数: 改前 {int(np.isin(tt, [1, 4, 6]).sum())}"
          f"  改后 {int(np.isin(tt2, [1, 4, 6]).sum())}（应相等 ✓）")
    json.dump({"cells_changed": n, "elev_band": [args.elev_min, args.elev_max],
               "road_cells": int(road.sum()), "interior_cells": int(interior.sum())},
              open(r"D:\sango_infinity\Temp\forest_add_log.json", "w", encoding="utf-8"),
              ensure_ascii=False)


if __name__ == "__main__":
    main()
