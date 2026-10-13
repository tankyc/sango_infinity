# -*- coding: utf-8 -*-
"""一次性：核验已写入的四季底图 —— 与 .bak 对比，确认只改了虚空格与据点底座像素。"""
from pathlib import Path

import numpy as np
from PIL import Image

TEX = Path(r"D:\sango_infinity\Build\Mods\m6dwbcwk\Assets\Map\xworld\BaseTex")
CX, CY = 1800, 1000
VOID = (74, 76, 78)

void = np.zeros((CX, CY), bool)
void[:20] = void[-20:] = True
void[:, :20] = void[:, -20:] = True

for i in range(4):
    p = TEX / f"BaseMap{i}.png"
    bak = TEX / f"BaseMap{i}.png.bak_before_patch"
    a = np.asarray(Image.open(p).convert("RGB"), np.int32)
    b = np.asarray(Image.open(bak).convert("RGB"), np.int32)
    px, py = a.shape[1] / CX, a.shape[0] / CY
    d = np.abs(a - b).sum(2)
    ys, xs = np.nonzero(d > 6)
    gx = np.clip((xs / px).astype(int), 0, CX - 1)
    gy = np.clip((ys / py).astype(int), 0, CY - 1)
    in_void = void[gx, gy]
    # 像素级虚空掩膜（外圈 20 格对应的行列范围）
    xc = np.zeros(a.shape[1], bool)
    xc[:int(round(20 * px))] = True
    xc[int(round((CX - 20) * px)):] = True
    yc = np.zeros(a.shape[0], bool)
    yc[:int(round(20 * py))] = True
    yc[int(round((CY - 20) * py)):] = True
    pv = yc[:, None] | xc[None, :]
    vc = (np.abs(a - np.array(VOID)) <= 6).all(2)
    print(f"BaseMap{i}: 改动像素 {len(xs):>7}  其中在虚空区 {in_void.mean():.3f}"
          f"  非虚空(据点底座) {int((~in_void).sum()):>6}")
    print(f"            虚空区已刷成 {VOID} 的比例 {float(vc[pv].mean()):.4f}")
