"""生成四季底图。

两种配色模式：
  --mode default （默认）直接用「反推自 DefaultMap」的调色板，色彩域与其完全一致。
      反推来源：DefaultMap.bin 的格地形 + Assets/Map/Default/BaseTex/BaseMap{0..3}.png，
      脚本见 _extract_default_palette.py，结果落在 data/default_palette.json。
      实测 Default 底图是**近乎中性的灰调**（秋·草 120,118,107 / 海 112,116,105 / 山 115,113,101，
      各地形之间只差 ±10，整体 meanSat 0.05~0.12、meanLuma 123~129、p2≈52 / p98≈197），
      明暗对比来自原版美术浮雕而非顶点高度（反推出的高度曲线几乎是平的 0.86~1.09）。
  --mode formula 用引擎 BaseMapGenerator 的公式 + 可调配色（--white-mix/--value-target/--sat-mul）。

色彩空间：本工程是 Gamma（ProjectSettings.asset: m_ActiveColorSpace = 0），
sango_shaderLib.hlsl:276-277 的 LinearToSRGB/SRGBToLinear 都是注释掉的，
着色器对 _BaseTex 无任何手动转换 —— 全部按 sRGB/伽马空间运算，不做 linear 往返。

尺寸规则：
  实际尺寸 = 顶点尺寸向下取整到 4 的倍数（块压缩 BC/ETC2 要求边长能被 4 整除）；
  压缩尺寸 = 保持宽高比、最长边 <= LIMIT（默认 2048）且两边仍为 4 的倍数。
输出两套：BaseMap{i}.png（压缩，引擎默认） + BaseMap{i}_full.png（实际尺寸）
"""
import argparse
import colorsys
import json
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent
DEF_SNG = ROOT / "data" / "sango"
DEF_PALETTE = ROOT / "data" / "default_palette.json"
DEF_OUT = Path(r"d:/sango_infinity/Build/Content/Assets/Map/world/BaseTex")

BASE_WHITE, SEA_LEVEL, SHADE_DEFAULT = 0.95, 10.0 / 255.0, 0.25

# 与 BaseMapGenerator.BuildTerrainColor 一致（Sango 地形 Id -> RGB），formula 模式与缺省回退用
TC = {0: (120, 120, 120), 1: (110, 170, 90), 2: (150, 120, 80), 3: (200, 185, 140),
      4: (110, 140, 110), 5: (140, 90, 160), 6: (70, 120, 70), 7: (90, 140, 185),
      8: (90, 140, 185), 9: (70, 115, 160), 10: (150, 140, 110), 11: (190, 175, 140),
      12: (150, 130, 100), 13: (140, 130, 110), 14: (120, 165, 175), 15: (170, 155, 120),
      16: (135, 130, 125), 17: (185, 160, 110), 18: (150, 140, 120), 19: (160, 150, 110),
      20: (175, 160, 130),
      # 与 BaseMapGenerator.TerrainColor / TerrainTypes.json 保持一致：远海/深海比近海(9)逐级加深
      21: (48, 86, 130), 22: (28, 56, 96)}

# 索引与 MapRender.SeasonNames = {Autumn, Spring, Summer, Winter} 一致
STYLES = [
    ((1.00, 0.93, 0.72), 0.12, 0.0),   # 0 秋
    ((0.94, 1.00, 0.90), 0.00, 0.0),   # 1 春
    ((0.86, 0.97, 0.84), 0.00, 0.0),   # 2 夏
    ((0.94, 0.96, 1.00), 0.55, 0.55),  # 3 冬
]


