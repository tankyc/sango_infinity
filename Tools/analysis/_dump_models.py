# -*- coding: utf-8 -*-
"""一次性：解析原版 DefaultMap.bin 的 mapModels 段，看官方怎么放植被。

同时验证「世界坐标 -> 格坐标」公式：
    x(col) = floor((worldZ - gridSize/2) / gridSize)
    y(row) = floor((worldX - gridSize/2 - (x&1)*gridSize/2) / gridSize)
    height = 顶点(4x+2, 4y+2*(x&1)+2).height * 0.5
"""
import collections
import sys
from pathlib import Path

import numpy as np

sys.path.insert(0, str(Path(".").resolve()))
from sango_scale_bin import parse_bin          # noqa: E402

GRID = 20.0
QUAD = 5


def world_to_cell(wz, wx):
    col = int(np.floor((wz - GRID * 0.5) / GRID))
    row = int(np.floor((wx - GRID * 0.5 - (col & 1) * GRID * 0.5) / GRID))
    return col, row


def dump(path, grid_tex_ok=True):
    print(f"\n{'=' * 74}\n{path}\n{'=' * 74}")
    d = parse_bin(path)
    cx, cy = d["cellsX"], d["cellsY"]
    print(f"version={d['version']} work={d['workContent']!r} 格={cx}x{cy} "
          f"gridSize={d['gridSize']} quadSize={d['quadSize']}")

    grid = d["gridRaw"]                    # (cx, cy, 7)
    tt = grid[..., 0]
    verts = d["verts"]                     # (mapW+1, mapH+1, 3)

    tc, ttlist = d["tail"]["models"][0], d["tail"]["models"][2]
    print(f"\nmapModels: {len(ttlist)} 条")
    by_type = collections.Counter(m[1] for m in ttlist)
    by_model = collections.Counter(m[3] for m in ttlist)
    print(f"  按 objType: {dict(sorted(by_type.items()))}")
    print(f"  按 modelId: {dict(sorted(by_model.items()))}")
    nbind = sum(1 for m in ttlist if m[2] > 0)
    print(f"  有 bindId(绑定到城/建筑) 的: {nbind}")

    # 树模型 5..14 的落点校验
    trees = [m for m in ttlist if 5 <= m[3] <= 14]
    print(f"\n树模型(id 5~14) 共 {len(trees)} 条")
    if trees:
        hit = collections.Counter()
        yerrs = []
        for _oid, _ot, _bid, _mid, pos, rot, scl in trees:
            c, r = world_to_cell(pos[2], pos[0])       # pos.z->col, pos.x->row
            if 0 <= c < cx and 0 <= r < cy:
                hit[int(tt[c, r])] += 1
                vx = 4 * c + 2
                vy = 4 * r + 2 * (c & 1) + 2
                if vx < verts.shape[0] and vy < verts.shape[1]:
                    yerrs.append(pos[1] - verts[vx, vy, 0] * 0.5)
        print(f"  落点地形分布(应几乎全是 6=森): {dict(sorted(hit.items()))}")
        if yerrs:
            e = np.array(yerrs)
            print(f"  pos.y 与顶点高度*0.5 的差: p50={np.median(e):+.3f} "
                  f"p10={np.percentile(e,10):+.3f} p90={np.percentile(e,90):+.3f} "
                  f"max|.{np.abs(e).max():.3f}|")
        print("  样例:", [(m[0], m[1], m[3], tuple(round(v, 1) for v in m[4])) for m in trees[:5]])
    else:
        print("  ** 原版没有在 mapModels 里放树模型 **")

    # 其它模型抽样
    for t in sorted(by_type):
        sample = [m for m in ttlist if m[1] == t][:3]
        print(f"  objType={t} 样例: {[(m[0], m[3], tuple(round(v, 1) for v in m[4])) for m in sample]}")


if __name__ == "__main__":
    p = Path(r"d:\sango_infinity\Build\Content\Map\DefaultMap.bin")
    if p.exists():
        dump(p)
    else:
        print(f"找不到 {p}")
    w = Path(r"D:\世界地图\Map\world_full.bin")
    if w.exists():
        d = parse_bin(w)
        print(f"\n{'=' * 74}\n{w}\n{'=' * 74}")
        print(f"  格 {d['cellsX']}x{d['cellsY']}  mapModels: {len(d['tail']['models'][2])} 条")
        print(f"  mapLabelSet: {len(d['tail']['labels'][2])} 条")
