# -*- coding: utf-8 -*-
"""sango_skybox.py —— 填充/修正地图 .bin 的 mapSkyBox 段（天空盒贴图区域）。

背景（MapSkyBox.cs）：
    .bin 里存的是「区域矩形 -> 4 个季节贴图名」的列表，
    运行时 MapSkyBox.Update() 取相机位置 (z, x) 去 Contains 匹配，命中哪个区域就用哪个贴图。
    OnLoad 会先 allAreas.Clear()（把 Create() 里那块白色兜底区域清掉），
    所以**如果矩形只覆盖地图一角，其余地方天空盒就没有贴图**。

    原版 DefaultMap 是世界 5120x5120、9 块 1706x1706 的 3x3 网格（1706*3≈5120）。
    世界图的世界尺寸是 36000x20000（= mapWidth/Height * quadSize），
    但它的矩形是从原版照抄的 —— 只铺满左上角 5118x5118，这正是「天空盒没贴图」的原因。

段格式：
    float sky_blend_start
    float sky_blend_end
    int32 area_count
    area_count x { float x, y, w, h ; 4 个季节字符串(顺序 = MapRender.SeasonNames = 秋春夏冬) }

本工具做的事：保持贴图名与区域顺序不变，只把矩形按检测到的网格重铺满整张地图。

用法：
    python sango_skybox.py --bin Map/xworld_full.bin --report
    python sango_skybox.py --bin Map/xworld_full.bin --tile --apply
    python sango_skybox.py --bin Map/x.bin --copy-from Map/DefaultMap.bin --tile --apply
"""
import argparse
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin, Reader, Writer   # noqa: E402


def read_skybox(buf, start, end):
    r = Reader(buf)
    r.o = start
    blend_start = r.f32()
    blend_end = r.f32()
    n = r.i32()
    areas = []
    for _ in range(n):
        x, y, w, h = r.f32(), r.f32(), r.f32(), r.f32()
        names = [r.string() for _ in range(4)]
        areas.append({"rect": (x, y, w, h), "names": names})
    if r.o != end:
        raise ValueError(f"mapSkyBox 段解析长度不符：解析到 {r.o}，段尾 {end}")
    return blend_start, blend_end, areas


def write_skybox(blend_start, blend_end, areas):
    w = Writer()
    w.f32(blend_start)
    w.f32(blend_end)
    w.i32(len(areas))
    for a in areas:
        x, y, ww, hh = a["rect"]
        w.f32(x); w.f32(y); w.f32(ww); w.f32(hh)
        for nm in a["names"]:
            w.string(nm if nm else "")
    return w.bytes()


