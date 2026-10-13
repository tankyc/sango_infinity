# -*- coding: utf-8 -*-
"""sango_basemap_repatch.py —— 在 mod 原底图上做「保样式局部重刷」＋「城市底座」。

设计原则：**不重新生成底图**。原美术一律保留，只在与当前地图不符的地方，
从**同一张美术**里克隆邻近贴图补正（exemplar patch），因此手绘质感、地域色、
笔触都自动跟着来。

不符的判定 = 两路并集：
  A) 数据差异：当前 .bin 与 mod 原始 .bin（*.bak_repair / *.bak_skybox）不同
     的格 —— 这是"地图被改过"造成的确定不符。
  B) 海岸线偏差：地形水域掩膜（默认含 川/河 7,8 + 海/远海/深海 9,21,22）
     与美术「水体度 (B-R)」在阈值化后的差异。

用法：
    # 只看分析、出偏差图（不写盘）
    python sango_basemap_repatch.py --bin <yyxk_2x.bin> --scenario <Scenario1.json> \
        --tex-dir <Assets/Map/yyxk/BaseTex> --analyze

    # 执行
    python sango_basemap_repatch.py ... --apply

可调：--water-ids / --mismatch-src {data,coast,both} / --min-region / --block
      --search-radius / --pedestal-cells / --pedestal-color / --site-types
"""
import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

try:
    from scipy import ndimage
except ImportError:                            # 退化：不做连通域清理
    ndimage = None

SEASONS = ("Autumn", "Spring", "Summer", "Winter")


# --------------------------------------------------------------------------- #
# 读取与掩膜
# --------------------------------------------------------------------------- #
def read_map(bin_path):
    d = parse_bin(str(bin_path))
    cx, cy = d["cellsX"], d["cellsY"]
    terr = d["gridRaw"][..., 0].astype(np.int32)                       # [x, y]
    st = (d["gridRaw"][..., 1].astype(np.uint32)
          | (d["gridRaw"][..., 2].astype(np.uint32) << 8)
          | (d["gridRaw"][..., 3].astype(np.uint32) << 16)
          | (d["gridRaw"][..., 4].astype(np.uint32) << 24))
    area = d["gridRaw"][..., 5].astype(np.int32) | (d["gridRaw"][..., 6].astype(np.int32) << 8)
    return dict(terr=terr, state=st, area=area, cx=cx, cy=cy, nmodels=len(d["tail"]["models"]))


def art_waterness(img):
    a = np.asarray(img.convert("RGB"), np.float32)
    return a[..., 2] - a[..., 0]


def mask_to_img(mask, size):
    """格分辨率掩膜 -> 图像尺寸的 uint8 掩膜。"""
    m = Image.fromarray((mask.astype(np.uint8) * 255), "L")
    return np.asarray(m.resize(size, Image.Resampling.NEAREST), np.uint8) > 127


def clean_mask(mask, min_cells, close_iter=1):
    """去掉零散噪点、补小洞。"""
    if ndimage is None:
        return mask
    m = mask
    if close_iter:
        m = ndimage.binary_closing(m, iterations=close_iter)
    lab, n = ndimage.label(m)
    if n:
        sizes = np.bincount(lab.ravel())
        keep = np.zeros_like(m)
        for i in range(1, n + 1):
            if sizes[i] >= min_cells:
                keep |= lab == i
        m = keep
    return m


def water_threshold_from_terrain(wn, terrain_water):
    """用「地形已给定的水/陆」标定美术水体度阈值：取两类均值中点。

    比按比例取分位数稳：美术里有大量同色块，分位数会被并列值卡住。
    """
    w = wn[terrain_water]
    l = wn[~terrain_water]
    if w.size < 50 or l.size < 50:
        return 0.0
    return float((w.mean() + l.mean()) / 2.0)


