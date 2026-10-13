# -*- coding: utf-8 -*-
"""一次性：体检 yyxk_2x.bin —— 为「三国地理植被 / 山体与沿岸美化 / 错误数据检查」提供依据。"""
import collections
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin, Reader          # noqa: E402

import numpy as np

BIN = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Map\yyxk_2x.bin")
TT = Path(r"D:\sango_infinity\Build\Content\Data\Common\TerrainTypes.json")

d = parse_bin(str(BIN))
cx, cy = d["cellsX"], d["cellsY"]
grid = d["gridRaw"]
tt = grid[..., 0].astype(np.int32)
st = (grid[..., 1].astype(np.uint32) | (grid[..., 2].astype(np.uint32) << 8)
      | (grid[..., 3].astype(np.uint32) << 16) | (grid[..., 4].astype(np.uint32) << 24))
aid = grid[..., 5].astype(np.int32) | (grid[..., 6].astype(np.int32) << 8)
verts = d["verts"]
G, QUAD = float(d["gridSize"]), float(d["quadSize"])

print("=" * 78)
print(f"{BIN}")
print("=" * 78)
print(f"  {BIN.stat().st_size / 1048576:.1f} MB  version={d['version']} work={d['workContent']!r}")
print(f"  格 {cx}x{cy}  gridSize={d['gridSize']} quadSize={d['quadSize']}"
      f"  世界 {d['mapWidth'] * d['quadSize']} x {d['mapHeight'] * d['quadSize']}")
print(f"  顶点 {verts.shape[0]}x{verts.shape[1]}  高度(字节) min/max = {verts[..., 0].min()}/{verts[..., 0].max()}"
      f"  p50={int(np.median(verts[..., 0]))}")

# 地形表（外层可能包一层 "TerrainTypes"）
raw = json.loads(TT.read_text(encoding="utf-8"))
tti = raw.get("TerrainTypes", raw) if isinstance(raw, dict) else raw
info = {}
for k, v in tti.items():
    if not str(k).lstrip("-").isdigit() or not isinstance(v, dict):
        continue
    info[int(k)] = {"name": v.get("Name", ""), "moveable": bool(v.get("moveable")),
                    "canBuild": bool(v.get("canBuild")), "isWater": bool(v.get("isWater")),
                    "cost": v.get("baseCost")}
print(f"地形表: {len(info)} 项，id 范围 {min(info)}..{max(info)}")

print("\n--- 地形分布（全图）---")
tot = tt.size
mv = np.zeros(tt.shape, bool)
for t, c in sorted(collections.Counter(tt.ravel().tolist()).items()):
    i = info.get(t, {})
    m = bool(i.get("moveable"))
    mv |= (tt == t) & m
    print(f"  {t:>2} {i.get('name', '?'):<6} {c:>8} 格 {c / tot:>6.2%}"
          f"  可通行={'是' if m else '否'} 可建造={'是' if i.get('canBuild') else '否'}"
          f" 水面={'是' if i.get('isWater') else '否'}")
print(f"  => 可通行格 {int(mv.sum())} / {tot} = {mv.mean():.3f}"
      f"；不可通行 {int((~mv).sum())} = {(~mv).mean():.3%}")

print("\n--- 领地 areaId ---")
print(f"  0 的格数 {int((aid == 0).sum())} ({(aid == 0).mean():.3%})"
      f"   最大值 {int(aid.max())}   不同值 {len(np.unique(aid))}")
print(f"  可通行但 areaId=0 的格: {int((mv & (aid == 0)).sum())}   ← 引擎里会报「无归属」")
print(f"  领地内不可通行的格: {int((~mv & (aid > 0)).sum())}")

print("\n--- 内政位 / 物件 / 标注 ---")
print(f"  terrainState 非 0 的格 {int((st != 0).sum())}"
      f"   其中内政位(4) {int((st == 4).sum())}")
print(f"  mapModels {len(d['tail']['models'][2])} 条   mapLabelSet {len(d['tail']['labels'][2])} 条")

# 天空盒覆盖
s, e = d["tail"]["skyBox"]
r = Reader(d["buf"]); r.o = s
bs, be = r.f32(), r.f32()
ac = r.i32()
areas = []
for _ in range(ac):
    areas.append((r.f32(), r.f32(), r.f32(), r.f32()))
    for _j in range(4):
        r.string()
if areas:
    wz, wx = d["mapWidth"] * d["quadSize"], d["mapHeight"] * d["quadSize"]
    covz = max(a[0] for a in areas) + areas[0][2]
    covx = max(a[1] for a in areas) + areas[0][3]
    print(f"\n--- 天空盒 ---\n  {ac} 个区域，覆盖 z 0..{covz:.0f}/{wz} ({covz / wz:.1%})"
          f"  x 0..{covx:.0f}/{wx} ({covx / wx:.1%})")
else:
    print("\n--- 天空盒 ---\n  0 个区域（无天空贴图）")

# 山体粗糙度
VS = int(G // QUAD)
xs = np.arange(cx, dtype=np.int64)[:, None]
ys = np.arange(cy, dtype=np.int64)[None, :]
vx = np.clip(VS * xs + VS // 2, 0, verts.shape[0] - 1)
vy = np.clip(VS * ys + (xs & 1) * (VS // 2) + VS // 2, 0, verts.shape[1] - 1)
hc = verts[vx, vy, 0].astype(np.int32)
NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}
dl = []
for x in range(0, cx, 2):
    for dx, dy in NEIGH[x & 1]:
        nx = x + dx
        if nx < 0 or nx >= cx:
            continue
        yy = np.arange(cy); ny = yy + dy
        ok = (ny >= 0) & (ny < cy)
        m = mv[x, yy[ok]] & mv[nx, ny[ok]]
        if m.any():
            dl.append(np.abs(hc[x, yy[ok][m]] - hc[nx, ny[ok][m]]))
dl = np.concatenate(dl) if dl else np.array([0])
print(f"\n--- 山体粗糙度（相邻可通行格高差，字节）---")
print(f"  均值 {dl.mean():.1f} 中位 {int(np.median(dl))} p90 {int(np.percentile(dl, 90))}"
      f" p99 {int(np.percentile(dl, 99))} max {int(dl.max())}")
print(f"  >4 {(dl > 4).mean():.2%}  >8 {(dl > 8).mean():.2%}"
      f"  >16 {(dl > 16).mean():.2%}  >32 {(dl > 32).mean():.2%}")
