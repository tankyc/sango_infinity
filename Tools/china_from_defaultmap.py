"""从 Sango Infinity 的 DefaultMap.bin 生成 san11-world 需要的：
    data/china_shex.npy    200x200x11  光荣中国地图的 SHEX 记录
    data/china_k3st_v.npy  1025x1025x8 光荣中国舞台的顶点记录

Sango 的地形编号与光荣 SHEX 的地形类型大体是「+1」关系（Sango 多了个 None=0），
但 关/港 两项与光荣不同（Sango: 18=港 19=关；光荣: 17=关 18=港），故显式映射。
"""
import struct
from collections import Counter
from pathlib import Path

import numpy as np

SRC = Path(r"d:/sango_infinity/Build/Content/Map/DefaultMap.bin")
ROOT = Path(__file__).resolve().parent
DATA = ROOT / "data"
OUTSIDE = 32

# Sango TerrainIds -> 光荣 SHEX 地形类型
S2K = {0: 15, 1: 0, 2: 1, 3: 2, 4: 3, 5: 4, 6: 5, 7: 6, 8: 7, 9: 8, 10: 9,
       11: 10, 12: 11, 13: 12, 14: 13, 15: 14, 16: 15, 17: 16, 18: 18, 19: 17, 20: 19}

# 编辑器配色（tools/editor/server.py）
PALETTE = {0: (126, 190, 80), 1: (196, 172, 112), 2: (232, 214, 152), 3: (104, 150, 118),
           4: (160, 60, 160), 5: (36, 104, 40), 6: (74, 136, 224), 7: (48, 98, 206),
           8: (28, 48, 150), 9: (150, 122, 82), 10: (226, 202, 64), 11: (186, 124, 60),
           12: (236, 236, 236), 13: (110, 200, 220), 14: (172, 172, 166), 15: (96, 78, 64),
           16: (228, 40, 40), 17: (200, 60, 60), 18: (200, 80, 80), 19: (204, 170, 64)}
SEA, RIVER, UNOWNED = 8, 7, 90


def read_dotnet_string(f):
    """按 .NET BinaryReader 规则读字符串：7-bit 变长长度 + UTF-8。"""
    n, shift = 0, 0
    while True:
        b = f.read(1)
        if not b:
            raise EOFError("字符串长度读越界")
        v = b[0]
        n |= (v & 0x7F) << shift
        if not (v & 0x80):
            break
        shift += 7
    return f.read(n).decode("utf-8", "replace")


def main():
    with open(SRC, "rb") as f:
        version = struct.unpack("<i", f.read(4))[0]
        work = read_dotnet_string(f) if version >= 6 else ""
        map_w = struct.unpack("<i", f.read(4))[0]
        map_h = struct.unpack("<i", f.read(4))[0]
        grid_size = struct.unpack("<i", f.read(4))[0]
        cells_x, cells_y = map_w * 5 // grid_size, map_h * 5 // grid_size
        raw = f.read(cells_x * cells_y * 7)
        grid_tex = read_dotnet_string(f)
        quad = struct.unpack("<i", f.read(4))[0]
        vx_n, vy_n = map_w + 1, map_h + 1
        verts = f.read(vx_n * vy_n * 3)

    print(f"源: {SRC.name} v{version} work={work!r} {map_w}x{map_h} gridSize={grid_size} "
          f"-> {cells_x}x{cells_y} 格, gridTex={grid_tex!r} quadSize={quad}")

    # ---- 地格：x 外层、y 内层，每格 7 字节
    g = np.frombuffer(raw, np.uint8).reshape(cells_x, cells_y, 7)
    terrain = g[..., 0]
    area = g[..., 5].astype(np.uint16) | (g[..., 6].astype(np.uint16) << 8)

    inside = terrain != OUTSIDE
    xs, ys = np.nonzero(inside.any(axis=1))[0], np.nonzero(inside.any(axis=0))[0]
    off_x, off_y = int(xs[0]), int(ys[0])
    span_x, span_y = int(xs[-1] - xs[0] + 1), int(ys[-1] - ys[0] + 1)
    print(f"中国区域: 起点 ({off_x},{off_y}) 尺寸 {span_x}x{span_y}")
    assert (span_x, span_y) == (200, 200), "中国区域不是 200x200"

    ct = terrain[off_x:off_x + 200, off_y:off_y + 200]
    ca = area[off_x:off_x + 200, off_y:off_y + 200]

    # ---- 组装 SHEX 记录（11 字节）
    shex = np.zeros((200, 200, 11), np.uint8)
    lut = np.zeros(256, np.uint8)
    for s, k in S2K.items():
        lut[s] = k
    koei_t = lut[ct]
    shex[..., 0] = koei_t
    shex[..., 1] = ca & 0xFF
    shex[..., 2] = ca >> 8
    shex[..., 4] = np.where(koei_t == SEA, 4, np.where(koei_t == RIVER, 2, 6))
    shex[..., 9] = 15
    np.save(DATA / "china_shex.npy", shex)
    print("地形(光荣编号)分布:", sorted(Counter(koei_t.ravel().tolist()).items()))
    print("区域号取值数:", len(np.unique(ca)), "最大:", int(ca.max()))

    # ---- 顶点：x 外层、y 内层，每顶点 3 字节 (height, textureIndex, water)
    v = np.frombuffer(verts, np.uint8).reshape(vx_n, vy_n, 3)
    k3st = np.zeros((vx_n, vy_n, 8), np.uint8)
    k3st[..., 0] = v[..., 0]                 # 高度
    k3st[..., 7] = np.minimum(v[..., 1], 63)  # 材质（用贴图索引近似）
    np.save(DATA / "china_k3st_v.npy", k3st)
    print(f"顶点 {vx_n}x{vy_n}  高度 min/max={v[..., 0].min()}/{v[..., 0].max()}  "
          f"材质取值数={len(np.unique(k3st[..., 7]))}")

    # ---- 预览图（编辑器配色）
    img = np.zeros((200, 200, 3), np.uint8)
    for tid, col in PALETTE.items():
        img[koei_t == tid] = col
    from PIL import Image
    Image.fromarray(img).resize((800, 800), Image.NEAREST).save(ROOT / "_china_preview.png")
    print("预览图:", ROOT / "_china_preview.png")


if __name__ == "__main__":
    main()
