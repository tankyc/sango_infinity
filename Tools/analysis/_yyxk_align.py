# -*- coding: utf-8 -*-
"""一次性：测量 mod 底图（美术）与地格网格之间的几何形变。

思路：这张图的水是干净的蓝灰（水 B-R>0，陆地 B-R<0），拿「地形水掩膜」与
「底图水体度」做配准：
  1) 水体度判据的可分性检验
  2) 全局仿射（缩放 + 平移）搜索：FFT 相位相关定平移，外层扫缩放
  3) 用最优变换对齐后，按 32 格块算残差 —— 看剩下的形变在哪里、有多大
输出：Temp\yyxk_align.png、Temp\yyxk_align.json
"""
import json
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
OUT = Path(r"D:\sango_infinity\Temp")
WATER_IDS = [9, 21, 22]

d = parse_bin(str(MOD / "Map/yyxk_2x.bin"))
GX, GY = d["cellsX"], d["cellsY"]
terr = d["gridRaw"][..., 0].astype(np.int32).T          # [y, x] 与底图同序
wd = np.isin(terr, WATER_IDS).astype(np.float32)        # 地形水掩膜

art = Image.open(MOD / "Assets/Map/yyxk/BaseTex/BaseMap0.png").convert("RGB")
a = np.asarray(art, np.float32)
br = a[..., 2] - a[..., 0]                              # 水体度

# ---- 1) 判据可分性 ----
wd_big = np.asarray(Image.fromarray((wd * 255).astype(np.uint8))
                    .resize(art.size, Image.Resampling.NEAREST), np.float32) / 255.0
m = wd_big > 0.5
print("=" * 76)
print("[1] 水体度判据 B-R 的可分性")
print(f"  水  B-R: mean {br[m].mean():+.1f}  std {br[m].std():.1f}  "
      f"p10 {np.percentile(br[m], 10):+.0f}")
print(f"  陆  B-R: mean {br[~m].mean():+.1f}  std {br[~m].std():.1f}  "
      f"p90 {np.percentile(br[~m], 90):+.0f}")
sep = (br[m].mean() - br[~m].mean()) / max(1e-6, (br[m].std() + br[~m].std()) / 2)
print(f"  可分性 {(br[m].mean() - br[~m].mean()) / max(1e-6, (br[m].std() + br[~m].std()) / 2):.3f}")
for thr in (0, 5, 10, 15, 20, 30):
    pred = br > thr
    tp = float((pred & m).sum())
    fp = float((pred & ~m).sum())
    fn = float((~pred & m).sum())
    iou = tp / max(1e-6, tp + fp + fn)
    print(f"  阈值 {thr:>3}: IoU {iou:.3f}  判为水的比例 {pred.mean():.3f}（真值 {m.mean():.3f}）")

# ---- 缩到格分辨率 ----
small = art.resize((GX, GY), Image.Resampling.BOX)
sa = np.asarray(small, np.float32)
sw = (sa[..., 2] - sa[..., 0])                          # 格分辨率水体度
print(f"\n格分辨率 {GX}x{GY}: 水体度 水 {sw[wd > 0.5].mean():+.1f}  陆 {sw[wd < 0.5].mean():+.1f}")


def phase_shift(A, B):
    """返回把 A 平移到与 B 最匹配的 (dy, dx)。"""
    FA = np.fft.rfft2(A - A.mean())
    FB = np.fft.rfft2(B - B.mean())
    R = FA * np.conj(FB)
    R /= np.maximum(np.abs(R), 1e-12)
    c = np.fft.irfft2(R, A.shape)
    iy, ix = np.unravel_index(int(np.argmax(c)), c.shape)
    dy = iy if iy <= A.shape[0] // 2 else iy - A.shape[0]
    dx = ix if ix <= A.shape[1] // 2 else ix - A.shape[1]
    return int(dy), int(dx), float(c[iy, ix])


def ncc(A, B):
    A2, B2 = A - A.mean(), B - B.mean()
    d1, d2 = np.sqrt((A2 * A2).sum()), np.sqrt((B2 * B2).sum())
    return float((A2 * B2).sum() / max(1e-9, d1 * d2))


