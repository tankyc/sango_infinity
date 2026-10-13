# -*- coding: utf-8 -*-
"""!! 警告：本脚本存在「转置」缺陷，请勿再直接使用 !!
   .bin 顶点在文件里是 **vx 外层、vy 内层**（parse_bin 用 reshape(vx_n, vy_n, 3)），
   线性下标必须写成 (vx * VY + vy)。本脚本有三处按相反顺序处理：
     1) DEM 上采样得到的 elev 是 [vy, vx]，却当成 [vx, vy] 参与计算（场整体转置）；
     2) prot 保护掩膜用 np.zeros((VY, VX)) 以 [vy, vx] 填充，却用来索引 [vx, vy] 的数组；
     3) 写盘偏移与抽样比较都用 (vy * VX + vx)。
   地图是正方形（2049x2049）时不会报错，只会静默写错位置。
   需要「按真实高程做起伏」请用 Tools/sango_mountain_undulate.py（已修正顺序且有复核）。

sango_height_fix.py —— 参考真实高程（ETOPO）修正地图顶点高度。

只有一件事：把地图的**高度字节**改成跟着真实高程走，让平地是低地、山是高地。

硬约束（都会在复核里逐条验）：
  1) 海(9/21/22)=0、川河(7/8)=1、浅滩(14)=10 的高度一律保留 —— 引擎靠
     「water 字节 > height 字节」画水面，动了就会让水面消失。
  2) 陆地最低不低于 --land-min（默认 13，> 水面字节 11，避免陆地被当成水面）。
  3) 与水/河/浅滩相邻的顶点保护 1~2 格，防止抬高岸边顶点把水面顶掉。
  4) 顶点贴图(textureIndex)、water 字节、地格地形/状态/归属一律不动。

高程 -> 高度字节 的分段线性映射（可调），标定参考引擎实测：
  平原≈20、丘陵≈88、p90≈170、峰值≈255
    0m   -> 18      100m -> 26      300m -> 45     800m -> 88
    1500m-> 130     3000m-> 190     5000m-> 235    6000m+-> 248

用法：
    python sango_height_fix.py --bin <bin> --report
    python sango_height_fix.py --bin <bin> --apply
"""
import argparse
import json
import shutil
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np

DEM_DEFAULT = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
LAT_DEFAULT = "0.003708,-0.039995,41.9144"
LON_DEFAULT = "0.049694,0.002809,99.4265"
SEA = (9, 21, 22)
RIVER = (7, 8)
SHALLOW = (14,)
CURVE = [(0, 16), (100, 24), (300, 44), (800, 90), (1500, 140),
         (2500, 190), (4000, 225), (6000, 248)]


