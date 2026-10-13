# -*- coding: utf-8 -*-
"""sango_realdata_check.py —— 用真实高程（ETOPO 60 弧秒 ≈1.85km）逐格核对地图地形。

做什么：
  1) 用地格 -> 经纬度 的线性拟合（--lat/--lon 系数）把每个地格定位到真实经纬度
  2) 从 ETOPO 取样真实高程与坡度（坡度由真实高程梯度算，不是地图的高度字节）
  3) 判「本该是 山地 / 丘陵 / 平原」，与地图的地形类型对照，列出冲突清单
  4) 顺带给出「地图上是草地/荒地、真实地形却是山地」这类可修正的清单，
     以及反例（地图画成崖、真实是平原）

判据（可调）：
  山地: 高程 >= --mountain-elev 或 坡度 >= --mountain-slope
  丘陵: 高程 >= --hill-elev 或 坡度 >= --hill-slope（且不满足山地）
  平原: 其余

输出：分类型冲突统计 + 冲突最密的地块（格坐标）+ 一张三联对照图。
"""
import argparse
import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image

DEM_DEFAULT = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
# 地图格 -> 经纬度（yyxk 的拟合）
LAT = dict(y=-0.0399951, x=0.0037077, b=41.91444)
LON = dict(x=0.0028091, y=0.0496939, b=99.42651)

