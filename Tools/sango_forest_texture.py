# -*- coding: utf-8 -*-
"""sango_forest_texture.py —— 把森林格的顶点贴图切到「本图实际使用的森林图层」。

背景（实测）：
    本图 **没有专属的森林地面层**：作者自己的森林(6)格与草地(1)格用的是同一套地表美术 ——
    草地 L15 66.0% / L30 17.2% / L29 8.1%；森 L15 66.4% / L30 16.8% / L29 5.9%。
    把三张图并排看，L15/L29/L30 都是同一片草苔（L30 偏深偏绿），L34 是深色砖块。
    所以「森林图层」在本图只能是这三个之一：
        --layer 30（默认）官方 TerrainLayerMap 里 森 用的层，也是本图森林格用的三个里最偏林地的
        --layer 15          本图森林格占比最高者 —— 但草地也同层，等于不改，仅做归一化
    改动只碰顶点通道 1(textureIndex)，高度/水面/地形/状态/归属/模型一律不动。

用法：
    python sango_forest_texture.py --bin <bin> --report
    python sango_forest_texture.py --bin <bin> --layer 30 --apply
"""
import argparse
from collections import Counter
from pathlib import Path

import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin                          # noqa: E402
from sango_terrain_undulate import vertex_cell_index, locate_vertex_segment   # noqa: E402

import numpy as np
from PIL import Image

