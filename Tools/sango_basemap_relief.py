# -*- coding: utf-8 -*-
"""sango_basemap_relief.py —— 在底图（BaseTex）上叠加「只在中／小尺度」的山体细节。

方案要点（按需求）：底图整体色调不变，只把地形高度里的**中尺度**与**小尺度**两个
频带取出来当明暗细节叠上去；大尺度趋势（山体总体高低）不参与，避免把图压暗或改色。

频带分解（都在格分辨率上算，尺度以「格」为单位）：
    大尺度  L = 高斯(高度, sigma_mid)
    中尺度  M = 高斯(高度, sigma_small) - L
    小尺度  S = 高度 - 高斯(高度, sigma_small)
两个频带各自按稳健尺度归一化到 [-1,1]，再按幅度叠加：
    底图' = 底图 × (1 + amp_mid × M + amp_small × S)

保护：水面格、地图外圈 N 格一律不动；海岸处羽化，避免硬边。

用法：
    python sango_basemap_relief.py --bin <bin> --tex-dir <BaseTex> --analyze
    python sango_basemap_relief.py ... --apply
"""
import argparse
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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--tex-dir", required=True)
    ap.add_argument("--water-ids", default="7,8,9,21,22")
    ap.add_argument("--seasons", default="0,1,2,3")
    ap.add_argument("--mid-sigma", type=float, default=12.0, help="中尺度频带下限（格）")
    ap.add_argument("--small-sigma", type=float, default=3.0, help="中/小尺度分界（格）")
    ap.add_argument("--mid-amp", type=float, default=0.10, help="中尺度细节幅度")
    ap.add_argument("--small-amp", type=float, default=0.05, help="小尺度细节幅度")
    ap.add_argument("--border", type=int, default=4, help="外圈保护宽度（格），不动")
    ap.add_argument("--feather", type=float, default=1.0, help="海岸/边界羽化（格）")
    ap.add_argument("--analyze", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    if ndimage is None:
        print("[错误] 需要 scipy")
        return

    d = parse_bin(str(args.bin_path))
    cx, cy = d["cellsX"], d["cellsY"]
    G, Q = float(d["gridSize"]), float(d["quadSize"])
    verts = d["verts"]
    H = (verts[..., 0] if verts.ndim == 3 else verts).astype(np.float32)
    tt = d["gridRaw"][..., 0].astype(np.int32)
    water = np.isin(tt, [int(x) for x in args.water_ids.split(",") if x.strip()])

    print("=" * 78)
    print(f"底图叠加山体细节: {Path(args.bin_path).name}  格 {cx}x{cy}")
    print(f"  顶点数组 {verts.shape} -> 高度场 {H.shape}  取值 {float(H.min()):.0f}~{float(H.max()):.0f}")
    # 格中心顶点 (vx=4x+2, vy=4y+(x&1)*2+2) 的高度；底图行=格 y、列=格 x
    xs = np.arange(cx, dtype=np.int32)[:, None]
    ys = np.arange(cy, dtype=np.int32)[None, :]
    vx = 4 * xs + 2
    vy = 4 * ys + (xs & 1) * 2 + 2
    hc = H[vy, vx]                                  # (cx, cy)
    print(f"  格中心高度 {hc.shape} 取值 {float(hc.min()):.0f}~{float(hc.max()):.0f}")

    # 频带分解（在 (cy, cx) 序上做，方便与底图行=格y 对齐）
    h = hc.T.astype(np.float32)                     # (cy, cx)
    L = ndimage.gaussian_filter(h, args.mid_sigma, mode="nearest")
    B = ndimage.gaussian_filter(h, args.small_sigma, mode="nearest")
    mid = B - L                                     # 中尺度
    small = h - B                                   # 小尺度
    for nm, b in (("中尺度", mid), ("小尺度", small)):
        sc = float(np.percentile(np.abs(b), 90)) or 1.0
        print(f"  {nm}: 原始 p90|值| {sc:.1f}  （归一化除以它）")
        b /= sc
    mid = np.clip(mid, -1, 1)
    small = np.clip(small, -1, 1)

    want = {int(x) for x in str(args.seasons).split(",") if x.strip()}
    for si, season in enumerate(SEASONS):
        if si not in want:
            continue
        p = Path(args.tex_dir) / f"BaseMap{si}.png"
        if not p.exists():
            print(f"  [跳过] {p.name}")
            continue
        img = Image.open(p).convert("RGB")
        W, Hh = img.size
        a = np.asarray(img, np.float32)
        # 细节场放大到图像尺寸
        mid_i = np.asarray(Image.fromarray(((mid + 1) * 127.5).astype(np.uint8))
                           .resize((W, Hh), Image.Resampling.BILINEAR), np.float32) / 127.5 - 1
        small_i = np.asarray(Image.fromarray(((small + 1) * 127.5).astype(np.uint8))
                             .resize((W, Hh), Image.Resampling.BILINEAR), np.float32) / 127.5 - 1
        # 陆地掩膜（图像分辨率）+ 外圈保护 + 羽化
        land = np.asarray(Image.fromarray(((~water.T).astype(np.uint8) * 255), "L")
                          .resize((W, Hh), Image.Resampling.NEAREST), np.uint8) > 127
        m = land.astype(np.float32)
        if args.feather > 0:
            m = np.asarray(Image.fromarray((m * 255).astype(np.uint8), "L")
                           .filter(ImageFilter.GaussianBlur(args.feather * min(W / cx, Hh / cy))),
                           np.float32) / 255.0
        w = m[..., None]
        mod = 1.0 + (args.mid_amp * mid_i + args.small_amp * small_i)[..., None]
        np.clip(mod, 0.55, 1.45, out=mod)
        out = np.clip(a * (1 - w) + a * mod * w, 0, 255)
        # 外圈：最后硬还原（羽化会把陆地掩膜渗进保护带，必须先羽化再还原）
        bpx = int(round(args.border * min(W / cx, Hh / cy)))
        if bpx > 0:
            out[:bpx] = a[:bpx]
            out[-bpx:] = a[-bpx:]
            out[:, :bpx] = a[:, :bpx]
            out[:, -bpx:] = a[:, -bpx:]
        water_i = ~land
        out[water_i] = a[water_i]                      # 水面也不动
        print(f"\n  BaseMap{si} ({season}) {W}x{Hh}  像素/格 {W / cx:.3f}")
        print(f"    幅度: 中尺度 ±{args.mid_amp:.2f}  小尺度 ±{args.small_amp:.2f}"
              f"    陆地羽化后权重均值 {float(m.mean()):.3f}")
        print(f"    全图 meanRGB {a.reshape(-1, 3).mean(0).round(1)} -> "
              f"{out.reshape(-1, 3).mean(0).round(1)}（整体色调应基本不变）")
        hard = np.zeros((Hh, W), bool)
        if bpx > 0:
            hard[:bpx] = hard[-bpx:] = True
            hard[:, :bpx] = hard[:, -bpx:] = True
        hard |= water_i
        chg_outside = float(np.abs(out - a).sum(2)[hard].max()) if hard.any() else 0.0
        print(f"    水面+外圈像素最大改动: {chg_outside:.1f}（应为 0 ✓）"
              f"    陆地像素平均改动 {float(np.abs(out - a).sum(2)[~hard].mean()):.1f}")
        res = Image.fromarray(out.astype(np.uint8))
        if args.apply:
            bak = Path(args.tex_dir) / f"BaseMap{si}.png.bak_before_relief"
            if not bak.exists():
                bak.write_bytes(p.read_bytes())
            res.save(p, "PNG", optimize=True)
            print(f"    已写出 {p.name}  {p.stat().st_size / 1024:.0f} KB（备份 {bak.name}）")
        k = min(1.0, 900.0 / max(W, Hh))
        sw, sh = int(W * k), int(Hh * k)
        sheet = Image.new("RGB", (sw * 2 + 8, sh), (20, 20, 24))
        sheet.paste(img.resize((sw, sh), Image.Resampling.LANCZOS), (0, 0))
        sheet.paste(res.resize((sw, sh), Image.Resampling.LANCZOS), (sw + 8, 0))
        o = Path(rf"D:\sango_infinity\Temp\relief_{si}_{season}_small.jpg")
        sheet.convert("RGB").save(o, quality=84)
        print(f"    预览 {o}")

    print("\n" + ("完成。" if args.apply else "（分析模式，未写盘。确认后 --apply。）"))


if __name__ == "__main__":
    main()
