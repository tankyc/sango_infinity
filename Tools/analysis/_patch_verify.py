# -*- coding: utf-8 -*-
"""一次性：数值核验底图补丁 —— 抽样比较原图与补丁后的像素，确认没写坏。"""
from pathlib import Path

import numpy as np
from PIL import Image

MOD = Path(r"D:\sango_infinity\Build\Mods\m6dwbcwk")
BASE = MOD / "Assets/Map/xworld/BaseTex/BaseMap0.png"
NEW = Path(r"D:\sango_infinity\Temp\basemap_patch_0_Autumn.png")
CX, CY = 1800, 1000

old = np.asarray(Image.open(BASE).convert("RGB"), np.int32)
new = np.asarray(Image.open(NEW).convert("RGB"), np.int32)
print(f"尺寸 old {old.shape} new {new.shape}")
px, py = old.shape[1] / CX, old.shape[0] / CY
print(f"像素/格 {px:.4f} x {py:.4f}")

d = np.abs(old - new).sum(2)
print(f"改动像素占比 {(d > 6).mean():.4f}   最大差 {int(d.max())}")


def probe(name, x, y, half=1):
    c0, c1 = int(x * px), int((x + half) * px)
    r0, r1 = int(y * py), int((y + half) * py)
    print(f"  {name:<22} 格({x},{y})  原 {tuple(old[r0:r1, c0:c1].reshape(-1, 3).mean(0).round(0).astype(int))}"
          f"  新 {tuple(new[r0:r1, c0:c1].reshape(-1, 3).mean(0).round(0).astype(int))}")


print("\n抽样：")
probe("虚空-左上角", 5, 5)
probe("虚空-右边缘", 1795, 500)
probe("虚空-下边缘", 900, 995)
probe("内圈-紧邻虚空内侧", 25, 25)
probe("内圈-地图中心", 900, 500)
probe("内圈-右下(看似纯黑处)", 45, 960)
probe("内圈-右下(再往里)", 60, 940)

# 只应改动：虚空格 + 据点底座
print("\n改动区域判定：")
void = np.zeros((CX, CY), bool)
void[:20] = void[-20:] = True
void[:, :20] = void[:, -20:] = True
print(f"  虚空格数 {int(void.sum())}")
# 把改动像素映射回格
ys, xs = np.nonzero(d > 6)
gx = np.clip((xs / px).astype(int), 0, CX - 1)
gy = np.clip((ys / py).astype(int), 0, CY - 1)
in_void = void[gx, gy]
print(f"  改动像素 {len(xs)}  落在虚空格内的占 {in_void.mean():.3f}")
print(f"  非虚空的改动像素数 {int((~in_void).sum())}（应≈据点底座面积 = 据点×底座像素）")