def detect_grid(areas):
    """从现有矩形的 x/y 取值反推网格：返回 (cols, rows, {(col,row): area_index})"""
    xs = sorted(set(round(a["rect"][0], 3) for a in areas))
    ys = sorted(set(round(a["rect"][1], 3) for a in areas))
    xmap = {v: i for i, v in enumerate(xs)}
    ymap = {v: i for i, v in enumerate(ys)}
    cell = {}
    for i, a in enumerate(areas):
        cell[(xmap[round(a["rect"][0], 3)], ymap[round(a["rect"][1], 3)])] = i
    return len(xs), len(ys), cell


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--copy-from", default=None, help="从另一张地图复制区域列表(贴图名)")
    ap.add_argument("--tile", action="store_true", help="按网格重铺满整张地图")
    ap.add_argument("--out", default=None)
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    p = Path(args.bin_path)
    if not p.exists():
        print(f"[错误] 找不到 {p}")
        return
    d = parse_bin(str(p))
    s, e = d["tail"]["skyBox"]
    world_z = d["mapWidth"] * d["quadSize"]
    world_x = d["mapHeight"] * d["quadSize"]
    print(f"文件: {p}")
    print(f"  格 {d['cellsX']}x{d['cellsY']}  quadSize={d['quadSize']}"
          f"  世界尺寸(mapWidth*quad) = {world_z} x {world_x}")
    print(f"  mapSkyBox 段: 偏移 {s}..{e}（{e - s} 字节）")

    if args.copy_from:
        d2 = parse_bin(args.copy_from)
        s2, e2 = d2["tail"]["skyBox"]
        bs, be, areas = read_skybox(d2["buf"], s2, e2)
        print(f"  从 {Path(args.copy_from).name} 复制 {len(areas)} 个区域"
              f"（blend {bs} ~ {be}）")
    else:
        bs, be, areas = read_skybox(d["buf"], s, e)

    print(f"  sky_blend_start/end = {bs} / {be}")
    print(f"  area_count = {len(areas)}")

    if not areas:
        print("  [提示] 区域数为 0，无法铺砖；请用 --copy-from 指定一张有天空盒的地图。")
        return

    cols, rows, cell = detect_grid(areas)
    print(f"  检测到网格: {cols} 列 x {rows} 行（按 rect.x 为列、rect.y 为行）")

    # 现有矩形覆盖范围
    xs = sorted(set(a["rect"][0] for a in areas))
    ys = sorted(set(a["rect"][1] for a in areas))
    w0 = areas[0]["rect"][2]
    h0 = areas[0]["rect"][3]
    cov_z = max(xs) + w0
    cov_x = max(ys) + h0
    print(f"  当前覆盖: z 0..{cov_z:.0f} / x 0..{cov_x:.0f}"
          f"  → 占全图 {cov_z / world_z:.1%} x {cov_x / world_x:.1%}")

    # 列出区域（简表）
    for i, a in enumerate(areas):
        r = a["rect"]
        nm = a["names"][2] or a["names"][0] or "?"
        print(f"    [{i}] x{r[0]:>8.1f} y{r[1]:>8.1f} w{r[2]:>8.1f} h{r[3]:>8.1f}   夏={nm!r}")

    if not args.tile:
        if args.copy_from and args.apply:
            pass
        else:
            print("\n（未指定 --tile，不改变矩形；加 --tile --apply 才会按网格铺满）")
            return

    # 重铺：列宽 = world_z/cols，行高 = world_x/rows
    tw = world_z / float(cols)
    th = world_x / float(rows)
    new_areas = []
    for a in areas:
        new_areas.append({"rect": (0.0, 0.0, tw, th), "names": list(a["names"])})
    # 用检测到的 (col,row) 放回正确位置
    placed = [None] * len(areas)
    idx_of_cell = {v: k for k, v in cell.items()}      # area_index -> (col,row)
    for i, a in enumerate(areas):
        c, rw = idx_of_cell[i]
        new_areas[i]["rect"] = (c * tw, rw * th, tw, th)
        placed[i] = (c, rw)
    print(f"\n重铺为 {cols}x{rows}：每块 {tw:.1f} x {th:.1f}"
          f"（覆盖 z 0..{tw * cols:.0f} / x 0..{th * rows:.0f}）")

    blob = write_skybox(bs, be, new_areas)
    data = d["buf"][:s] + blob + d["buf"][e:]
    print(f"mapSkyBox 段 {e - s} -> {len(blob)} 字节（总长 {len(data) - len(d['buf']):+d}）")

    if args.report or not args.apply:
        print("\n--report/未加 --apply：未写盘。")
        return

    out = Path(args.out) if args.out else p
    if out == p:
        bak = p.with_suffix(p.suffix + ".bak_skybox")
        if not bak.exists():
            bak.write_bytes(d["buf"])
            print(f"已备份 -> {bak.name}")
    out.write_bytes(data)
    print(f"已写出 {out}  {len(data)} 字节")

    # 复核
    d3 = parse_bin(str(out))
    s3, e3 = d3["tail"]["skyBox"]
    bs3, be3, areas3 = read_skybox(d3["buf"], s3, e3)
    xs3 = sorted(set(a["rect"][0] for a in areas3))
    ys3 = sorted(set(a["rect"][1] for a in areas3))
    cz = max(xs3) + areas3[0]["rect"][2]
    cx = max(ys3) + areas3[0]["rect"][3]
    print(f"复核: 区域 {len(areas3)} 个，覆盖 z 0..{cz:.0f} / x 0..{cx:.0f}"
          f"  → 全图 {cz / world_z:.1%} x {cx / world_x:.1%}")
    print(f"      贴图名与原来一致: "
          f"{[a['names'] for a in areas3] == [a['names'] for a in areas]}")
    print(f"      相邻块无缝无重叠: "
          f"{abs(xs3[1] - xs3[0] - areas3[0]['rect'][2]) < 0.01 and abs(ys3[1] - ys3[0] - areas3[0]['rect'][3]) < 0.01}")


if __name__ == "__main__":
    main()
