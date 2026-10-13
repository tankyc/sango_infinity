# -*- coding: utf-8 -*-
"""一次性：量化「底图 ↔ 生成它的源数据 ↔ 当前 .bin」三者的漂移。

分解成两个可判定的问题：
  (a) 当前 BaseTex 是否还是 data/sango_full 那套数据的渲染结果？
      —— 用同一套管线重渲染一张，与现有 BaseMap0.png 逐像素比较。
  (b) data/sango_full 的 terrain 与当前 .bin 的地形差多少？（这就是「形变」的来源）
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image

SRC = Path(r"D:\sango_infinity\Tools\Map\data\sango_full")     # 分离出来的源数据（正本）
XBIN = Path(r"D:\sango_infinity\Build\Mods\m6dwbcwk\Map\xworld_full.bin")
XBASE = Path(r"D:\sango_infinity\Build\Mods\m6dwbcwk\Assets\Map\xworld\BaseTex\BaseMap0.png")
OLD_SRC = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\sango_full")

d = parse_bin(str(XBIN))
cx, cy = d["cellsX"], d["cellsY"]
bin_tt = d["gridRaw"][..., 0].astype(np.int32)          # [x, y]
print("=" * 74)
print(f"当前 .bin: 格 {cx}x{cy}")
print("=" * 74)

# ---- (b) 源数据 terrain 与 .bin 地形对比 ----
for label, p in (("Tools\\Map (正本)", SRC / "terrain.npy"),
                 ("Downloads 源目录", OLD_SRC / "terrain.npy")):
    if not p.exists():
        print(f"  缺 {p}")
        continue
    t = np.load(p)
    print(f"\n[{label}] {p}")
    print(f"  shape {t.shape} dtype {t.dtype}  取值 {sorted(set(t.ravel().tolist()))[:14]}...")
    if t.shape != bin_tt.shape:
        print(f"  ** 形状与 .bin 不符（{t.shape} vs {bin_tt.shape}），无法逐格比 **")
        continue
    same = (t == bin_tt)
    print(f"  与 .bin 地形一致的比例 {same.mean():.4f}   不同 {int((~same).sum())} 格")
    if not same.all():
        diff = ~same
        xs, ys = np.where(diff)
        print(f"    差异格范围 x {xs.min()}..{xs.max()}  y {ys.min()}..{ys.max()}")
        import collections
        pairs = collections.Counter(zip(t[diff].tolist(), bin_tt[diff].tolist()))
        print(f"    差异中最多的是「源 -> .bin」: {pairs.most_common(8)}")

# ---- (a) 现有底图 vs 由 .bin 渲染（同尺寸直接比） ----
print("\n[现有底图] 与 .bin 的地形一致性（按 4 像素/格 采样比对色调）")
if XBASE.exists():
    im = Image.open(XBASE).convert("RGB")
    print(f"  {XBASE.name} {im.size[0]}x{im.size[1]}  = {im.size[0] / cx:.4f} x {im.size[1] / cy:.4f} 像素/格")
    a = np.asarray(im, np.float32)
    # 与「按地形分类的色相」做相关性：把底图降采样到格分辨率，取每格的色相
    small = np.asarray(im.resize((cx, cy), Image.Resampling.BOX), np.float32)
    # 逐地形看底图在该类的平均 RGB（若底图来自当前地形，同类地形应聚集且类间可分）
    for t in (9, 1, 6, 16, 22):
        m = (bin_tt == t).T
        if m.sum() == 0:
            continue
        v = small[m]
        print(f"    地形{t:>2}: {int(m.sum()):>7} 格  底图均值 RGB "
              f"{tuple(v.mean(0).round(0).astype(int))}  标准差 {v.std():.1f}")
