"""用 data/sango/ 的原始数组生成 Sango Infinity 的地图 .bin（version 10）。

做法：自己写 mapGrid + mapData 段，尾部（mapLayer/mapTerrain/light/fog/baseColor/
skybox/camera/models/labels）直接沿用 DefaultMap.bin 的字节，保证格式完全一致；
之后在 Unity 里 loadMap 让引擎自己重算顶点坐标/法线/水面位置。
"""
import argparse
import heapq
import json
import struct
from collections import deque
from pathlib import Path

import numpy as np

SNG = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\sango")
DEF = Path(r"d:/sango_infinity/Build/Content/Map/DefaultMap.bin")
OUT = Path(r"d:/sango_infinity/Build/Content/Map/world.bin")
SCEN = Path(r"d:/sango_infinity/Build/Content/Scenario")

VERSION = 10
WORK = "world"
GRID_SIZE = 20
QUAD_SIZE = 5
GRID_TEX = "grid"

# Sango 地形
S_SEA, S_STREAM, S_RIVER, S_SHALLOW, S_CLIFF, S_CITY = 9, 7, 8, 14, 16, 17
WATER = (S_SEA, S_STREAM, S_RIVER, S_SHALLOW)
# TerrainTypes.json 里 moveable = true 的地形。注意 海(9)/河(8)/浅滩(14) 都是可行走的
# （船能走），所以它们也必须有 areaId。
MOVEABLE = {1, 2, 3, 4, 5, 6, 8, 9, 10, 11, 12, 13, 14, 17, 18, 19, 20}
# 不可行走地形的穿越代价：不能当墙（会把地图切成碎块、块内可行走格永远没归属），
# 只能让 Dijkstra「借道」时付出更高代价。
BYPASS_COST = 6.0


def read_str(f):
    n, sh = 0, 0
    while True:
        v = f.read(1)[0]
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    return f.read(n).decode("utf-8", "replace")


def enc_str(s):
    b = s.encode("utf-8")
    n, out = len(b), bytearray()
    while True:
        v = n & 0x7F
        n >>= 7
        out.append(v | 0x80 if n else v)
        if not n:
            break
    return bytes(out) + b


