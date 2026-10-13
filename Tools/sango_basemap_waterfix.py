# -*- coding: utf-8 -*-
"""sango_basemap_waterfix.py —— 按地形的「水域 / 非水域」修正底图（v2）。

v1 的问题（用户反馈）：用「就近克隆像素」补，会在地图上抹出奇奇怪怪的色块。
v2 改成两条干净的规则：

  1) 水域：地形判为水的格，**统一成一种颜色**（取美术自身在水域处的颜色中位），
     可选叠加一层艺术智能体生成的水面材质（默认不叠，保证"统一颜色"）。
  2) 陆地：地形判为陆、但美术颜色偏蓝（看着像水）的格，用**确信陆地的平滑场**填充 ——
     平滑场 = 以确信陆地为权重的归一化箱式均值（大半径），因此是连续过渡，
     不会出现斑块或怪色。
  3) **外圈保护**：最外 N 格（默认 4 格）一律不动，任何替换都不做。
  4) 只在上述两类掩膜内改动，边缘做羽化。

"偏蓝"判据：水体度 B-R 的阈值取「地形水/地形陆 两类均值中点」，用地形标定。

用法：
    python sango_basemap_waterfix.py --bin <bin> --tex-dir <BaseTex> --analyze
    python sango_basemap_waterfix.py ... --apply
    # 叠加 AI 水面材质（不保证统一色，仅供对比）
    python sango_basemap_waterfix.py ... --texture <png> --texture-alpha 0.15 --apply
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
    ap.add_argument("--border", type=int, default=4, help="外圈保护宽度（格），一律不动")
    ap.add_argument("--water-color", default=None, help="如 131,145,173；默认取美术自身水色")
    ap.add_argument("--texture", default=None, help="可选：水面材质 png（会按尺寸铺贴）")
    ap.add_argument("--texture-alpha", type=float, default=0.0,
                    help="材质叠加强度，0 = 纯统一色（默认）")
    ap.add_argument("--land-smooth", type=int, default=16, help="陆地平滑场半径（格）")
    ap.add_argument("--blend", type=float, default=0.7, help="掩膜边缘羽化（格）")
    ap.add_argument("--preview-alpha", type=float, default=0.15,
                    help="额外输出一版叠材质的效果供对比")
    ap.add_argument("--analyze", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    if ndimage is None:
        print("[错误] 需要 scipy")
        return

    d = parse_bin(str(args.bin_path))
    cx, cy = d["cellsX"], d["cellsY"]
    terr = d["gridRaw"][..., 0].astype(np.int32).T
    Wcell = np.isin(terr, [int(x) for x in args.water_ids.split(",") if x.strip()])
    tex = Path(args.tex_dir)
    border_cells = max(0, args.border)
    logs = []

    def log(s=""):
        print(s, flush=True)
        logs.append(s)

    log("=" * 78)
    log(f"水/非水修正 v2: {Path(args.bin_path).name}  格 {cx}x{cy}  水域 id {args.water_ids}")
    log(f"  外圈保护 {border_cells} 格（不动）   陆地平滑半径 {args.land_smooth} 格"
        f"   材质叠加 {args.texture_alpha}")

    teximg = None
    if args.texture and args.texture_alpha > 0:
        teximg = Image.open(args.texture).convert("RGB")

    want = {int(x) for x in str(args.seasons).split(",") if x.strip()}
    for si, season in enumerate(SEASONS):
        if si not in want:
            continue
        p = tex / f"BaseMap{si}.png"
        if not p.exists():
            log(f"  [跳过] {p.name}")
            continue
        img = Image.open(p).convert("RGB")
        W, H = img.size
        a = np.asarray(img, np.float32)
        Wm = np.asarray(Image.fromarray((Wcell.astype(np.uint8) * 255), "L")
                        .resize((W, H), Image.Resampling.NEAREST), np.uint8) > 127
        # 外圈保护掩膜（图像分辨率）
        bpx = int(round(border_cells * min(W / cx, H / cy)))
        prot = np.zeros((H, W), bool)
        if bpx > 0:
            prot[:bpx] = prot[-bpx:] = True
            prot[:, :bpx] = prot[:, -bpx:] = True
        wn = a[..., 2] - a[..., 0]
        thr = float((wn[Wm].mean() + wn[~Wm].mean()) / 2.0)
        blueish = wn > thr

        # 水色：美术在「地形水 ∧ 偏蓝」处的中位色；不足则退化为「地形水」处中位色
        sel = Wm & blueish
        if sel.sum() < 500:
            sel = Wm
        if args.water_color:
            wcol = np.array([int(x) for x in args.water_color.split(",")], np.float32)
        else:
            wcol = np.median(a[sel], 0)
        conf_w = Wm & blueish
        conf_l = (~Wm) & (~blueish)
        log(f"\n  BaseMap{si} ({season}) {W}x{H}  {W / cx:.3f}x{H / cy:.3f} 像素/格")
        log(f"    偏蓝阈值 {thr:+.1f}   地形水域 {Wm.mean():.3f}   美术偏蓝 {blueish.mean():.3f}")
        log(f"    水色(取美术自身) {tuple(wcol.round(0).astype(int))}"
            f"   水域内原色标准差 {a[Wm].std(0).round(1)}")
        log(f"    确信水 {int(conf_w.sum())} px   确信陆 {int(conf_l.sum())} px")

        # 陆地平滑场：以确信陆地为权重的归一化箱式均值
        R = max(1, int(round(args.land_smooth * min(W / cx, H / cy))))
        nf = conf_l.astype(np.float32)
        den = ndimage.uniform_filter(nf, R)
        field = np.zeros_like(a)
        for c in range(3):
            field[..., c] = ndimage.uniform_filter(a[..., c] * nf, R) / np.maximum(den, 1e-6)

        # 目标图
        new = a.copy()
        # 1) 水域统一色（可叠材质）
        if teximg is not None and args.texture_alpha > 0:
            t = np.asarray(teximg.resize((W, H), Image.Resampling.LANCZOS), np.float32)
            al = args.texture_alpha
            new[Wm] = wcol * (1 - al) + t[Wm] * al
        else:
            new[Wm] = wcol
        # 2) 陆地上偏蓝的（看着像水）-> 平滑陆色
        needL = (~Wm) & blueish
        new[needL] = field[needL]

        # 3) 掩膜 + 羽化 + 外圈保护
        chg = (Wm | needL) & ~prot
        w = np.asarray(Image.fromarray((chg * 255).astype(np.uint8), "L")
                       .filter(ImageFilter.GaussianBlur(args.blend * min(W / cx, H / cy))),
                       np.float32) / 255.0
        out = a * (1 - w[..., None]) + new * w[..., None]
        out[prot] = a[prot]                      # 外圈绝对不动
        out = np.clip(out, 0, 255)
        log(f"    改动: 水域填充 {int((Wm & ~prot).sum())} px   陆地去水 {int((needL & ~prot).sum())}"
            f" px   外圈保护 {int(prot.sum())} px（未动）")
        log(f"    水域内颜色标准差: 原 {a[Wm].std(0).round(1)} -> 新 {out[Wm].std(0).round(1)}"
            f"   （越接近 0 越「统一颜色」）")

        res = Image.fromarray(out.astype(np.uint8))
        if args.apply:
            bak = tex / f"BaseMap{si}.png.bak_before_waterfix"
            if not bak.exists():
                bak.write_bytes(p.read_bytes())
            res.save(p, "PNG", optimize=True)
            log(f"    已写出 {p.name}  {p.stat().st_size / 1024:.0f} KB（备份 {bak.name}）")
        # 预览：原 | 修正 | 掩膜；另出叠材质版
        k = min(1.0, 1400.0 / max(W, H))
        sw, sh = int(W * k), int(H * k)
        mm = (np.stack([(chg * 255).astype(np.uint8)] * 3, -1))
        sheet = Image.new("RGB", (sw * 3 + 16, sh), (20, 20, 24))
        sheet.paste(img.resize((sw, sh), Image.Resampling.LANCZOS), (0, 0))
        sheet.paste(res.resize((sw, sh), Image.Resampling.LANCZOS), (sw + 8, 0))
        sheet.paste(Image.fromarray(mm).resize((sw, sh), Image.Resampling.NEAREST), (sw * 2 + 16, 0))
        sheet.save(Path(rf"D:\sango_infinity\Temp\waterfix2_{si}_{season}.png"))
        sheet.convert("RGB").resize((820, max(1, int(sh * 820 / (sw * 3 + 16)))),
                                    Image.Resampling.LANCZOS).save(
            Path(rf"D:\sango_infinity\Temp\waterfix2_{si}_{season}_small.jpg"), quality=82)
        if teximg is not None and args.preview_alpha > 0 and args.texture_alpha == 0:
            pv = a.copy()
            t = np.asarray(teximg.resize((W, H), Image.Resampling.LANCZOS), np.float32)
            pv[Wm] = wcol * (1 - args.preview_alpha) + t[Wm] * args.preview_alpha
            pv[needL] = field[needL]
            Image.fromarray(np.clip(pv, 0, 255).astype(np.uint8)).resize(
                (sw, sh), Image.Resampling.LANCZOS).convert("RGB").save(
                Path(rf"D:\sango_infinity\Temp\waterfix2_{si}_{season}_tex_small.jpg"), quality=82)
            log(f"    另出叠材质预览 waterfix2_{si}_{season}_tex_small.jpg")
        log(f"    预览 waterfix2_{si}_{season}_small.jpg")

    if not args.apply:
        log("\n（分析模式，未写盘）")
    else:
        log("\n完成。")
    Path(r"D:\sango_infinity\Temp\waterfix2_log.txt").write_text("\n".join(logs), encoding="utf-8")


if __name__ == "__main__":
    main()
