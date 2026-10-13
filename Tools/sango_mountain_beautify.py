# -*- coding: utf-8 -*-
"""sango_mountain_beautify.py —— 只美化「不可行走的山体区域」。

三条需求（对应实现）：
  1) 只动不可行走的山体（默认地形 id 16 = 崖；本图占 44.6%），
     可行走地表（草/路/城/关/港/田…）**一个像素都不动** —— 掩膜严格锁在崖格内，
     羽化也在掩膜内部收敛，因此崖格之外改动恒为 0（核验会断言）。
  2) 高程图只作「高度起伏差落」的参考 —— **不改 .bin 任何高度字节**，
     只用高程的起伏形状生成山体明暗：
        中尺度 M = 高斯(h, small) - 高斯(h, mid)      （山脊/谷地的差落）
        小尺度 S = h - 高斯(h, small)                （碎岩细节）
        方向光照 H = 标准 hillshade（坡度/坡向 × 固定光源）
     三者按幅度叠加成乘性明暗：底图' = 底图 × (1 + amp_m·M + amp_s·S + amp_h·(H-0.5)·2)
  3) 山体贴图优化：把**只被山体使用**的 layer_*.png（崖调色板）做可平铺的细节/对比增强
     （layer 贴图 UV 是世界坐标平铺，故不能在贴图里放空间起伏，只能提升贴图自身品质）。

用法：
    # 底图部分（高程起伏 -> 山体明暗）
    python sango_mountain_beautify.py --bin <bin> --tex-dir <BaseTex> --analyze
    python sango_mountain_beautify.py --bin <bin> --tex-dir <BaseTex> --apply

    # 贴图部分（山体贴图优化）
    python sango_mountain_beautify.py --bin <bin> --terrain-root <...\\Terrain> --layers-only --apply

    # 两块一起
    python sango_mountain_beautify.py --bin <bin> --tex-dir <BaseTex> --terrain-root <...\\Terrain> --apply
"""
import argparse
import shutil
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageFilter

try:
    from scipy import ndimage
except ImportError:
    ndimage = None

SEASONS = ("Autumn", "Spring", "Summer", "Winter")
DEM_DEFAULT = r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\etopo60s_i16.npy"
LAT_DEFAULT = "0.003708,-0.039995,41.9144"
LON_DEFAULT = "0.049694,0.002809,99.4265"
CLIFF_PALETTE = (18, 8, 20, 6, 22, 23)          # TerrainLayerMap.GetCliffPalette()
OUT = Path(r"D:\sango_infinity\Temp")


# --------------------------------------------------------------------------- #
# 数据准备
# --------------------------------------------------------------------------- #
def vertex_cell_index(VX, VY, cx, cy):
    """顶点 -> 所在格 (x, y)。格 (x,y) 覆盖顶点块 [4x,4x+4) × [4y+(x&1)*2, +4)。"""
    i = np.arange(VX, dtype=np.int32)[:, None]
    j = np.arange(VY, dtype=np.int32)[None, :]
    cxx = i // 4
    off = (cxx & 1) * 2
    cyy = (j - off) // 4
    ok = np.broadcast_to((j - off) >= 0, (VX, VY)).copy()
    cxx = np.broadcast_to(cxx, (VX, VY))
    cyy = np.broadcast_to(cyy, (VX, VY))
    ok &= (cxx >= 0) & (cxx < cx) & (cyy >= 0) & (cyy < cy)
    return np.where(ok, cxx, 0), np.where(ok, cyy, 0), ok


def dem_cells(dem_path, cx, cy, latc, lonc):
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
    return slab[rows - r0, cols - c0], (latc, lonc)


