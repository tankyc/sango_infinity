# -*- coding: utf-8 -*-
"""sango_basemap_waterfix.py —— 按地形的「水域 / 非水域」修正底图。

真值取地形：水域 id 默认 7(川) 8(河) 9(海) 21(远海) 22(深海)。
美术的水/陆由水体度 B-R 阈值化判定，阈值用地形标定（两类均值中点）。

修正方式（不重新生成、不做任意位移搜索）：
  确信水域 = 美术判水 ∧ 地形判水；确信陆地 = 美术判陆 ∧ 地形判陆。
  对每个判错像素，按「到最近的**同类确信**像素」的位移去取材：
    - 该是水却看着像陆 -> 取最近确信水域的像素
    - 该是陆却看着像水 -> 取最近确信陆地的像素
  位移场只在判错区域内有效（区域内局部平均），因此纹理来自同一片邻近区域，
  手绘质感与地域色都跟着过来。最后按判错掩膜羽化混合，未判错处一律不动。

注意：不要用「分块一致性最优位移」——块内若同质（整块水/整块陆），
该度量退化，会得到 (±8,±8) 这类边界解（实测已复现），结果不可用。

用法：
    python sango_basemap_waterfix.py --bin <bin> --tex-dir <BaseTex> --analyze
    python sango_basemap_waterfix.py ... --apply
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
    ap.add_argument("--min-region", type=int, default=40, help="连通域小于此像素数视为噪声")
    ap.add_argument("--min-thick", type=float, default=4.0,
                    help="只修厚度不小于该值（格）的错位区；细河网等艺术笔触不动。0=关闭")
    ap.add_argument("--smooth", type=int, default=9, help="位移场局部平均窗口（像素）")
    ap.add_argument("--blend", type=float, default=1.2, help="羽化半径（格）")
    ap.add_argument("--analyze", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    d = parse_bin(str(args.bin_path))
    cx, cy = d["cellsX"], d["cellsY"]
    terr = d["gridRaw"][..., 0].astype(np.int32).T                 # [y, x]
    water_cells = np.isin(terr, [int(x) for x in args.water_ids.split(",") if x.strip()])
    tex = Path(args.tex_dir)

    logs = []

    def log(s=""):
        print(s, flush=True)
        logs.append(s)

    log("=" * 78)
    log(f"按水/非水修正底图: {Path(args.bin_path).name}  格 {cx}x{cy}  水域 id {args.water_ids}"
        f"  地形水域占比 {water_cells.mean():.3f}")

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
        wn = a[..., 2] - a[..., 0]
        terW = np.asarray(Image.fromarray((water_cells.astype(np.uint8) * 255), "L")
                          .resize((W, H), Image.Resampling.NEAREST),
                          np.uint8) > 127
        thr = float((wn[terW].mean() + wn[~terW].mean()) / 2.0)
        artW = wn > thr
        mis = artW != terW
        log(f"\n  BaseMap{si} ({season}) {W}x{H}  {W / cx:.3f}x{H / cy:.3f} 像素/格")
        log(f"    阈值 {thr:+.1f}   美术判水 {artW.mean():.3f}  地形水域 {terW.mean():.3f}"
            f"   判错 {mis.mean():.4f}")

        if ndimage is None:
            log("    [错误] 需要 scipy.ndimage")
            return
        # 清理：闭运算 + 去过小连通域（按像素尺度）
        m = ndimage.binary_closing(mis, iterations=1)
        lab, n = ndimage.label(m)
        if n:
            sizes = np.bincount(lab.ravel())
            keep = [i for i in range(1, n + 1) if sizes[i] >= args.min_region]
            m = np.isin(lab, keep) if keep else np.zeros_like(m)
        mis = m
        # 只修「够厚」的错位区：细河网/谷地是作者的艺术笔触，宽 1~3 格，一律不动。
        # 做法：腐蚀掉 T 格宽以内的部分，剩下的才算真错位，再膨胀回去并交回原掩膜。
        if args.min_thick > 0:
            kpx = max(1, int(round(args.min_thick * min(W / cx, H / cy))))
            core = ndimage.binary_erosion(mis, iterations=kpx)
            if core.any():
                mis = ndimage.binary_dilation(core, iterations=kpx) & mis
            else:
                mis = np.zeros_like(mis)
            log(f"    只保留厚度 >= {args.min_thick} 格的错位区（腐蚀 {kpx} 像素）"
                f" -> {int(mis.sum())} 像素")
        need_w = mis & terW            # 该是水、看着像陆
        need_l = mis & ~terW           # 该是陆、看着像水
        conf_w = artW & terW           # 确信水
        conf_l = (~artW) & (~terW)     # 确信陆
        log(f"    清理后判错 {int(mis.sum())} 像素（该变水 {int(need_w.sum())}"
            f"  该变陆 {int(need_l.sum())}）")
        log(f"    确信源: 水 {int(conf_w.sum())}  陆 {int(conf_l.sum())}")
        if not mis.any() or not conf_w.any() or not conf_l.any():
            log("    （无需修正或确信源不足，跳过）")
            continue

        YY, XX = np.mgrid[0:H, 0:W]
        moved = a.copy()
        used = np.zeros((H, W), bool)
        stats = {}
        for name, need, src in (("水", need_w, conf_w), ("陆", need_l, conf_l)):
            if not need.any():
                continue
            _, idx = ndimage.distance_transform_edt(~src, return_indices=True)
            dy = (idx[0] - YY).astype(np.float32)
            dx = (idx[1] - XX).astype(np.float32)
            md = np.hypot(dy, dx)
            stats[name] = (int(need.sum()), float(md[need].min()), float(np.median(md[need])),
                           float(md[need].max()))
            # 位移场只在该类判错区域内有效，区域内局部平均
            nf = need.astype(np.float32)
            k = args.smooth
            if k > 1:
                num_y = ndimage.uniform_filter(dy * nf, k)
                num_x = ndimage.uniform_filter(dx * nf, k)
                den = ndimage.uniform_filter(nf, k)
                dy = np.where(den > 1e-6, num_y / np.maximum(den, 1e-6), 0)
                dx = np.where(den > 1e-6, num_x / np.maximum(den, 1e-6), 0)
            sy = np.clip((YY + dy).round().astype(np.int32), 0, H - 1)
            sx = np.clip((XX + dx).round().astype(np.int32), 0, W - 1)
            moved[need] = a[sy[need], sx[need]]
            used |= need
        for name, (cnt, mn, med, mx) in stats.items():
            log(f"    变{name}: {cnt} 像素  到最近确信源的位移(像素) 最小 {mn:.1f}"
                f" 中位 {med:.1f} 最大 {mx:.1f}  = {med / min(W / cx, H / cy):.2f} 格")

        w = np.asarray(Image.fromarray((used * 255).astype(np.uint8), "L")
                       .filter(ImageFilter.GaussianBlur(args.blend * min(W / cx, H / cy))),
                       np.float32) / 255.0
        out = a * (1 - w[..., None]) + moved * w[..., None]

        # 效果评估：修正后（按位移取材）与地形的判水一致性
        outW = (out[..., 2] - out[..., 0]) > thr
        log(f"    一致性（美术判水 vs 地形水域）: 修正前 {float((artW == terW).mean()):.4f}"
            f" -> 修正后 {float((outW == terW).mean()):.4f}")

        prev = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8))
        if args.apply:
            bak = tex / f"BaseMap{si}.png.bak_before_waterfix"
            if not bak.exists():
                bak.write_bytes(p.read_bytes())
            prev.save(p, "PNG", optimize=True)
            log(f"    已写出 {p.name}  {p.stat().st_size / 1024:.0f} KB（备份 {bak.name}）")
        # 预览（并排 + 判错掩膜），缩到可看大小
        k = min(1.0, 1400.0 / max(W, H))
        sw, sh = int(W * k), int(H * k)
        mm = (np.stack([used * 255] * 3, -1)).astype(np.uint8)
        sheet = Image.new("RGB", (sw * 3 + 16, sh), (20, 20, 24))
        sheet.paste(img.resize((sw, sh), Image.Resampling.LANCZOS), (0, 0))
        sheet.paste(prev.resize((sw, sh), Image.Resampling.LANCZOS), (sw + 8, 0))
        sheet.paste(Image.fromarray(mm).resize((sw, sh), Image.Resampling.NEAREST), (sw * 2 + 16, 0))
        o = Path(rf"D:\sango_infinity\Temp\waterfix_{si}_{season}.png")
        sheet.save(o)
        sheet.convert("RGB").resize((760, max(1, int(sh * 760 / (sw * 3 + 16)))),
                                    Image.Resampling.LANCZOS).save(
            Path(rf"D:\sango_infinity\Temp\waterfix_{si}_{season}_small.jpg"), quality=80)
        log(f"    预览 {o}")

    if not args.apply:
        log("\n（分析模式，未写盘。确认后用 --apply 执行。）")
    else:
        log("\n完成。")
    Path(r"D:\sango_infinity\Temp\waterfix_log.txt").write_text("\n".join(logs), encoding="utf-8")


if __name__ == "__main__":
    main()
