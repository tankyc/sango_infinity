"""按「光荣风格羊皮纸地图」渲染世界地图。

风格参照：Project/Assets/Mods/Content/Assets/UI/AtlasTexture/4855-1.png
（原版 200x200 中国区域地形图，448x448，2.24 px/格）
要素：米黄做旧纸面 / 深青海 + 波纹 / 山地棕褐浮雕 / 海岸光晕+描边 / 云纹角饰边框

数据来源：data/sango_full/{terrain.npy, vheight.npy}
  terrain (CX,CY) = (1800,1000) 格，x 为东西、y 为南北
  vheight (VX,VY) = (7201,4001) 顶点高度
朝向：与底图 BaseMapGenerator 一致 —— 图像行 = (CY-1) - y（北在上）
"""
import argparse
import colorsys
import struct
from pathlib import Path

import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parent
DEF_SRC = ROOT / "data" / "sango_full"
REF = Path(r"D:\sango_infinity\Project\Assets\Mods\Content\Assets\UI\AtlasTexture\4855-1.png")


def _read_str(f):
    n = sh = 0
    while True:
        b = f.read(1)[0]
        n |= (b & 0x7F) << sh
        if not (b & 0x80):
            break
        sh += 7
    return f.read(n).decode("utf-8", "replace")


def load_bin(path):
    """直接解析地图 .bin，返回 (terrain(CX,CY), height(VX,VY), quadSize, mapWidth, mapHeight)。"""
    f = open(path, "rb")
    ver = struct.unpack("<i", f.read(4))[0]
    if ver >= 6:
        _read_str(f)
    if ver <= 2:
        f.read(4)
    w, h = struct.unpack("<ii", f.read(8))
    f.read(4)                                     # gridSize
    gw, gh = w // 4, h // 4
    cell = np.frombuffer(f.read(gw * gh * 7), np.uint8).reshape(gw, gh, 7)
    terrain = cell[:, :, 0].astype(np.int32)
    _read_str(f)                                  # 格子贴图名
    quad = struct.unpack("<i", f.read(4))[0]
    vw, vh = w + 1, h + 1
    vtx = np.frombuffer(f.read(vw * vh * 3), np.uint8).reshape(vw, vh, 3)
    f.close()
    return terrain, vtx[:, :, 0], quad, w, h

# 参考图取色
SEA_DEEP = (26, 52, 60)
SEA_WAVE = (44, 78, 88)
SEA_EDGE = (18, 38, 46)
PAPER_DARK = (150, 139, 119)
PAPER_LIGHT = (206, 198, 182)
MOUNT_DARK = (104, 74, 58)
MOUNT_LIGHT = (186, 158, 132)
RIVER = (36, 74, 84)
HALO = (226, 219, 202)
FRAME = (240, 238, 232)

SEA_TYPES = (9,)
SHOAL_TYPES = (13, 14)
RIVER_TYPES = (7, 8)
MOUNT_TYPES = (16, 32)      # 32 是 mod 地图里超出 TerrainTypes(0..31) 的自定义类型，按岩地画

# 逐地形的羊皮纸色调（围绕 1.0），让不同地貌在同一色域内仍可分辨。
# 缺省 1.0；32 是 mod 地图外圈的边框环，画得比地面更暗。
TERRAIN_TINT = {
    2: (1.05, 1.00, 0.94), 3: (1.08, 1.02, 0.90), 4: (0.97, 1.00, 0.97),
    5: (1.02, 0.94, 1.02), 6: (0.93, 1.01, 0.91), 10: (1.04, 1.00, 0.95),
    11: (1.10, 1.06, 0.96), 12: (1.02, 0.99, 0.95), 15: (0.99, 0.96, 0.92),
    16: (0.96, 0.92, 0.87), 17: (1.09, 1.04, 0.94), 18: (1.07, 1.03, 0.96),
    19: (1.06, 1.03, 0.95), 20: (1.05, 1.02, 0.96), 32: (0.86, 0.82, 0.78),
}


