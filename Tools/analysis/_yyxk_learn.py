# -*- coding: utf-8 -*-
"""一次性：学习 yyxk mod 自带底图的风格，并测量它与当前地图的「部分形变」。

只做量测与学习，不生成任何底图。
  1) 朝向确认：用「水/陆两类的可分性」判朝向（这张图水是亮的，不能用"水更暗"判）
  2) 地格色：按 terrainType 统计 4 季 BaseTex 的实际颜色（中位/分位/标准差）
  3) 整体：每季全图 meanRGB / 饱和度 / 明度 / 高频能量（纹理感）
  4) 地域：按 areaId 统计颜色相对全局调色板的偏移
  5) 形变：以「地形水掩膜 × 底图亮度」做相关，搜索全局偏移与分块偏移场
输出：Temp\yyxk_palette.json、Temp\yyxk_learn.txt、Temp\yyxk_deform.png
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
BIN = MOD / "Map/yyxk_2x.bin"
TEX = MOD / "Assets/Map/yyxk/BaseTex"
SC = MOD / "Scenario/Scenario1.json"
OUT = Path(r"D:\sango_infinity\Temp")
SEASONS = ("Autumn", "Spring", "Summer", "Winter")

log_lines = []


def log(s=""):
    print(s, flush=True)
    log_lines.append(s)


d = parse_bin(str(BIN))
GX, GY = d["cellsX"], d["cellsY"]
raw_terr = d["gridRaw"][..., 0].astype(np.int32)     # [x, y]
raw_state = (d["gridRaw"][..., 1].astype(np.uint32) | (d["gridRaw"][..., 2].astype(np.uint32) << 8)
             | (d["gridRaw"][..., 3].astype(np.uint32) << 16)
             | (d["gridRaw"][..., 4].astype(np.uint32) << 24))
raw_area = d["gridRaw"][..., 5].astype(np.int32) | (d["gridRaw"][..., 6].astype(np.int32) << 8)

log("=" * 76)
log(f"yyxk 地图: 格 {GX}x{GY}")
log(f"  地形取值 {sorted(set(raw_terr.ravel().tolist()))}")
log(f"  都市格(17) {int((raw_terr == 17).sum())}   内政位(state==4) {int((raw_state == 4).sum())}"
    f"   areaId 种类 {len(set(raw_area.ravel().tolist()))}")
log("=" * 76)

imgs = {}
for s, nm in enumerate(SEASONS):
    p = TEX / f"BaseMap{s}.png"
    im = Image.open(p).convert("RGB")
    imgs[s] = im
    log(f"BaseMap{s} ({nm}) {im.size[0]}x{im.size[1]}  {p.stat().st_size / 1024:.0f} KB  "
        f"{im.size[0] / GX:.3f}x{im.size[1] / GY:.3f} 像素/格")

a0 = np.asarray(imgs[0].resize((GX, GY), Image.Resampling.BOX), np.float32)   # [row, col]
luma0 = a0 @ [0.299, 0.587, 0.114]

# ---------------- 1) 朝向确认 ----------------
log("\n[1] 朝向确认：水/陆两类用底图亮度的可分性（这张图水偏亮，取 |差值| 最大者）")
WATER_IDS = [9, 21, 22]
ORIENTS = {
    "行=y列=x": lambda t: t.T,
    "行=x列=y": lambda t: t,
    "行=y列=x(x翻转)": lambda t: t.T[:, ::-1],
    "行=y列=x(y翻转)": lambda t: t.T[::-1, :],
}
best = None
for nm, f in ORIENTS.items():
    m = f(raw_terr)
    w = np.isin(m, WATER_IDS)
    l = np.isin(m, [1, 2, 3, 6, 16])
    if w.sum() < 100 or l.sum() < 100:
        continue
    vw, vl = float(luma0[w].mean()), float(luma0[l].mean())
    sw, sl = float(luma0[w].std()), float(luma0[l].std())
    sep = abs(vw - vl) / max(1e-6, (sw + sl) / 2)
    log(f"    {nm:<18} 水域亮度 {vw:6.1f}±{sw:4.1f}  陆地 {vl:6.1f}±{sl:4.1f}  可分性 {sep:.3f}")
    if best is None or sep > best[1]:
        best = (nm, sep, "水偏亮" if vw > vl else "水偏暗")
log(f"  -> 采用: {best[0]}（{best[2]}，可分性 {best[1]:.3f}）")
OR = ORIENTS[best[0]]
TERR = OR(raw_terr)
AREA = OR(raw_area)
STATE = OR(raw_state)
water_mask = np.isin(TERR, WATER_IDS)
log(f"  水域格 {int(water_mask.sum())} / {GX * GY} = {water_mask.mean():.3f}")

# ---------------- 2) 地格色 ----------------
log("\n[2] 地格色：按 terrainType 统计底图实际颜色（中位数 / 25~75 分位 / 标准差）")
palette = {}
for s, nm in enumerate(SEASONS):
    a = np.asarray(imgs[s].resize((GX, GY), Image.Resampling.BOX), np.float32)
    pal = {}
    for t in sorted(set(TERR.ravel().tolist())):
        m = TERR == t
        n = int(m.sum())
        if n < 20:
            continue
        v = a[m]
        q1, q2, q3 = np.percentile(v, [25, 50, 75], axis=0)
        pal[int(t)] = dict(n=n, rgb=[int(x) for x in q2.round(0)],
                           p25=[int(x) for x in q1.round(0)], p75=[int(x) for x in q3.round(0)],
                           std=round(float(v.std()), 1))
    palette[nm] = pal
    keys = sorted(pal)
    log(f"  {nm}:  " + " ".join(f"t{t}={tuple(pal[t]['rgb'])}" for t in keys[:8]))
    log(f"         " + " ".join(f"t{t}={tuple(pal[t]['rgb'])}" for t in keys[8:16]))
    log(f"         " + " ".join(f"t{t}={tuple(pal[t]['rgb'])}" for t in keys[16:]))
    # 类间离散度：如果美术是按地形上色，这个值应该很大；接近 0 说明不按地形上色
    med = np.array([pal[t]["rgb"] for t in keys], np.float32)
    log(f"         类间色差 std={med.std(0).round(1).tolist()}  "
        f"（近似 0 说明底图颜色不由地形决定）")
    # 水的特殊色
    wm = np.isin(TERR, WATER_IDS)
    if wm.any():
        log(f"         水色中位 {tuple(np.median(a[wm], 0).round(0).astype(int))}"
            f"   陆地 {tuple(np.median(a[~wm], 0).round(0).astype(int))}")

# ---------------- 3) 整体 ----------------
log("\n[3] 整体：每季基调与纹理感（高频 = 相邻像素平均绝对差）")
overall = {}
for s, nm in enumerate(SEASONS):
    f = np.asarray(imgs[s], np.float64)
    mx, mn = f.max(2), f.min(2)
    sat = float(np.where(mx > 0, (mx - mn) / np.maximum(mx, 1), 0).mean())
    g = f @ [0.299, 0.587, 0.114]
    hf = float((np.abs(np.diff(g, axis=1)).mean() + np.abs(np.diff(g, axis=0)).mean()) / 2)
    overall[nm] = dict(size=list(imgs[s].size),
                       mean=[round(float(x), 1) for x in f.reshape(-1, 3).mean(0)],
                       sat=round(sat, 3), luma=round(float(g.mean()), 1), hf=round(hf, 2))
    o = overall[nm]
    log(f"  {nm}: {o['size'][0]}x{o['size'][1]}  meanRGB={tuple(o['mean'])}  "
        f"sat={o['sat']:.3f}  luma={o['luma']:.1f}  高频={o['hf']:.2f}")

# ---------------- 4) 地域 ----------------
log("\n[4] 地域：按 areaId 统计颜色相对全局调色板的偏移（|偏移| 最大的前 8 个）")
glob = {t: np.array(v["rgb"], np.float32) for t, v in palette["Autumn"].items()}
region = {}
for aid in sorted(set(AREA.ravel().tolist())):
    if aid == 0:
        continue
    m = AREA == aid
    n = int(m.sum())
    if n < 300:
        continue
    v = a0[m]
    tt = TERR[m]
    exp = np.zeros((n, 3), np.float32)
    for t in np.unique(tt):
        if int(t) in glob:
            exp[tt == t] = glob[int(t)]
    off = (v - exp).mean(0)
    region[int(aid)] = dict(n=n, offset=[round(float(x), 1) for x in off])
top = sorted(region.items(), key=lambda kv: -abs(np.array(kv[1]["offset"])).sum())[:8]
for aid, r in top:
    log(f"    areaId {aid:>3}  格数 {r['n']:>6}  偏移 RGB {tuple(r['offset'])}")
log(f"  参与统计的区域数 {len(region)}")

# ---------------- 5) 形变测量 ----------------
log("\n[5] 形变：以「地形水掩膜 × 底图亮度」做相关，搜全局偏移与分块偏移场")
wmask = water_mask.astype(np.float32)
wm_c = wmask - wmask.mean()
lm_c = luma0 - luma0.mean()


def corr(dy, dx):
    e = wm_c[24:-24, 24:-24]
    o = lm_c[24 + dy:-24 + dy, 24 + dx:-24 + dx]
    if e.shape != o.shape:
        return None
    d1, d2 = np.sqrt((e * e).sum()), np.sqrt((o * o).sum())
    if d1 == 0 or d2 == 0:
        return None
    return float((e * o).sum() / (d1 * d2))


R = 12
sc = {}
for dy in range(-R, R + 1):
    for dx in range(-R, R + 1):
        v = corr(dy, dx)
        if v is not None:
            sc[(dy, dx)] = v
gb = max(sc, key=sc.get)
log(f"  零偏移相关 {sc[(0, 0)]:+.4f}   最佳偏移 (dy={gb[0]}, dx={gb[1]}) 相关 {sc[gb]:+.4f}")
log(f"  |相关| 最优 6 个: "
    f"{[(k, round(v, 4)) for k, v in sorted(sc.items(), key=lambda kv: -abs(kv[1]))[:6]]}")

T, ST = 32, 16
rows = []
for y0 in range(0, GY - T + 1, ST):
    for x0 in range(0, GX - T + 1, ST):
        bestt = None
        for dy in range(gb[0] - 4, gb[0] + 5):
            for dx in range(gb[1] - 4, gb[1] + 5):
                yy, xx = y0 + dy, x0 + dx
                if yy < 0 or xx < 0 or yy + T > GY or xx + T > GX:
                    continue
                e = wmask[y0:y0 + T, x0:x0 + T]
                o = luma0[yy:yy + T, xx:xx + T]
                ee = e - e.mean()
                oo = o - o.mean()
                d1, d2 = np.sqrt((ee * ee).sum()), np.sqrt((oo * oo).sum())
                if d1 == 0 or d2 == 0:
                    continue
                cc = float((ee * oo).sum() / (d1 * d2))
                if bestt is None or cc > bestt[2]:
                    bestt = (dy, dx, cc)
        if bestt:
            zero_cc = None
            e = wmask[y0:y0 + T, x0:x0 + T]
            o = luma0[y0:y0 + T, x0:x0 + T]
            ee, oo = e - e.mean(), o - o.mean()
            d1, d2 = np.sqrt((ee * ee).sum()), np.sqrt((oo * oo).sum())
            if d1 and d2:
                zero_cc = float((ee * oo).sum() / (d1 * d2))
            rows.append((x0, y0, bestt[0], bestt[1], bestt[2], zero_cc if zero_cc is not None else 0.0))
arr = np.array(rows, np.float32)
log(f"  分块 {len(rows)} 块（{T} 格/块，搜索范围 ±4 格）")
log(f"    块最佳偏移分布: "
    f"{Counter((int(r[2]), int(r[3])) for r in arr).most_common(6)}")
log(f"    块相关（最佳 / 零偏移）中位 {np.median(arr[:, 4]):+.3f} / {np.median(arr[:, 5]):+.3f}")
improved = arr[arr[:, 4] > arr[:, 5] + 0.06]
log(f"    靠平移明显改善的块 {len(improved)}/{len(arr)} = {len(improved) / len(arr):.3f}"
    f"   这些块的偏移: {Counter((int(r[2]), int(r[3])) for r in improved).most_common(6)}")

heat = np.full((GY // ST + 1, GX // ST + 1), np.nan, np.float32)
for x0, y0, _, _, _, z in arr:
    heat[int(y0) // ST, int(x0) // ST] = z
h = heat[:len(range(0, GY - T + 1, ST)), :len(range(0, GX - T + 1, ST))]
lo, hi = np.nanpercentile(h, 5), np.nanpercentile(h, 95)
hn = np.clip((h - lo) / max(1e-6, hi - lo), 0, 1)
rgb = np.stack([(hn * 255), ((1 - hn) * 160), ((1 - hn) * 255)], -1).astype(np.uint8)
rgb[np.isnan(h)] = 0
sheet = Image.new("RGB", (GX * 2 + 8, GY), (20, 20, 24))
sheet.paste(imgs[0].resize((GX, GY), Image.Resampling.BOX), (0, 0))
sheet.paste(Image.fromarray(rgb).resize((GX, GY), Image.Resampling.NEAREST), (GX + 8, 0))
dr = ImageDraw.Draw(sheet)
dr.text((4, 4), "左: mod 原底图(秋)", fill=(255, 255, 0))
dr.text((GX + 12, 4), "右: 水掩膜与亮度的相关(红=对得上,蓝=对不上)", fill=(255, 255, 0))
sheet.save(OUT / "yyxk_deform.png")
log(f"  已写出 {OUT / 'yyxk_deform.png'}")

(OUT / "yyxk_palette.json").write_text(json.dumps(
    {"map": "yyxk_2x.bin", "cells": [GX, GY], "orientation": best[0],
     "water_ids": WATER_IDS, "water_note": best[2],
     "palette": palette, "overall": overall, "region": region,
     "global_offset": [int(gb[0]), int(gb[1])],
     "global_corr_zero": round(sc[(0, 0)], 4), "global_corr_best": round(sc[gb], 4),
     "tiles": [[int(r[0]), int(r[1]), int(r[2]), int(r[3]), round(r[4], 3), round(r[5], 3)]
               for r in arr]},
    ensure_ascii=False, indent=1), encoding="utf-8")
(OUT / "yyxk_learn.txt").write_text("\n".join(log_lines), encoding="utf-8")
log(f"\n已写出 {OUT / 'yyxk_palette.json'} 与 yyxk_learn.txt")
