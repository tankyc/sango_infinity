"""把地图外圈 N 格做成「虚空」：terrainType = 0(无)、areaId = 0(无所属城市)。

用途：给地图加一圈不可通行、不归属任何城的边界（单位走不出去，也不会被算进
领地/内政判定）。

实现要点：
- 只改 mapGrid 段每格 7 字节里的 **字节 0（terrainType）** 与 **字节 5..6（areaId, u16 小端）**；
  mapData（顶点）与尾部段原样保留，**文件总长不变、后续字节偏移不变**，因此可以就地改写。
- terrainType 0 = TerrainTypes.json 里的「无」，moveable=false / canBuild=false。
- areaId 0 会让 Map.Load 里 `citySet.Get(0)` 返回 null，该格不进入任何 City.areaCellList。
- 顺带报告外圈里 terrainState 非 0 的格子：若带着内政/防守位，清掉更干净（--keep-state 可保留）。

用法：
    python sango_edge_void.py --report                     # 只体检
    python sango_edge_void.py --apply                      # 就地改写（自动 .bak 备份）
    python sango_edge_void.py --bin <别的.bin> --margin 20 --apply
"""
import argparse
import shutil
import struct
from pathlib import Path

import numpy as np

DEF_BIN = Path(r"D:\世界地图\Map\world_full.bin")
REF_BIN = Path(r"d:\sango_infinity\Build\Content\Map\world_full.bin")

TERRAIN_NONE = 0


def read_str(buf, off):
    """Unity BinaryWriter 的 7-bit 长度前缀字符串。"""
    n, sh = 0, 0
    while True:
        v = buf[off]
        off += 1
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    return buf[off:off + n].decode("utf-8", "replace"), off + n


def parse_header(buf):
    ver = struct.unpack_from("<i", buf, 0)[0]
    off = 4
    work = ""
    if ver >= 6:
        work, off = read_str(buf, off)
    w, h = struct.unpack_from("<ii", buf, off)
    off += 8
    gs = struct.unpack_from("<i", buf, off)[0]
    off += 4
    cx, cy = w * 5 // gs, h * 5 // gs
    return ver, work, w, h, gs, cx, cy, off


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", default=str(DEF_BIN))
    ap.add_argument("--margin", type=int, default=20, help="外圈多少格做成虚空")
    ap.add_argument("--keep-state", action="store_true", help="保留外圈的 terrainState（默认清零）")
    ap.add_argument("--report", action="store_true", help="只体检不改写")
    ap.add_argument("--apply", action="store_true", help="就地改写（自动备份 .bak）")
    args = ap.parse_args()

    p = Path(args.bin)
    if not p.exists():
        print(f"[错误] 找不到 {p}", flush=True)
        return
    print(f"文件: {p}  {p.stat().st_size / 1048576:.1f} MB", flush=True)
    if REF_BIN.exists() and p.resolve() != REF_BIN.resolve():
        a, b = p.stat().st_size, REF_BIN.stat().st_size
        print(f"  参照 {REF_BIN.name}: {b / 1048576:.1f} MB"
              f"{'（大小一致）' if a == b else f'（大小不同 {a} vs {b}）'}", flush=True)

    buf = bytearray(p.read_bytes())
    ver, work, w, h, gs, cx, cy, cell_off = parse_header(buf)
    print(f"  version={ver} work={work!r} mapWidth={w} mapHeight={h} gridSize={gs}"
          f" -> 格 {cx}x{cy}", flush=True)
    need = cell_off + cx * cy * 7
    print(f"  mapGrid 段: 偏移 {cell_off} .. {need}（7 字节/格）；文件 {len(buf)} 字节，"
          f"{'完整' if len(buf) >= need else '**截断**'}", flush=True)

    cells = np.frombuffer(bytes(buf[cell_off:need]), np.uint8).reshape(cx, cy, 7)
    tt = cells[..., 0]
    st = (cells[..., 1].astype(np.uint32) | (cells[..., 2].astype(np.uint32) << 8)
          | (cells[..., 3].astype(np.uint32) << 16) | (cells[..., 4].astype(np.uint32) << 24))
    aid = cells[..., 5].astype(np.int32) | (cells[..., 6].astype(np.int32) << 8)

    m = args.margin
    xs = np.arange(cx)[:, None]
    ys = np.arange(cy)[None, :]
    edge = (xs < m) | (xs >= cx - m) | (ys < m) | (ys >= cy - m)
    print(f"\n外圈 {m} 格: {int(edge.sum())} 格 / 全图 {int(edge.size)} 格"
          f"（{edge.mean():.3f}）", flush=True)
    print(f"  当前地形分布: {dict(zip(*np.unique(tt[edge], return_counts=True)))}"
          if edge.any() else "", flush=True)
    print(f"  当前 areaId>0 的: {int((aid[edge] > 0).sum())} 格", flush=True)
    nz = int((st[edge] != 0).sum())
    print(f"  当前 terrainState 非 0 的: {nz} 格 {dict(zip(*np.unique(st[edge][st[edge] != 0], return_counts=True)))}"
          if nz else "  当前 terrainState 全 0", flush=True)
    print(f"  已满足(terrainType=0 且 areaId=0)的: "
          f"{int(((tt[edge] == 0) & (aid[edge] == 0)).sum())} 格", flush=True)

    if not args.apply or args.report:
        print("\n--report：未改写任何文件", flush=True)
        return

    if not p.with_suffix(p.suffix + ".bak").exists():
        shutil.copy(p, p.with_suffix(p.suffix + ".bak"))
        print(f"\n已备份 -> {p.name}.bak", flush=True)

    sub = np.frombuffer(bytes(buf[cell_off:need]), np.uint8).reshape(cx, cy, 7).copy()
    ei, ej = np.where(edge)
    sub[ei, ej, 0] = TERRAIN_NONE
    sub[ei, ej, 5] = 0
    sub[ei, ej, 6] = 0
    if not args.keep_state:
        sub[ei, ej, 1:5] = 0
    buf[cell_off:need] = sub.tobytes()
    p.write_bytes(bytes(buf))
    print(f"已写回 {p}（{len(buf) / 1048576:.1f} MB）", flush=True)

    # 复核
    chk = np.frombuffer(bytes(buf[cell_off:need]), np.uint8).reshape(cx, cy, 7)
    ctt = chk[..., 0]
    caid = chk[..., 5].astype(np.int32) | (chk[..., 6].astype(np.int32) << 8)
    cst = (chk[..., 1].astype(np.uint32) | (chk[..., 2].astype(np.uint32) << 8)
           | (chk[..., 3].astype(np.uint32) << 16) | (chk[..., 4].astype(np.uint32) << 24))
    bad = int(((ctt[edge] != 0) | (caid[edge] != 0)).sum())
    print(f"复核: 外圈未清零的 {bad} 格；外圈 terrainState 非 0 的 {int((cst[edge] != 0).sum())} 格；"
          f"文件总长 {len(buf)}（未变）", flush=True)
    print(f"  内圈 terrainType 分布: "
          f"{dict(zip(*np.unique(ctt[~edge], return_counts=True)))}", flush=True)


if __name__ == "__main__":
    main()