def up(mask, w, h, resample=Image.BILINEAR):
    """(CY,CX) 的 0/1 掩码 -> (h,w) 浮点掩码；自动做南北翻转（北在上）。"""
    a = (np.clip(np.asarray(mask, np.float32), 0, 1) * 255).astype(np.uint8)
    im = Image.fromarray(a.T[::-1], "L")           # .T -> (CY,CX) 行=y；[::-1] 让 y 大的在上
    return np.asarray(im.resize((w, h), resample), np.float32) / 255.0


def to_img(arr, w, h, resample=Image.BILINEAR):
    """(VX,VY) 顶点数组 -> (h,w) 浮点图，同样南北翻转；支持 2D 与 (...,3)。"""
    a = np.asarray(arr)
    if a.ndim == 2:
        im = Image.fromarray(a.T[::-1])
    else:
        im = Image.fromarray(np.ascontiguousarray(a.transpose(1, 0, 2)[::-1]))
    return np.asarray(im.resize((w, h), resample), np.float32)


def fbm(w, h, seed, octaves=6, base=6, gain=0.55):
    """多倍频噪声，返回 (h,w) 的 0..1。"""
    rng = np.random.default_rng(seed)
    acc = np.zeros((h, w), np.float32)
    tot = 0.0
    amp = 1.0
    for i in range(octaves):
        sw = max(2, w // (base << i))
        sh = max(2, h // (base << i))
        small = (rng.random((sh, sw)) * 255).astype(np.uint8)
        big = np.asarray(Image.fromarray(small, "L").resize((w, h), Image.BICUBIC), np.float32) / 255.0
        acc += big * amp
        tot += amp
        amp *= gain
    return acc / tot


def wave_pattern(w, h, cell, amp, seed=7):
    """交错圆环，模拟参考图海面的波纹/鱼鳞纹。"""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    row = np.floor(yy / (cell * 0.5))
    shift = (row % 2) * cell * 0.5
    u = np.mod(xx + shift, cell) - cell * 0.5
    v = np.mod(yy, cell * 0.5) - cell * 0.25
    r = np.sqrt(u * u + (v * 2.0) ** 2)
    ring = np.clip(np.sin(r / cell * 5.5), -1, 1) * 0.5 + 0.5
    return (ring ** 2) * amp


def make_mask(terrain, types):
    return np.isin(terrain, types).astype(np.float32)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--src", default=str(DEF_SRC))
    ap.add_argument("--bin", default="", help="直接读地图 .bin（给了就忽略 --src）")
    ap.add_argument("--out", default=str(ROOT / "data" / "sango_full" / "world_map_koei.png"))
    ap.add_argument("--width", type=int, default=4032, help="输出宽（参考图密度 2.24px/格 => 4032）")
    ap.add_argument("--flip-y", type=int, default=1, help="1=北在上（与底图一致）")
    ap.add_argument("--relief", type=float, default=0.55, help="浮雕强度")
    ap.add_argument("--tint-strength", type=float, default=1.0, help="逐地形色调的放大倍数")
    ap.add_argument("--border", type=int, default=1, help="1=绘制云纹边框")
    ap.add_argument("--seed", type=int, default=20261011)
    args = ap.parse_args()

    if args.bin:
        terrain, vh, quad, mw, mh = load_bin(args.bin)
        print(f"来源 {args.bin}: {mw}x{mh} px, quadSize={quad}", flush=True)
    else:
        src = Path(args.src)
        terrain = np.load(src / "terrain.npy")            # (CX,CY)
        vh = np.load(src / "vheight.npy")                 # (VX,VY)
    CX, CY = terrain.shape
    W = args.width
    H = int(round(W * CY / CX))
    print(f"格 {CX}x{CY}  顶点 {vh.shape}  输出 {W}x{H}  北在上={args.flip_y}", flush=True)

    if not args.flip_y:                                # 关掉翻转：把掩码上下翻回来
        terrain = terrain[:, ::-1]
        vh = vh[:, ::-1]

    # ---------- 掩码 ----------
    m_sea = up(make_mask(terrain, SEA_TYPES), W, H)
    m_shoal = up(make_mask(terrain, SHOAL_TYPES), W, H)
    m_river8 = up(make_mask(terrain, (8,)), W, H, Image.BILINEAR)     # 河：主线
    m_river7 = up(make_mask(terrain, (7,)), W, H, Image.BILINEAR)     # 川：淡线
    m_mount = up(make_mask(terrain, MOUNT_TYPES), W, H)
    m_river = np.clip(m_river8 + m_river7, 0, 1)
    m_water = np.clip(m_sea + m_shoal + m_river, 0, 1)
    m_land = 1.0 - np.clip(m_sea + m_shoal, 0, 1)

    # ---------- 纸张 ----------
    print("  纸张纹理…", flush=True)
    paper_n = fbm(W, H, args.seed, octaves=7, base=5, gain=0.52)
    grain = fbm(W, H, args.seed + 1, octaves=2, base=1, gain=0.5)
    paper_n = np.clip(paper_n * 0.75 + grain * 0.25, 0, 1)
    # 大尺度做旧斑块
    stain = fbm(W, H, args.seed + 2, octaves=3, base=24, gain=0.6)
    paper_n = np.clip(paper_n * (0.86 + 0.28 * stain), 0, 1)

    def ramp(t, a, b):
        ca = np.array(a, np.float32) / 255.0
        cb = np.array(b, np.float32) / 255.0
        return ca + (cb - ca) * t[..., None]

    paper = ramp(paper_n, PAPER_DARK, PAPER_LIGHT)

    # ---------- 浮雕 ----------
    print("  高度浮雕…", flush=True)
    hv = to_img(vh.astype(np.float32) / 255.0, W, H)
    # 平滑一下，避免逐像素噪点
    hv = np.asarray(Image.fromarray((np.clip(hv, 0, 1) * 255).astype(np.uint8), "L")
                    .filter(ImageFilter.GaussianBlur(1.2)), np.float32) / 255.0
    gy, gx = np.gradient(hv)
    k = 1.0 / max(1e-3, args.relief)
    az = np.radians(315.0)                       # 光源左上
    lx, ly = np.cos(az), np.sin(az)
    denom = np.sqrt(gx * gx + gy * gy + k * k)
    shade = np.clip((-gx * lx - gy * ly + k) / denom, 0, 1)
    shade = np.clip((shade - shade.mean()) * 3.4 + 0.55, 0, 1)
    # 山地更强：按高度与地形类型加权
    alt = np.clip((hv - 10.0 / 255.0) / (1.0 - 10.0 / 255.0), 0, 1)
    mm = np.clip(m_mount * 0.75 + alt * 0.85, 0, 1)
    relief = np.clip(shade * (0.35 + 0.65 * mm), 0, 1)

    # ---------- 海 ----------
    print("  海面…", flush=True)
    sea = ramp(np.clip(m_sea, 0, 1), SEA_DEEP, SEA_WAVE)
    wp = wave_pattern(W, H, cell=max(48, W // 78), amp=1.0)
    sea = sea * (0.90 + 0.18 * wp[..., None])
    sea = np.clip(sea, 0, 1)

    # ---------- 陆地 ----------
    # 逐地形色调（上采样到输出分辨率）
    tint = np.ones(terrain.shape + (3,), np.float32)
    for t, c in TERRAIN_TINT.items():
        tint[terrain == t] = c
    tint_w = np.stack([to_img(tint[:, :, k], W, H) for k in range(3)], axis=2)
    tint_w = 1.0 + (tint_w - 1.0) * args.tint_strength
    land_col = paper * (0.86 + 0.30 * relief[..., None]) * tint_w
    # 陡坡/高山往棕褐偏
    brown = ramp(relief, MOUNT_DARK, MOUNT_LIGHT)
    mix = np.clip((1.0 - relief) * mm * 1.15, 0, 1)[..., None]
    land_col = land_col * (1 - mix) + brown * mix
    land_col = np.clip(land_col, 0, 1)

    # ---------- 合成 ----------
    print("  合成…", flush=True)
    img = land_col * m_land[..., None] + sea * (1.0 - m_land[..., None])
    # 海岸光晕：陆地边缘提亮
    halo = np.asarray(Image.fromarray((m_land * 255).astype(np.uint8), "L")
                      .filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(max(2, W // 700))),
                      np.float32) / 255.0
    halo_band = np.clip(halo - m_land, 0, 1)
    img = img + (np.array(HALO, np.float32) / 255.0 - img) * np.clip(halo_band * 1.5, 0, 1)[..., None]
    # 海岸描边（水侧一条深线）
    edge = np.asarray(Image.fromarray((np.clip(m_water, 0, 1) * 255).astype(np.uint8), "L")
                      .filter(ImageFilter.GaussianBlur(max(1, W // 1400))), np.float32) / 255.0
    edge_line = np.clip(edge - np.clip(edge - 0.5, 0, 1) * 2.0, 0, 1)
    img = img * (1 - (edge_line * 0.45)[..., None]) + (np.array(SEA_EDGE, np.float32) / 255.0) * (edge_line * 0.45)[..., None]
    # 河流：河(8) 为主线、川(7) 压到很淡，避免整块大陆被裂纹网覆盖
    def thin(mask, blur, gain):
        m = np.asarray(Image.fromarray((np.clip(mask, 0, 1) * 255).astype(np.uint8), "L")
                       .filter(ImageFilter.GaussianBlur(blur)), np.float32) / 255.0
        return np.clip(m * gain, 0, 1)

    rc = np.array(RIVER, np.float32) / 255.0
    for mask, op in ((m_river8, 0.55), (m_river7, 0.20)):
        a = thin(mask, 0.8, 1.5) * op
        img = img * (1 - a[..., None]) + rc * a[..., None]
    img = np.clip(img, 0, 1)

    # ---------- 暗角 ----------
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    dx = (xx / W - 0.5) * 2
    dy = (yy / H - 0.5) * 2
    vig = np.clip(1.0 - 0.30 * (dx * dx + dy * dy), 0, 1)
    img *= vig[..., None]

    out = Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8), "RGB")

    # ---------- 边框 ----------
    if args.border:
        print("  边框…", flush=True)
        o = np.asarray(out).copy()
        fw = max(4, W // 300)
        t = W // 420
        for off in (fw, fw + t * 2):
            o[off:off + t, :] = FRAME
            o[-(off + t):-off, :] = FRAME
            o[:, off:off + t] = FRAME
            o[:, -(off + t):-off] = FRAME
        # 四角云纹：取参考图角部，但只抠出「白色笔画」。
        # 参考图角部是白色云纹画在地图上，整块贴会把它的地图内容一起带进来。
        if REF.exists():
            ref = Image.open(REF).convert("RGB")
            rw, rh = ref.size
            c = int(rw * 0.30)
            corners_src = [(0, 0, c, c), (rw - c, 0, rw, c), (0, rh - c, c, rh), (rw - c, rh - c, rw, rh)]
            dst_c = int(min(W, H) * 0.22)
            places = [(fw, fw), (W - fw - dst_c, fw), (fw, H - fw - dst_c), (W - fw - dst_c, H - fw - dst_c)]
            frame_rgb = np.array(FRAME, np.float32)
            for (bx, by, bx1, by1), (px, py) in zip(corners_src, places):
                tile = np.asarray(ref.crop((bx, by, bx1, by1)).resize((dst_c, dst_c), Image.LANCZOS), np.float32)
                lum = tile.mean(2)
                a = np.clip((lum - 212.0) / 40.0, 0, 1) ** 1.1
                a = np.asarray(Image.fromarray((a * 255).astype(np.uint8), "L")
                               .filter(ImageFilter.GaussianBlur(0.6)), np.float32) / 255.0
                dst = o[py:py + dst_c, px:px + dst_c].astype(np.float32)
                o[py:py + dst_c, px:px + dst_c] = np.clip(
                    dst * (1 - a[..., None]) + frame_rgb * a[..., None], 0, 255).astype(np.uint8)
        out = Image.fromarray(o, "RGB")

    Path(args.out).parent.mkdir(parents=True, exist_ok=True)
    out.save(args.out)
    a = np.asarray(out).astype(np.float64)
    print(f"已写入 {args.out}  {out.size}  mean={a.reshape(-1,3).mean(0).round(1)}", flush=True)


if __name__ == "__main__":
    main()