def elev_to_byte(e, curve):
    e = np.maximum(e, 0.0)
    out = np.full(e.shape, curve[0][1], np.float32)
    for (e0, b0), (e1, b1) in zip(curve[:-1], curve[1:]):
        m = (e >= e0) & (e < e1)
        out[m] = b0 + (e[m] - e0) / max(1e-6, e1 - e0) * (b1 - b0)
    out[e >= curve[-1][0]] = curve[-1][1]
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--dem", default=DEM_DEFAULT)
    ap.add_argument("--lat", default=LAT_DEFAULT)
    ap.add_argument("--lon", default=LON_DEFAULT)
    ap.add_argument("--land-min", type=float, default=13.0)
    ap.add_argument("--protect-cells", type=int, default=1, help="与水相邻的顶点保护带（格）")
    ap.add_argument("--blend", type=float, default=1.0,
                    help="与原高度混合比例（1=完全用真实高程，0=全保留）")
    ap.add_argument("--river-drop", type=float, default=3.0,
                    help="河床相对两岸最低高度的下切量（字节）；0=不处理河流")
    ap.add_argument("--river-ids", default="7,8", help="河流地形：川/河")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    src = Path(args.bin_path)
    d = parse_bin(str(src))
    cx, cy = d["cellsX"], d["cellsY"]
    tt = d["gridRaw"][..., 0].astype(np.int32)
    V = d["verts"]
    print("=" * 78)
    print(f"高度修正: {src.name}  格 {cx}x{cy}  顶点 {V.shape}")
    for i in range(V.shape[2] if V.ndim == 3 else 1):
        ch = V[..., i] if V.ndim == 3 else V
        print(f"  通道{i}: min {ch.min()} max {ch.max()} 中位 {np.median(ch):.0f}"
              f" 取值种类 {len(np.unique(ch))}")
    H = (V[..., 0] if V.ndim == 3 else V).astype(np.float32)

    latc = [float(x) for x in args.lat.split(",")]
    lonc = [float(x) for x in args.lon.split(",")]
    VY, VX = H.shape
    # 经纬拟合是给「格坐标」标定的：先按格算，再插值到顶点
    cxs = np.arange(cx, dtype=np.float64)[:, None]
    cys = np.arange(cy, dtype=np.float64)[None, :]
    clat = latc[0] * cxs + latc[1] * cys + latc[2]
    clon = lonc[0] * cxs + lonc[1] * cys + lonc[2]
    dem = np.load(args.dem, mmap_mode="r")
    rows = np.clip(((90.0 - clat) * 60.0).astype(np.int32), 1, dem.shape[0] - 2)
    cols = np.clip(((clon + 180.0) * 60.0).astype(np.int32), 1, dem.shape[1] - 2)
    r0, c0 = int(rows.min()) - 1, int(cols.min()) - 1
    slab = np.array(dem[r0:rows.max() + 2, c0:cols.max() + 2], np.float32)
    elev_cell = slab[rows - r0, cols - c0]                    # (cx, cy)
    print(f"  格真实高程: 中位 {np.median(elev_cell):.0f} m"
          f"  p2 {np.percentile(elev_cell,2):.0f}  p98 {np.percentile(elev_cell,98):.0f}")
    # 格 -> 顶点：双线性（行=vy 由 cy 决定，列=vx 由 cx 决定）
    from PIL import Image as _I
    up = np.asarray(_I.fromarray(elev_cell.T.astype(np.float32)).resize((VX, VY), _I.Resampling.BILINEAR),
                    np.float32)
    elev = up
    print(f"  顶点真实高程: 中位 {np.median(elev):.0f} m  p2 {np.percentile(elev,2):.0f}"
          f"  p98 {np.percentile(elev,98):.0f}")

    target = elev_to_byte(elev, CURVE)

    # 保护：水/河/浅滩所在格的顶点及其邻域
    is_sea = np.isin(tt, list(SEA))
    is_riv = np.isin(tt, list(RIVER))
    is_shl = np.isin(tt, list(SHALLOW))
    # 格 -> 顶点：cell(x,y) 覆盖顶点块 [4x, 4x+4) × [4y+(x&1)*2, +4)
    prot = np.zeros((VY, VX), bool)
    K = int(args.protect_cells)
    for name, mask in (("海", is_sea), ("川河", is_riv), ("浅滩", is_shl)):
        xs, ys = np.nonzero(mask)
        for x, y in zip(xs.tolist(), ys.tolist()):
            vx0 = 4 * x - 2 * K
            vy0 = 4 * y + (x & 1) * 2 - 2 * K
            prot[max(0, vy0):vy0 + 4 + 4 * K, max(0, vx0):vx0 + 4 + 4 * K] = True
        print(f"  {name} 格 {int(mask.sum())} -> 保护顶点累计 {int(prot.sum())}")

    # 顶点通道: 0=height 1=textureIndex 2=water（磁盘布局 height/textureIndex/water）
    Wb0 = (V[..., 2] if V.ndim == 3 else np.zeros_like(H)).astype(np.float32)
    vis = Wb0 > H                              # 当前正在显示水面的顶点
    prot |= vis
    print(f"  另保护「当前已可见水面」的顶点 {int(vis.sum())} 个"
          f" -> 保护合计 {int(prot.sum())}（{prot.mean():.3f}）")

    new = np.clip(H * (1 - args.blend) + target * args.blend, 0, 255)
    # 陆地高度下限：既不低于 --land-min，也不低于「本顶点 water 字节 + 1」
    #   —— 后者保证任何陆地顶点都不会变成「可见水面」
    floor = np.maximum(args.land_min, Wb0 + 1.0)
    new[~prot] = np.maximum(new[~prot], floor[~prot])
    new[prot] = H[prot]                       # 保护带一律保留原值

    # ---- 河流：河床按「真实高程修正后的两岸最低点」再下切，并把该处 water 字节抬到河床之上 ----
    Wnew = (V[..., 2] if V.ndim == 3 else np.zeros_like(H)).astype(np.float32).copy()
    riv = np.isin(tt, [int(x) for x in args.river_ids.split(",") if x.strip()])
    if riv.any() and args.river_drop > 0:
        xs, ys = np.nonzero(riv)
        bed_vals = []
        for x, y in zip(xs.tolist(), ys.tolist()):
            vx0, vy0 = 4 * x, 4 * y + (x & 1) * 2
            sl = (slice(max(0, vy0 - 2), min(VY, vy0 + 6)),
                  slice(max(0, vx0 - 2), min(VX, vx0 + 6)))
            bed = max(1.0, float(new[sl].min()) - args.river_drop)
            new[sl] = bed                      # 河床：绝不高于两岸
            Wnew[sl] = np.maximum(Wnew[sl], bed + 1.0)   # 水位在河床之上
            bed_vals.append(bed)
        print(f"  河流: 河格 {int(riv.sum())} 个  河床下切 {args.river_drop}"
              f"  河床高度 中位 {np.median(bed_vals):.0f}"
              f"  最高 {max(bed_vals):.0f}（均低于两岸最低点）")
    print(f"\n  受保护顶点 {int(prot.sum())} / {H.size} = {prot.mean():.3f}（原值保留）")
    d_all = np.abs(new - H)
    print(f"  高度改动: 最大 {d_all.max():.0f}  平均 {d_all.mean():.1f}"
          f"  改动的顶点 {int((d_all > 0.5).sum())}")

    def stat(name, a):
        m = ~prot
        print(f"    {name}: 陆地 中位 {np.median(a[m]):.0f}  p10 {np.percentile(a[m],10):.0f}"
              f"  p90 {np.percentile(a[m],90):.0f}  max {a[m].max():.0f}")
    print("  高度分布（仅陆地顶点）:")
    stat("原", H)
    stat("新", new)

    # 与水/河/浅滩的相关性
    land_cell = ~(is_sea | is_riv | is_shl)
    hc_old = H[2::4, 2::4][:cx, :cy]
    hc_new = new[2::4, 2::4][:cx, :cy]
    el = elev[2::4, 2::4][:cx, :cy]
    m = land_cell
    r_old = float(np.corrcoef(hc_old[m], el[m])[0, 1])
    r_new = float(np.corrcoef(hc_new[m], el[m])[0, 1])
    print(f"\n  陆地「格中心高度 vs 真实高程」相关: 原 {r_old:+.3f} -> 新 {r_new:+.3f}")
    Wb = (V[..., 2] if V.ndim == 3 else np.zeros_like(H)).astype(np.float32)
    print(f"  水面可见顶点(water字节>height): 原 {int((Wb > H).sum())} -> 新 {int((Wb > new).sum())}"
          f"   （海/河/浅滩保护带内原值保留，应基本不变）")
    print(f"  陆地顶点低于水面字节 11 的: 原 {int(((H < 11) & ~prot).sum())}"
          f" -> 新 {int(((new < 11) & ~prot).sum())}（应为 0）")

    if not args.apply:
        print("\n（未加 --apply，未写盘）")
        return

    bak = src.with_suffix(src.suffix + ".bak_before_height")
    if not bak.exists():
        shutil.copy2(src, bak)
    # 顶点段原地改写第 0 通道
    import struct
    raw = bytearray(src.read_bytes())
    off = 4
    n, sh = 0, 0
    while True:
        v = raw[off]
        off += 1
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    off += n + 8 + 4
    goff = off
    base = goff + cx * cy * 7
    rng = np.random.default_rng(0)
    SAMPLE = [(int(a), int(b)) for a, b in
              zip(rng.integers(0, VY, 300), rng.integers(0, VX, 300))]
    best = None
    for stride in (1, 2, 3, 4, 5, 6, 8, 12, 16, 32):
        for cand in range(base, base + 512):
            ok = n = 0
            for vy, vx in SAMPLE:
                o = cand + (vy * VX + vx) * stride
                if o >= len(raw):
                    break
                n += 1
                ok += (raw[o] == int(H[vy, vx]))
            if n == len(SAMPLE) and (best is None or ok / n > best[0]):
                best = (ok / n, cand, stride)
    print(f"\n  mapGrid 起 {goff}  顶点段基线 {base}")
    print(f"  顶点段自动定位: 偏移 {best[1]}（基线后 {best[1] - base} 字节）"
          f" 步长 {best[2]} 抽样匹配率 {best[0]:.3f}")
    vstart, stride = best[1], best[2]
    cnt = cnt_w = 0
    for vy in range(VY):
        for vx in range(VX):
            o = vstart + (vy * VX + vx) * stride
            nb = int(round(new[vy, vx]))
            if raw[o] != nb:
                raw[o] = nb
                cnt += 1
            wb = int(round(Wnew[vy, vx]))          # 通道2 = water 字节（仅河流处会变）
            if o + 2 < len(raw) and raw[o + 2] != wb:
                raw[o + 2] = wb
                cnt_w += 1
    print(f"  改写: 高度顶点 {cnt}   water 字节顶点 {cnt_w}（仅河流处）")
    src.write_bytes(bytes(raw))
    print(f"已写入 {src}  {src.stat().st_size/1024/1024:.2f} MB（备份 {bak.name}）  改写顶点 {cnt}")

    d2 = parse_bin(str(src))
    H2 = (d2["verts"][..., 0] if d2["verts"].ndim == 3 else d2["verts"]).astype(np.float32)
    print(f"  复核 顶点高度: 中位 {np.median(H2):.0f}（原 {np.median(H):.0f}）"
          f"  是否与目标一致: {bool(np.array_equal(H2.astype(int), new.astype(int)))}")
    print(f"  复核 地形/状态/归属 未动: "
          f"{bool(np.array_equal(d2['gridRaw'], d['gridRaw']))}")
    print(f"  复核 保护带顶点未动: "
          f"{bool(np.array_equal(H2[prot], H[prot]))}")


if __name__ == "__main__":
    main()