OUT = Path(r"D:\sango_infinity\Temp")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--terrain", type=int, default=6, help="目标地形（默认 6=森）")
    ap.add_argument("--layer", type=int, default=30,
                    help="切成目标图层（--uniform on 时全部用它；否则作为 --map 的缺省目标）")
    ap.add_argument("--map", default="15:30,0:30,33:30",
                    help="逐层映射，格式 旧:新,旧:新。默认把草苔底色 L15 与误落在陆地上的"
                         "海层 L0/L33 归到 L30，其余层(L29/L25/L23…)保持不动以保留混叠层次")
    ap.add_argument("--uniform", default="off", choices=("on", "off"),
                    help="on=目标顶点一律切到 --layer（会失去 L15/L29/L30 的混叠层次）")
    ap.add_argument("--only-model-cells", default="on", choices=("on", "off"),
                    help="只处理「有树模型」的森林格（对应上一轮刚转过来的那批）")
    ap.add_argument("--backup-tag", default="forest_tex")
    ap.add_argument("--report", action="store_true")
    ap.add_argument("--apply", action="store_true")
    args = ap.parse_args()
    if not (args.report or args.apply):
        args.report = True

    src = Path(args.bin_path)
    d = parse_bin(str(src))
    cx, cy = d["cellsX"], d["cellsY"]
    tt = d["gridRaw"][..., 0].astype(np.int32)
    V = d["verts"]
    VX, VY = V.shape[0], V.shape[1]
    H = V[..., 0].astype(np.int32)
    TI = V[..., 1].astype(np.int32)
    WB = V[..., 2].astype(np.int32)
    q = float(d["quadSize"])

    cxx, cyy, ok = vertex_cell_index(VX, VY, cx, cy)
    ct = np.where(ok, tt[cxx, cyy], -1)
    forest = ok & (ct == args.terrain)

    cells = np.zeros((cx, cy), bool)
    if args.only_model_cells == "on":
        for m in d["tail"]["models"][2]:
            vx, vy = int(round(float(m[4][2]) / q)), int(round(float(m[4][0]) / q))
            x, y = vx // 4, (vy - ((vx // 4) & 1) * 2) // 4
            if 0 <= x < cx and 0 <= y < cy:
                cells[x, y] = True
        tgt_cells = cells & (tt == args.terrain)
    else:
        tgt_cells = (tt == args.terrain)
    tgt = ok & tgt_cells[cxx, cyy]

    print("=" * 84)
    print(f"森林贴图归一: {src.name}  地形 {args.terrain} 格 {int((tt == args.terrain).sum())}"
          f"  处理格 {int(tgt_cells.sum())}  目标顶点 {int(tgt.sum())}  切到 layer {args.layer}"
          f"  模式 {'写盘' if args.apply else '预览'}")
    print("=" * 84)
    c = Counter(TI[tgt].tolist())
    tot = max(1, sum(c.values()))
    print("  当前这些顶点的 textureIndex: "
          + "  ".join(f"L{k}:{100*v/tot:.1f}%" for k, v in c.most_common(8)))
    remap = {}
    if args.uniform == "on":
        remap = {}
    else:
        for kv in str(args.map).split(","):
            if ":" in kv:
                a, b = kv.split(":")
                remap[int(a)] = int(b)
    print(f"  映射: " + ("全部 -> L%d（uniform）" % args.layer if args.uniform == "on"
                        else "  ".join(f"L{k}->L{v}" for k, v in remap.items()) or "无"))
    new_ti = np.full(TI.shape, -1, np.int32)
    if args.uniform == "on":
        new_ti[tgt] = args.layer
    else:
        new_ti[tgt] = [remap.get(int(v), int(v)) for v in TI[tgt]]
    n_change = int(((new_ti != TI) & tgt).sum())
    c2 = Counter(new_ti[tgt].tolist())
    print("  改后这些顶点的 textureIndex: "
          + "  ".join(f"L{k}:{100*v/tot:.1f}%" for k, v in c2.most_common(8)))
    print(f"  需要改写顶点 {n_change}")

    # 预览：按「该格主要图层」上色（灰=不变，绿=会切到目标层）
    img = np.zeros((cy, cx, 3), np.uint8)
    img[tt == args.terrain] = (45, 70, 45)
    for x in range(cx):
        ys = np.nonzero(tgt_cells[x])[0]
        for y in ys:
            img[y, x] = (0, 200, 90)
    Image.fromarray(img).resize((780, 780), Image.Resampling.NEAREST)\
        .save(OUT / "forest_texture.jpg", quality=90)
    print(f"  预览 {OUT / 'forest_texture.jpg'}（绿=本次切图层的森林格）")

    if not args.apply:
        print("\n（预览模式，未写盘。确认后加 --apply）")
        return

    raw = bytearray(src.read_bytes())
    base, best = locate_vertex_segment(raw, cx, cy, H, VX, VY)
    print(f"\n  顶点段基线 {base} 定位 {best[1]} 步长 {best[2]} 抽样匹配 {best[0]:.3f}")
    vstart, stride = best[1], best[2]
    if stride != 3:
        print(f"  [警告] 步长 {stride} 不是 3，通道布局存疑，已中止")
        return
    xs, ys = np.nonzero((new_ti != TI) & tgt)        # 只写真正需要变的顶点
    cnt = 0
    for vx, vy in zip(xs.tolist(), ys.tolist()):
        o = vstart + (vx * VY + vy) * stride + 1     # 通道1 = textureIndex
        nb = int(new_ti[vx, vy])
        if raw[o] != nb:
            raw[o] = nb
            cnt += 1
    bak = src.with_suffix(src.suffix + f".bak_before_{args.backup_tag}")
    k = 1
    while bak.exists():
        bak = src.with_suffix(src.suffix + f".bak_before_{args.backup_tag}_{k}")
        k += 1
    bak.write_bytes(src.read_bytes())
    src.write_bytes(bytes(raw))
    print(f"  已写入 {src.name}  {src.stat().st_size/1024/1024:.2f} MB  改写顶点 {cnt}"
          f"（备份 {bak.name}）")

    d2 = parse_bin(str(src))
    t2 = d2["verts"][..., 1].astype(np.int32)
    print(f"  复核 textureIndex: 改动顶点 {int((t2 != TI).sum())}（应为 {cnt}）"
          f"   全部落在目标集内: {bool(((t2 != TI) & ~tgt).sum() == 0)}"
          f"   目标集与预期一致: {bool((t2[tgt] == new_ti[tgt]).all())}"
          f"   目标集外的顶点未动: {bool((t2[~tgt] == TI[~tgt]).all())}")
    print(f"  复核 高度未动: {bool(np.array_equal(d2['verts'][...,0], H))}"
          f"   water 未动: {bool(np.array_equal(d2['verts'][...,2], WB))}")
    print(f"  复核 地形/状态/归属未动: {bool(np.array_equal(d2['gridRaw'], d['gridRaw']))}"
          f"   模型 {len(d['tail']['models'][2])} -> {len(d2['tail']['models'][2])}"
          f"   标注 {len(d['tail']['labels'][2])} -> {len(d2['tail']['labels'][2])}")


if __name__ == "__main__":
    main()
