# -*- coding: utf-8 -*-
"""lit_world 投影设计：从 ETOPO 求「中国区域放大到 300x300、其余（含海洋）压缩进 900x500」的映射。

做法：经纬各自用一个**单调的平滑权重**重映射（中国核心区放大 boost 倍，向外 feather 度内平滑回到 1），
      boost 用二分求出，使中国核心恰好落在 300 格。映射可逆（按目标格反查源经纬）且无接缝。
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ETOPO = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
OUT = Path(r"D:\sango_infinity\Temp")

# --- 目标 ---
NX, NY = 900, 500                  # 地图总格数
CN_LON = (73.0, 135.0)             # 中国核心经度
CN_LAT = (18.0, 54.0)              # 中国核心纬度
CN_CELLS = (320, 200)              # 中国核心占格（320:200 = 1.60:1，与中国真实纵横比一致 -> 零变形）
WORLD_LON = (-15.0, 160.0)         # 世界覆盖（与现有世界图一致：欧亚+非洲）
WORLD_LAT = (-13.0, 77.0)          # 纬度取 90 度：500 行 / 90 度 = 5.556 行/度，中国 36 度 -> 恰好 200 行
FEATHER = 12.0                     # 核心外多少度内平滑过渡


def axis_map(lo, hi, n_out, core, n_core, feather):
    v = np.linspace(lo, hi, 6000)
    d = np.maximum(np.maximum(core[0] - v, v - core[1]), 0.0)
    s = np.clip(d / feather, 0, 1)
    s = s * s * (3 - 2 * s)                       # smoothstep：核内 0、核外 1

    def u_of(boost):
        u = np.cumsum(1 + (boost - 1) * (1 - s))
        return (u - u[0]) / (u[-1] - u[0]) * n_out

    lo_b, hi_b = 1.0, 20.0
    for _ in range(60):
        b = (lo_b + hi_b) / 2
        u = u_of(b)
        got = np.interp(core[1], v, u) - np.interp(core[0], v, u)
        if got < n_core:
            lo_b = b
        else:
            hi_b = b
    b = (lo_b + hi_b) / 2
    u = u_of(b)
    return v, u, b


vlon, ulon, blon = axis_map(WORLD_LON[0], WORLD_LON[1], NX, CN_LON, CN_CELLS[0], FEATHER)
vlat, ulat, blat = axis_map(WORLD_LAT[0], WORLD_LAT[1], NY, CN_LAT, CN_CELLS[1], FEATHER)
print(f"经度 boost {blon:.3f}x（核内 {NX/ (WORLD_LON[1]-WORLD_LON[0]):.2f} -> "
      f"{NX/(WORLD_LON[1]-WORLD_LON[0])*blon:.2f} 格/度）")
print(f"纬度 boost {blat:.3f}x（核内 {NY/(WORLD_LAT[1]-WORLD_LAT[0]):.2f} -> "
      f"{NY/(WORLD_LAT[1]-WORLD_LAT[0])*blat:.2f} 格/度）")

# 目标格 -> 源经纬
j = np.arange(NX) + 0.5
i = np.arange(NY) + 0.5
lon_t = np.interp(j, ulon, vlon)
lat_t = np.interp(i, ulat, vlat)
print(f"核内实际占格: 经 {(lon_t <= CN_LON[1]).sum() - (lon_t < CN_LON[0]).sum()}"
      f"  纬 {(lat_t <= CN_LAT[1]).sum() - (lat_t < CN_LAT[0]).sum()}")

# 每格地面尺寸（km），看形变
mid = (CN_LAT[0] + CN_LAT[1]) / 2
km_lon = 111.32 * np.cos(np.deg2rad(mid))
print(f"核内单格地面尺寸: 东西 {km_lon*(CN_LON[1]-CN_LON[0])/CN_CELLS[0]:.1f} km"
      f"  南北 {110.57*(CN_LAT[1]-CN_LAT[0])/CN_CELLS[1]:.1f} km"
      f"  → 宽高比 {(km_lon*(CN_LON[1]-CN_LON[0])/CN_CELLS[0])/(110.57*(CN_LAT[1]-CN_LAT[0])/CN_CELLS[1]):.2f}")
edge = np.diff(lon_t).mean()
print(f"核外平均 {edge * km_lon:.1f} km/格（东西），核内 "
      f"{(CN_LON[1]-CN_LON[0])/CN_CELLS[0]*km_lon:.1f} km/格")

# --- 采样 ETOPO 画预览 ---
dem = np.load(ETOPO, mmap_mode="r")
rows = np.clip(((90.0 - lat_t) * 60).astype(int), 0, dem.shape[0] - 1)
cols = np.clip(((lon_t + 180.0) * 60).astype(int), 0, dem.shape[1] - 1)
E = np.array(dem[np.ix_(rows, cols)], np.float32)          # (NY, NX) 米
land = E > 0
print(f"\n目标图 {NX}x{NY}：陆地占比 {land.mean():.3f}")
in_cn = ((lon_t[None, :] >= CN_LON[0]) & (lon_t[None, :] <= CN_LON[1])
         & (lat_t[:, None] >= CN_LAT[0]) & (lat_t[:, None] <= CN_LAT[1]))
print(f"  中国核内陆地占比 {land[in_cn].mean():.3f}（核内格数 {int(in_cn.sum())}）")

h = np.clip((E - E.min()) / max(1, E.max() - E.min()), 0, 1)
img = np.zeros((NY, NX, 3), np.uint8)
img[..., 0] = (h * 120).astype(np.uint8) + 40
img[..., 1] = (h * 150).astype(np.uint8) + 50
img[..., 2] = (h * 170).astype(np.uint8) + 60
img[land] = (np.stack([h, h, h], -1) * 160 + 70).astype(np.uint8)[land]
sea = ~land
img[sea] = (60, 90, 130)
im = Image.fromarray(img)
im.resize((900, 500), Image.Resampling.LANCZOS).save(OUT / "lit_projection_preview.jpg", quality=88)
print(f"已写出预览 {OUT / 'lit_projection_preview.jpg'}（900x500，亮度=高程，蓝=海）")
