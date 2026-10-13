# -*- coding: utf-8 -*-
"""sango_basemap_patch.py —— 就地修正地图的四季底图（BaseTex），不改内部美术。

做两件事：
  1) 虚空重绘：把 .bin 里 terrainType==0（虚空）的格，在底图上刷成该季的虚空色。
     世界图外圈 20 格在 .bin 里已置 0，但底图还是改动前的地形（海/陆地/山），
     造成「数据与贴图不一致」。内部像素一律不动，避免破坏已有美术。
  2) 城市底座：给每个据点（按剧本 coords + BuildingType）在底图上盖一个黑色底座，
     并对齐官方 Default 底图的画法（尺寸/颜色可用 --measure 从官方底图量出来）。

尺寸约定（已实测）：底图行 = 格 y、列 = 格 x（行↔worldX↔vy、列↔worldZ↔vx）。
  世界图 2048x1136 / 1800x1000 = 1.1378 x 1.1360 像素每格（非整数）
  官方 Default 1024x1024 / 256x256 = 4.000 像素每格

用法：
    python sango_basemap_patch.py --bin <图.bin> --scenario <剧本.json> \
        --tex-dir <Assets/Map/<wc>/BaseTex> [--measure] [--report] [--apply]
"""
import argparse
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin          # noqa: E402

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SEASONS = ("Autumn", "Spring", "Summer", "Winter")
# 虚空色（sRGB，四季各一份）：中性偏冷的深灰，与「域外」语义相符
VOID_COLOR = [(74, 76, 78), (74, 78, 74), (72, 76, 72), (78, 80, 86)]