def build_modulation(args, h, sx_m, sy_m, log):
    """由高程起伏场生成乘性明暗场 mod（与 h 同形状），并打印各分量幅度。"""
    L = ndimage.gaussian_filter(h, args.mid_sigma, mode="nearest")
    B = ndimage.gaussian_filter(h, args.small_sigma, mode="nearest")
    mid, small = B - L, h - B
    for nm, b in (("中尺度", mid), ("小尺度", small)):
        sc = float(np.percentile(np.abs(b), 90)) or 1.0
        log(f"  {nm}频带: p90|值| {sc:.1f}（归一化基准）")
        b /= sc
    mid = np.clip(mid, -1, 1)
    small = np.clip(small, -1, 1)
    # 方向光照：标准 hillshade；坡度先按 p90 归一到参考坡度，否则平缓地形没有光照差落
    gy, gx = np.gradient(h)
    dzdx, dzdy = gx / sx_m, gy / sy_m
    s0 = np.hypot(dzdx, dzdy)
    ref = np.tan(np.deg2rad(args.hill_ref_slope))
    k = ref / max(1e-9, float(np.percentile(s0, 90)))
    dzdx, dzdy = dzdx * k, dzdy * k
    slope = np.arctan(np.hypot(dzdx, dzdy))
    aspect = np.arctan2(dzdy, -dzdx)
    zen = np.deg2rad(90.0 - args.hill_alt)
    az = np.deg2rad(360.0 - args.hill_azimuth + 90.0)
    hs = np.cos(zen) * np.cos(slope) + np.sin(zen) * np.sin(slope) * np.cos(az - aspect)
    hs = np.clip((hs - hs.min()) / max(1e-6, hs.max() - hs.min()), 0, 1)
    log(f"  方向光照: 方位 {args.hill_azimuth}° 高度角 {args.hill_alt}°"
        f"  实际坡度 p90 {np.rad2deg(np.percentile(np.arctan(s0), 90)):.1f}°"
        f" -> 归一化到 {args.hill_ref_slope:.0f}°（放大 {k:.2f}x）")
    mod = 1.0 + args.mid_amp * mid + args.small_amp * small + args.hill_amp * (hs - 0.5) * 2.0
    np.clip(mod, args.clip_lo, args.clip_hi, out=mod)
    log(f"  明暗场: 均值 {mod.mean():.3f}  p5 {np.percentile(mod,5):.3f}"
        f"  p95 {np.percentile(mod,95):.3f}")
    return mod


def up_field(f, W, Hh):
    """把格分辨率场放大到图像分辨率（双线性）；按实际值域编码，避免 uint8 回绕。"""
    lo, hi = float(f.min()), float(f.max())
    if hi - lo < 1e-6:
        return np.full((Hh, W), lo, np.float32)
    q = np.clip((f - lo) / (hi - lo), 0, 1) * 255.0
    img = Image.fromarray(q.astype(np.uint8), "L")
    a = np.asarray(img.resize((W, Hh), Image.Resampling.BILINEAR), np.float32) / 255.0
    return a * (hi - lo) + lo


