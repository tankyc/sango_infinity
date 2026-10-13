# -*- coding: utf-8 -*-
"""一次性：对比 DefaultMap.bin 与 xworld_full.bin 的 mapSkyBox 段。

段格式（已在 sango_scale_bin.py 里验证）：
    float  sky_blend_start
    float  sky_blend_end
    int32  area_count
    area_count x { Rect(x, y, width, height) 4*float ; 4 个季节字符串 }
"""
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(".").resolve()))
from sango_scale_bin import parse_bin, Reader          # noqa: E402

FILES = [
    r"d:\sango_infinity\Build\Content\Map\DefaultMap.bin",
    r"D:\sango_infinity\Build\Mods\m6dwbcwk\Map\xworld_full.bin",
]

for p in FILES:
    f = Path(p)
    if not f.exists():
        print(f"缺 {p}")
        continue
    d = parse_bin(str(f))
    s, e = d["tail"]["skyBox"]
    buf = d["buf"]
    r = Reader(buf)
    r.o = s
    bstart = r.f32()
    bend = r.f32()
    n = r.i32()
    print("=" * 78)
    print(f"{f.name}")
    print(f"  格 {d['cellsX']}x{d['cellsY']}  mapWidth/Height = {d['mapWidth']}x{d['mapHeight']}"
          f"  （世界尺寸 = {d['mapWidth'] * d['quadSize']} x {d['mapHeight'] * d['quadSize']}）")
    print(f"  mapSkyBox 段: 偏移 {s}..{e}  共 {e - s} 字节")
    print(f"  sky_blend_start = {bstart}")
    print(f"  sky_blend_end   = {bend}")
    print(f"  area_count      = {n}")
    for i in range(n):
        x, y, w, h = r.f32(), r.f32(), r.f32(), r.f32()
        seasons = [r.string() for _ in range(4)]
        print(f"    [{i}] rect = x{x:.1f} y{y:.1f} w{w:.1f} h{h:.1f}")
        for j, sn in enumerate(("秋", "春", "夏", "冬")):
            print(f"          {sn} : {seasons[j]!r}")
    print(f"  解析结束位置 {r.o}，段尾 {e}  一致: {r.o == e}")
    if r.o != e:
        print(f"    ** 剩余字节: {buf[r.o:e][:64].hex(' ')}")

    # 顺带看看 mapLayer 的材质字符串，确认贴图路径的写法习惯
    ls, le = d["tail"]["layer"]
    r2 = Reader(buf)
    r2.o = ls
    lc = r2.i32()
    print(f"  （参考）mapLayer 有 {lc} 层，前 2 层的夏/冬 diffuse:")
    for i in range(min(lc, 2)):
        isLit = r2.u8()
        sx, sy = r2.f32(), r2.f32()
        names = []
        for _ in range(4):
            names.append((r2.string(), r2.string(), r2.string()))
        print(f"    layer{i}: lit={isLit} scale=({sx},{sy})")
        for j, sn in enumerate(("秋", "春", "夏", "冬")):
            print(f"       {sn} diffuse={names[j][0]!r}")
