# -*- coding: utf-8 -*-
"""lit_world 世界图生成器：ETOPO 高程图 + Natural Earth 河流/湖泊 -> Sango 地形数组。

与现有 sango_convert.py 产出的数据格式完全一致（terrain/vheight/vtex/vwater + cities.json + meta.json
+ 裸 bin + preview.png），但**不走光荣中国块那套 RBF 弯曲**，而是固定线性经纬窗口：

    窗口    经 west~east / 纬 south~north（默认 -15~150E / 7.5~70N）
    地格    cx(东西) x cy(南北) = 900 x 500          cell 中心：
              lon(cx) = west + (cx + 0.5) * DLON
              lat(cx,cy) = north - (cy + 0.5 + 0.5*(cx&1)) * DLAT      # odd-q 半格错位
    顶点    vx(东西) x vy(南北) = 3601 x 2001          （vx=0 -> west，vx=3600 -> east；vy=0 -> north）
              lon(vx) = west + (vx/4) * DLON
              lat(vy) = north - (vy/4) * DLAT
    格中心顶点 = (4*cx+2, 4*cy + 2*(cx&1) + 2)        # 与 MapGrid.cs 一致

字节口径（与管线一致）：
    海    height=0   water=11
    川/河 height=1   water=11          （水面可见的条件是 water > height）
    陆地  height = 地形高度模型（平原 ~20，山地中位 ~88，峰值 ~250），water=0
"""
import argparse
import json
import zipfile
from collections import Counter
from pathlib import Path

import numpy as np
import shapefile                      # pyshp
from PIL import Image, ImageDraw
from scipy import ndimage

SW = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main")
DEM_PATH = SW / "data" / "etopo60s_i16.npy"
RIVERS_ZIP = SW / "data" / "raw" / "ne_rivers" / "ne_10m_rivers_lake_centerlines.zip"
LAKES_ZIP = SW / "data" / "raw" / "ne_lakes" / "ne_10m_lakes.zip"
CITIES_GEO = SW / "data" / "cities_georef.json"
CITIES_WORLD = SW / "data" / "world" / "cities_world.json"
WORLD_INFO = SW / "data" / "world" / "world_info.json"
DEM_DLON, DEM_DLAT = 0.0969, 0.0825      # 原世界图（1800x1000）每格经纬
DEM_LON0, DEM_LAT0 = -15.07, 77.0

# ---- Sango 地形 id（TerrainIds.cs / TerrainTypes.json）----
GRASS, SOIL, SAND, MARSH, POISON, FOREST = 1, 2, 3, 4, 5, 6
STREAM, RIVER, SEA, WASTE = 7, 8, 9, 10
SHALLOW, SHORE, CLIFF, CITY = 14, 15, 16, 17
PORT, GATE = 18, 19
# 真实港口城市（河港/湖港/海港），用于「按实际港口位置增加港」
PORTS = [("上海", 121.5, 31.2), ("广州", 113.3, 23.1), ("天津", 117.7, 39.0),
         ("武汉", 114.3, 30.6), ("重庆", 106.5, 29.6), ("南京", 118.8, 32.1),
         ("九江", 116.0, 29.7), ("岳阳", 113.1, 29.4), ("兰州", 103.8, 36.1),
         ("包头", 110.0, 40.7), ("哈尔滨", 126.6, 45.8), ("曼谷", 100.5, 13.75),
         ("河内", 105.8, 21.0), ("加尔各答", 88.4, 22.6), ("巴格达", 44.4, 33.3),
         ("汉堡", 10.0, 53.55), ("巴黎", 2.35, 48.86), ("莫斯科", 37.6, 55.75),
         ("伏尔加格勒", 44.5, 48.7), ("开罗", 31.2, 30.0)]
PORTS_JSON = Path(r"D:\sango_infinity\Tools\lit_world_ports.json")


def load_ports(rank_max=3):
    """读港口数据集（三国时期世界港口）；读不到就回退内置精简表。"""
    try:
        d = json.loads(PORTS_JSON.read_text(encoding="utf-8"))
        out = [(p["name"], float(p["lon"]), float(p["lat"]), int(p.get("rank", 3)))
               for p in d["ports"] if int(p.get("rank", 3)) <= rank_max]
        if out:
            return out, f"数据集 {PORTS_JSON.name}（{len(out)} 个，rank<={rank_max}）"
    except Exception as ex:                                       # noqa: BLE001
        print(f"  [港口数据] 读 {PORTS_JSON} 失败（{ex}），改用内置表")
    return [(n, lo, la, 3) for n, lo, la in PORTS], "内置精简表"
# ---- 地形 -> 贴图图层（TerrainLayerMap.cs 实测众数）----
LAYER = {GRASS: 29, SOIL: 27, SAND: 3, MARSH: 24, POISON: 1, FOREST: 30,
         STREAM: 0, RIVER: 0, SEA: 0, WASTE: 21, SHALLOW: 5, SHORE: 2,
         CLIFF: 18, CITY: 28, PORT: 3, GATE: 27}
# ---- 预览配色（沿用 sango_convert.py 的 PALETTE，Sango id 空间）----
PALETTE = {0: (0, 0, 0), 1: (126, 190, 80), 2: (196, 172, 112), 3: (232, 214, 152),
           4: (104, 150, 118), 5: (160, 60, 160), 6: (36, 104, 40), 7: (74, 136, 224),
           8: (48, 98, 206), 9: (28, 48, 150), 10: (150, 122, 82), 11: (226, 202, 64),
           12: (186, 124, 60), 13: (236, 236, 236), 14: (110, 200, 220), 15: (172, 172, 166),
           16: (96, 78, 64), 17: (228, 40, 40), 18: (200, 80, 80), 19: (200, 60, 60),
           20: (204, 170, 64)}


