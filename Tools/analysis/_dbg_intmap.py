import json
from pathlib import Path

import numpy as np

import sango_map_fixes as M

fd = M.defaultmap_interior(ds=1)
print("城数", len(fd), " 格数", sum(len(v) for v in fd.values()))
cs = json.loads(Path("data/sango_full/cities.json").read_text(encoding="utf-8"))
for cid in (1, 2, 4, 5):
    v = fd.get(cid, [])
    if not v:
        continue
    xs = [a for a, b in v]
    ys = [b for a, b in v]
    c = cs[cid - 1]
    print(f"  城{cid} {c['name']} @({c['x']},{c['y']}) -> 内政格 {len(v)} 个,"
          f" x {min(xs)}~{max(xs)} y {min(ys)}~{max(ys)}")
    print("      前6:", v[:6])
allx = [a for v in fd.values() for a, b in v]
ally = [b for v in fd.values() for a, b in v]
print("全部内政格 x", min(allx), "~", max(allx), " y", min(ally), "~", max(ally))
print("42 中国城市 x", min(c["x"] for c in cs[:42]), "~", max(c["x"] for c in cs[:42]),
      " y", min(c["y"] for c in cs[:42]), "~", max(c["y"] for c in cs[:42]))
