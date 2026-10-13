# -*- coding: utf-8 -*-
"""一次性：用颜色聚类找出这张美术的「水体色」，并验证颜色判据是否可靠。

用户指出：水体本就是近似统一颜色，用 B-R 阈值判水是错的工具。这里换成
「颜色聚类 + 到水色簇的距离」，并用量化指标（IoU / 精确率 / 召回率）验证。
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
WATER = [7, 8, 9, 21, 22]
SEASONS = ("Autumn", "Spring", "Summer", "Winter")

d = parse_bin(str(MOD / "Map/yyxk_2x.bin"))
cx, cy = d["cellsX"], d["cellsY"]
terr = d["gridRaw"][..., 0].astype(np.int32).T
terW = np.isin(terr, WATER)

for si, nm in enumerate(SEASONS):
    img = Image.open(MOD / f"Assets/Map/yyxk/BaseTex/BaseMap{si}.png").convert("RGB")
    W, H = img.size
    a = np.asarray(img, np.float32)
    Wm = np.asarray(Image.fromarray((terW.astype(np.uint8) * 255), "L")
                    .resize((W, H), Image.Resampling.NEAREST), np.uint8) > 127
    flat = a.reshape(-1, 3)
    mflat = Wm.reshape(-1)
    print(f"\n=== {nm}  {W}x{H} ===")
    print(f"  地形水域平均色 {tuple(flat[mflat].mean(0).round(1))}"
          f"   标准差 {flat[mflat].std(0).round(1)}")
    print(f"  地形陆地平均色 {tuple(flat[~mflat].mean(0).round(1))}"
          f"   标准差 {flat[~mflat].std(0).round(1)}")

    # k-means（numpy 手写，样本 200k）
    rng = np.random.default_rng(0)
    idx = rng.choice(flat.shape[0], size=min(200000, flat.shape[0]), replace=False)
    S, MS = flat[idx], mflat[idx]
    k = 8
    cen = S[rng.choice(S.shape[0], k, replace=False)]
    for _ in range(25):
        dd = ((S[:, None, :] - cen[None, :, :]) ** 2).sum(2)
        lab = dd.argmin(1)
        for i in range(k):
            sel = lab == i
            if sel.any():
                cen[i] = S[sel].mean(0)
    print("  颜色簇（按水体占比排序）:")
    rows = []
    for i in range(k):
        sel = lab == i
        if sel.sum() < 100:
            continue
        frac_w = float(MS[sel].mean())
        rows.append((frac_w, tuple(cen[i].round(0).astype(int)), int(sel.sum()),
                     tuple(cen[i].round(0).astype(int)) == cen[i].round(0).astype(int)))
    rows.sort(reverse=True)
    for frac_w, c, n, _ in rows:
        print(f"    色 {c}  样本 {n:>6}  水体占比 {frac_w:.3f}")

    # 用「到水体色簇的距离 < 到陆色簇的距离」做判据
    wcen = np.array([c for f, c, n, _ in rows if f > 0.5], np.float32)
    lcen = np.array([c for f, c, n, _ in rows if f <= 0.2], np.float32)
    if len(wcen) == 0 or len(lcen) == 0:
        print("    （无法分组）")
        continue
    dw = ((flat[:, None, :] - wcen[None, :, :]) ** 2).sum(2).min(1)
    dl = ((flat[:, None, :] - lcen[None, :, :]) ** 2).sum(2).min(1)
    pred = (dw < dl).reshape(H, W)
    tp = float((pred & Wm).sum())
    fp = float((pred & ~Wm).sum())
    fn = float((~pred & Wm).sum())
    print(f"  颜色判据（最近簇）: IoU {tp / max(1e-6, tp + fp + fn):.3f}"
          f"  精确率 {tp / max(1e-6, tp + fp):.3f}  召回率 {tp / max(1e-6, tp + fn):.3f}"
          f"  判为水比例 {pred.mean():.3f}（真值 {Wm.mean():.3f}）")
    # 水体色的 90 分位范围（用于统一上色的取值）
    q = np.percentile(flat[mflat], [5, 25, 50, 75, 95], axis=0)
    print(f"  水体色分位 5/25/50/75/95: "
          f"{[tuple(x.round(0).astype(int)) for x in q]}")
