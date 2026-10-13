# -*- coding: utf-8 -*-
"""一次性：核对各份 world_full.bin 的模型段，确认植被数据在不在游戏加载的那一份里。"""
import collections
import hashlib
import sys
from pathlib import Path

sys.path.insert(0, str(Path(".").resolve()))
from sango_scale_bin import parse_bin          # noqa: E402

FILES = [
    r"D:\sango_infinity\Build\Mods\m6dwbcwk\Map\world_full.bin",
    r"D:\sango_infinity\Build\Mods\vcfu6xr6\Map\world_full.bin",
    r"D:\sango_infinity\Build\Mods111\m6dwbcwk\Map\world_full.bin",
    r"D:\世界地图\Map\world_full.bin",
]

for p in FILES:
    f = Path(p)
    if not f.exists():
        print(f"缺 {p}")
        continue
    d = parse_bin(str(f))
    ms = d["tail"]["models"][2]
    h = hashlib.md5(f.read_bytes()).hexdigest()[:12]
    print(f"{f.stat().st_size:>10} B  md5={h}  work={d['workContent']!r}  "
          f"{d['cellsX']}x{d['cellsY']}  models={len(ms)}  "
          f"objType={dict(collections.Counter(m[1] for m in ms))}")
    print(f"           labels={len(d['tail']['labels'][2])}  {p}")
