"""把 san11-world 生成的世界地图（1800x1000 格）转成 Sango Infinity 需要的原始数组。

DS = 降采样倍数：
  DS=2 -> 900x500 格（默认，现有 world.bin 用的就是这套）
  DS=1 -> 1800x1000 格（1:1，不降采样）

映射：
  目标顶点 (vx,vy) <- 源像素 (Z = DS*vx + OFF, X = DS*vy + OFF)
  目标格 (x,y)     <- 源 hex（把目标格中心换算到源像素后取最近 hex，与顶点同一套换算）

引擎原先用 vertex_x_max 当行跨度，非正方形会下标碰撞；已改为 vertex_y_max，见 MapData.cs。
"""
import argparse
import json
from collections import Counter
from pathlib import Path

import numpy as np

SRC = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\world")
GEO = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data")
DEF_OUT = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\sango")
OFF = 110

# 光荣(SHEX)地形类型 -> Sango TerrainIds（整体 +1，但 关/港 顺序不同）
K2S = {0: 1, 1: 2, 2: 3, 3: 4, 4: 5, 5: 6, 6: 7, 7: 8, 8: 9, 9: 10, 10: 11,
       11: 12, 12: 13, 13: 14, 14: 15, 15: 16, 16: 17, 17: 19, 18: 18, 19: 20}
S_SEA, S_STREAM, S_RIVER, S_SHALLOW, S_CLIFF, S_CITY, S_SOIL = 9, 7, 8, 14, 16, 17, 2
SEA_BYTE, WATER_BYTE, RIVERBED_BYTE, SHALLOW_BYTE, SHALLOW_WATER = 0, 11, 1, 10, 12

PALETTE = {0: (0, 0, 0), 1: (126, 190, 80), 2: (196, 172, 112), 3: (232, 214, 152),
           4: (104, 150, 118), 5: (160, 60, 160), 6: (36, 104, 40), 7: (74, 136, 224),
           8: (48, 98, 206), 9: (28, 48, 150), 10: (150, 122, 82), 11: (226, 202, 64),
           12: (186, 124, 60), 13: (236, 236, 236), 14: (110, 200, 220), 15: (172, 172, 166),
           16: (96, 78, 64), 17: (228, 40, 40), 18: (200, 80, 80), 19: (200, 60, 60),
           20: (204, 170, 64)}