M = 24
A0 = wd[M:-M, M:-M]
B0 = sw[M:-M, M:-M]
print("\n[2] 全局仿射搜索（FFT 相位相关定平移，外层扫缩放）")
base = ncc(A0, B0)
print(f"  不做任何变换: NCC {base:+.4f}")
results = []
for sc in np.arange(0.94, 1.0601, 0.005):
    # 把底图按 1/scale 重采样，等于把地形水掩膜缩放到美术的尺度
    n = int(round(GX * sc))
    img = Image.fromarray((wd.T * 255).astype(np.uint8)).resize((n, n), Image.Resampling.BILINEAR)
    wd2 = np.asarray(img, np.float32)[:: -1, ::-1] if False else np.asarray(img, np.float32)
    # 裁回中心区域对齐尺寸
    if n > GX:
        o = (n - GX) // 2
        wd2 = wd2[o:o + GX, o:o + GX]
    else:
        pad = GX - n
        wd2 = np.pad(wd2, ((pad // 2, pad - pad // 2),) * 2)
    A = wd2[M:-M, M:-M]
    dy, dx, peak = phase_shift(A, B0)
    results.append((float(sc), dy, dx, peak, ncc(np.roll(np.roll(A, dy, 0), dx, 1), B0)))
results.sort(key=lambda r: -r[4])
for r in results[:6]:
    print(f"  scale {r[0]:.3f}  平移 (dy={r[1]:+3d}, dx={r[2]:+3d})  相位峰 {r[3]:.3f}  NCC {r[4]:+.4f}")
sc_b, dy_b, dx_b, _, ncc_b = results[0]
print(f"  -> 最优: 缩放 {sc_b:.3f}  平移 (dy={dy_b}, dx={dx_b})  NCC {ncc_b:+.4f}"
      f"（原始 {base:+.4f}，提升 {ncc_b - base:+.4f}）")

# ---- 3) 对齐后的残差场 ----
n = int(round(GX * sc_b))
img = Image.fromarray((wd.T * 255).astype(np.uint8)).resize((n, n), Image.Resampling.BILINEAR)
wd2 = np.asarray(img, np.float32)
if n > GX:
    o = (n - GX) // 2
    wd2 = wd2[o:o + GX, o:o + GX]
else:
    p = GX - n
    wd2 = np.pad(wd2, ((p // 2, p - p // 2),) * 2)
wd2 = np.roll(np.roll(wd2, dy_b, 0), dx_b, 1)

T, ST = 32, 16
mis_before, mis_after, tiles = [], [], []
for y0 in range(0, GY - T + 1, ST):
    for x0 in range(0, GX - T + 1, ST):
        e = wd[y0:y0 + T, x0:x0 + T] > 0.5
        o = sw[y0:y0 + T, x0:x0 + T] > 10
        o2 = wd2[y0:y0 + T, x0:x0 + T] > 0.5
        if e.sum() == 0 and o.sum() == 0:
            continue
        mb = float((e != o).mean())
        ma = float((e != o2).mean())
        mis_before.append(mb)
        mis_after.append(ma)
        tiles.append((x0, y0, mb, ma))
mb, ma = np.array(mis_before), np.array(mis_after)
print(f"\n[3] 分块残差（{T} 格/块，{len(tiles)} 块）—— 水/陆判错比例")
print(f"  变换前 中位 {np.median(mb):.3f}  均值 {mb.mean():.3f}")
print(f"  变换后 中位 {np.median(ma):.3f}  均值 {ma.mean():.3f}")
print(f"  改善的块 {int((ma < mb - 0.02).sum())}/{len(tiles)}"
      f"   变差的块 {int((ma > mb + 0.02).sum())}")
worst = sorted(tiles, key=lambda r: -r[3])[:8]
print(f"  变换后仍最不一致的块: {[(int(x), int(y), round(m, 2)) for x, y, _, m in worst]}")

# 可视化
wdm = (wd * 255).astype(np.uint8)
om = (np.clip((sw - 5) / 30, 0, 1) * 255).astype(np.uint8)
o2m = (wd2 * 255).astype(np.uint8)
sheet = Image.new("RGB", (GX * 3 + 16, GY), (20, 20, 24))
sheet.paste(Image.fromarray(np.stack([wdm] * 3, -1)), (0, 0))
sheet.paste(Image.fromarray(np.stack([om] * 3, -1)), (GX + 8, 0))
sheet.paste(Image.fromarray(np.stack([o2m] * 3, -1)), (GX * 2 + 16, 0))
dr = ImageDraw.Draw(sheet)
dr.text((4, 4), "地形水掩膜(真值)", fill=(255, 255, 0))
dr.text((GX + 12, 4), "底图水体度", fill=(255, 255, 0))
dr.text((GX * 2 + 20, 4), f"地形水掩膜(缩放{sc_b:.3f}+平移{dy_b},{dx_b}后)", fill=(255, 255, 0))
sheet.save(OUT / "yyxk_align.png")
print(f"\n已写出 {OUT / 'yyxk_align.png'}")

(OUT / "yyxk_align.json").write_text(json.dumps(
    {"baseline_ncc": round(base, 4), "best": {"scale": sc_b, "dy": dy_b, "dx": dx_b,
                                              "ncc": round(ncc_b, 4)},
     "top": [[r[0], r[1], r[2], round(r[3], 3), round(r[4], 4)] for r in results[:12]],
     "tile_mismatch_before_median": round(float(np.median(mb)), 4),
     "tile_mismatch_after_median": round(float(np.median(ma)), 4),
     "worst_tiles": [[int(x), int(y), round(mm, 3)] for x, y, _, mm in worst]},
    ensure_ascii=False, indent=1), encoding="utf-8")
print(f"已写出 {OUT / 'yyxk_align.json'}")
