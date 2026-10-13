# -*- coding: utf-8 -*-
"""一次性：查 yyxk 的 mapLayer —— layer_35 是不是水面层（最后一层），以及缺哪些贴图。"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin, Reader          # noqa: E402

BIN = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Map\yyxk_2x.bin")
MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
CONTENT = Path(r"D:\sango_infinity\Build\Content")
SEASONS = ("Autumn", "Spring", "Summer", "Winter")

d = parse_bin(str(BIN))
s, e = d["tail"]["layer"]
r = Reader(d["buf"]); r.o = s
lc = r.i32()
layers = []
for idx in range(lc):
    is_lit = r.u8()
    sx, sy = r.f32(), r.f32()
    seasons = []
    for _ in range(4):
        seasons.append((r.string(), r.string(), r.string()))
    layers.append({"lit": is_lit, "scale": (sx, sy), "seasons": seasons})

print(f"mapLayer: {lc} 层")
print(f"  第 0 层    : {layers[0]['seasons'][0]}")
print(f"  最后一层({lc - 1}): {layers[-1]['seasons'][0]}")
last = layers[-1]
print(f"\n最后一层（按 MapCell 的判定 = 水面层）引用的贴图:")
for j, sn in enumerate(SEASONS):
    dif, nor, msk = last["seasons"][j]
    print(f"  {sn:<6} diffuse={dif!r} normal={nor!r} mask={msk!r}")

refs = {}
for idx, L in enumerate(layers):
    for j, (dif, nor, msk) in enumerate(L["seasons"]):
        for nm in (dif, nor, msk):
            if nm:
                refs.setdefault(nm, []).append(idx)

print(f"\n不同贴图名 {len(refs)} 个")
for nm in sorted(refs):
    mark = "   <== 就是它" if nm in [x for x in last["seasons"][0]] else ""
    print(f"  {nm:<12} 被 {len(refs[nm])} 层引用(层号示例 {refs[nm][:6]}){mark}")

# 存在性：mod 自己的 Terrain 目录 + Content 的 Default
print("\n贴图存在性检查（Autumn 季）:")
for nm in sorted(refs):
    p1 = MOD / "Assets/Map/yyxk/Terrain/Autumn" / f"{nm}.png"
    p2 = CONTENT / "Assets/Map/Default/Terrain/Autumn" / f"{nm}.png"
    tag = "mod" if p1.exists() else ("Content/Default" if p2.exists() else "**缺失**")
    if tag == "**缺失**":
        print(f"  {nm:<12} {tag}   (mod: {p1.exists()}  content: {p2.exists()})")

print("\nmod 提供的水体贴图候选（文件名含水/water）:")
for f in sorted((MOD / "Assets/Map/yyxk").rglob("*.png")):
    if "water" in f.name.lower() or "水" in f.name:
        print(f"  {f.relative_to(MOD)}")
print("基础内容里的水体贴图:")
for f in sorted((CONTENT / "Assets/Map/Default/Terrain").rglob("water*")):
    print(f"  {f.relative_to(CONTENT)}")