def align4(n):
    return max(4, (n // 4) * 4)


def fit_within(w, h, limit):
    if max(w, h) > limit:
        s = limit / float(max(w, h))
        w, h = w * s, h * s
    return align4(int(w)), align4(int(h))


def formula_lut(white_mix, value_target, value_mix, sat_mul):
    """引擎公式 + 可调配色，返回 {地形Id: rgb(0..1)}。"""
    ids = sorted(TC)
    rgb = np.array([TC[i] for i in ids], np.float32) / 255.0
    if white_mix > 0:
        rgb = BASE_WHITE + (rgb - BASE_WHITE) * white_mix
    hsv = np.array([colorsys.rgb_to_hsv(*c) for c in rgb], np.float32)
    h, s, v = hsv[:, 0], hsv[:, 1], hsv[:, 2]
    v = v + (value_target - v) * value_mix
    s = np.clip(s * sat_mul, 0.0, 1.0)
    out = np.array([colorsys.hsv_to_rgb(float(h[i]), float(s[i]), float(v[i]))
                    for i in range(len(ids))], np.float32)
    return {tid: out[k] for k, tid in enumerate(ids)}


def gamut_lut(target_sat, target_v):
    """把地形色压进 Default 底图的色彩域：保留色相，只对齐饱和度与明度。

    Default 底图（Assets/Map/Default/BaseTex/BaseMap{0..3}.png）实测：
      meanSat 0.053~0.122、meanLuma 122.7~129.1、p2≈52~57 / p98≈196~197。
    但它是**手绘美术贴图**，画面颜色来自笔触细节而非地形类型
    （按地形反推出来的中位色各地形之间只差 ±10，海与草几乎同色），
    所以程序化生成无法、也不该照搬它的「地形配色」。
    这里只借用它的色彩域：色相保持我们自己的地形色以保证可读，
    饱和度/明度对齐到同一个量级。
    """
    raw = formula_lut(0.0, 0.0, 0.0, 1.0)          # 原始地形色（sRGB 归一化）
    ids = sorted(TC)
    s_mean = float(np.mean([colorsys.rgb_to_hsv(*raw[i])[1] for i in ids]))
    k = target_sat / max(1e-6, s_mean)
    lut = {}
    for i in ids:
        h, s, v = colorsys.rgb_to_hsv(*raw[i])
        lut[i] = np.array(colorsys.hsv_to_rgb(h, min(1.0, s * k), target_v), np.float32)
    print(f"  色彩域对齐: 原平均饱和度 {s_mean:.3f} -> {target_sat:.3f} (x{k:.2f}), 明度 -> {target_v:.3f}",
          flush=True)
    return lut


def default_lut(palette, season, boost=1.0):
    """DefaultMap 反推调色板；缺的地形回退到引擎公式色。

    boost: 把各地形色相对「调色板均值」的差异放大若干倍。
    Default 底图是手绘美术（各地形只差 ±10），直接照搬会得到一张平坦的灰图；
    放大差异只改变地形之间的相对关系，整体均值/饱和度仍在 Default 的量级内。
    boost=1 即完全照搬。
    """
    e = palette["palette"][str(season)]
    lum = formula_lut(0.35, 0.0, 0.0, 1.0)      # 引擎原式（tint=0.35，不额外调色）
    lut = {}
    for t in sorted(TC):
        v = e.get(str(t))
        lut[t] = (np.array(v["rgb"], np.float32) / 255.0) if v else lum[t]
    if boost != 1.0:
        mu = np.mean([lut[t] for t in sorted(TC)], 0)
        for t in lut:
            lut[t] = np.clip(mu + (lut[t] - mu) * boost, 0.0, 1.0)
    return lut


def norm_alt(vh, vx, h):
    """与引擎同一套归一化高度：InverseLerp(seaLevel=10/255, 1, h/255)。"""
    return np.clip((vh[vx, :h].astype(np.float32) / 255.0 - SEA_LEVEL) / (1.0 - SEA_LEVEL), 0.0, 1.0)


def terrain_alt_mean(terrain, vh, w, h):
    """每种地形在整图上的平均归一化高度。

    用途：把高度明暗项做成「均值 1」——反推自 DefaultMap 的调色板本身已经包含
    该地形在其典型高度上的明暗，若再乘一个 0.75~1.0 的项就会二次压暗
    （海洋 alt=0 会被整体压到 0.75 倍）。按地形各自的平均高度居中即可避免。
    """
    CX, CY = terrain.shape
    VX = vh.shape[0]
    s = np.zeros(32, np.float64)
    n = np.zeros(32, np.int64)
    vy_all = np.arange(h)
    for vx in range(0, min(w, VX), 3):          # 抽样即可
        cx = min(vx // 4, CX - 1)
        cy = np.clip((vy_all - (cx & 1) * 2) // 4, 0, CY - 1)
        tt = terrain[cx, cy]
        alt = norm_alt(vh, vx, h)
        for t in np.unique(tt):
            m = tt == t
            s[t] += float(alt[m].sum())
            n[t] += int(m.sum())
    mean = np.full(32, 0.3, np.float32)
    idx = n > 0
    mean[idx] = (s[idx] / n[idx]).astype(np.float32)
    return mean


def build(terrain, vh, w, h, lut, tint_mul, desat, snow, shade, alt_mean):
    """按逐顶点地形色 + 高度明暗生成一张底图（行 0 在图像顶部）。"""
    CX, CY = terrain.shape
    VX, VY = vh.shape
    tint = np.array(tint_mul, np.float32)
    luma_w = np.array([0.299, 0.587, 0.114], np.float32)
    out = np.zeros((h, w, 3), np.uint8)
    vy_all = np.arange(h)

    for vx in range(w):
        if vx >= VX:
            break
        cx = min(vx // 4, CX - 1)
        cy = np.clip((vy_all - (cx & 1) * 2) // 4, 0, CY - 1)
        tt = terrain[cx, cy]
        col = np.empty((h, 3), np.float32)
        known = np.zeros(h, bool)
        for tid, c in lut.items():
            m = tt == tid
            if m.any():
                col[m] = c
                known |= m
        if not known.all():
            col[~known] = lut.get(0, np.array(TC[0], np.float32) / 255.0)

        if tint_mul != (1.0, 1.0, 1.0):
            col *= tint
        if desat > 0:
            luma = col @ luma_w
            col = col + (luma[:, None] - col) * desat

        alt = norm_alt(vh, vx, h)
        if shade > 0:
            # 均值 1 的高度明暗：按地形各自的平均高度居中，只做浮雕，不整体压暗
            term = 1.0 + shade * (alt - alt_mean[tt])
            np.clip(term, 0.2, 1.8, out=term)
            col *= term[:, None]

        if snow > 0:
            t = np.clip((alt - snow) / (1.0 - snow), 0.0, 1.0)
            t = t * t * (3.0 - 2.0 * t)
            col = col + (1.0 - col) * t[:, None]

        # 行序：PIL 的 row0 是图像顶部 = Unity 纹理 v=1。
        # 着色器 v = 1 - worldX/_MapHeight，而 worldX 正比于 vy，
        # 所以 vy=0 必须落在 PNG 的第 0 行。
        # 注意：引擎 BaseMapGenerator 写的是 pixels[(height-1)-vy]，那是配 SetPixels32 的
        # （Unity 像素数组自下而上），照抄到 PIL 会得到上下镜像的 PNG。
        out[vy_all, vx] = np.clip(col * 255.0, 0, 255).astype(np.uint8)
    return out


def stats(a):
    # 注意：必须用 float64。2880 万元素用 float32 求均值时累积误差可达 30%
    # （实测 float32 得 81.3，float64 才是真值 115.7）。
    f = a.astype(np.float64)
    mx, mn = f.max(2), f.min(2)
    sat = np.where(mx > 0, (mx - mn) / np.maximum(mx, 1), 0)
    return (f.reshape(-1, 3).mean(0).round(1), round(float(sat.mean()), 3),
            round(float((f @ [0.299, 0.587, 0.114]).mean()), 1),
            np.percentile(f, 2, axis=(0, 1)).round(0), np.percentile(f, 98, axis=(0, 1)).round(0))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=str(DEF_SNG), help="sango_convert.py 的输出目录")
    ap.add_argument("--out", default=str(DEF_OUT), help="BaseTex 输出目录")
    ap.add_argument("--mode", choices=("gamut", "default", "formula"), default="gamut",
                    help="gamut = 保留地形色相、饱和度/明度对齐 Default 底图色彩域(推荐)；"
                         "default = 照搬反推自 DefaultMap 的调色板；formula = 引擎公式 + 可调配色")
    ap.add_argument("--target-sat", type=float, default=0.155,
                    help="gamut 模式的目标平均饱和度（色型加权；实测图面约为其 1.3 倍）")
    ap.add_argument("--target-v", type=float, default=0.57, help="gamut 模式的目标 HSV 明度")
    ap.add_argument("--palette", default=str(DEF_PALETTE), help="反推调色板 json")
    ap.add_argument("--palette-boost", type=float, default=2.0,
                    help="default 模式：地形色相对调色板均值的差异放大倍数(1=完全照搬 Default)")
    ap.add_argument("--limit", type=int, default=2048)
    ap.add_argument("--only", choices=("both", "full", "small"), default="both")
    ap.add_argument("--shade", type=float, default=SHADE_DEFAULT, help="高度明暗幅度")
    ap.add_argument("--season-tint", action="store_true", help="额外叠加季节色调乘子")
    ap.add_argument("--snow", type=float, default=0.0, help="冬季雪线起点(0 关闭)")
    # 仅 formula 模式生效
    ap.add_argument("--white-mix", type=float, default=0.0)
    ap.add_argument("--value-target", type=float, default=0.94)
    ap.add_argument("--value-mix", type=float, default=0.88)
    ap.add_argument("--sat-mul", type=float, default=1.30)
    args = ap.parse_args()

    src = Path(args.src)
    out_dir = Path(args.out)
    out_dir.mkdir(parents=True, exist_ok=True)

    terrain = np.load(src / "terrain.npy")
    vh = np.load(src / "vheight.npy")
    VX, VY = vh.shape
    full_w, full_h = align4(VX), align4(VY)
    small_w, small_h = fit_within(full_w, full_h, args.limit)

    palette = None
    if args.mode == "default":
        palette = json.loads(Path(args.palette).read_text(encoding="utf-8"))
        print(f"配色模式: default(反推自 DefaultMap) 调色板={args.palette}", flush=True)
    else:
        print(f"配色模式: formula(sRGB/伽马空间) white_mix={args.white_mix} "
              f"value_target={args.value_target} value_mix={args.value_mix} sat_mul={args.sat_mul}", flush=True)
    print(f"顶点 {VX}x{VY}  实际尺寸 {full_w}x{full_h}  压缩尺寸 {small_w}x{small_h}", flush=True)
    alt_mean = terrain_alt_mean(terrain, vh, full_w, full_h)
    print("各地形平均高度(归一化):",
          {t: round(float(alt_mean[t]), 3) for t in (9, 1, 6, 3, 16, 20) if alt_mean[t] > 0}, flush=True)

    for s, (tint_mul, desat, snow) in enumerate(STYLES):
        if args.mode == "gamut":
            lut = gamut_lut(args.target_sat, args.target_v)
            tm, ds = tint_mul, desat              # gamut 模式固定使用四季色调，四张图必须不同
            sn = snow if args.snow > 0 else 0.0
        elif args.mode == "default":
            lut = default_lut(palette, s, args.palette_boost)
            tm = tint_mul if args.season_tint else (1.0, 1.0, 1.0)
            ds = desat if args.season_tint else 0.0
            sn = snow if args.snow > 0 else 0.0
        else:
            lut = formula_lut(args.white_mix, args.value_target, args.value_mix, args.sat_mul)
            tm, ds, sn = tint_mul, desat, snow
        if s == 0:
            for tid in (9, 1, 6, 3, 16):
                c = np.clip(lut[tid] * 255, 0, 255).astype(int)
                print(f"  地形{tid}: 引擎原色{TC[tid]} -> 底图色{tuple(c)}", flush=True)

        img = build(terrain, vh, full_w, full_h, lut, tm, ds, sn, args.shade, alt_mean)
        im = Image.fromarray(img)
        if args.only in ("both", "small"):
            im.resize((small_w, small_h), Image.Resampling.BOX).save(out_dir / f"BaseMap{s}.png")
        if args.only in ("both", "full"):
            im.save(out_dir / f"BaseMap{s}_full.png")
        mean, sat, luma, p2, p98 = stats(img)
        print(f"  BaseMap{s} 完成 meanRGB={mean} meanSat={sat} meanLuma={luma} p2={p2} p98={p98}", flush=True)
        del img, im

    (out_dir / "basemap_meta.json").write_text(json.dumps(
        {"vertexX": VX, "vertexY": VY, "full": [full_w, full_h], "small": [small_w, small_h],
         "limit": args.limit, "colorSpace": "sRGB/gamma", "mode": args.mode,
         "shade": args.shade, "seasonTint": args.season_tint}, ensure_ascii=False, indent=1),
        encoding="utf-8")
    print(f"四季底图完成 -> {out_dir}", flush=True)


if __name__ == "__main__":
    main()