def measure_official(tex: Path, bin_path: Path, win_cells=3):
    """从官方底图量出城市底座的实际尺寸与颜色（像素每格、方块半径、颜色）。"""
    d = parse_bin(str(bin_path))
    cx, cy = d["cellsX"], d["cellsY"]
    tt = d["gridRaw"][..., 0].astype(np.int32)
    cities = np.argwhere(tt == 17)
    im = Image.open(tex).convert("RGB")
    a = np.asarray(im, np.float32)
    px, py = im.size[0] / float(cx), im.size[1] / float(cy)
    # 取互相远离的样本
    sel = []
    for x, y in cities[::max(1, len(cities) // 40)]:
        if all(abs(int(x) - sx) + abs(int(y) - sy) > 30 for sx, sy in sel):
            sel.append((int(x), int(y)))
        if len(sel) >= 12:
            break
    w = int(round(win_cells * px))
    h = int(round(win_cells * py))
    patches = []
    for x, y in sel:
        x0 = int(x * px) - w
        y0 = int(y * py) - h
        p = a[max(0, y0):y0 + 2 * h, max(0, x0):x0 + 2 * w]
        if p.size:
            patches.append((p, w, h))
    if not patches:
        return None
    # 以「比该窗口内均值暗 25%」为阈值找底座像素
    dark_frac, sizes, cols = [], [], []
    for p, w, h in patches:
        lum = p @ [0.299, 0.587, 0.114]
        c = np.array([lum[h, w]])
        local = lum.mean()
        m = lum < local * 0.65
        dark_frac.append(float(m.mean()))
        if m.any():
            ys, xs = np.nonzero(m)
            sizes.append((xs.max() - xs.min() + 1, ys.max() - ys.min() + 1))
            cols.append(p[m].mean(0))
    out = {
        "px_per_cell": (round(px, 4), round(py, 4)),
        "samples": len(patches),
        "dark_cover": round(float(np.mean(dark_frac)), 4),
        "size_px": np.mean(sizes, 0).round(1).tolist() if sizes else None,
        "color": np.mean(cols, 0).round(0).astype(int).tolist() if cols else None,
    }
    out["size_cells"] = [round(out["size_px"][0] / px, 2), round(out["size_px"][1] / py, 2)] \
        if out["size_px"] else None
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--scenario", required=True)
    ap.add_argument("--tex-dir", required=True, help="Assets/Map/<workContent>/BaseTex")
    ap.add_argument("--void", default="on", choices=("on", "off"), help="是否重绘虚空格")
    ap.add_argument("--cities", default="on", choices=("on", "off"), help="是否盖城市底座")
    ap.add_argument("--site-types", default="1,2,3",
                    help="要盖底座的据点类型：1=城 2=关 3=港，逗号分隔（默认全部）")
    ap.add_argument("--pedestal-cells", type=float, default=1.15, help="底座边长（格）")
    ap.add_argument("--pedestal-color", default=None, help="如 48,45,42；默认按官方量测")
    ap.add_argument("--measure", action="store_true", help="量测官方底图的底座画法")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()

    binp = Path(args.bin_path)
    d = parse_bin(str(binp))
    cx, cy = d["cellsX"], d["cellsY"]
    tt = d["gridRaw"][..., 0].astype(np.int32)          # [x, y]

    sc = json.loads(Path(args.scenario).read_text(encoding="utf-8"))["citySet"]
    keep_types = {int(x) for x in str(args.site_types).split(",") if x.strip()}
    cities = []
    for v in sc.values():
        if "x" not in v:
            continue
        bt = int(v.get("BuildingType", 1))
        if bt not in keep_types:
            continue
        cities.append((v.get("Name"), int(v["x"]), int(v["y"]), bt, int(v.get("Id", -1))))
    print("=" * 74)
    print(f"底图修正: {binp.name}  格 {cx}x{cy}  "
          f"据点 {len(cities)}（类型 {sorted(keep_types)}，1城/2关/3港）")
    print(f"  虚空格(terrainType==0): {int((tt == 0).sum())} 格")
    print("=" * 74)

    if args.measure:
        off = Path(r"D:\sango_infinity\Build\Content\Assets\Map\Default")
        spec = measure_official(off / "BaseTex/BaseMap0.png",
                                off.parent.parent.parent / "Map/DefaultMap.bin")
        print("\n[官方 Default 底图的底座量测]")
        print("  ", spec)

    ped_color = None
    if args.pedestal_color:
        ped_color = tuple(int(x) for x in args.pedestal_color.split(","))
    print(f"\n底座参数: 边长 {args.pedestal_cells} 格, 颜色 "
          f"{ped_color if ped_color else '按底层像素压暗 40%'}")

    tex_dir = Path(args.tex_dir)
    if not tex_dir.is_dir():
        print(f"[错误] 底图目录不存在: {tex_dir}")
        return

    changed_total = 0
    for si, season in enumerate(SEASONS):
        p = tex_dir / f"BaseMap{si}.png"
        if not p.exists():
            print(f"  [跳过] {p.name} 不存在")
            continue
        im = Image.open(p).convert("RGB")
        a = np.asarray(im).copy()
        H, W = a.shape[:2]
        px, py = W / float(cx), H / float(cy)
        n_void = 0
        n_ped = 0

        # ---- 1) 虚空格重绘（按格精确刷，不碰内部）----
        if args.void == "on":
            vc = np.array(VOID_COLOR[si], np.uint8)
            mask = (tt == 0)                                # [x, y]
            # 逐格刷：格 (x,y) -> 图像 col=[x*px,(x+1)*px), row=[y*py,(y+1)*py)
            for x in np.nonzero(mask.any(1))[0]:
                c0 = int(round(x * px))
                c1 = max(c0 + 1, int(round((x + 1) * px)))
                for y in np.nonzero(mask[x])[0]:
                    r0 = int(round(y * py))
                    r1 = max(r0 + 1, int(round((y + 1) * py)))
                    a[r0:r1, c0:c1] = vc
                    n_void += 1
            print(f"\n  BaseMap{si} ({season}) {W}x{H}  {px:.4f}x{py:.4f} 像素/格")
            print(f"    虚空重绘 {n_void} 格")

        # ---- 2) 城市底座 ----
        if args.cities == "on":
            layer = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            dr = ImageDraw.Draw(layer)
            half = args.pedestal_cells / 2.0
            for name, x, y, bt, cid in cities:
                if not (0 <= x < cx and 0 <= y < cy):
                    continue
                # 底座边长按「格」给定，横竖分别换算成像素（行列像素率可能不同）
                cxp = (x + 0.5) * px
                cyp = (y + 0.5) * py
                rxs = half * px
                rys = half * py
                core = (ped_color + (232,)) if ped_color else (0, 0, 0, 232)
                soft = (ped_color + (85,)) if ped_color else (0, 0, 0, 85)
                # 外圈柔化阴影 + 内圈接近纯黑的底座
                dr.rectangle([cxp - rxs * 1.35, cyp - rys * 1.35,
                              cxp + rxs * 1.35, cyp + rys * 1.35], fill=soft)
                dr.rectangle([cxp - rxs, cyp - rys, cxp + rxs, cyp + rys], fill=core)
                n_ped += 1
            layer = layer.filter(ImageFilter.GaussianBlur(radius=max(0.6, px * 0.35)))
            base = Image.fromarray(a).convert("RGBA")
            base.alpha_composite(layer)
            a = np.asarray(base.convert("RGB")).copy()
            print(f"    城市底座 {n_ped} 个（边长 {args.pedestal_cells} 格 + 柔化）")

        changed_total += 1
        if args.report or not args.apply:
            out = Path(rf"D:\sango_infinity\Temp\basemap_patch_{si}_{season}.png")
            Image.fromarray(a).save(out)
            print(f"    [报告模式] 预览写出 {out}")
        else:
            bak = tex_dir / f"BaseMap{si}.png.bak_before_patch"
            if not bak.exists():
                bak.write_bytes(p.read_bytes())
                print(f"    已备份 -> {bak.name}")
            Image.fromarray(a).save(p, "PNG", optimize=True)
            print(f"    已写出 {p.name}  {p.stat().st_size / 1024:.0f} KB")

    if not args.apply:
        print("\n--report/未加 --apply：未写盘（预览图在 Temp\\ 下）。")
    else:
        print(f"\n完成，共处理 {changed_total} 张。")


if __name__ == "__main__":
    main()
