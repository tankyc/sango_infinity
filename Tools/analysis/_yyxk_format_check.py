# -*- coding: utf-8 -*-
"""一次性：写盘前的格式安全检查 —— yyxk_2x.bin 的段结构与 mapModels 段布局。

sango_vegetation.py 是按世界图（xworld_full.bin）的格式写的。yyxk 是第三方做的图，
必须确认版本号、段顺序、模型记录大小一致，否则写入会破坏文件。
"""
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

FILES = [
    ("yyxk", Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Map\yyxk_2x.bin")),
    ("xworld(世界图,工具按它写的)", Path(r"D:\sango_infinity\Build\Mods\m6dwbcwk\Map\xworld_full.bin")),
    ("DefaultMap(原版)", Path(r"D:\sango_infinity\Build\Content\Map\DefaultMap.bin")),
]

for tag, p in FILES:
    if not p.exists():
        print(f"[缺] {p}")
        continue
    d = parse_bin(str(p))
    print("=" * 78)
    print(f"[{tag}] {p.name}  {p.stat().st_size / 1024 / 1024:.1f} MB")
    print(f"  顶层键: {[k for k in d.keys()]}")
    for k, v in d.items():
        if isinstance(v, (int, float, str)):
            print(f"    {k} = {v}")
    tail = d.get("tail") or {}
    print(f"  tail 键: {list(tail.keys())}")
    for k, v in tail.items():
        if isinstance(v, list):
            print(f"    {k}: list len {len(v)}"
                  + (f"  首元素类型 {type(v[0]).__name__}" if v else "")
                  + (f"  首元素 {str(v[0])[:120]}" if v else ""))
        elif isinstance(v, (int, float, str)):
            print(f"    {k} = {v}")

# 直接看 yyxk 的 mapModels 段原始字节
print("\n" + "=" * 78)
p = FILES[0][1]
raw = p.read_bytes()
print(f"yyxk 原始头部 32 字节: {raw[:32].hex(' ')}")
off = 4
n = 0
sh = 0
while True:
    v = raw[off]
    off += 1
    n |= (v & 0x7F) << sh
    if not (v & 0x80):
        break
    sh += 7
off += n
w, h = struct.unpack_from("<ii", raw, off)
off += 8
gs = struct.unpack_from("<i", raw, off)[0]
off += 4
print(f"  worldLen={n}  mapWidth={w} mapHeight={h} gridSize={gs}"
      f"  -> 格 {(w * 5) // gs}x{(h * 5) // gs}")
print(f"  头部后偏移 {off}")
print(f"  接下来 48 字节: {raw[off:off + 48].hex(' ')}")
# 与世界图对比同样位置
p2 = FILES[1][1]
raw2 = p2.read_bytes()
off2 = 4
n2 = 0
sh = 0
while True:
    v = raw2[off2]
    off2 += 1
    n2 |= (v & 0x7F) << sh
    if not (v & 0x80):
        break
    sh += 7
off2 += n2
w2, h2 = struct.unpack_from("<ii", raw2, off2)
off2 += 8
gs2 = struct.unpack_from("<i", raw2, off2)[0]
off2 += 4
print(f"\nxworld: worldLen={n2} mapWidth={w2} mapHeight={h2} gridSize={gs2}"
      f"  -> 格 {(w2 * 5) // gs2}x{(h2 * 5) // gs2}")
print(f"  头部字符串: {raw[4:4 + n2][:60]!r}")
print(f"yyxk 头部字符串: {raw[4:4 + n][:60]!r}")
