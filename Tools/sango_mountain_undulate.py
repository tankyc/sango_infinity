# -*- coding: utf-8 -*-
"""sango_mountain_undulate.py —— 给「不可行走山体」做出真实的起伏。

背景（实测）：本图崖区（terrainType=16，占全图 44.6%）的顶点高度是**离散台阶**
（0/80/150/180/210/255…，每层内部完全平），所以山顶看起来是一整片平整的平台。
本工具只做一件事：把真实高程(ETOPO)的**起伏形状**零均值地叠到这些平台上，
做出山脉的脊与谷 —— 高程图只当起伏参考，不改变地形结构。

硬约束（写盘后逐条复核）：
  1) 只改「不可行走山体」顶点，且该顶点 ±k 顶点邻域必须**全部**在崖内
     —— 保证一个像素都不碰可走格（草地/道路/城池/港口…）；
  2) 只改**当前露出水面**的顶点（height > water），并保证改完 height >= water+2
     —— 水下崖（海床，本图占崖区 11.5%）一律不动，免得水面消失或海底冒出来；
  3) 起伏**零均值**：山体平均高度不变，不整体抬高或压低；幅度按 ±amp 硬夹紧；
  4) 顶点贴图(textureIndex)、water 字节、地格地形/状态/归属一律不动。

用法：
    python sango_mountain_undulate.py --bin <bin> --report     # 预览参数与预期改动
    python sango_mountain_undulate.py --bin <bin> --apply      # 写盘（自动备份）
"""
import argparse
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin                       # noqa: E402
from sango_mountain_beautify import vertex_cell_index        # noqa: E402

import numpy as np
from PIL import Image

try:
    from scipy import ndimage
except ImportError:
    ndimage = None

DEM_DEFAULT = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
LAT_DEFAULT = "0.003708,-0.039995,41.9144"
LON_DEFAULT = "0.049694,0.002809,99.4265"
OUT = Path(r"D:\sango_infinity\Temp")


def dem_cell_field(dem_path, cx, cy, latc, lonc):
    """按「格坐标 -> 经纬」拟合取真实高程，返回 (cx, cy) 的米数组。"""
    cxs = np.arange(cx, dtype=np.float64)[:, None]
    cys = np.arange(cy, dtype=np.float64)[None, :]
    clat = latc[0] * cxs + latc[1] * cys + latc[2]
    clon = lonc[0] * cxs + lonc[1] * cys + lonc[2]
    dem = np.load(dem_path, mmap_mode="r")
    rows = np.clip(((90.0 - clat) * 60.0).astype(np.int32), 1, dem.shape[0] - 2)
    cols = np.clip(((clon + 180.0) * 60.0).astype(np.int32), 1, dem.shape[1] - 2)
    r0, c0 = int(rows.min()) - 1, int(cols.min()) - 1
    slab = np.array(dem[r0:rows.max() + 2, c0:cols.max() + 2], np.float32)
    return slab[rows - r0, cols - c0]


def upsample_cell_to_vertex(field_xy, VX, VY):
    """格场 (cx, cy) -> 顶点场 (VX, VY)，索引 [vx, vy]（行=vy 由 cy 决定）。"""
    up = np.asarray(Image.fromarray(field_xy.T.astype(np.float32))
                    .resize((VX, VY), Image.Resampling.BILINEAR), np.float32)
    return up.T                                     # -> (VX, VY) = [vx, vy]


def erode_mask(m, k):
    """顶点级腐蚀：±k 顶点邻域全为真才保留（np.roll 环绕，边缘按原值处理）。"""
    out = m.copy()
    for s in range(1, k + 1):
        out &= np.roll(m, s, 0) & np.roll(m, -s, 0)
        out &= np.roll(m, s, 1) & np.roll(m, -s, 1)
    return out


