# -*- coding: utf-8 -*-
"""一次性：对比两个 mod 提供的文件，判断改 modList 顺序的副作用。"""
import hashlib
from collections import Counter
from pathlib import Path

MODS = Path(r"D:\sango_infinity\Build\Mods")


def inv(m):
    d = MODS / m
    out = {}
    for f in d.rglob("*"):
        if f.is_file() and f.name not in ("mod.info", "poster.jpg", "preview.png", "README.txt"):
            rel = str(f.relative_to(d)).replace("\\", "/")
            out[rel] = (f.stat().st_size, hashlib.md5(f.read_bytes()).hexdigest()[:8])
    return out


for m in ("m6dwbcwk", "vcfu6xr6"):
    i = inv(m)
    top = Counter(str(Path(k).parts[0]) for k in i)
    print(f"\n{m}: {len(i)} 个文件  顶层分布 {dict(top)}")
    for k in sorted(i):
        if not k.startswith("Assets/Face"):
            print(f"   {i[k][0]:>10} B  {i[k][1]}  {k}")

a, b = inv("m6dwbcwk"), inv("vcfu6xr6")
both = sorted(set(a) & set(b))
print(f"\n两边都有的文件 {len(both)} 个：")
for k in both:
    same = "相同" if a[k][1] == b[k][1] else f"**不同** ({a[k][1]} vs {b[k][1]})"
    print(f"   {k}   {same}")

print("\n=== 存档目录 ===")
for d in sorted(Path(r"D:\sango_infinity\Build").iterdir()):
    if d.is_dir() and "save" in d.name.lower():
        files = list(d.rglob("*"))
        print(f"   {d.name}  {len(files)} 项")