def default_tail(target_w=None, target_h=None):
    """解析 DefaultMap.bin 的尾部段，返回 (拼接好的尾部字节, mapWidth, mapHeight)。

    尾部 = mapLayer + mapTerrain + mapLight + mapFog + mapBaseColor + mapSkyBox
           + mapCamera + mapModels + mapLabelSet（见 MapRender.LoadMap 的读取顺序）。

    其中 mapModels / mapLabelSet **必须置空**：DefaultMap 里带的是它自己的模型，
    直接抄过来挂到别的尺寸的地图上，MapModels.OnLoad 会因模型越界/异步未就绪而崩
    （MapObject.position 空引用）。其余段原样保留，保证观感与 DefaultMap 一致。

    唯一的例外是 mapSkyBox 的矩形：它是「相机位于世界哪个区域 -> 用哪张天空贴图」的
    划分（MapSkyBox.Update 用相机 (z,x) 去 Contains 匹配），单位是世界坐标
    （= mapWidth/Height * quadSize），**必须随地图尺寸等比缩放**。
    原版 DefaultMap 是世界 5120x5120 上 3x3 的 1706 网格（1706*3≈5120，刚好铺满），
    照抄到 36000x20000 的世界图就只覆盖左上角 14% x 26%，其余地方天空盒没有贴图
    （MapSkyBox.OnLoad 会先 allAreas.Clear()，把 Create() 里的白色兜底区域清掉）。
    传入 target_w/target_h（目标地图的 mapWidth/mapHeight）即自动缩放。
    """
    with open(DEF, "rb") as f:
        assert struct.unpack("<i", f.read(4))[0] == VERSION
        read_str(f)
        w, h = struct.unpack("<ii", f.read(8))
        gs = struct.unpack("<i", f.read(4))[0]
        f.read((w * 5 // gs) * (h * 5 // gs) * 7)      # mapGrid 格子数据
        read_str(f)                                     # mapGrid 贴图名
        f.read(4)                                       # mapData.quadSize
        f.read((w + 1) * (h + 1) * 3)                   # mapData 顶点

        # 1) MapLayer: int32 count; count x { bool isLit; float2 textureScale;
        #                                   4 x (diffuse, normal, mask) 字符串 }
        buf = bytearray()
        buf += f.read(4)
        n = struct.unpack("<i", buf[-4:])[0]
        for _ in range(n):
            buf += f.read(9)
            for _i in range(4):
                for _k in range(3):
                    buf += enc_str(read_str(f))
        layer = bytes(buf)

        # 2) MapTerrain: int32 cellSize
        terrain = f.read(4)

        # 3) MapLight: 4 季 x (dir.xyz, color.rgb, intensity, shadow.rgb, shadowStrength) = 4*11*4
        light = f.read(4 * 11 * 4)

        # 4) MapFog: 4 季 x (color.rgb, start, end, density) = 4*6*4
        fog = f.read(4 * 6 * 4)

        # 5) MapBaseColor: versionCode > 5 时一个字节都不写
        basecolor = b""

        # 6) MapSkyBox: float2 blend; int32 areaCount; areaCount x { Rect(16) + 4 个字符串 }
        #    Rect 是世界坐标，必须按「目标地图 / DefaultMap」的尺寸比例缩放（见 docstring）
        sx = (target_w / float(w)) if target_w else 1.0
        sy = (target_h / float(h)) if target_h else 1.0
        buf = bytearray(f.read(8))
        ac = struct.unpack("<i", f.read(4))[0]
        buf += struct.pack("<i", ac)
        for _ in range(ac):
            rx, ry, rw, rh = struct.unpack("<ffff", f.read(16))
            buf += struct.pack("<ffff", rx * sx, ry * sy, rw * sx, rh * sy)
            for _j in range(4):
                buf += enc_str(read_str(f))
        skybox = bytes(buf)

        # 7) MapCamera: versionCode >= 9 只写 float safeBoder
        camera = f.read(4)

        # 8) MapModels: 丢弃原模型，写 0 个。单模型 52 字节（含 v>=4 的 bindId）
        mc = struct.unpack("<i", f.read(4))[0]
        f.read(mc * 52)
        models = struct.pack("<i", 0)

        # 9) MapLabelSet: 丢弃原标注，写 0 个。单标注 19 字节 + 字符串
        lc = struct.unpack("<i", f.read(4))[0]
        for _ in range(lc):
            read_str(f)
            f.read(12 + 3 + 4)
        labels = struct.pack("<i", 0)

        rest = f.read()
        assert rest == b"", f"尾部段解析后仍剩 {len(rest)} 字节，格式假设有误"
        print(f"尾部段: layer={len(layer)} terrain={len(terrain)} light={len(light)} "
              f"fog={len(fog)} skybox={len(skybox)} camera={len(camera)} "
              f"models(原{mc}->0) labels(原{lc}->0)", flush=True)
        return layer + terrain + light + fog + basecolor + skybox + camera + models + labels, w, h


NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}


def assign_areas(terrain, seeds):
    """多源 Dijkstra：每格归给移动代价最小的城市，返回 areaId 网格。

    seeds = [(x, y, city_id), ...]，必须用**剧本里的城 id**（引擎是按
    `scenario.citySet.Get(areaId)` 反查的）。种子要包含全部城市：都市 + 港(18) + 关所(19)。
    DefaultMap.bin 里港/关并非都挂在母城名下 —— 35 个港映射到 25 个不同 areaId，
    还存在「areaId=65 的领地 152 格、含 1 个港关格、0 个都市格」这种港关自己当种子的情况。

    另外要让**所有可行走格**都有归属，不能把不可行走地形当墙、也不能截断搜索距离：
      - 海(9)/河(8)/浅滩(14) 在 TerrainTypes.json 里 moveable=true（船能走），
        旧实现却把它们当墙，导致整片海域 80 万格没有 areaId；
      - 崖(16)/川(7)/岸(15) 占全图 20%+，当墙会把地图切成大量互不相连的碎块，
        块内若没有城市，那里的可行走格同样永远拿不到归属；
      - 旧实现还有 MAX_COST=90 的截断，离城偏远的内陆地块（森/荒地/湿地…）也落空。
    改成「可以穿过、但不可行走地形代价高」+ 不做距离截断，保证全图收敛到有归属。
    """
    cx_n, cy_n = terrain.shape
    area = np.zeros((cx_n, cy_n), np.uint16)
    dist = np.full((cx_n, cy_n), np.inf, np.float32)
    heap = []
    passable = np.isin(terrain, list(MOVEABLE))
    for x, y, cid in seeds:
        if 0 <= x < cx_n and 0 <= y < cy_n and dist[x, y] > 0:
            dist[x, y] = 0.0
            area[x, y] = cid                      # 种子格自身也归该城
            heapq.heappush(heap, (0.0, x, y, cid))
    while heap:
        d, x, y, cid = heapq.heappop(heap)
        if d > dist[x, y] + 1e-6:
            continue
        for dx, dy in NEIGH[x & 1]:
            nx, ny = x + dx, y + dy
            if nx < 0 or ny < 0 or nx >= cx_n or ny >= cy_n:
                continue
            cost = 1.0 if passable[nx, ny] else BYPASS_COST
            nd = d + cost
            if nd < dist[nx, ny]:
                dist[nx, ny] = nd
                area[nx, ny] = cid
                heapq.heappush(heap, (nd, nx, ny, cid))
    return area


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=str(SNG), help="sango_convert.py 的输出目录")
    ap.add_argument("--out", default=str(OUT), help="目标 .bin 路径")
    ap.add_argument("--work", default=WORK, help="地图标识(WorkContent)，决定 Assets/Map/<work>/ 底图目录")
    ap.add_argument("--scenario", default="",
                    help="剧本 json。用来取全部城市（含港/关）当领地种子；默认 Scenario_<work>.json")
    args = ap.parse_args()
    sng = Path(args.src)
    out_path = Path(args.out)
    work = args.work

    terrain = np.load(sng / "terrain.npy")          # (CX,CY) uint8
    vheight = np.load(sng / "vheight.npy")
    vtex = np.load(sng / "vtex.npy")
    vwater = np.load(sng / "vwater.npy")
    cities = json.loads((sng / "cities.json").read_text(encoding="utf-8"))
    meta = json.loads((sng / "meta.json").read_text(encoding="utf-8"))
    cxn, cyn = terrain.shape
    vxn, vyn = vheight.shape
    print(f"格 {cxn}x{cyn}  顶点 {vxn}x{vyn}  城市 {len(cities)}", flush=True)

    # 领地种子 = 剧本里的全部城市（都市 + 港 + 关所）。
    # 必须用剧本城的真实 id：引擎是 scenario.citySet.Get(areaId) 反查城市，
    # 用别的编号会查不到城市（就是那句被注释掉的「可以行走,但是所属城市找不到!!」）。
    seeds = None
    # 剧本默认按「输出文件名」推导：Map/world_full.bin -> Scenario_world_full.json。
    # 不能用 --work（它的默认值是 world），否则 1:1 图会拿到 900x500 剧本的半比例城市坐标，
    # 领地会整体错位。
    cand = [Path(args.scenario)] if args.scenario else [SCEN / f"Scenario_{out_path.stem}.json",
                                                        SCEN / f"Scenario_{work}.json"]
    scen_path = next((p for p in cand if p.exists()), cand[0])
    if scen_path.exists():
        sd = json.loads(scen_path.read_text(encoding="utf-8"))
        seeds = []
        kinds = {}
        for k, v in sd.get("citySet", {}).items():
            cid = int(k)
            if cid <= 0:
                continue
            x, y = int(v.get("x", -1)), int(v.get("y", -1))
            if 0 <= x < cxn and 0 <= y < cyn:
                seeds.append((x, y, cid))
                bt = int(v.get("BuildingType", 1))
                kinds[bt] = kinds.get(bt, 0) + 1
        print(f"领地种子: {len(seeds)} 座（按 BuildingType 分布 {kinds}，1=都市 2=关 3=港），"
              f"来自 {scen_path.name}", flush=True)
    if not seeds:
        print(f"[提示] 未找到剧本 {scen_path}，退回只用 cities.json 的 {len(cities)} 座都市", flush=True)
        seeds = [(c["x"], c["y"], i + 1) for i, c in enumerate(cities)]

    area = assign_areas(terrain, seeds)

    # 内政地：interior.npy 里 0 = 非内政地，>0 = 所属城市 id。
    # 要进 City.interiorCellList 必须同时成立两件事：
    #   1) 该格 terrainState 置内政位（下面 cell[...,1] 写 4）
    #   2) 该格 areaId 就是那座城（interiorCellList 从「本城 areaId 的格子」里筛 IsInterior）
    # 所以内政格要强制归属：它离城 2~8 格，可能落在 Dijkstra 的 MAX_COST=90 之外
    # 而 areaId=0，那样它不属于任何城、槽位白给。
    interior_owner, imask = None, None
    interior_path = sng / "interior.npy"
    if interior_path.exists():
        interior_owner = np.load(interior_path).astype(np.int32)
        if interior_owner.shape == (cxn, cyn):
            imask = interior_owner > 0
            no_area = int((imask & (area == 0)).sum())
            area = np.where(imask, interior_owner, area).astype(area.dtype)
            print(f"内政地: {int(imask.sum())} 格；强制归属后修正 areaId=0 的 {no_area} 格", flush=True)
        else:
            print(f"[警告] interior.npy 尺寸 {interior_owner.shape} 与地形 {(cxn, cyn)} 不符，已忽略", flush=True)
            interior_owner = None
    else:
        print("内政地: 未找到 interior.npy，请先运行 sango_map_fixes.py", flush=True)

    owned = int((area > 0).sum())
    pmask = np.isin(terrain, list(MOVEABLE))
    miss = int((pmask & (area == 0)).sum())
    print(f"领地: 已归属 {owned} / {area.size} 格，最大城市 Id {int(area.max())}；"
          f"可行走格 {int(pmask.sum())}，其中无归属 {miss}", flush=True)
    if miss:
        u, c = np.unique(terrain[pmask & (area == 0)], return_counts=True)
        print(f"  [警告] 仍有可行走格没有 areaId，按地形: {dict(zip(u.tolist(), c.tolist()))}", flush=True)
    np.save(sng / "area.npy", area)
    (sng / "area.bin").write_bytes(area.tobytes())

    tail, dw, dh = default_tail(cxn * 4, cyn * 4)
    print(f"DefaultMap 尾部段 {len(tail)} 字节（源 {dw}x{dh} -> 本图 {cxn * 4}x{cyn * 4}，"
          f"天空盒矩形已同比缩放）", flush=True)

    out = bytearray()
    out += struct.pack("<i", VERSION)
    out += enc_str(work)
    out += struct.pack("<ii", cxn * 4, cyn * 4)
    out += struct.pack("<i", GRID_SIZE)
    # 每格: terrainType(u8) + terrainState(i32) + areaId(u16)，x 外层 y 内层
    cell = np.zeros((cxn, cyn, 7), np.uint8)
    cell[..., 0] = terrain
    cell[..., 1:5] = np.zeros((cxn, cyn, 4), np.uint8)
    if imask is not None:
        # terrainState 是 int32 小端（字节 1..4），内政位 = 1 << GridState.Interior
        # = 1 << 2 = 4。City.interiorCellList 就是从这里筛出来的。
        cell[..., 1] = np.where(imask, 4, 0).astype(np.uint8)
    cell[..., 5] = (area & 0xFF).astype(np.uint8)
    cell[..., 6] = (area >> 8).astype(np.uint8)
    out += cell.tobytes()
    out += enc_str(GRID_TEX)
    out += struct.pack("<i", QUAD_SIZE)
    # 每顶点: height(u8) + textureIndex(u8) + water(u8)，x 外层 y 内层
    vert = np.empty((vxn, vyn, 3), np.uint8)
    vert[..., 0] = vheight
    vert[..., 1] = vtex
    vert[..., 2] = vwater
    out += vert.tobytes()
    out += tail

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_bytes(bytes(out))
    print(f"已写入 {out_path}（{len(out) / 1048576:.1f} MB）", flush=True)
    # 回读自检
    with open(out_path, "rb") as f:
        assert struct.unpack("<i", f.read(4))[0] == VERSION
        assert read_str(f) == work
        w, h = struct.unpack("<ii", f.read(8))
        gs = struct.unpack("<i", f.read(4))[0]
        print(f"自检: {w}x{h} px, gridSize={gs}, 格 {w * 5 // gs}x{h * 5 // gs}", flush=True)


if __name__ == "__main__":
    main()
