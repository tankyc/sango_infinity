# -*- coding: utf-8 -*-
"""一次性：把 san11-world-main 里我们真正需要的部分复制到 D:\\sango_infinity\\Tools\\Map。

保持 data/ 的目录结构，这样在 Tools\\Map 下（cwd）就能直接跑那些按相对路径
找 data/ 的脚本（例如 sango_map_fixes.py 要读 data/world/world_info.json）。
"""
import shutil
import sys
from pathlib import Path

SRC = Path(".")
DST = Path(r"D:\sango_infinity\Tools\Map")

# ---- 核心：世界地图源数组（工具链的输入） ----
CORE = [
    "terrain.npy", "vheight.npy", "vtex.npy", "vwater.npy",
    "area.npy", "interior.npy", "cities.json", "meta.json",
]
# ---- 重跑 sango_convert.py 所需的 san11-world 产出 ----
WORLD = [
    "world_height.npy", "world_mat.npy", "world_types.npy",
    "world_info.json", "cities_world.json",
]
# ---- data/ 根下的小文件 ----
DATA_SMALL = [
    "cities_georef.json", "cities_west.json", "cities_more.json",
    "new_cities.json", "default_palette.json",
    "china_k3st_v.npy", "china_shex.npy",
]
# ---- 参考文档 ----
REFS = ["NOTES.md", "TUTORIAL.md", "COORDS.md", "TERRAIN_WINDOW.md", "BUILDINGS.md"]
REF_DIRS = ["docs"]

PLAN = [
    ("data/sango_full", CORE),
    ("data/world", WORLD),
    ("data", DATA_SMALL),
    # 参考文档在仓库根目录，目标和源不同名，所以单独列 (源, 目标)
]
REF_FILES = [(".", f, f"refs/{f}") for f in REFS]

ok = missing = 0
total = 0
manifest = []


def copy_one(rel_src, rel_dst):
    global ok, missing, total
    s = SRC / rel_src
    d = DST / rel_dst
    if not s.exists():
        print(f"  [缺失] {rel_src}")
        missing += 1
        return
    if s.is_dir():
        n = 0
        for f in s.rglob("*"):
            if f.is_file():
                rel = f.relative_to(s)
                t = d / rel
                t.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(f, t)
                n += f.stat().st_size
        print(f"  {rel_dst}/  <- {rel_src}/  ({n / 1048576:.2f} MB)")
        total += n
        ok += 1
        return
    d.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(s, d)
    sz = s.stat().st_size
    total += sz
    ok += 1
    manifest.append((rel_dst, sz))


print("=" * 74)
print(f"源: {SRC.resolve()}")
print(f"目标: {DST}")
print("=" * 74)

for dst_dir, files in PLAN:
    print(f"\n[{dst_dir}]")
    for f in files:
        copy_one(str(Path(dst_dir) / f), str(Path(dst_dir) / f))

print("\n[refs]")
for src_root, src_name, dst_rel in REF_FILES:
    copy_one(str(Path(src_root) / src_name), dst_rel)

print("\n[refs/docs]")
for d in REF_DIRS:
    copy_one(d, f"refs/{d}")

print("\n" + "=" * 74)
print(f"复制条目 {ok}，缺失 {missing}，合计 {total / 1048576:.1f} MB")
print("=" * 74)

print(f"\n目标目录清单（{DST}）:")
tot2 = 0
for f in sorted(DST.rglob("*")):
    if f.is_file():
        tot2 += f.stat().st_size
    if f.is_dir() and len(f.relative_to(DST).parts) <= 2:
        print(f"  {f.relative_to(DST)}/")
print(f"\n实际落盘合计 {tot2 / 1048576:.1f} MB")

# 校验：核心文件大小一致
print("\n关键文件校验（源 -> 目标，字节）:")
allok = True
for rel in [f"data/sango_full/{x}" for x in CORE] + [f"data/world/{x}" for x in WORLD]:
    a, b = SRC / rel, DST / rel
    if not a.exists() or not b.exists():
        print(f"  [异常] {rel} 不存在")
        allok = False
        continue
    same = a.stat().st_size == b.stat().st_size
    allok &= same
    if not same:
        print(f"  [不一致] {rel}: {a.stat().st_size} vs {b.stat().st_size}")
print("  全部一致 ✓" if allok else "  ** 有不一致 **")