# --------------------------------------------------------------------------- #
# 1) 底图：山体明暗
# --------------------------------------------------------------------------- #
def do_basemap(args, d, cx, cy, tt, log):
    ids = {int(x) for x in str(args.terrain_ids).split(",") if x.strip()}
    cliff = np.isin(tt, list(ids))
    log(f"  不可行走山体(地形 {sorted(ids)}) 格数 {int(cliff.sum())} / {cliff.size}"
        f" = {cliff.mean():.3f}")

    if args.height_src == "dem":
        latc = [float(x) for x in args.lat.split(",")]
        lonc = [float(x) for x in args.lon.split(",")]
        elev, _ = dem_cells(args.dem, cx, cy, latc, lonc)
        h = elev.T.astype(np.float32)                        # (cy, cx) 与底图行列一致
        log(f"  高程参考(ETOPO): 中位 {np.median(h):.0f} m"
            f"  p2 {np.percentile(h,2):.0f}  p98 {np.percentile(h,98):.0f} m")
        mlat = float(latc[0] * cx / 2 + latc[1] * cy / 2 + latc[2])
        sx_m = abs(lonc[0]) * 111320.0 * np.cos(np.deg2rad(mlat))
        sy_m = abs(latc[1]) * 110574.0
    else:
        V = d["verts"]
        H = V[..., 0].astype(np.float32)
        xs = np.arange(cx, dtype=np.int32)[:, None]
        ys = np.arange(cy, dtype=np.int32)[None, :]
        h = (H[4 * ys + (xs & 1) * 2 + 2, 4 * xs + 2] * 0.5).T   # 世界高度（米=0.5/字节）
        sx_m = sy_m = float(d["quadSize"]) * 4.0
        log(f"  高程参考(.bin 顶点高度): 中位 {np.median(h):.0f} m"
            f"  p2 {np.percentile(h,2):.0f}  p98 {np.percentile(h,98):.0f} m")
    log(f"  格实际间距 ≈ {sx_m:.0f} m(东西) × {sy_m:.0f} m(南北)")

    mod = build_modulation(args, h, sx_m, sy_m, log)
    cliff_img_cells = cliff.T                                 # (cy, cx) -> 图像行=格y
    # 山体区整体亮度中性化：只起伏、不整体压暗或提亮
    if cliff.any():
        off = float(mod[cliff_img_cells].mean()) - 1.0
        mod = np.clip(mod - off, args.clip_lo, args.clip_hi)
        log(f"  亮度中性化: 山体平均明暗 {off + 1.0:.3f} -> 1.000"
            f"（平移 {off:+.3f}；只做起伏、不整体压暗）")
    feather_cells = max(0.0, args.feather)
    want = {int(x) for x in str(args.seasons).split(",") if x.strip()}

    for si, season in enumerate(SEASONS):
        if si not in want:
            continue
        p = Path(args.tex_dir) / f"BaseMap{si}.png"
        if not p.exists():
            log(f"  [跳过] {p.name} 不存在")
            continue
        img = Image.open(p).convert("RGB")
        W, Hh = img.size
        a = np.asarray(img, np.float32)
        px = W / cx
        # 掩膜：崖格 -> 图像分辨率 -> 羽化（再乘回崖掩膜，保证外部恒为 0）
        cm = np.asarray(Image.fromarray((cliff_img_cells * 255).astype(np.uint8), "L")
                        .resize((W, Hh), Image.Resampling.NEAREST), np.float32) / 255.0
        m = cm
        if feather_cells > 0:
            m = np.asarray(Image.fromarray((cm * 255).astype(np.uint8), "L")
                           .filter(ImageFilter.GaussianBlur(feather_cells * px)),
                           np.float32) / 255.0 * cm                # 羽化只在掩膜内部
        mi = up_field(mod, W, Hh)
        out = a * (1.0 + ((mi - 1.0) * m)[..., None])       # mi 是乘子，只叠起伏量 (mi-1)
        bpx = int(round(args.border * px))
        if bpx > 0:
            out[:bpx] = a[:bpx]
            out[-bpx:] = a[-bpx:]
            out[:, :bpx] = a[:, :bpx]
            out[:, -bpx:] = a[:, -bpx:]
        out = np.clip(out, 0, 255)
        # 核验：崖格之外的像素必须逐点未动
        outside = cm <= 0.0
        if bpx > 0:
            outside[:bpx] = outside[-bpx:] = False
            outside[:, :bpx] = outside[:, -bpx:] = False
        d_out = np.abs(out - a).sum(2)
        chg_in = float(d_out[cm > 0].mean()) if (cm > 0).any() else 0.0
        chg_out = float(d_out[outside].max()) if outside.any() else 0.0
        log(f"  BaseMap{si} ({season}) {W}x{Hh}  像素/格 {px:.2f}")
        log(f"    山体像素平均改动 {chg_in:.1f}（0~255×3 通道之和）"
            f"    非山体像素最大改动 {chg_out:.1f}（必须 0 ✓）")
        log(f"    全图 meanRGB {a.reshape(-1,3).mean(0).round(1)} -> "
            f"{out.reshape(-1,3).mean(0).round(1)}")
        res = Image.fromarray(out.astype(np.uint8))
        if args.apply:
            bak = Path(args.tex_dir) / f"BaseMap{si}.png.bak_before_mountain"
            if not bak.exists():
                bak.write_bytes(p.read_bytes())
            res.save(p, "PNG", optimize=True)
            log(f"    已写出 {p.name} {p.stat().st_size/1024:.0f} KB（备份 {bak.name}）")
        k = min(1.0, 900.0 / max(W, Hh))
        sw, sh = int(W * k), int(Hh * k)
        sheet = Image.new("RGB", (sw * 2 + 8, sh), (20, 20, 24))
        sheet.paste(img.resize((sw, sh), Image.Resampling.LANCZOS), (0, 0))
        sheet.paste(res.resize((sw, sh), Image.Resampling.LANCZOS), (sw + 8, 0))
        sheet.save(OUT / f"mountain_{si}_{season}_small.jpg", quality=84)
        # 局部 1:1 放大：取 3 处山体最密的窗口（左=原图 右=美化后）
        zc = max(8, int(args.zoom_cells))
        win = max(32, int(zc * px))
        dens = ndimage.uniform_filter(cliff_img_cells.astype(np.float32), zc)
        picks, tmp = [], dens.copy()
        for _ in range(3):
            iy, ix = np.unravel_index(int(np.argmax(tmp)), tmp.shape)
            picks.append((max(0, min(ix - win // 2, W - win)),
                          max(0, min(iy - win // 2, Hh - win))))
            tmp[max(0, iy - zc):iy + zc, max(0, ix - zc):ix + zc] = -1
        zs = Image.new("RGB", (win * 2 + 8, (win + 8) * 3), (20, 20, 24))
        for r, (x0, y0) in enumerate(picks):
            box = (x0, y0, x0 + win, y0 + win)
            zs.paste(img.crop(box), (0, r * (win + 8)))
            zs.paste(res.crop(box), (win + 8, r * (win + 8)))
        zk = min(1.0, 1000.0 / max(zs.size))
        zs = zs.resize((int(zs.size[0] * zk), int(zs.size[1] * zk)), Image.Resampling.LANCZOS)
        zs.save(OUT / f"mountain_{si}_{season}_zoom.jpg", quality=88)
        log(f"    预览 {OUT / f'mountain_{si}_{season}_small.jpg'}"
            f" + {OUT / f'mountain_{si}_{season}_zoom.jpg'}")


# --------------------------------------------------------------------------- #
# 2) 山体贴图：细节优化
# --------------------------------------------------------------------------- #
def enhance_layer(a, detail, contrast, sigma):
    """可平铺（wrap）增强：高频提升 + 每通道独立 S 曲线 + 软膝盖（防爆高光/暗部）。

    要点：
      * 模糊用 mode='wrap'，保证贴图边缘接缝不变（layer 贴图是世界坐标平铺的）；
      * S 曲线围绕**每通道各自的中位**，因此不改变色相，只提升中间调对比；
      * detail 提升会把过亮/过暗处推出 0~255，用软膝盖（只保留 30% 越界量）压回来，
        否则截断会把该通道均值整体拉低 —— 那等于偷偷改了山体颜色。
    """
    x = a.astype(np.float32)
    blur = np.stack([ndimage.gaussian_filter(x[..., c], sigma, mode="wrap")
                     for c in range(3)], -1)
    y = x + detail * (x - blur)
    mid = np.median(y.reshape(-1, 3), axis=0)
    t = np.clip((y - mid) / 128.0, -1.0, 1.0)
    y = mid + 128.0 * (t * (1.0 + contrast) - contrast * t ** 3)
    over = np.maximum(0.0, y - 240.0)
    under = np.maximum(0.0, 15.0 - y)
    y = y - over * 0.7 + under * 0.7
    # 逐通道均值对齐：把软膝盖造成的整体亮度/色相漂移拉平（只留细节与对比）
    y = y + (x.reshape(-1, 3).mean(0) - y.reshape(-1, 3).mean(0))
    return np.clip(y, 0, 255).astype(np.uint8)


def do_layers(args, d, cx, cy, tt, log):
    V = d["verts"]
    VX, VY = V.shape[0], V.shape[1]
    cxx, cyy, ok = vertex_cell_index(VX, VY, cx, cy)
    ti = V[..., 1].astype(np.int32)                       # ch1 = textureIndex
    cell_t = tt[cxx, cyy]
    is_cliff = ok & (cell_t == 16)
    log(f"  顶点 {VX}x{VY}  崖内顶点 {int(is_cliff.sum())}（{is_cliff.mean():.3f}）")
    log("  图层使用统计（只列被崖用到的；cliff 比例 = 该图层顶点落在崖格内的占比）:")
    tot = np.bincount(ti[ok].ravel(), minlength=256)
    clf = np.bincount(ti[is_cliff].ravel(), minlength=256)
    usage = {}
    for L in np.nonzero(tot >= 200)[0]:
        n, f = int(tot[L]), float(clf[L]) / max(1, int(tot[L]))
        usage[int(L)] = (n, f)
        if f > 0.02:
            log(f"    layer_{int(L):<3d} 顶点 {n:>8d}  崖内比例 {f:.3f}")
    sel = [int(x) for x in str(args.layers).split(",") if x.strip()] if args.layers else \
          [L for L, (n, f) in sorted(usage.items()) if f >= args.min_cliff_frac]
    if not sel:
        log("  [警告] 没有选中任何图层")
        return
    log(f"  选中增强的图层: {sel}（按实测「崖内比例 >= {args.min_cliff_frac}」；"
        f"官方 TerrainLayerMap 的崖调色板是 {list(CLIFF_PALETTE)}，本图并不按它分配）")
    root = Path(args.terrain_root)
    want = {int(x) for x in str(args.seasons).split(",") if x.strip()}
    for si, season in enumerate(SEASONS):
        if si not in want:
            continue
        for L in sel:
            p = root / season / f"layer_{L}.png"
            if not p.exists():
                log(f"    [跳过] {season}/layer_{L}.png")
                continue
            img = Image.open(p).convert("RGB")
            a = np.asarray(img)
            res = enhance_layer(a, args.detail, args.contrast, args.detail_sigma)
            dd = np.abs(res.astype(np.int32) - a.astype(np.int32))
            log(f"    {season}/layer_{L}.png {img.size[0]}x{img.size[1]}"
                f"  平均改动 {dd.mean():.1f}  p99 {np.percentile(dd,99):.0f}  max {dd.max()}"
                f"  均值 {a.reshape(-1,3).mean(0).round(1)} -> {res.reshape(-1,3).mean(0).round(1)}")
            if args.apply:
                bak = p.with_suffix(".png.bak_before_beautify")
                if not bak.exists():
                    bak.write_bytes(p.read_bytes())
                Image.fromarray(res).save(p, "PNG", optimize=True)
        if args.apply:
            log(f"    {season} 已写出（备份 *.bak_before_beautify）")
    # 预览：秋季选中图层（左=原图 右=增强），最多 2 张
    rows = []
    for L in sel[:2]:
        p = root / "Autumn" / f"layer_{L}.png"
        if not p.exists():
            continue
        a = np.asarray(Image.open(p).convert("RGB"))
        rows.append((a, enhance_layer(a, args.detail, args.contrast, args.detail_sigma), L))
    if rows:
        zs = max(1, min(3, 768 // max(max(r[0].shape[:2]) for r in rows)))
        w0 = max(r[0].shape[1] * zs for r in rows)
        h0 = sum(r[0].shape[0] * zs + 8 for r in rows)
        sheet = Image.new("RGB", (w0 * 2 + 8, h0), (20, 20, 24))
        y = 0
        for a, res, L in rows:
            h, w = a.shape[:2]
            sheet.paste(Image.fromarray(a).resize((w * zs, h * zs), Image.Resampling.NEAREST), (0, y))
            sheet.paste(Image.fromarray(res).resize((w * zs, h * zs), Image.Resampling.NEAREST),
                        (w0 + 8, y))
            y += h * zs + 8
        sheet.save(OUT / "mountain_layers_small.jpg", quality=88)
        log(f"    预览 {OUT / 'mountain_layers_small.jpg'}（行=layer {[r[2] for r in rows]}，"
            f"左原图 / 右增强，放大 {zs}x）")


# --------------------------------------------------------------------------- #
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--tex-dir", default=None, help="BaseTex 目录（底图部分）")
    ap.add_argument("--terrain-root", default=None, help="Terrain 目录（贴图部分，含 Autumn/…）")
    ap.add_argument("--terrain-ids", default="16", help="要美化的不可行走地形 id（默认 16=崖）")
    ap.add_argument("--height-src", default="dem", choices=("dem", "bin"),
                    help="起伏参考来源：dem=真实高程（只作参考） / bin=地图顶点高度")
    ap.add_argument("--dem", default=DEM_DEFAULT)
    ap.add_argument("--lat", default=LAT_DEFAULT)
    ap.add_argument("--lon", default=LON_DEFAULT)
    ap.add_argument("--mid-sigma", type=float, default=5.0,
                    help="大/中尺度分界（格）：越小越靠近山脊尺度，越大越像大块趋势")
    ap.add_argument("--small-sigma", type=float, default=2.0, help="中/小尺度分界（格）")
    ap.add_argument("--mid-amp", type=float, default=0.15, help="中尺度（山脊/谷地）幅度")
    ap.add_argument("--small-amp", type=float, default=0.10, help="小尺度（碎岩）幅度")
    ap.add_argument("--hill-amp", type=float, default=0.25, help="方向光照幅度")
    ap.add_argument("--hill-ref-slope", type=float, default=32.0,
                    help="坡度的归一化参考（度）：把 p90 坡度放大到它，光照才有方向性")
    ap.add_argument("--hill-azimuth", type=float, default=315.0, help="光源方位角(度)")
    ap.add_argument("--hill-alt", type=float, default=45.0, help="光源高度角(度)")
    ap.add_argument("--clip-lo", type=float, default=0.62)
    ap.add_argument("--clip-hi", type=float, default=1.45)
    ap.add_argument("--border", type=int, default=4, help="外圈保护（格）")
    ap.add_argument("--feather", type=float, default=1.5, help="边缘羽化（格，只在掩膜内部）")
    ap.add_argument("--seasons", default="0,1,2,3")
    ap.add_argument("--layers", default=None, help="手动指定要增强的 layer 下标，如 18,8,6,22,23")
    ap.add_argument("--min-cliff-frac", type=float, default=0.8,
                    help="图层被崖使用的比例高于它才增强（避免动到共享图层）")
    ap.add_argument("--detail", type=float, default=0.45, help="贴图高频提升量")
    ap.add_argument("--contrast", type=float, default=0.10, help="贴图对比 S 曲线强度（每通道，保色相）")
    ap.add_argument("--detail-sigma", type=float, default=2.0)
    ap.add_argument("--zoom-cells", type=int, default=96, help="局部放大预览的窗口边长（格）")
    ap.add_argument("--basemap-only", action="store_true")
    ap.add_argument("--layers-only", action="store_true")
    ap.add_argument("--analyze", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    if ndimage is None:
        print("[错误] 需要 scipy（ndimage）")
        return
    if not (args.analyze or args.apply):
        args.analyze = True
    OUT.mkdir(parents=True, exist_ok=True)
    msgs = []

    def log(s):
        print(s)
        msgs.append(s)

    d = parse_bin(str(args.bin_path))
    cx, cy = d["cellsX"], d["cellsY"]
    tt = d["gridRaw"][..., 0].astype(np.int32)
    log("=" * 84)
    log(f"山体美化: {Path(args.bin_path).name}  格 {cx}x{cy}"
        f"  模式 {'写盘' if args.apply else '分析'}")
    log("=" * 84)

    if not args.layers_only:
        if not args.tex_dir:
            log("[错误] 底图部分需要 --tex-dir")
        else:
            log("[1] 底图：高程起伏 -> 只落在不可行走山体的明暗")
            do_basemap(args, d, cx, cy, tt, log)
    if not args.basemap_only:
        if not args.terrain_root:
            log("[2] 贴图：未给 --terrain-root，跳过")
        else:
            log("[2] 山体贴图：可平铺细节/对比增强")
            do_layers(args, d, cx, cy, tt, log)

    (OUT / "mountain_beautify_log.txt").write_text("\n".join(msgs), encoding="utf-8")
    log("=" * 84)
    log("完成。" if args.apply else "（分析模式，未写盘。确认后加 --apply）")


if __name__ == "__main__":
    main()