def coast_offset_mask(art_water_big, terr_water_big, tol_px):
    """只挑「真的错位」的格：某类水所在处，另一类的最近水超过容差。

    容差内的边界抖动（阈值化误差、笔触毛边）会被自动忽略。
    """
    if ndimage is None:
        return art_water_big != terr_water_big
    d_art = ndimage.distance_transform_edt(~art_water_big)
    d_ter = ndimage.distance_transform_edt(~terr_water_big)
    return (terr_water_big & (d_art > tol_px)) | (art_water_big & (d_ter > tol_px))


# --------------------------------------------------------------------------- #
# exemplar 克隆补正
# --------------------------------------------------------------------------- #
def exemplar_fill(img, mask_big, block_cells, px, py, radius_cells, log, block_min=0.06):
    """对 mask_big（图像分辨率）覆盖处，从同一张图里克隆贴图补齐。

    做法：按 block_cells 分块，仅在「已知像素（mask 之外）」上比较候选源块，
    取最相似的一块填进本块的 mask 处；用高斯羽化过渡。
    """
    a = np.asarray(img.convert("RGB"), np.float32)
    H, W = a.shape[:2]
    hard = mask_big
    if not hard.any():
        log("    没有需要补正的像素")
        return img, 0
    weight = np.asarray(Image.fromarray((hard * 255).astype(np.uint8), "L")
                        .filter(ImageFilter.GaussianBlur(radius=max(1.0, px * 0.6))),
                        np.float32) / 255.0
    out = a.copy()
    bw, bh = int(round(block_cells * px)), int(round(block_cells * py))
    rw, rh = int(round(radius_cells * px)), int(round(radius_cells * py))
    ys = list(range(0, H, max(1, bh // 2)))
    xs = list(range(0, W, max(1, bw // 2)))
    done = 0
    # 先在 1/4 分辨率下建索引，加速候选打分
    k = 4
    asm = a[::k, ::k]
    msm = hard[::k, ::k]
    for y0 in ys:
        for x0 in xs:
            y1, x1 = min(y0 + bh, H), min(x0 + bw, W)
            tm = hard[y0:y1, x0:x1]
            if tm.mean() < block_min:                  # 该块几乎没问题
                continue
            tgt = a[y0:y1, x0:x1]
            known = ~tm
            if known.sum() < 0.15 * tm.size:           # 几乎没有参照，跳过
                continue
            # 候选：全局粗网格，排除目标周边 radius
            bh2, bw2 = max(1, (y1 - y0) // k), max(1, (x1 - x0) // k)
            tgt_s = asm[y0 // k:y0 // k + bh2, x0 // k:x0 // k + bw2]      # (h,w,3)
            tmask = tm[::k, ::k]
            best = None
            step = max(1, bw2 // 2)
            for sy in range(0, H - (y1 - y0), step * k):
                for sx in range(0, W - (x1 - x0), step * k):
                    if abs(sy - y0) <= rh and abs(sx - x0) <= rw:
                        continue
                    src_s = asm[sy // k:sy // k + bh2, sx // k:sx // k + bw2]
                    if src_s.shape != tgt_s.shape:          # 必须与目标同尺寸(含通道)
                        continue
                    kn_s = msm[sy // k:sy // k + bh2, sx // k:sx // k + bw2]
                    if kn_s.shape != tmask.shape:
                        continue
                    sel = ~kn_s                              # 只用源块的「已知」像素做比较
                    if sel.sum() < 0.2 * sel.size:           # 已知像素太少则不作为源
                        continue
                    d = float(np.abs(src_s[sel] - tgt_s[sel]).mean())
                    if best is None or d < best[0]:
                        best = (d, sy, sx)
            if best is None:
                continue
            _, sy, sx = best
            src = a[sy:sy + (y1 - y0), sx:sx + (x1 - x0)]
            if src.shape != tgt.shape:
                continue
            w = weight[y0:y1, x0:x1][..., None]
            out[y0:y1, x0:x1] = tgt * (1 - w) + src * w
            done += 1
    log(f"    克隆补正 {done} 块（块 {block_cells} 格，搜索半径 {radius_cells} 格）")
    return Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)), done


# --------------------------------------------------------------------------- #
# 城市底座
# --------------------------------------------------------------------------- #
def stamp_pedestals(img, cities, px, py, cells_by_type, color, feather):
    im = img.convert("RGBA")
    layer = Image.new("RGBA", im.size, (0, 0, 0, 0))
    dr = ImageDraw.Draw(layer)
    core = (color + (225,)) if color else (0, 0, 0, 225)
    soft = (color + (80,)) if color else (0, 0, 0, 80)
    for name, x, y, bt in cities:
        cells = cells_by_type.get(bt, cells_by_type.get(0, 1.6))
        half = cells / 2.0
        cx = (x + 0.5) * px
        cy = (y + 0.5) * py
        rx, ry = half * px, half * py
        dr.rectangle([cx - rx * 1.4, cy - ry * 1.4, cx + rx * 1.4, cy + ry * 1.4], fill=soft)
        dr.rectangle([cx - rx, cy - ry, cx + rx, cy + ry], fill=core)
    if feather > 0:
        layer = layer.filter(ImageFilter.GaussianBlur(radius=feather))
    im.alpha_composite(layer)
    return im.convert("RGB")


# --------------------------------------------------------------------------- #
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--scenario", required=True)
    ap.add_argument("--tex-dir", required=True)
    ap.add_argument("--orig-bin", default=None, help="mod 原始 .bin（默认自动找 .bak_repair/.bak_skybox）")
    ap.add_argument("--water-ids", default="7,8,9,21,22", help="水域地形 id（默认含 川/河）")
    ap.add_argument("--mismatch-src", default="both", choices=("data", "coast", "both"))
    ap.add_argument("--coast-tol", type=float, default=3.0,
                    help="海岸线错位容差（格）：地形水与美术水的最近距离超过它才算错位")
    ap.add_argument("--min-region", type=int, default=60, help="连通域小于该格数视为噪声，忽略")
    ap.add_argument("--block", type=int, default=16, help="克隆补正的分块（格）")
    ap.add_argument("--block-min", type=float, default=0.06,
                    help="块内不符比例超过它才处理（数据差异很小时调低，如 0.001）")
    ap.add_argument("--search-radius", type=int, default=110, help="克隆源搜索半径（格）")
    ap.add_argument("--seasons", default="0,1,2,3")
    ap.add_argument("--pedestal", default="on", choices=("on", "off"))
    ap.add_argument("--pedestal-cells", type=float, default=1.6, help="默认底座边长（格）")
    ap.add_argument("--pedestal-cells-by-type", default=None,
                    help="按据点类型分设边长，如 \"1:1.6,2:1.0,3:1.0\"（1城 2关 3港）")
    ap.add_argument("--pedestal-color", default=None)
    ap.add_argument("--pedestal-feather", type=float, default=0.8)
    ap.add_argument("--site-types", default="1,2,3", help="1=城 2=关 3=港")
    ap.add_argument("--analyze", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    binp = Path(args.bin_path)
    tex = Path(args.tex_dir)
    cur = read_map(binp)
    cx, cy = cur["cx"], cur["cy"]
    water_ids = [int(x) for x in args.water_ids.split(",") if x.strip()]
    cur_water = np.isin(cur["terr"], water_ids)                    # [x, y]

    log_lines = []

    def log(s=""):
        print(s, flush=True)
        log_lines.append(s)

    log("=" * 78)
    log(f"底图保样式重刷: {binp.name}  格 {cx}x{cy}  水域 id {water_ids}")

    # ---- A) 数据差异 ----
    orig = None
    if args.orig_bin:
        orig = Path(args.orig_bin)
    else:
        for cand in ("bak_repair", "bak_skybox", "bak"):
            p = binp.with_suffix(binp.suffix + "." + cand)
            if p.exists():
                orig = p
                break
    data_diff = np.zeros((cx, cy), bool)
    if orig and orig.exists():
        o = read_map(orig)
        data_diff = ((cur["terr"] != o["terr"]) | (cur["area"] != o["area"])
                     | (cur["state"] != o["state"]))
        log(f"  原始 .bin（{orig.name}）与当前差异: 地形 {int((cur['terr'] != o['terr']).sum())}"
            f"  areaId {int((cur['area'] != o['area']).sum())}"
            f"  state {int((cur['state'] != o['state']).sum())}  合计 {int(data_diff.sum())} 格")
    else:
        log("  [警告] 找不到原始 .bin，跳过数据差异项（只做海岸线偏差）")

    # ---- 据点 ----
    sc = json.loads(Path(args.scenario).read_text(encoding="utf-8"))["citySet"]
    keep = {int(x) for x in str(args.site_types).split(",") if x.strip()}
    cities = [(v.get("Name"), int(v["x"]), int(v["y"]), int(v.get("BuildingType", 1)))
              for v in sc.values() if "x" in v and int(v.get("BuildingType", 1)) in keep]
    from collections import Counter
    log(f"  据点 {len(cities)} 个，类型分布 "
        f"{dict(Counter(bt for _, _, _, bt in cities))}（1城 2关 3港）")

    want = {int(x) for x in str(args.seasons).split(",") if x.strip()}
    sheets = []
    total_fix = 0
    for si, season in enumerate(SEASONS):
        if si not in want:
            continue
        p = tex / f"BaseMap{si}.png"
        if not p.exists():
            log(f"  [跳过] {p.name} 不存在")
            continue
        img = Image.open(p).convert("RGB")
        W, H = img.size
        px, py = W / float(cx), H / float(cy)
        wn = art_waterness(img)
        terr_water_big = mask_to_img(cur_water.T, (W, H))          # 地形水（图像分辨率）
        thr = water_threshold_from_terrain(wn, terr_water_big)
        art_water_big = wn > thr
        art_water = np.asarray(Image.fromarray((art_water_big * 255).astype(np.uint8), "L")
                               .resize((cx, cy), Image.Resampling.BOX), np.float32) > 0.5
        # 几何错位：只认偏离超过容差的，忽略容差内的边界抖动
        tol_px = max(1.0, args.coast_tol * min(px, py))
        off_big = coast_offset_mask(art_water_big, terr_water_big, tol_px)
        coast_diff = np.asarray(Image.fromarray((off_big * 255).astype(np.uint8), "L")
                                .resize((cx, cy), Image.Resampling.BOX),
                                np.float32) > 0.4                   # [y, x]
        mm = np.zeros((cy, cx), bool)
        if args.mismatch_src in ("data", "both"):
            mm |= data_diff.T
        if args.mismatch_src in ("coast", "both"):
            mm |= coast_diff
        mm = clean_mask(mm, args.min_region)
        log(f"\n  BaseMap{si} ({season}) {W}x{H}  {px:.3f}x{py:.3f} 像素/格")
        log(f"    水体度阈值 {thr:+.1f}（美术判为水 {art_water_big.mean():.3f}，"
            f"地形水域 {cur_water.mean():.3f}）")
        log(f"    不符: 数据差异 {int(data_diff.sum())} 格 + 海岸线偏差 "
            f"{int(coast_diff.sum())} 格 -> 清理后 {int(mm.sum())} 格 = {mm.mean():.3f}")

        mask_big = mask_to_img(mm, (W, H))
        img2 = img
        if args.apply:
            img2, nfix = exemplar_fill(img2, mask_big, args.block, px, py,
                                       args.search_radius, log, block_min=args.block_min)
            total_fix += nfix
        if args.pedestal == "on":
            col = tuple(int(x) for x in args.pedestal_color.split(",")) if args.pedestal_color else None
            cells_by_type = {0: args.pedestal_cells}
            if args.pedestal_cells_by_type:
                for kv in args.pedestal_cells_by_type.split(","):
                    if ":" in kv:
                        k, v = kv.split(":")
                        cells_by_type[int(k)] = float(v)
            if args.apply:
                img2 = stamp_pedestals(img2, cities, px, py, cells_by_type,
                                       col, args.pedestal_feather)
                from collections import Counter as _C
                log(f"    城市底座 {len(cities)} 个  按类型边长 "
                    f"{ {k: v for k, v in sorted(cells_by_type.items()) if k} }"
                    f"  实际分布 {_C(bt for *_, bt in cities)}")
            else:
                log(f"    [分析模式] 将盖底座 {len(cities)} 个（边长 {args.pedestal_cells} 格）")

        # 前后对比小图
        if args.apply:
            bak = tex / f"BaseMap{si}.png.bak_before_repatch"
            if not bak.exists():
                bak.write_bytes(p.read_bytes())
            img2.save(p, "PNG", optimize=True)
            log(f"    已写出 {p.name}  {p.stat().st_size / 1024:.0f} KB（备份 {bak.name}）")
        else:
            o = Path(rf"D:\sango_infinity\Temp\repatch_preview_{si}_{season}.png")
            img2.save(o)
            log(f"    [分析模式] 预览 {o}")
        # 对比窗口：挑不符最密的块
        if mm.any():
            T = 48
            dens = []
            for y0 in range(0, cy - T + 1, T // 2):
                for x0 in range(0, cx - T + 1, T // 2):
                    dens.append((int(mm[y0:y0 + T, x0:x0 + T].sum()), x0, y0))
            dens.sort(reverse=True)
            _, x0, y0 = dens[0]
            a = img.crop((int(x0 * px), int(y0 * py), int((x0 + T) * px), int((y0 + T) * py)))
            b = img2.crop((int(x0 * px), int(y0 * py), int((x0 + T) * px), int((y0 + T) * py)))
            mmv = (mm[y0:y0 + T, x0:x0 + T] * 255).astype(np.uint8)
            c = Image.fromarray(np.stack([mmv] * 3, -1)).resize(a.size, Image.Resampling.NEAREST)
            z = 3
            pair = Image.new("RGB", (a.size[0] * 3 * z + 16, a.size[1] * z), (25, 25, 30))
            for i, im in enumerate((a, b, c)):
                pair.paste(im.resize((a.size[0] * z, a.size[1] * z), Image.Resampling.NEAREST),
                           (i * (a.size[0] * z + 8), 0))
            dr = ImageDraw.Draw(pair)
            dr.text((4, 2), f"{season} 不符最密处 格({x0},{y0})  原图 | 处理后 | 不符掩膜",
                    fill=(255, 255, 0))
            sheets.append(pair)

    if sheets:
        wid = max(s.size[0] for s in sheets)
        hgt = sum(s.size[1] for s in sheets) + 8 * (len(sheets) - 1)
        sheet = Image.new("RGB", (wid, hgt), (18, 18, 22))
        yy = 0
        for s in sheets:
            sheet.paste(s, (0, yy))
            yy += s.size[1] + 8
        if max(sheet.size) > 1500:
            k = 1500 / float(max(sheet.size))
            sheet = sheet.resize((int(sheet.size[0] * k), int(sheet.size[1] * k)),
                                 Image.Resampling.LANCZOS)
        o = Path(r"D:\sango_infinity\Temp\repatch_compare.png")
        sheet.save(o)
        log(f"\n前后对比图: {o}")

    if not args.apply:
        log("\n（分析模式，未写盘。确认后用 --apply 执行。）")
    else:
        log(f"\n完成。共补正 {total_fix} 块。")
    Path(r"D:\sango_infinity\Temp\repatch_log.txt").write_text("\n".join(log_lines), encoding="utf-8")


if __name__ == "__main__":
    main()
