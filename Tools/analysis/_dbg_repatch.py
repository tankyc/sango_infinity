# -*- coding: utf-8 -*-
"""一次性：排查 exemplar_fill 为什么一块都没处理。"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
import numpy as np
from PIL import Image
from sango_basemap_repatch import (read_map, mask_to_img, clean_mask,   # noqa: E402
                                   exemplar_fill)

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
cur = read_map(MOD / "Map/yyxk_2x.bin")
orig = read_map(MOD / "Map/yyxk_2x.bin.bak_repair")
cx, cy = cur["cx"], cur["cy"]
dd = ((cur["terr"] != orig["terr"]) | (cur["area"] != orig["area"])
      | (cur["state"] != orig["state"]))
mm = clean_mask(dd.T, 1)
print(f"格 {cx}x{cy}   数据差异 {int(dd.sum())} 格  清理后 mm {int(mm.sum())}  shape {mm.shape}")
ys, xs = np.nonzero(mm)
print(f"  差异格坐标 (y,x): {list(zip(ys.tolist(), xs.tolist()))}")
print(f"  行的范围 {ys.min()}..{ys.max()}   列 {xs.min()}..{xs.max()}")

p = MOD / "Assets/Map/yyxk/BaseTex/BaseMap0.png"
img = Image.open(p).convert("RGB")
W, H = img.size
mb = mask_to_img(mm, (W, H))
print(f"底图 {W}x{H}   mask_big 真值像素 {int(mb.sum())}  shape {mb.shape}")
if mb.any():
    bys, bxs = np.nonzero(mb)
    print(f"  mask_big 像素范围 行 {bys.min()}..{bys.max()}  列 {bxs.min()}..{bxs.max()}")

px, py = W / float(cx), H / float(cy)
block = 8
bh = int(round(block * py))
print(f"  像素/格 {px}x{py}  block {block} 格 -> {bh} 像素")
cnt = 0
for y0 in range(0, H, max(1, bh // 2)):
    for x0 in range(0, W, max(1, bh // 2)):
        t = mb[y0:y0 + bh, x0:x0 + bh]
        if t.size and t.mean() > 0.001:
            cnt += 1
            if cnt <= 6:
                print(f"    命中块 ({y0},{x0}) 不符像素 {int(t.sum())}  比例 {t.mean():.4f}")
print(f"  满足块门限 0.001 的块数 {cnt}")

out, n = exemplar_fill(img, mb, block, px, py, 110, lambda s="": None, block_min=0.001)
print(f"  exemplar_fill -> 处理 {n} 块")
if n:
    d = np.abs(np.asarray(out, np.int32) - np.asarray(img, np.int32)).sum(2)
    print(f"  改动像素 {int((d > 6).sum())}  最大差 {int(d.max())}")