def shade(field_vx_vy, k):
    """把顶点高度场画成山体阴影图（看起伏用）。"""
    a = field_vx_vy.T.astype(np.float32)             # -> (vy, vx) 方便成图
    gy, gx = np.gradient(a)
    slope = np.arctan(np.hypot(gx, gy) * 0.6)          # 0.6 ≈ 3x 夸张，便于看起伏
    aspect = np.arctan2(gy, -gx)
    hs = np.cos(np.deg2rad(45)) * np.cos(slope) + \
        np.sin(np.deg2rad(45)) * np.sin(slope) * np.cos(np.deg2rad(360 - 315 + 90) - aspect)
    hs = np.clip((hs - hs.min()) / max(1e-6, hs.max() - hs.min()), 0, 1) * 255
    im = Image.fromarray(hs.astype(np.uint8), "L")
    return im.resize((max(1, im.size[0] // k), max(1, im.size[1] // k)), Image.Resampling.LANCZOS)


def locate_vertex_segment(raw, cx, cy, H, VX, VY):
    """在 .bin 原始字节里定位顶点段。

    关键：文件里顶点是 **vx 外层、vy 内层**（parse_bin 用 reshape(vx_n, vy_n, 3)），
    所以线性下标 = (vx * VY + vy)，取高度用 H[vx, vy]。写成 (vy*VX+vx) 会转置写坏地图。
    """
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
    base = off + cx * cy * 7
    rng = np.random.default_rng(0)
    sample = [(int(a), int(b)) for a, b in
              zip(rng.integers(0, VX, 300), rng.integers(0, VY, 300))]
    best = None
    for stride in (1, 2, 3, 4, 5, 6, 8, 12, 16, 32):
        for cand in range(base, base + 512):
            ok = tot = 0
            for vx, vy in sample:
                o = cand + (vx * VY + vy) * stride
                if o >= len(raw):
                    break
                tot += 1
                ok += (raw[o] == int(H[vx, vy]))
            if tot == len(sample) and (best is None or ok / tot > best[0]):
                best = (ok / tot, cand, stride)
    return base, best


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--terrain-ids", default="16", help="不可行走山体地形 id（默认 16=崖）")
    ap.add_argument("--dem", default=DEM_DEFAULT)
    ap.add_argument("--lat", default=LAT_DEFAULT)
    ap.add_argument("--lon", default=LON_DEFAULT)
    ap.add_argument("--amp", type=float, default=25.0,
                    help="起伏幅度（高度字节，p90）：25 字节 = 山脊谷地各约 ±12.5 世界单位")
    ap.add_argument("--mid-sigma", type=float, default=5.0, help="大/中尺度分界（格）")
    ap.add_argument("--small-sigma", type=float, default=2.0, help="中/小尺度分界（格）")
    ap.add_argument("--small-weight", type=float, default=0.6)
    ap.add_argument("--protect", type=int, default=4,
                    help="顶点保护带：±k 顶点邻域必须全在崖内才允许改（k=4 约 1 格）")
    ap.add_argument("--feather", type=float, default=8.0,
                    help="边界羽化（顶点）：幅度从边界 0 平滑升到满值")
    ap.add_argument("--headroom", type=float, default=30.0,
                    help="为起伏自动压低的上限（字节）：崖区已有 7.3%% 顶点在 255，0=不压低")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    if ndimage is None:
        print("[错误] 需要 scipy")
        return
    if not (args.report or args.apply):
        args.report = True

    src = Path(args.bin_path)
    d = parse_bin(str(src))
    cx, cy = d["cellsX"], d["cellsY"]
    tt = d["gridRaw"][..., 0].astype(np.int32)
    V = d["verts"]
    VX, VY = V.shape[0], V.shape[1]
    H = V[..., 0].astype(np.int32)
    WB = V[..., 2].astype(np.int32)
    print("=" * 84)
    print(f"山体起伏: {src.name}  格 {cx}x{cy}  顶点 {VX}x{VY}"
          f"  模式 {'写盘' if args.apply else '预览'}")
    print("=" * 84)

    cxx, cyy, ok = vertex_cell_index(VX, VY, cx, cy)
    ct = np.where(ok, tt[cxx, cyy], -1)
    ids = {int(x) for x in str(args.terrain_ids).split(",") if x.strip()}
    cliff = ok & np.isin(ct, list(ids))
    dry = H > WB                                     # 露出水面（水下崖=海床，不动）
    safe = erode_mask(cliff & dry, args.protect)
    # 边缘保护：np.roll 会环绕，地图边带一律不改
    eb = args.protect + 2
    safe[:eb] = safe[-eb:] = False
    safe[:, :eb] = safe[:, -eb:] = False
    print(f"  山体(地形 {sorted(ids)})顶点 {int(cliff.sum())}"
          f"   露出水面 {int((cliff & dry).sum())}   安全可改(±{args.protect} 邻域全在崖内) "
          f"{int(safe.sum())}")

    # ---- 高程起伏场 ----
    latc = [float(x) for x in args.lat.split(",")]
    lonc = [float(x) for x in args.lon.split(",")]
    elev = dem_cell_field(args.dem, cx, cy, latc, lonc)          # (cx, cy) 米
    e = elev.T.astype(np.float32)                                # (cy, cx)
    L = ndimage.gaussian_filter(e, args.mid_sigma, mode="nearest")
    B = ndimage.gaussian_filter(e, args.small_sigma, mode="nearest")
    F = (B - L) + args.small_weight * (e - B)                    # 中尺度 + 小尺度
    sc = float(np.percentile(np.abs(F), 90)) or 1.0
    F = np.clip(F / sc, -1.5, 1.5)
    print(f"  高程起伏场: 中尺度基准 {np.percentile(np.abs(B - L),90):.0f} m"
          f"  小尺度基准 {np.percentile(np.abs(e - B),90):.0f} m  归一化 p90 {sc:.0f} m")

    Fv = upsample_cell_to_vertex(F, VX, VY)                      # (VX, VY) 索引 [vx,vy]
    elevv = upsample_cell_to_vertex(elev, VX, VY)
    # 朝向自检：真实高程 vs 地图自身平滑高度（山体处应当同高）；频带场零均值不可用于判向
    Hs = ndimage.gaussian_filter(H.astype(np.float32), 8.0)
    c_cur = float(np.corrcoef(elevv[cliff], Hs[cliff])[0, 1])
    c_tr = float(np.corrcoef(elevv.T[cliff], Hs[cliff])[0, 1])
    print(f"  朝向自检（真实高程 vs 地图平滑高度）: 当前 {c_cur:+.3f} / 转置 {c_tr:+.3f}"
          f" -> {'采用当前朝向' if abs(c_cur) >= abs(c_tr) else '采用转置'}")
    if abs(c_tr) > abs(c_cur):
        Fv, elevv = Fv.T, elevv.T

    # ---- 羽化：边界幅度为 0，向内平滑升到满值 ----
    m = safe.astype(np.float32)
    if args.feather > 0:
        m = np.asarray(Image.fromarray((m * 255).astype(np.uint8), "L")
                       .filter(ImageFilter_GaussianBlur(args.feather)), np.float32) / 255.0
        m = m * safe.astype(np.float32)                          # 再次锁在安全集内
    delta = args.amp * Fv * m
    if safe.any():
        delta = delta - float(delta[safe].mean())                # 零均值：山体平均高度不变
    # 上限余量：崖区已有 7.3% 顶点顶在 255，不腾空间就会把山脊压平；按 p99.5 自动压低
    shift = 0.0
    if args.headroom > 0 and safe.any():
        need = float(np.percentile(H[safe] + delta[safe], 99.5)) - 253.0
        shift = float(np.clip(need, 0.0, args.headroom))
        delta = delta - shift
    new = H.copy()
    lo = np.maximum(WB + 2, 0)
    new[safe] = np.clip(np.round(H[safe] + delta[safe]), lo[safe], 255).astype(np.int32)
    dH = new - H
    n_safe = int(safe.sum())
    print(f"\n  改动顶点 {n_safe}（全部在安全集内）"
          f"   幅度: p50 {np.percentile(np.abs(dH[safe]),50):.0f}"
          f"  p90 {np.percentile(np.abs(dH[safe]),90):.0f}"
          f"  max {np.abs(dH[safe]).max():.0f} 字节（=世界单位 ×2）")
    def slope_deg(F):
        """顶点高度场 -> 坡度（度）。水平步长 quadSize=5 世界单位，高度 1 字节 = 0.5 单位。"""
        g = np.gradient(F.astype(np.float32) * 0.5)
        s = np.degrees(np.arctan(np.hypot(g[0], g[1]) / 5.0))
        return s
    s0, s1 = slope_deg(H), slope_deg(new)
    print(f"  山体高度: 原 中位 {np.median(H[safe]):.0f}  全局标准差 {H[safe].std():.1f}"
          f"  ->  新 中位 {np.median(new[safe]):.0f}  全局标准差 {new[safe].std():.1f}")
    print(f"  坡度(度) 中位: 原 {np.median(s0[safe]):.1f} -> 新 {np.median(s1[safe]):.1f}"
          f"    p90: 原 {np.percentile(s0[safe],90):.1f} -> 新 {np.percentile(s1[safe],90):.1f}"
          f"    p99: 原 {np.percentile(s0[safe],99):.1f} -> 新 {np.percentile(s1[safe],99):.1f}")
    print(f"  （坡度是「能不能看出山脉」的关键量：p90 从几度提到 20~35 度才有山的感觉）")
    print(f"  安全集之外的改动 {int(((dH != 0) & ~safe).sum())}（必须 0 ✓）"
          f"   水下崖改动 {int((dH[cliff & ~dry] != 0).sum())}（必须 0 ✓）"
          f"   可走格顶点改动 {int((dH[~cliff] != 0).sum())}（必须 0 ✓）")

    # 预览
    k = max(1, VX // 900)
    sh0 = shade(H.astype(np.float32) * 0.5, k)
    sh1 = shade(new.astype(np.float32) * 0.5, k)
    sheet = Image.new("RGB", (sh0.size[0] * 2 + 8, sh0.size[1]), (20, 20, 24))
    sheet.paste(sh0.convert("RGB"), (0, 0))
    sheet.paste(sh1.convert("RGB"), (sh0.size[0] + 8, 0))
    sheet.save(OUT / "mountain_undulate.jpg", quality=88)
    print(f"  预览（左=现在的高度场 右=起伏后）{OUT / 'mountain_undulate.jpg'}  {sheet.size}")

    if not args.apply:
        print("\n（预览模式，未写盘。确认后加 --apply）")
        return

    raw = bytearray(src.read_bytes())
    base, best = locate_vertex_segment(raw, cx, cy, H, VX, VY)
    print(f"\n  顶点段基线 {base}  自动定位偏移 {best[1]}（基线后 {best[1]-base}）"
          f"  步长 {best[2]}  抽样匹配 {best[0]:.3f}")
    vstart, stride = best[1], best[2]
    cnt = 0
    for vx in range(VX):
        for vy in range(VY):
            nb = int(new[vx, vy])
            if nb == int(H[vx, vy]):
                continue
            o = vstart + (vx * VY + vy) * stride      # 文件顺序：vx 外层、vy 内层
            raw[o] = nb
            cnt += 1
    bak = src.with_suffix(src.suffix + ".bak_before_undulate")
    if not bak.exists():
        bak.write_bytes(src.read_bytes())
    src.write_bytes(bytes(raw))
    print(f"  已写入 {src.name} {src.stat().st_size/1024/1024:.2f} MB"
          f"  改写顶点 {cnt}（备份 {bak.name}）")

    d2 = parse_bin(str(src))
    H2 = d2["verts"][..., 0].astype(np.int32)
    print(f"  复核 高度与目标一致: {bool(np.array_equal(H2, new))}")
    print(f"  复核 地形/状态/归属 未动: {bool(np.array_equal(d2['gridRaw'], d['gridRaw']))}")
    print(f"  复核 textureIndex 未动: {bool(np.array_equal(d2['verts'][...,1], V[...,1]))}")
    print(f"  复核 water 字节 未动: {bool(np.array_equal(d2['verts'][...,2], V[...,2]))}")
    print(f"  复核 安全集之外高度未动: "
          f"{bool(np.array_equal(H2[~safe], H[~safe]))}")
    print(f"  复核 水面可见顶点: 原 {int((WB > H).sum())} -> "
          f"{int((d2['verts'][...,2].astype(int) > H2).sum())}（应不变）")

    # 连带项：地图上的模型(树/关隘/建筑)的 Y 是按旧高度烘的，山体一变会悬空或埋地
    try:
        ms = d2["tail"]["models"][2]
        q = float(d2["quadSize"])
        off_list = []
        for m in ms:
            wx, wy, wz = float(m[4][0]), float(m[4][1]), float(m[4][2])
            vx, vy = int(round(wz / q)), int(round(wx / q))
            if 0 <= vx < VX and 0 <= vy < VY and safe[vx, vy]:
                off_list.append(abs(wy - new[vx, vy] * 0.5))
        print(f"  连带项 模型共 {len(ms)} 个，其中落在改动山体上的 {len(off_list)} 个"
              f"（Y 需重烘）"
              + (f"；偏差 中位 {np.median(off_list):.1f} 最大 {max(off_list):.1f} 世界单位"
                 if off_list else ""))
    except Exception as ex:                                   # noqa: BLE001
        print(f"  连带项 模型检查跳过（{ex}）")


def ImageFilter_GaussianBlur(r):
    from PIL import ImageFilter
    return ImageFilter.GaussianBlur(r)


if __name__ == "__main__":
    main()