def ss(x, a, b):
    """smoothstep。"""
    t = np.clip((np.asarray(x, np.float64) - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def fbm(shape, seed, octaves=4, base_sigma=8.0):
    rng = np.random.default_rng(seed)
    out = np.zeros(shape, np.float64)
    amp, sig = 1.0, base_sigma
    for _ in range(octaves):
        n = rng.standard_normal(shape)
        out += amp * ndimage.gaussian_filter(n, sig)
        amp *= 0.5
        sig *= 0.5
    s = out.std()
    return out / s if s > 1e-9 else out


class Dem:
    """ETOPO 60s 双线性采样（与 make_world.Dem.sample 同一套公式）。

    先把窗口切片读进内存，再做双线性 —— 否则每点随机读 mmap 会慢到不可用。
    """

    def __init__(self, path, west, east, south, north, pad=8):
        a = np.load(path, mmap_mode="r")
        self.c0 = max(0, int(np.floor((west + 180.0) * 60.0)) - pad)
        self.c1 = min(a.shape[1], int(np.ceil((east + 180.0) * 60.0)) + pad)
        self.r0 = max(0, int(np.floor((90.0 - north) * 60.0)) - pad)
        self.r1 = min(a.shape[0], int(np.ceil((90.0 - south) * 60.0)) + pad)
        self.sub = np.array(a[self.r0:self.r1, self.c0:self.c1], np.float32)
        del a

    def sample(self, lon, lat):
        x = (np.asarray(lon, np.float64) + 180.0) * 60.0 - 0.5 - self.c0
        y = (90.0 - np.clip(np.asarray(lat, np.float64), -89.99, 89.99)) * 60.0 - 0.5 - self.r0
        x = np.clip(x, 0.0, self.sub.shape[1] - 2.001)
        y = np.clip(y, 0.0, self.sub.shape[0] - 2.001)
        x0 = np.floor(x).astype(np.int64)
        y0 = np.floor(y).astype(np.int64)
        fx, fy = x - x0, y - y0
        s = self.sub
        v = ((s[y0, x0] * (1 - fx) + s[y0, x0 + 1] * fx) * (1 - fy)
             + (s[y0 + 1, x0] * (1 - fx) + s[y0 + 1, x0 + 1] * fx) * fy)
        return np.asarray(v, np.float64)


def read_zip_shapes(zip_path):
    """从 zip 里读 shp/dbf。

    解压到**持久缓存目录**（只解一次，之后复用）—— 在 Windows 上临时目录会被 pyshp
    占着的 .dbf/.cpg 卡住删除，所以不要用 TemporaryDirectory。

    返回 [(record_dict, [段点列表...], shapeType)]。
    """
    td = Path(zip_path).parent / ("_x_" + Path(zip_path).stem)
    if not any(td.glob("*.shp")):
        td.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(zip_path) as z:
            z.extractall(td)
    shp = next(td.glob("*.shp"))
    sf = shapefile.Reader(str(shp.with_suffix("")))
    out = []
    fields = [f[0] for f in sf.fields[1:]]
    for sr in sf.iterShapeRecords():
        rec = dict(zip(fields, sr.record))
        sh = sr.shape
        pts = [(float(p[0]), float(p[1])) for p in sh.points]
        parts = list(sh.parts) + [len(pts)]
        segs = [pts[parts[i]:parts[i + 1]] for i in range(len(parts) - 1)]
        out.append((rec, segs, sh.shapeType))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--out", default=str(SW / "data" / "lit_world"))
    ap.add_argument("--west", type=float, default=-15.0)
    ap.add_argument("--east", type=float, default=150.0)
    ap.add_argument("--south", type=float, default=7.5)
    ap.add_argument("--north", type=float, default=70.0)
    ap.add_argument("--cx", type=int, default=900)
    ap.add_argument("--cy", type=int, default=500)
    # 河湖「占比缩小」的三个旋钮
    ap.add_argument("--river-rank-max", type=int, default=5,
                    help="scalerank 大于它就完全不画（越小河越少；5 = 只留 NE 的主要河道）")
    ap.add_argument("--river-width", type=float, default=0.28,
                    help="河宽系数（1 格约 15km；0.28 -> 主流 2 格、支流 1 格）")
    ap.add_argument("--lake-min-km2", type=float, default=1800.0,
                    help="小于此面积的湖不画（湖占比主要靠它压；1800 可保住纳木错等高原大湖）")
    ap.add_argument("--lake-sea-km2", type=float, default=20000.0,
                    help="大于此面积的湖按海(9)处理，否则按河(8)")
    ap.add_argument("--min-sea-km2", type=float, default=6000.0,
                    help="小于此面积的海斑（如死海/死亡谷）不算海")
    ap.add_argument("--peak-share", type=float, default=0.12,
                    help="崖(16，不可行走山体)占陆地的比例（在「山峰候选」里按起伏强度取前 N）")
    ap.add_argument("--peak-tpi", type=float, default=60.0, help="山峰候选：相对周边高差(米)")
    ap.add_argument("--peak-prom", type=float, default=200.0, help="山峰候选：相对突出度(米)")
    ap.add_argument("--peak-slope", type=float, default=60.0, help="山峰候选：坡度(米/格)")
    ap.add_argument("--valley-tpi", type=float, default=-60.0, help="山谷判据：低于周边(米)")
    ap.add_argument("--valley-slope", type=float, default=55.0, help="山谷判据：坡度上限(米/格)")
    ap.add_argument("--valley-flat", type=float, default=0.70,
                    help="谷地/平原平整强度（0=不压平，1=完全压成平面）")
    ap.add_argument("--relief-peak", type=float, default=1.25,
                    help="山峰细部起伏增强倍数（1.0=严格按真实高程，不增强）")
    ap.add_argument("--wall-step", type=float, default=25.0,
                    help="山壁限步长：相邻顶点步高差上限（字节；0=不平滑。25 字节≈横向5单位/纵向12.5单位）")
    ap.add_argument("--wall-iters", type=int, default=60, help="山壁平滑迭代轮数")
    ap.add_argument("--wall-alpha", type=float, default=0.5, help="山壁平滑松弛系数")
    ap.add_argument("--water-smooth", type=float, default=0.7,
                    help="河湖水面沿水面遮罩的平滑强度（0=不平滑）")
    ap.add_argument("--water-iters", type=int, default=20, help="水面平滑迭代轮数")
    ap.add_argument("--no-bank", action="store_true",
                    help="不给河湖加「岸(15,不可行走)」围边")
    ap.add_argument("--no-ports", action="store_true", help="不放置港口(18)")
    ap.add_argument("--port-min-cells", type=int, default=120,
                    help="水体连通块大于此格数才给它配一个港（避免湖水变孤岛）")
    ap.add_argument("--port-rank-max", type=int, default=3,
                    help="只用数据集中 rank <= 它的港口（1=只要区域枢纽/首都外港）")
    ap.add_argument("--city-rank-max", type=int, default=0,
                    help="只保留 rank <= 它的城（0=全部；1=只要三国城）")
    ap.add_argument("--city-only-flat", action="store_true",
                    help="城市只落在平地（草/土），否则只在非水非崖")
    ap.add_argument("--city-limit", type=int, default=0, help="只放前 N 座（0=不限）")
    ap.add_argument("--city-gap", type=int, default=14,
                    help="城市之间最小间距（格）；太近时按重要度裁掉低的那个")
    ap.add_argument("--city-target", type=int, default=200,
                    help="目标城市数；裁完不够就用内置世界城表按同一规则补充（0=不补）")
    ap.add_argument("--port-city-gap", type=int, default=5,
                    help="港与任何城市的最小距离（格）；4 格内的港一律不放")
    ap.add_argument("--port-port-gap", type=int, default=6,
                    help="港与港的最小距离（格）；太近时按重要度删掉低的那个")
    ap.add_argument("--seed", type=int, default=20261014)
    args = ap.parse_args()

    OUT = Path(args.out)
    OUT.mkdir(parents=True, exist_ok=True)
    CX, CY = args.cx, args.cy
    VX, VY = CX * 4 + 1, CY * 4 + 1
    DLON = (args.east - args.west) / CX
    DLAT = (args.north - args.south) / CY
    print(f"窗口 {args.west:g}~{args.east:g}E / {args.south:g}~{args.north:g}N"
          f"  地格 {CX}x{CY}  顶点 {VX}x{VY}")
    print(f"格尺寸 经纬 {DLON:.5f}° x {DLAT:.5f}°  "
          f"(中纬约 {DLON*111.32*np.cos(np.deg2rad((args.south+args.north)/2)):.1f} km x "
          f"{DLAT*111.32:.1f} km)")

    # ---------- 坐标轴 ----------
    cxs = np.arange(CX, dtype=np.int64)
    cys = np.arange(CY, dtype=np.int64)
    lon_c = args.west + (cxs + 0.5) * DLON                       # (CX,)
    lat_c = args.north - (cys[None, :] + 0.5 + 0.5 * (cxs[:, None] & 1)) * DLAT  # (CX,CY)
    lon_v = args.west + (np.arange(VX) / 4.0) * DLON             # (VX,)
    lat_v = args.north - (np.arange(VY) / 4.0) * DLAT            # (VY,)

    dem = Dem(DEM_PATH, args.west, args.east, args.south, args.north)
    e_cell = dem.sample(lon_c[:, None] * np.ones((1, CY)), lat_c)      # (CX,CY)
    e_vert = dem.sample(lon_v[:, None] * np.ones((1, VY)), lat_v[None, :])  # (VX,VY)
    print(f"高程: 地格 {e_cell.min():.0f}~{e_cell.max():.0f} m  "
          f"顶点 {e_vert.min():.0f}~{e_vert.max():.0f} m")

    # ---------- 海（DEM<0，按面积过滤小海斑）----------
    sea = e_cell < 0.0
    lab, n = ndimage.label(sea)
    if n:
        sizes = ndimage.sum(sea, lab, range(1, n + 1))
        km2 = sizes * (DLON * 111.32 * np.cos(np.deg2rad(np.abs(lat_c).mean())) * DLAT * 111.32)
        keep = np.concatenate([[False], km2 >= args.min_sea_km2])
        sea = keep[lab]
        print(f"海域: 连通斑 {n} 个 -> 保留 {int(keep.sum() - 1)} 个（>= {args.min_sea_km2:g} km²）")

    terrain = np.where(sea, SEA, GRASS).astype(np.uint8)

    # ---------- 河流 ----------
    riv = np.zeros((CX, CY), np.uint8)

    def to_px(lon, lat):
        x = (np.asarray(lon, np.float64) - args.west) / (args.east - args.west) * CX
        y = (args.north - np.asarray(lat, np.float64)) / (args.north - args.south) * CY
        return x, y

    shapes = read_zip_shapes(RIVERS_ZIP)
    plan, n_skip = [], 0
    for rec, segs, _ in shapes:
        sr = rec.get("scalerank", rec.get("SCALERANK", 99))
        try:
            sr = int(sr)
        except (TypeError, ValueError):
            sr = 99
        if sr > args.river_rank_max:
            n_skip += 1
            continue
        plan.append((RIVER if sr <= 3 else STREAM, sr, segs))   # sr<=3 主流(河,可通行)
    riv_img = Image.new("L", (CX, CY), 0)
    riv_dr = ImageDraw.Draw(riv_img)
    drawn = n_thin = 0
    for kind in (STREAM, RIVER):                    # 两遍：先支流后主流，河(8)覆盖川(7)
        for k, sr, segs in plan:
            if k != kind:
                continue
            w = int(round(args.river_width * (5.0 if sr <= 3 else 3.0 if sr <= 5 else 2.0) * 1.6))
            if w < 1:
                n_thin += 1                         # 连一格河水都撑不起 -> 整条抹除
                continue
            for seg in segs:
                xs, ys = to_px([p[0] for p in seg], [p[1] for p in seg])
                if xs.size < 2 or xs.max() < -20 or xs.min() > CX + 20 \
                        or ys.max() < -20 or ys.min() > CY + 20:
                    continue
                riv_dr.line(list(zip(xs.tolist(), ys.tolist())), fill=int(kind), width=w)
                drawn += 1
    riv = np.asarray(riv_img, np.uint8).T          # PIL 是 (高,宽) -> 转成 (cx,cy)
    print(f"河流: 图元 {len(shapes)} 条（跳过 {n_skip} 条 rank>{args.river_rank_max}，"
          f"规格不足抹除 {n_thin} 条，绘制 {drawn} 段）  河格 {(riv > 0).sum()}")

    # ---------- 湖泊 ----------
    lk_shapes = read_zip_shapes(LAKES_ZIP)
    limg = Image.new("I", (CX, CY), 0)
    ldr = ImageDraw.Draw(limg)
    areas = {}
    for i, (rec, segs, _) in enumerate(lk_shapes, start=1):
        tot = 0.0
        for seg in segs:
            xs, ys = to_px([p[0] for p in seg], [p[1] for p in seg])
            if xs.size < 3:
                continue
            ldr.polygon(list(zip(xs.tolist(), ys.tolist())), fill=i)
            tot += abs(np.sum(xs * np.roll(ys, 1) - np.roll(xs, 1) * ys)) / 2.0
        areas[i] = tot * (DLON * 111.32 * np.cos(np.deg2rad(np.abs(lat_c).mean())) * DLAT * 111.32)
    la = np.asarray(limg, np.int32).T              # PIL 是 (高,宽) -> 转成 (cx,cy)
    lake_id = la
    dropped = 0
    kept = np.zeros(len(lk_shapes) + 1, bool)
    lake_is_sea = np.zeros(len(lk_shapes) + 1, bool)
    for i in range(1, len(lk_shapes) + 1):
        km2 = areas.get(i, 0.0)
        if km2 < args.lake_min_km2:
            dropped += 1
            continue
        kept[i] = True
        lake_is_sea[i] = km2 >= args.lake_sea_km2
    lm = (lake_id > 0) & kept[np.clip(lake_id, 0, len(lk_shapes))]
    lm_sea = lm & lake_is_sea[np.clip(lake_id, 0, len(lk_shapes))]
    lm_riv = lm & ~lake_is_sea[np.clip(lake_id, 0, len(lk_shapes))]
    print(f"湖泊: 图元 {len(lk_shapes)} 个（丢弃 {dropped} 个 < {args.lake_min_km2:g} km²）"
          f"  湖格 {lm.sum()}（按海 {lm_sea.sum()} / 按河 {lm_riv.sum()}）")

    # ---------- 合成地形 ----------
    terrain[lm_sea] = SEA
    terrain[lm_riv] = RIVER
    terrain[(riv == STREAM)] = STREAM
    terrain[(riv == RIVER)] = RIVER
    terrain[sea] = SEA

    # ---------- 陆地覆盖（简化版气候规则，只依赖纬度/高程/离海距离/噪声）----------
    water = np.isin(terrain, (SEA, RIVER, STREAM))
    dist_km = ndimage.distance_transform_edt(~water) * (DLON * 111.32 *
                                                        np.cos(np.deg2rad(np.abs(lat_c).mean())))
    rng = np.random.default_rng(args.seed)
    lat_a = np.abs(lat_c)
    noise = ndimage.gaussian_filter(rng.standard_normal((CX, CY)), 6.0)
    noise /= max(1e-9, noise.std())
    interior = np.clip((dist_km - 300.0) / 1500.0, 0, 1)
    subtropic = np.exp(-(((lat_a - 24.0) / 9.0) ** 2))
    temperate_dry = np.exp(-(((lat_a - 42.0) / 8.0) ** 2)) * interior
    arid = 0.9 * subtropic * np.clip(dist_km / 400.0, 0.2, 1) + 0.8 * temperate_dry + 0.12 * noise
    desert = ss(arid, 0.55, 0.72)
    steppe = ss(arid, 0.36, 0.50) * (1 - desert)
    tundra = ss(lat_a + 2.5 * noise, 60, 67)
    taiga = ss(lat_a + 2.0 * noise, 49, 56) * (1 - tundra) * (1 - steppe - desert)
    tropical = (1 - ss(lat_a, 13, 23)) * (1 - desert - steppe)
    alpine = ss(e_cell, 2600, 3800)
    rem = np.clip(1 - (desert + steppe + tundra + taiga + tropical + alpine), 0, 1)
    land_t = np.full((CX, CY), GRASS, np.uint8)
    land_t[tropical > 0.5] = FOREST
    land_t[taiga > 0.5] = FOREST
    land_t[steppe > 0.5] = SOIL
    land_t[desert > 0.5] = SAND
    land_t[alpine > 0.5] = WASTE
    land_t[tundra > 0.5] = WASTE
    land_t[rem > 0.6] = GRASS
    # 湿地：低海拔 + 近海 + 平坦
    relief_c = e_cell - ndimage.gaussian_filter(e_cell, 2)
    marsh = (~water) & (e_cell < 60) & (dist_km < 220) & (relief_c < 12) & (tropical + taiga > 0.3)
    land_t[marsh] = MARSH

    # ---------- 山峰 / 山谷（地貌判读，不用绝对高程）----------
    # 出发点：青藏高原整体高，但内部有大面积可住人的宽谷盆地；欧洲多平原但也有真山脉。
    # 判据只看「相对周边的起伏」：
    #   tpi   = 高程 - 高斯(高程, 4 格 ≈ 60km)        >0 = 相对周边耸起的山块/山脊
    #   prom  = 高程 - 最小滤波(高程, ±3 格 ≈ 45km)    = 相对突出度（山峰越高越突出）
    #   slope = |∇高程|（米/格）                       = 坡度
    E = ndimage.gaussian_filter(e_cell.astype(np.float64), 0.8)
    tpi = E - ndimage.gaussian_filter(E, 4.0)
    Es = ndimage.gaussian_filter(E, 1.0)
    prom = Es - ndimage.minimum_filter(Es, size=7)
    gy, gx = np.gradient(E)
    slope = np.hypot(gx, gy)
    land_all = ~water
    # 山谷 / 平原：相对周边低（tpi<0），或又平又没突出度 -> 保住覆盖类型，绝不做崖
    valley = land_all & ((tpi < args.valley_tpi)
                         | ((slope < args.valley_slope) & (prom < 320.0)))
    # 山峰候选：相对耸起 + 有突出度 + 有坡度；再按起伏强度取前 peak-share（份额可控）
    cand = land_all & ~valley & (tpi > args.peak_tpi) & (prom > args.peak_prom) \
        & (slope > args.peak_slope)
    score = (1.4 * np.clip(tpi / 1200.0, 0, 1) + 1.0 * np.clip(prom / 2500.0, 0, 1)
             + 1.0 * np.clip(slope / 1500.0, 0, 1))
    # 强度场先平滑再排名：相邻格分数接近 -> 选出的是成片山脊，不是散点
    score = ndimage.gaussian_filter(score, 1.2) \
        + 0.02 * ndimage.gaussian_filter(rng.standard_normal((CX, CY)), 2.0)
    mtn = np.zeros((CX, CY), bool)
    ci = np.flatnonzero(cand.ravel())
    n_peak = int(args.peak_share * land_all.sum())
    if ci.size:
        k = min(n_peak, ci.size)
        mtn.ravel()[ci[np.argsort(-score.ravel()[ci])[:k]]] = True
    mtn = ndimage.binary_closing(mtn, np.ones((3, 3), bool), iterations=2)
    mtn &= ~valley                                  # 山谷里绝不放崖
    # 高海拔的谷地/盆地：荒地改成可住人的土（青藏高原的宽谷因此能住人、能建城）。
    # 只在高程 > 2000 m 处做 —— 否则平坦的沙漠（撒哈拉/塔克拉玛干）也会被当成"谷地"改土。
    land_t[valley & (e_cell > 2000.0) & np.isin(land_t, (WASTE, SAND))] = SOIL
    print(f"  山峰/山谷: 候选 {int(cand.sum())} 格 -> 崖(不可行走) {int(mtn.sum())} 格；"
          f"山谷/平原 {int(valley.sum())} 格；谷地改土 {int(((land_t == SOIL) & valley).sum())} 格")

    terrain[~water] = land_t[~water]
    terrain[mtn & ~water] = CLIFF

    # ---------- 河湖断面：河心(河8) / 湖心(河8或海9) 的两侧各加 1 格「岸(15)」----------
    # 岸(15)：isWater=false + moveable=false —— 陆地观感、不可行走，正好当河岸/湖岸；
    # 于是最小断面 = 岸 + 水 + 岸 = 3 格；湖泊以整圈岸线为界，进出靠港。
    if not args.no_bank:
        river_w = np.isin(terrain, (RIVER, STREAM))
        lake_w = (terrain == SEA) & (e_cell >= 0.0)       # 内陆湖 / 封闭海（排除全球海洋）
        water_in = river_w | lake_w
        bank = ndimage.binary_dilation(water_in, np.ones((3, 3), bool)) & ~water
        bank &= ~np.isin(terrain, (CITY, PORT, GATE))
        terrain[bank] = SHORE
        print(f"  河湖岸(15): {int(bank.sum())} 格围边（水体 河/川 {int(river_w.sum())} + "
              f"湖海 {int(lake_w.sum())} 格）")

    # ---------- 城市：从数据集按重要度分批放置 ----------
    # 数据源 Tools/lit_world_cities.json（约 200 座 2-3 世纪名城）；读不到则回退内置世界城表。
    # 分批 rank1 -> rank2 -> rank3：每批一个最小间距（越重要允许越近），
    # rank<=2 的名城有强保证（最多搜 30 格，必要时把崖削成土），确保落地。
    CITY_JSON = Path(r"D:\sango_infinity\Tools\lit_world_cities.json")
    src = []
    try:
        _dj = json.loads(CITY_JSON.read_text(encoding="utf-8"))
        src = [(c["name"], float(c["lon"]), float(c["lat"]), int(c.get("rank", 3)),
                c.get("region", "")) for c in _dj["cities"]]
        city_tag = f"{CITY_JSON.name}（{len(src)} 座）"
    except Exception as ex:                                       # noqa: BLE001
        print(f"  [城市数据] 读 {CITY_JSON} 失败（{ex}），回退内置世界城表")
        info = json.loads(WORLD_INFO.read_text(encoding="utf-8"))
        clo, chi = info["china_lo"], info["china_hi"]
        for c in json.loads(CITIES_GEO.read_text(encoding="utf-8")):
            src.append((c["name"], DEM_LON0 + DEM_DLON * (c["lo"] + clo),
                        DEM_LAT0 - DEM_DLAT * (c["hi"] + chi), 1, "中国"))
        for c in json.loads(CITIES_WORLD.read_text(encoding="utf-8")):
            src.append((c["name"], DEM_LON0 + DEM_DLON * c["lo"],
                        DEM_LAT0 - DEM_DLAT * c["hi"], c.get("rank", 3), "世界"))
        city_tag = "内置世界城表"

    def hdist(ax, ay, bx, by):
        """引擎的六边形距离（odd-q，轴向坐标 q=x / r=y-floor(x/2)）。"""
        qa, ra = ax, ay - (ax >> 1)
        qb, rb = bx, by - (bx >> 1)
        dq, dr = qa - qb, ra - rb
        return (abs(dq) + abs(dr) + abs(dq + dr)) // 2

    def city_cell(x, y):
        return int(terrain[x, y]) not in (SEA, RIVER, STREAM, PORT, GATE, CITY, CLIFF)

    def city_flat(x, y):
        return int(terrain[x, y]) in (GRASS, SOIL, MARSH)

    def snap(x, y, pred, rmax):
        """就近找满足 pred 的格（从 0 半径开始逐圈外扩）。"""
        if pred(x, y):
            return x, y
        for r in range(1, rmax + 1):
            xs = slice(max(0, x - r), min(CX, x + r + 1))
            ys = slice(max(0, y - r), min(CY, y + r + 1))
            sub = terrain[xs, ys]
            ok = np.zeros(sub.shape, bool)
            for i in range(sub.shape[0]):
                for j in range(sub.shape[1]):
                    ok[i, j] = pred(xs.start + i, ys.start + j)
            if ok.any():
                ii, jj = np.nonzero(ok)
                k = int(np.argmin((ii + xs.start - x) ** 2 + (jj + ys.start - y) ** 2))
                return int(ii[k] + xs.start), int(jj[k] + ys.start)
        return None

    print(f"  城市数据源: {city_tag}；城间距 >={args.city_gap} 格，太近按重要度裁低者")
    cities, used = [], []          # 两个列表一一对应（同序、同步增删）
    region_cnt, drop_cnt = {}, {}
    removed_pairs = []
    nudges, nudge_fail = [], []     # rank1 沿经纬微调的记录
    n_flatten = []                  # rank1/2 为钉在史实点而削平的崖

    def city_place(nm, lon, lat, rk, rg, src_tag, displace=True):
        """放一座城；与已有城距离 < city-gap 时，按重要度裁掉低的那个。

        displace=False（补充城池时）：只填空位，绝不挤走已落地的名城。"""
        if not (args.west < lon < args.east and args.south < lat < args.north):
            return False
        xt = int(np.clip(round((lon - args.west) / DLON - 0.5), 0, CX - 1))
        yt = int(np.clip(round((args.north - lat) / DLAT - 0.5), 0, CY - 1))
        if rk <= 2:
            # rank1/2 钉在史实点：只在 4 格内找可建地；找不到就把崖削平成土就地落地，
            # 不允许被"吸附"到十几格外的平地（那会挪掉几百公里）。
            p = snap(xt, yt, city_flat, 4) or snap(xt, yt, city_cell, 4)
            if p is None:
                p = snap(xt, yt, lambda a, b: int(terrain[a, b])
                         not in (SEA, RIVER, STREAM), 6)
                if p is None:
                    p = snap(xt, yt, lambda a, b: int(terrain[a, b])
                             not in (SEA, RIVER), 14)
                if p is not None and int(terrain[p[0], p[1]]) == CLIFF:
                    terrain[p[0], p[1]] = SOIL         # 削平崖面，钉在史实点
                    n_flatten.append(nm)
        else:
            p = snap(xt, yt, city_flat, 12) or snap(xt, yt, city_cell, 16)
        if p is None:
            drop_cnt[rk] = drop_cnt.get(rk, 0) + 1     # 地形上实在放不下
            return False
        x, y = p
        if (x, y) in used:
            return False
        # rank1 与已有 rank1 太近：沿真实经纬微调（<=2°，从最小位移试起），把两座名城拉开
        if rk == 1 and any(cities[i]["rank"] == 1 and hdist(x, y, a, b) < args.city_gap
                           for i, (a, b) in enumerate(used)):
            best = None
            for dist in (0.25, 0.5, 0.75, 1.0, 1.25, 1.5, 1.75, 2.0):
                for ux, uy in ((1, 0), (-1, 0), (0, 1), (0, -1),
                               (1, 1), (1, -1), (-1, 1), (-1, -1)):
                    lon2, lat2 = lon + ux * dist, lat + uy * dist
                    if not (args.west < lon2 < args.east
                            and args.south < lat2 < args.north):
                        continue
                    x2 = int(np.clip(round((lon2 - args.west) / DLON - 0.5), 0, CX - 1))
                    y2 = int(np.clip(round((args.north - lat2) / DLAT - 0.5), 0, CY - 1))
                    p2 = snap(x2, y2, city_flat, 6) or snap(x2, y2, city_cell, 8)
                    if p2 is None or p2 in used:
                        continue
                    if all(hdist(p2[0], p2[1], a, b) >= args.city_gap for a, b in used):
                        best = (p2[0], p2[1], lon2, lat2, dist)
                        break
                if best:
                    break
            if best:
                nudges.append((nm, round(lon, 3), round(lat, 3), round(best[2], 3),
                               round(best[3], 3), best[4]))
                x, y = best[0], best[1]
            else:
                nudge_fail.append(nm)
                lon2, lat2 = lon, lat
        close = [i for i, (a, b) in enumerate(used) if hdist(x, y, a, b) < args.city_gap]
        if close:
            if not displace:
                drop_cnt[rk] = drop_cnt.get(rk, 0) + 1  # 补充城池：只填空位
                return False
            # rank1 永不裁：rank1 之间太近也全部保留，只挤掉更低级的
            if rk > 1 and any(cities[i]["rank"] <= rk for i in close):
                drop_cnt[rk] = drop_cnt.get(rk, 0) + 1  # 同/更高重要度已占位 -> 裁掉这座
                return False
            for i in sorted([i for i in close if cities[i]["rank"] > rk], reverse=True):
                removed_pairs.append((cities[i]["name"], cities[i]["rank"], nm, rk))
                used.pop(i)
                cities.pop(i)
        used.append((x, y))
        cities.append({"name": nm, "x": x, "y": y, "rank": rk, "type": "city",
                       "region": rg, "source": src_tag})
        region_cnt[rg] = region_cnt.get(rg, 0) + 1
        return True

    for rank in (1, 2, 3, 4, 5):
        batch = [c for c in src if c[3] == rank]
        if not batch:
            continue
        n0 = len(cities)
        for nm, lon, lat, rk, rg in batch:
            if args.city_limit and len(cities) >= args.city_limit:
                break
            city_place(nm, lon, lat, rk, rg, "dataset")
        print(f"    rank{rank}: 数据集 {len(batch)} 座 -> 落地 {len(cities) - n0} 座")

    # 裁完不够目标数：用内置世界城表（未用过的名字、真实经纬）按同一规则补充
    n_ds = len(cities)
    if args.city_target and len(cities) < args.city_target:
        pool = []
        used_names = {c["name"] for c in cities} | {c[0] for c in src}
        try:
            for c in json.loads(CITIES_WORLD.read_text(encoding="utf-8")):
                if c["name"] in used_names:
                    continue
                pool.append((c["name"], DEM_LON0 + DEM_DLON * c["lo"],
                             DEM_LAT0 - DEM_DLAT * c["hi"], int(c.get("rank", 3)), "补充"))
        except Exception as ex:                                   # noqa: BLE001
            print(f"  [补充城池] 读 {CITIES_WORLD} 失败：{ex}")
        pool.sort(key=lambda t: t[3])
        for nm, lon, lat, rk, rg in pool:
            if len(cities) >= args.city_target:
                break
            city_place(nm, lon, lat, rk, rg, "supplement", displace=False)
        print(f"  补充城池: 数据集落地 {n_ds} 座 -> 按同一规则补到 {len(cities)} 座"
              f"（目标 {args.city_target}）")

    for c in cities:
        terrain[c["x"], c["y"]] = CITY
    if len(cities) > 1:
        _q = np.array([c["x"] for c in cities], np.int64)
        _r = np.array([c["y"] - (c["x"] >> 1) for c in cities], np.int64)
        _dq = _q[:, None] - _q[None, :]
        _dr = _r[:, None] - _r[None, :]
        _d = (np.abs(_dq) + np.abs(_dr) + np.abs(_dq + _dr)) // 2
        np.fill_diagonal(_d, 10 ** 9)
        dmin = float(_d.min())
    else:
        dmin = float("nan")
    n_sup = sum(1 for c in cities if c.get("source") == "supplement")
    print(f"  城市合计 {len(cities)} 座（数据集 {len(cities) - n_sup} + 补充 {n_sup}）；"
          f"最近两城 {dmin:.0f} 格（六边形距离；规则 >={args.city_gap}，rank1 之间允许更近）")
    if nudges or nudge_fail:
        print(f"  rank1 微调拉开: {len(nudges)} 座成功"
              + "".join(f"，{n}({a},{b}->{c},{d} 约{e}°)" for n, a, b, c, d, e in nudges[:5])
              + (f"；{len(nudge_fail)} 座 2° 内无合规点仍在原位: {nudge_fail[:4]}"
                 if nudge_fail else ""))
    else:
        print("  rank1 微调拉开: 无（没有 rank1 互相冲突）")
    print(f"  rank1/2 钉史实点: 为落地削平崖面 {len(n_flatten)} 处"
          + (f"（{n_flatten[:6]}）" if n_flatten else ""))
    print("  因间距/地形被裁: "
          + ("  ".join(f"rank{k} {v}" for k, v in sorted(drop_cnt.items())) or "无")
          + (f"；新来者顶掉旧城 {len(removed_pairs)} 例" if removed_pairs else ""))
    print("  分区分布: " + "  ".join(f"{k} {v}" for k, v in sorted(region_cnt.items())))

    # ---------- 港口(18) ----------
    # 1) 真实港口城市：就近落到「紧邻可航行水体（河8/海9）」的陆地/岸格上
    # 2) 每个够大的内陆水体（湖/封闭海）配 1 个港 —— 湖水可被利用，不会成为孤岛
    if not args.no_ports:
        nav = np.isin(terrain, (RIVER, SEA))
        cand = ndimage.binary_dilation(nav, np.ones((3, 3), bool)) & ~nav \
            & ~np.isin(terrain, (CITY, PORT, GATE))

        # 港必须离任何城市 >= --port-city-gap 格（默认 5：即 4 格内的港一律不放）
        _HEX = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
                1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}

        def _sh(m, u, v):
            """把 m 整体平移 (u, v)（越界丢弃）。"""
            out = np.zeros_like(m)
            src_x, dst_x = slice(max(0, -u), CX + min(0, -u)), slice(max(0, u), CX + min(0, u))
            src_y, dst_y = slice(max(0, -v), CY + min(0, -v)), slice(max(0, v), CY + min(0, v))
            out[dst_x, dst_y] = m[src_x, src_y]
            return out

        def hex_dilate(m, steps):
            """六边形距离 <= steps 的格（按 x 奇偶用不同邻接，与引擎/map_fixes 一致）。"""
            even = ((np.arange(CX) & 1) == 0)[:, None]
            for _ in range(steps):
                out = m.copy()
                e, o = m & even, m & ~even
                for u, v in _HEX[0]:
                    out |= _sh(e, u, v)
                for u, v in _HEX[1]:
                    out |= _sh(o, u, v)
                m = out
            return m

        city_m = np.zeros((CX, CY), bool)
        for c0 in cities:
            city_m[int(c0["x"]), int(c0["y"])] = True
        near_city = (hex_dilate(city_m, max(0, args.port_city_gap - 1)) if cities
                     else np.zeros((CX, CY), bool))
        n_blocked = int((cand & near_city).sum())
        cand &= ~near_city
        print(f"  [港] 离城 <{args.port_city_gap} 格可泊岸格 {n_blocked} 个已排除", flush=True)
        placed, taken = [], set()

        def uniq_name(nm):
            """保证港名唯一：与已有城市重名时补「港」，仍冲突则编号。"""
            if not nm:
                nm = f"港{len(placed) + 1}"
            base_nm = nm if nm.endswith("港") else nm + "港"
            nm, k = base_nm, 1
            while any(cc["name"] == nm for cc in cities):
                k += 1
                nm = f"{base_nm}{k}"
            return nm

        def put_port(x, y, nm="", rk=3):
            """放一个港(18)：须离任何城 >=port-city-gap、离任何已有港 >=port-port-gap。"""
            if 0 <= x < CX and 0 <= y < CY and cand[x, y] and (x, y) not in taken:
                if placed and any(hdist(x, y, a, b) < args.port_port_gap for a, b in placed):
                    return False                       # 港距太近：保留先占位（更重要）的那个
                terrain[x, y] = PORT
                cand[x, y] = False
                taken.add((x, y))
                placed.append((x, y))
                cities.append({"name": uniq_name(nm), "x": int(x), "y": int(y),
                               "rank": int(rk), "type": "port"})
                return True
            return False

        def nearest_cand(xt, yt, rmax=14):
            for r in range(0, rmax + 1):
                xs = slice(max(0, xt - r), min(CX, xt + r + 1))
                ys = slice(max(0, yt - r), min(CY, yt + r + 1))
                ii, jj = np.nonzero(cand[xs, ys])
                if ii.size:
                    k = int(np.argmin((ii + xs.start - xt) ** 2 + (jj + ys.start - yt) ** 2))
                    return int(ii[k] + xs.start), int(jj[k] + ys.start)
            return None

        port_src, port_tag = load_ports(args.port_rank_max)
        port_src = sorted(port_src, key=lambda t: t[3])   # 重要的先占位，太近的后来者删掉
        n_named = n_drop_named = 0
        for nm, lo, la, rk in port_src:
            if not (args.west < lo < args.east and args.south < la < args.north):
                continue
            xt = int(np.clip(round((lo - args.west) / DLON - 0.5), 0, CX - 1))
            yt = int(np.clip(round((args.north - la) / DLAT - 0.5), 0, CY - 1))
            p = nearest_cand(xt, yt)
            if p and put_port(p[0], p[1], nm, rk):
                n_named += 1
            else:
                n_drop_named += 1
        print(f"  港口数据源: {port_tag}；落在窗口内并成功放置 {n_named} 个命名港"
              + (f"，删掉离城太近/无合规岸线的 {n_drop_named} 个" if n_drop_named else ""))
        lab3, n3 = ndimage.label(nav, np.ones((3, 3), int))
        sizes3 = np.array(ndimage.sum(nav, lab3, range(1, n3 + 1)))
        n_lake_port = n_lake_skip = 0
        for i in np.nonzero(sizes3 >= args.port_min_cells)[0]:
            comp = lab3 == (i + 1)
            ii, jj = np.nonzero(ndimage.binary_dilation(comp, np.ones((3, 3), bool)) & cand)
            if ii.size == 0:
                continue
            flat = np.array([abs(e_cell[a, b]
                                 - e_cell[max(0, a - 1):a + 2, max(0, b - 1):b + 2].mean())
                             for a, b in zip(ii, jj)])
            base_cities = [cc for cc in cities if cc.get("type") == "city"] or cities
            ok_lake = False
            for k in np.argsort(flat)[:16].tolist():   # 从最平的岸格试起，取第一个合规的
                x0, y0 = int(ii[k]), int(jj[k])
                near = min(((cc["x"] - x0) ** 2 + (cc["y"] - y0) ** 2, cc["name"])
                           for cc in base_cities) if base_cities else (0, "内湖")
                if put_port(x0, y0, f"{near[1]}港", 3):
                    n_lake_port += 1
                    ok_lake = True
                    break
            if not ok_lake:
                n_lake_skip += 1
        print(f"  港口(18): 共 {len(placed)} 个（内陆水体配 {n_lake_port} 个）"
              + ("  " + " ".join(f"({a},{b})" for a, b in placed[:10]) if placed else ""))

    # ---------- 顶点高度 / 材质 / 水面 ----------
    cx_of_v = np.clip(np.round((np.arange(VX) - 2) / 4.0), 0, CX - 1).astype(np.int64)
    cy_of_v = np.zeros((VX, VY), np.int64)
    for vx in range(VX):
        cx = cx_of_v[vx]
        cy_of_v[vx] = np.clip(np.round((np.arange(VY) - 2 - 2 * (cx & 1)) / 4.0), 0, CY - 1)
    vt = terrain[cx_of_v[:, None], cy_of_v]                       # (VX,VY) 顶点所属格地形

    is_sea = vt == SEA
    is_riv = (vt == STREAM) | (vt == RIVER)
    land_v = ~(is_sea | is_riv)
    # 顶点级的山谷/山峰掩膜（格 -> 顶点，规则同 cell 级）
    valley_v = np.zeros((VX, VY), bool)
    valley_v[cx_of_v[:, None], cy_of_v] = valley[cx_of_v[:, None], cy_of_v]
    peak_v = np.zeros((VX, VY), bool)
    peak_v[cx_of_v[:, None], cy_of_v] = mtn[cx_of_v[:, None], cy_of_v]

    # 陆地高度 = 真实高程线性标定（海平面 10 / 5000m=200 / 上限 255）：
    #   h = 10 + 190 * e / 5000，clamp [10,255]；e < 0（海面以下）恒为 0。
    # 在此标定之上只做两件「起伏优化」（可用 1.0 / 0 关掉，回到严格真实高程）：
    #   relief-peak：山峰区把真实细部起伏放大（山更嶙峋）
    #   valley-flat：谷地/平原向 30km 尺度的谷底平面收敛（平原更平、可住人）
    hv = np.clip(10.0 + 190.0 * e_vert / 5000.0, 10.0, 255.0)

    # 先把水顶点填成「最近陆顶点的高度」——之后的起伏优化、山壁平滑、水体定高
    # 都在这个连续场上做，避免 0 值的水面把陆地拖出坑、把谷底压到海平面以下。
    idx = ndimage.distance_transform_edt(~land_v, return_distances=False, return_indices=True)
    hv = np.where(land_v, hv, hv[tuple(idx)])

    # 起伏优化（可关掉回到严格真实高程）：山峰细部放大；谷地/平原向 30km 谷底平面收敛
    if args.relief_peak != 1.0:
        fine = e_vert - ndimage.gaussian_filter(e_vert, 2.0)      # 米
        hv = hv + (args.relief_peak - 1.0) * (fine * 190.0 / 5000.0) * peak_v   # 米 -> 字节
    if args.valley_flat > 0:
        floor = ndimage.gaussian_filter(hv, 8.0)
        hv = np.where(valley_v, hv * (1.0 - args.valley_flat) + floor * args.valley_flat, hv)
    hv = np.where((vt == MARSH) & land_v, np.minimum(hv, 22.0), hv)

    # ---------- ① 山壁平滑：限步长扩散 ----------
    # 只在「相邻顶点步高差 > wall-step」处扩散，平原/谷地完全不动
    # （把那种垂直柱面的山壁削成陡坡）。
    if args.wall_step > 0 and args.wall_iters > 0:
        base = hv.copy()
        for _ in range(args.wall_iters):
            gy, gx = np.gradient(hv)
            m = np.clip((np.hypot(gx, gy) - args.wall_step) / max(args.wall_step, 1e-6), 0.0, 1.0)
            b = ndimage.gaussian_filter(hv, 1.0)
            hv = hv + args.wall_alpha * (b - hv) * m
        d = np.abs(hv[land_v] - base[land_v])
        ii, jj = np.argwhere(land_v)[int(np.argmax(d))]
        print(f"  山壁平滑: 步长上限 {args.wall_step:g} 字节 × {args.wall_iters} 轮"
              f"（松弛 {args.wall_alpha:g}）；陆地高度改动 p50 {np.percentile(d, 50):.1f} "
              f"p99 {np.percentile(d, 99):.1f} max {d.max():.0f} 字节"
              f"  @({int(ii)},{int(jj)}) {base[ii, jj]:.0f}->{hv[ii, jj]:.0f}")
    hv = np.clip(hv, 0.0, 255.0)

    h = np.zeros((VX, VY), np.uint8)
    w = np.zeros((VX, VY), np.uint8)
    h[land_v] = np.clip(np.round(hv[land_v]), 10, 255).astype(np.uint8)

    # ---------- ② 水体高度 ----------
    # 海（DEM < 0，与全球海洋连通）：河床 0 / 水面 11
    # 内陆水（高山湖泊、高原河流，DEM >= 0）：水面取**该顶点自身的真实高程**
    #   （ETOPO 对湖泊给的就是湖面高程、对河谷给的是谷底高程），再沿水面遮罩做归一化平滑
    #   （去掉逐点尖刺、保留下游整体下降），最后压到附近岸高 - 2 以下；河床 = 水面 - 11
    #   —— 与引擎规则同口径（RiverWaterByte = max(SeaWaterByte, LandGround-2)，
    #      RiverBedByte = max(0, RiverWaterByte - riverDrop)），水面恒比河床高 11 字节，
    #      保证 water > height（否则水面不显示）。
    inland = (~land_v) & (e_vert >= 0.0)
    ocean = (~land_v) & (e_vert < 0.0)
    # 水面初值：该顶点自身真实高程 -> 高度字节
    ws = np.clip(10.0 + 190.0 * e_vert / 5000.0, 0.0, 255.0)

    def water_spike(a):
        """水面尖刺指标：只在「水-水-水」三个相邻顶点之间取二阶差分（排除岸线干扰），
        返回 95 分位（字节）。越低越顺（河流本身的下游落差是线性的，不会计入）。"""
        out = []
        for ax in (0, 1):
            s0 = [slice(None)] * 2
            s1 = [slice(None)] * 2
            s2 = [slice(None)] * 2
            s0[ax], s1[ax], s2[ax] = slice(0, -2), slice(1, -1), slice(2, None)
            m = inland[tuple(s0)] & inland[tuple(s1)] & inland[tuple(s2)]
            if m.any():
                out.append(np.abs(a[tuple(s2)] - 2.0 * a[tuple(s1)]
                                  + a[tuple(s0)])[m])
        v = np.concatenate(out) if out else np.zeros(0)
        return float(np.percentile(v, 95)) if v.size else 0.0

    s_before = water_spike(ws)
    if args.water_smooth > 0 and args.water_iters > 0:
        wm = inland.astype(np.float64)
        for _ in range(args.water_iters):
            num = ndimage.gaussian_filter(ws * wm, 1.2)
            den = ndimage.gaussian_filter(wm, 1.2)
            sm = num / np.maximum(den, 1e-9)
            ws = np.where(inland, ws + args.water_smooth * (sm - ws), ws)
    # 不超过附近岸高 - 2（避免水面高出河岸成为水墙）
    bank = ndimage.minimum_filter(np.where(land_v, hv, 1e9), size=5)
    ws = np.clip(np.where(inland, np.minimum(ws, bank - 2.0), ws), 11.0, 255.0)
    wbed = np.maximum(0.0, ws - 11.0)
    print(f"  水面平滑: {args.water_smooth:g} × {args.water_iters} 轮；尖刺(水-水-水二阶差分 p95) "
          f"{s_before:.2f} -> {water_spike(ws):.2f} 字节；内陆水顶点 {int(inland.sum())}")
    h[inland] = np.clip(np.round(wbed[inland]), 0, 255).astype(np.uint8)
    w[inland] = np.clip(np.round(ws[inland]), 0, 255).astype(np.uint8)
    h[ocean] = 0
    w[ocean] = 11
    lut = np.zeros(32, np.uint8)
    for k, v in LAYER.items():
        lut[k] = v
    vtex = lut[np.clip(vt, 0, 31)]

    # ---------- 统计与导出 ----------
    cnt = Counter(terrain.ravel().tolist())
    n_cell = CX * CY
    river_share = (cnt.get(RIVER, 0) + cnt.get(STREAM, 0)) / n_cell
    sea_share = cnt.get(SEA, 0) / n_cell
    print(f"地形统计(cell): " + "  ".join(f"{k}:{v}({v/n_cell*100:.2f}%)"
                                        for k, v in sorted(cnt.items())))
    print(f"河+湖占比 {river_share*100:.2f}%   海占比 {sea_share*100:.2f}%   "
          f"崖占陆地 {cnt.get(CLIFF,0)/max(1,sum(v for k,v in cnt.items() if k not in (SEA,RIVER,STREAM)))*100:.1f}%")
    ref = SW / "data" / "sango" / "terrain.npy"
    if ref.exists():
        rt = np.load(ref)
        nref = rt.size
        print(f"参考 现有世界图 DS=2（窗口略大 -15~159E/-5.5~77N）: "
              f"河 {np.isin(rt,(RIVER,)).mean()*100:.2f}%  "
              f"川 {np.isin(rt,(STREAM,)).mean()*100:.2f}%  "
              f"合计 {(np.isin(rt,(RIVER,STREAM))).mean()*100:.2f}%  "
              f"海 {np.isin(rt,(SEA,)).mean()*100:.2f}%")
    print(f"顶点: 水面 {w.astype(bool).mean()*100:.2f}%  高度 p50={np.percentile(h,50):.0f} "
          f"p90={np.percentile(h,90):.0f} max={h.max()}")
    # 高度标定自检：抽查几个区域，核对「高程 m -> 高度字节」是否按 10+190*e/5000
    print("  标定自检（高程 m -> 高度字节）:")
    for nm, l0, l1, a0, a1 in (("青藏高原", 80, 95, 28, 36), ("华北平原", 112, 120, 32, 40),
                               ("塔里木盆地", 78, 88, 37, 42), ("恒河平原", 76, 90, 24, 28)):
        m = ((lon_v[:, None] >= l0) & (lon_v[:, None] <= l1)
             & (lat_v[None, :] >= a0) & (lat_v[None, :] <= a1) & land_v)
        if m.any():
            print(f"    {nm}: 高程 p50 {np.percentile(e_vert[m], 50):>5.0f} m -> 高度 p50 "
                  f"{np.percentile(h[m], 50):>3.0f}   （高程 max {e_vert[m].max():>5.0f} m"
                  f" -> 高度 max {h[m].max():>3.0f}）")
    # 山峰/山谷掩膜落盘（供选址与后续工具使用）
    np.save(OUT / "peak_mask.npy", mtn.astype(np.uint8))
    np.save(OUT / "valley_mask.npy", valley.astype(np.uint8))
    # 水体高度抽查：高山湖泊 / 高原河流的水面是否跟着地形走
    print("  水体高度抽查（顶点地形 / 河床字节 / 水面字节）:")
    for nm, lo, la in (("纳木错 90.6E,30.7N", 90.6, 30.7), ("青海湖 100.2E,36.9N", 100.2, 36.9),
                       ("贝加尔湖 107.5E,53.5N", 107.5, 53.5), ("里海 51.0E,42.0N", 51.0, 42.0),
                       ("雅鲁藏布 91.0E,29.5N", 91.0, 29.5), ("长江中游 114E,30.5N", 114.0, 30.5),
                       ("死海 35.5E,31.5N", 35.5, 31.5), ("太平洋 150E,30N", 150.0, 30.0)):
        iv = int(np.clip(round((lo - args.west) / DLON * 4.0), 0, VX - 1))
        jv = int(np.clip(round((args.north - la) / DLAT * 4.0), 0, VY - 1))
        print(f"    {nm}: 地形 {int(vt[iv, jv]):>2d}  河床 {int(h[iv, jv]):>3d}  "
              f"水面 {int(w[iv, jv]):>3d}   （该点高程对应陆高 "
              f"{int(np.clip(round(10 + 190 * e_vert[iv, jv] / 5000), 0, 255))}）")
    # 区域核对：#1 高原内部的宽谷是否可住；#2 欧洲平原 vs 真山脉
    print("  区域核对（山谷/平原 占比 vs 崖 占比）:")
    for nm, l0, l1, a0, a1 in (("青藏高原", 80, 95, 28, 36), ("欧洲 5~30E/42~58N", 5, 30, 42, 58),
                               ("阿尔卑斯 5~15E/44~48N", 5, 15, 44, 48),
                               ("华北平原", 112, 120, 32, 40)):
        mm = (lon_c[:, None] >= l0) & (lon_c[:, None] <= l1) & (lat_c >= a0) & (lat_c <= a1)
        if mm.any():
            print(f"    {nm}: 山谷/平原 {valley[mm].mean()*100:>5.1f}%   崖 {mtn[mm].mean()*100:>5.1f}%"
                  f"   谷地改土 {int((valley & (land_t == SOIL) & mm).sum()):>5d} 格")

    np.save(OUT / "terrain.npy", terrain)
    np.save(OUT / "vheight.npy", h)
    np.save(OUT / "vtex.npy", vtex)
    np.save(OUT / "vwater.npy", w)
    (OUT / "terrain.bin").write_bytes(terrain.tobytes())
    (OUT / "vheight.bin").write_bytes(h.tobytes())
    (OUT / "vtex.bin").write_bytes(vtex.tobytes())
    (OUT / "vwater.bin").write_bytes(w.tobytes())
    (OUT / "cities.json").write_text(json.dumps(cities, ensure_ascii=False, indent=1),
                                     encoding="utf-8")
    (OUT / "meta.json").write_text(json.dumps(
        {"ds": 1, "cellsX": CX, "cellsY": CY, "vertexX": VX, "vertexY": VY,
         "mapWidth": CX * 4, "mapHeight": CY * 4, "west": args.west, "east": args.east,
         "south": args.south, "north": args.north}, ensure_ascii=False, indent=1),
        encoding="utf-8")

    img = np.zeros((CY, CX, 3), np.uint8)
    disp = terrain.T
    for tid, col in PALETTE.items():
        img[disp == tid] = col
    im = Image.fromarray(img)
    im.save(OUT / "preview.png")
    # 中国细节图（2.5x）
    x0 = int((73 - args.west) / DLON); x1 = int((135 - args.west) / DLON)
    y0 = int((args.north - 54) / DLAT); y1 = int((args.north - 18) / DLAT)
    crop = im.crop((x0, y0, x1, y1)).resize(((x1 - x0) * 2, (y1 - y0) * 2), Image.NEAREST)
    crop.save(OUT / "preview_china.png")
    print(f"预览: {OUT/'preview.png'} {im.size}   {OUT/'preview_china.png'} {crop.size}")
    print(f"输出目录: {OUT}")


if __name__ == "__main__":
    main()