MOUNTAIN_T = 16          # 崖
PLAIN_T = 1              # 草地
FOREST_T = 6             # 森
SAND_T = 3               # 砂地


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--dem", default=DEM_DEFAULT)
    ap.add_argument("--lat", default=None, help="lat=y*k,x*k,b=k 覆盖默认")
    ap.add_argument("--lon", default=None)
    ap.add_argument("--mountain-elev", type=float, default=1500.0, help="判山的高程阈值(m)")
    ap.add_argument("--mountain-slope", type=float, default=15.0, help="判山的坡度阈值(度)")
    ap.add_argument("--hill-elev", type=float, default=300.0)
    ap.add_argument("--hill-slope", type=float, default=5.0)
    ap.add_argument("--forest-elev-min", type=float, default=200.0)
    ap.add_argument("--forest-elev-max", type=float, default=2500.0)
    ap.add_argument("--forest-slope-max", type=float, default=12.0)
    ap.add_argument("--out", default=r"D:\sango_infinity\Temp\realdata_check.png")
    args = ap.parse_args()

    for k, v in (("lat", args.lat), ("lon", args.lon)):
        if v:
            vals = [float(x) for x in v.split(",")]
            d = LAT if k == "lat" else LON
            for key, val in zip(("x", "y", "b"), vals):
                d[key] = val

    d = parse_bin(str(args.bin_path))
    cx, cy = d["cellsX"], d["cellsY"]
    tt = d["gridRaw"][..., 0].astype(np.int32)
    print("=" * 78)
    print(f"真实数据核对: {Path(args.bin_path).name}  格 {cx}x{cy}")

    dem = np.load(args.dem, mmap_mode="r")
    print(f"DEM {Path(args.dem).name}  shape {dem.shape} dtype {dem.dtype}")

    xs = np.arange(cx)[:, None]
    ys = np.arange(cy)[None, :]
    lat = LAT["y"] * ys + LAT["x"] * xs + LAT["b"]
    lon = LON["x"] * xs + LON["y"] * ys + LON["b"]
    print(f"地图经纬范围: lat {lat.min():.2f}~{lat.max():.2f}  lon {lon.min():.2f}~{lon.max():.2f}")

    rows = np.clip(((90.0 - lat) * 60.0).astype(np.int32), 1, dem.shape[0] - 2)
    cols = np.clip(((lon + 180.0) * 60.0).astype(np.int32), 1, dem.shape[1] - 2)
    r0, r1 = int(rows.min()) - 2, int(rows.max()) + 3
    c0, c1 = int(cols.min()) - 2, int(cols.max()) + 3
    slab = np.array(dem[r0:r1, c0:c1], np.float32)
    print(f"DEM 切片 rows {r0}~{r1}  cols {c0}~{c1}  ({slab.shape})")
    er, ec = rows - r0, cols - c0
    elev = slab[er, ec]
    # 坡度：ETOPO 60 弧秒 ≈ 1.85 km/点
    km = 1852.0
    dzdx = (slab[er, ec + 1] - slab[er, ec - 1]) / (2 * km)
    dzdy = (slab[er + 1, ec] - slab[er - 1, ec]) / (2 * km)
    slope = np.degrees(np.arctan(np.hypot(dzdx, dzdy)))
    print(f"取样高程: 中位 {np.median(elev):.0f} m  p2 {np.percentile(elev, 2):.0f}"
          f"  p98 {np.percentile(elev, 98):.0f} m")
    print(f"取样坡度: 中位 {np.median(slope):.1f}°  p98 {np.percentile(slope, 98):.1f}°")

    exp = np.where((elev >= args.mountain_elev) | (slope >= args.mountain_slope), 2,
                   np.where((elev >= args.hill_elev) | (slope >= args.hill_slope), 1, 0))
    names = {0: "平原", 1: "丘陵", 2: "山地"}
    print(f"\n真实地形分布: " +
          str({names[k]: int((exp == k).sum()) for k in (0, 1, 2)}))

    # 地图侧哪类地形算「山地」：崖(16) 及表外的 32 归外框
    map_mtn = tt == MOUNTAIN_T
    low_t = (tt == PLAIN_T) | (tt == FOREST_T) | (tt == SAND_T)
    print(f"\n地图侧: 崖(16) {int(map_mtn.sum())} 格   低地类(草/森/砂) {int(low_t.sum())} 格")

    conf = {}
    conf["地图是山地、真实是平原"] = int((map_mtn & (exp == 0)).sum())
    conf["地图是平原/低地、真实是山地"] = int((low_t & (exp == 2)).sum())
    conf["地图是山地、真实是丘陵"] = int((map_mtn & (exp == 1)).sum())
    conf["地图是低地、真实是丘陵"] = int((low_t & (exp == 1)).sum())
    for k, v in conf.items():
        tot = int(map_mtn.sum()) if "地图是山地" in k else int(low_t.sum())
        print(f"  {k}: {v} 格（占该类地图格 {v / max(1, tot):.3f}）")

    # 冲突最密的 32 格块
    print("\n冲突最密的 32 格块（地图是低地、真实是山地）:")
    bad = low_t & (exp == 2)
    B = 32
    dens = []
    for y0 in range(0, cy - B + 1, B):
        for x0 in range(0, cx - B + 1, B):
            n = int(bad[x0:x0 + B, y0:y0 + B].sum())
            if n:
                dens.append((n, x0, y0))
    dens.sort(reverse=True)
    for n, x0, y0 in dens[:10]:
        print(f"    格({x0},{y0})~({x0 + B},{y0 + B})  冲突 {n} 格"
              f"  该处真实高程中位 "
              f"{np.median(elev[x0:x0 + B, y0:y0 + B][bad[x0:x0 + B, y0:y0 + B]]):.0f} m")

    # 三联图：地图地形 / 真实地形 / 冲突
    pal = {0: (60, 60, 60), 1: (200, 120, 30), 2: (240, 240, 240)}
    eimg = np.zeros((cx, cy, 3), np.uint8)
    for k in (0, 1, 2):
        eimg[exp == k] = pal[k]
    ttimg = np.zeros((cx, cy, 3), np.uint8)
    info = json.loads(Path(r"D:\sango_infinity\Build\Content\Data\Common\TerrainTypes.json")
                      .read_text(encoding="utf-8"))["TerrainTypes"]
    for t in np.unique(tt):
        i = info.get(str(int(t)))
        ttimg[tt == t] = i["color"] if i else (255, 0, 255)
    cimg = np.zeros((cx, cy, 3), np.uint8)
    cimg[bad] = (255, 40, 40)
    cimg[map_mtn & (exp == 0)] = (40, 120, 255)
    g = Image.new("RGB", (cx * 3 + 16, cy), (20, 20, 24))
    g.paste(Image.fromarray(np.transpose(ttimg, (1, 0, 2))), (0, 0))
    g.paste(Image.fromarray(np.transpose(eimg, (1, 0, 2))), (cx + 8, 0))
    g.paste(Image.fromarray(np.transpose(cimg, (1, 0, 2))), (cx * 2 + 16, 0))
    if max(g.size) > 1500:
        k = 1500 / float(max(g.size))
        g = g.resize((int(g.size[0] * k), int(g.size[1] * k)), Image.Resampling.NEAREST)
    g.save(args.out)
    print(f"\n已写出 {args.out}（左=地图地形 中=真实地形 右=冲突：红=该是山却是低地 蓝=该是平原却画成山）")

    # ---- 森林候选（供 item 1：增加森林格）----
    open_t = (tt == PLAIN_T) | (tt == 4)           # 草地 / 湿地（可通行、非荒漠）
    ok = ((elev >= args.forest_elev_min) & (elev <= args.forest_elev_max)
          & (slope <= args.forest_slope_max))
    cand = open_t & ok
    print(f"\n[森林候选] 地图是 草(1)/湿地(4) 且 真实高程 {args.forest_elev_min:.0f}~"
          f"{args.forest_elev_max:.0f} m、坡度 <= {args.forest_slope_max:.0f}° 的格: {int(cand.sum())}")
    print(f"  占 草/湿地 的 {cand.sum() / max(1, open_t.sum()):.3f}")
    bands = [(0, 200, "低地"), (200, 500, "低丘"), (500, 1000, "中丘"),
             (1000, 1500, "高丘/低山"), (1500, 2500, "山地")]
    for lo, hi, nm in bands:
        m = open_t & (elev >= lo) & (elev < hi) & (slope <= args.forest_slope_max)
        if m.sum():
            print(f"    高程 {lo}~{hi} m（{nm}）: {int(m.sum())} 格")
    fc = Path(r"D:\sango_infinity\Temp\forest_candidates.png")
    v = np.zeros((cx, cy, 3), np.uint8)
    v[open_t] = (70, 110, 70)
    v[cand] = (90, 230, 90)
    vm = Image.fromarray(np.transpose(v, (1, 0, 2)))
    vm.resize((900, 900), Image.Resampling.NEAREST).save(fc)
    print(f"  候选可视化 {fc}（浅绿=草地/湿地，亮绿=森林候选）")


if __name__ == "__main__":
    main()