def nearest_hex(Z, X, R, C):
    """源像素坐标 -> 最近 hex（与 make_world 同一套换算）。Z/X 可广播。"""
    lo = np.clip(np.floor((Z - 114) / 4.0 + 0.5).astype(np.int64), 0, R - 1)
    hi = np.clip(np.floor((X - 114 - 2 * (lo & 1)) / 4.0 + 0.5).astype(np.int64), 0, C - 1)
    return lo, hi


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--ds", type=int, default=2, choices=(1, 2), help="降采样倍数")
    ap.add_argument("--out", default=str(DEF_OUT), help="输出目录")
    ap.add_argument("--preview", default="", help="预览图路径（默认写到输出目录）")
    ap.add_argument("--diag", action="store_true", help="只做诊断，不写文件")
    args = ap.parse_args()

    DS = args.ds
    OUT = Path(args.out)
    CX, CY = 1800 // DS, 1000 // DS          # x=东西, y=南北
    VX, VY = CX * 4 + 1, CY * 4 + 1
    print(f"DS={DS}  格 {CX}x{CY}  顶点 {VX}x{VY}  输出 {OUT}", flush=True)

    types = np.load(SRC / "world_types.npy")                 # (1800,1000) 光荣编号
    hgt = np.load(SRC / "world_height.npy", mmap_mode="r")   # (7425,4225)
    mat = np.load(SRC / "world_mat.npy", mmap_mode="r")
    R, C = types.shape
    print(f"源: types{types.shape} height{hgt.shape} mat{mat.shape}", flush=True)

    # ---- 每个源顶点对应的源 hex 地形
    Zs = DS * np.arange(VX, dtype=np.int64) + OFF
    Xs = DS * np.arange(VY, dtype=np.int64) + OFF
    assert Zs.max() < hgt.shape[0] and Xs.max() < hgt.shape[1], (Zs.max(), Xs.max())
    lo, hi = nearest_hex(Zs[:, None], Xs[None, :], R, C)
    vt = types[lo, hi]
    del lo, hi

    OUT.mkdir(parents=True, exist_ok=True)

    # ---- 1) 地格地形：目标格中心换算到源像素后取最近 hex
    xs = np.arange(CX, dtype=np.int64)[:, None]
    ys = np.arange(CY, dtype=np.int64)[None, :]
    Zc = DS * (4 * xs + 2) + OFF
    Xc = DS * (4 * ys + 2 * (xs & 1) + 2) + OFF
    s_lo, s_hi = nearest_hex(Zc, Xc, R, C)
    src_cell = types[s_lo, s_hi]                 # (CX, CY)
    del s_lo, s_hi, Zc, Xc
    lut = np.zeros(256, np.uint8)
    for k, v in K2S.items():
        lut[k] = v
    cells = lut[src_cell]
    print("目标地形统计:", sorted(Counter(cells.ravel().tolist()).items()), flush=True)

    # ---- 2) 顶点
    h = np.asarray(hgt[np.ix_(Zs, Xs)]).astype(np.uint8)     # (VX, VY)
    m = np.asarray(mat[np.ix_(Zs, Xs)]).astype(np.uint8)
    t = vt
    assert h.shape == (VX, VY), h.shape
    assert t.shape == (VX, VY), t.shape

    for tid, name in ((8, "海"), (6, "川"), (7, "河"), (15, "山"), (0, "草")):
        sel = t == tid
        if sel.any():
            v = h[sel]
            print(f"  地形{tid}({name}) 顶点高度 min={v.min()} p50={np.percentile(v, 50):.0f} max={v.max()}", flush=True)
    if args.diag:
        return

    water = np.zeros((VX, VY), np.uint8)
    h[t == 8] = SEA_BYTE
    h[(t == 6) | (t == 7)] = RIVERBED_BYTE
    h[t == 13] = SHALLOW_BYTE
    water[t == 8] = WATER_BYTE
    water[(t == 6) | (t == 7)] = WATER_BYTE
    water[t == 13] = SHALLOW_WATER
    vtex = np.minimum(m, 35)
    np.save(OUT / "vheight.npy", h)
    np.save(OUT / "vtex.npy", vtex)
    np.save(OUT / "vwater.npy", water)
    print(f"顶点: {VX}x{VY}= {VX * VY}  水面顶点占比 {water.astype(bool).mean():.3f}", flush=True)

    # ---- 3) 城市
    # 注意：cities_georef.json 的 lo/hi 是「相对中国块」的坐标，必须加 china_lo/china_hi
    # 才是世界图坐标（见 worldgeo.WorldGeo.__init__）；cities_world.json 存的已是绝对坐标。
    info = json.loads((SRC / "world_info.json").read_text(encoding="utf-8"))
    clo, chi = info["china_lo"], info["china_hi"]
    src_cities = []
    for c in json.loads((GEO / "cities_georef.json").read_text(encoding="utf-8")):
        src_cities.append({"name": c["name"], "lo": c["lo"] + clo, "hi": c["hi"] + chi, "rank": 1})
    for c in json.loads((SRC / "cities_world.json").read_text(encoding="utf-8")):
        src_cities.append({"name": c["name"], "lo": c["lo"], "hi": c["hi"], "rank": c.get("rank", 3)})
    print(f"中国块偏移 ({clo},{chi})：三国城池已换算到世界图坐标", flush=True)
    out = []
    for c in src_cities:
        x = int(np.floor((c["lo"] + 0.5) / DS))
        y = int(np.floor((c["hi"] + 0.5) / DS))
        if 0 <= x < CX and 0 <= y < CY:
            out.append({"name": c["name"], "x": x, "y": y, "rank": c["rank"]})

    # 降采样会漏掉细小的「都市」格：按城市坐标强制盖印
    before = int((cells == S_CITY).sum())
    for c in out:
        if cells[c["x"], c["y"]] in (S_SEA, S_STREAM, S_RIVER, S_SHALLOW, S_CLIFF):
            cells[c["x"], c["y"]] = S_SOIL
        cells[c["x"], c["y"]] = S_CITY
    np.save(OUT / "terrain.npy", cells)
    print(f"都市格: {before} -> {int((cells == S_CITY).sum())}；城市 {len(out)}/{len(src_cities)} 座在界内", flush=True)
    (OUT / "cities.json").write_text(json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")

    # ---- 预览（行=南北北在上, 列=东西西在左）
    scale = max(1, 1800 // CX)
    img = np.zeros((CY, CX, 3), np.uint8)
    disp = cells.T                                # (CY, CX) -> [y][x]
    for tid, col in PALETTE.items():
        img[disp == tid] = col
    from PIL import Image, ImageDraw
    im = Image.fromarray(img).resize((CX * scale, CY * scale), Image.NEAREST)
    d = ImageDraw.Draw(im)
    for c in out:
        px, py = c["x"] * scale, c["y"] * scale
        d.ellipse([px - 3, py - 3, px + 3, py + 3], fill=(255, 255, 0))
    pv = Path(args.preview) if args.preview else (OUT / "preview.png")
    im.save(pv)
    print(f"预览图已写 {pv} {im.size}", flush=True)

    # ---- 无头部的裸 bin，供 Unity 侧 C# 读取
    (OUT / "terrain.bin").write_bytes(cells.tobytes())
    (OUT / "vheight.bin").write_bytes(h.tobytes())
    (OUT / "vtex.bin").write_bytes(vtex.tobytes())
    (OUT / "vwater.bin").write_bytes(water.tobytes())
    (OUT / "meta.json").write_text(json.dumps(
        {"ds": DS, "cellsX": CX, "cellsY": CY, "vertexX": VX, "vertexY": VY,
         "mapWidth": CX * 4, "mapHeight": CY * 4}, ensure_ascii=False, indent=1), encoding="utf-8")
    print("裸 bin 已写", flush=True)


if __name__ == "__main__":
    main()
