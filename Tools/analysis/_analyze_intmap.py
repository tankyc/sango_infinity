"""临时分析：求 DefaultMap(中国区) -> 世界图 的格子坐标映射。

做法：DefaultMap 里 42 个「都市(17) 七格块」的中心 ↔ 世界图 rank=1（三国）城市的坐标，
两套点集做平移配准（含 y 翻转候选），取误差最小的偏移。
"""
import json
import struct
from collections import deque
from pathlib import Path

import numpy as np

DEF = Path(r"d:/sango_infinity/Build/Content/Map/DefaultMap.bin")
D = Path("data/sango_full")
SRC = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data") / "src"
NEIGH = {0: [(1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (0, 1)],
         1: [(1, 1), (1, 0), (0, -1), (-1, 0), (-1, 1), (0, 1)]}


def read_str(f):
    n, sh = 0, 0
    while True:
        v = f.read(1)[0]
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    return f.read(n).decode("utf-8", "replace")


def load_def_grid():
    with open(DEF, "rb") as f:
        assert struct.unpack("<i", f.read(4))[0] == 10
        read_str(f)
        w, h = struct.unpack("<ii", f.read(8))
        gs = struct.unpack("<i", f.read(4))[0]
        gx, gy = w * 5 // gs, h * 5 // gs
        b = np.frombuffer(f.read(gx * gy * 7), np.uint8).reshape(gx, gy, 7)
    tt = b[..., 0].astype(np.int32)
    st = (b[..., 1].astype(np.uint32) | (b[..., 2].astype(np.uint32) << 8)
          | (b[..., 3].astype(np.uint32) << 16) | (b[..., 4].astype(np.uint32) << 24))
    return tt, st, gx, gy


def blobs(tt, kind):
    """把 terrain==kind 的格子按六边形邻接分块，返回 [(中心x, 中心y, 格数)]。"""
    gx, gy = tt.shape
    lab = np.zeros((gx, gy), np.int32)
    out = []
    n = 0
    for x0 in range(gx):
        for y0 in range(gy):
            if tt[x0, y0] == kind and lab[x0, y0] == 0:
                n += 1
                q = deque([(x0, y0)])
                lab[x0, y0] = n
                cells = []
                while q:
                    x, y = q.popleft()
                    cells.append((x, y))
                    for dx, dy in NEIGH[x & 1]:
                        a, b = x + dx, y + dy
                        if 0 <= a < gx and 0 <= b < gy and lab[a, b] == 0 and tt[a, b] == kind:
                            lab[a, b] = n
                            q.append((a, b))
                xs = [c[0] for c in cells]
                ys = [c[1] for c in cells]
                out.append((int(round(np.mean(xs))), int(round(np.mean(ys))), len(cells)))
    return out


def main():
    tt, st, gx, gy = load_def_grid()
    interior = (st & 4) != 0
    city = blobs(tt, 17)
    gp = blobs(tt, 18) + blobs(tt, 19)
    print(f"DefaultMap 格 {gx}x{gy}; 都市块 {len(city)} 个(应 42); 港关块 {len(gp)} 个; 内政格 {int(interior.sum())}")

    cities = json.loads((D / "cities.json").read_text(encoding="utf-8"))
    r1 = [(c["x"], c["y"], c["name"]) for c in cities if int(c.get("rank", 3)) == 1]
    print(f"世界图 rank=1 城市 {len(r1)} 个, x {min(c[0] for c in r1)}~{max(c[0] for c in r1)},"
          f" y {min(c[1] for c in r1)}~{max(c[1] for c in r1)}")
    dxs = [c[0] for c in city]
    dys = [c[1] for c in city]
    print(f"DefaultMap 都市块中心 x {min(dxs)}~{max(dxs)}, y {min(dys)}~{max(dys)}")

    info_p = SRC / "world_info.json"
    if info_p.exists():
        info = json.loads(info_p.read_text(encoding="utf-8"))
        print("world_info:", {k: info[k] for k in info if "china" in k or "lo" in k or "hi" in k})

    A = np.array([[c[0], c[1]] for c in city], np.float64)
    B = np.array([[c[0], c[1]] for c in r1], np.float64)
    best = None
    for flip in (False, True):
        Ay = (gy - 1 - A[:, 1]) if flip else A[:, 1]
        a = np.stack([A[:, 0], Ay], 1)
        c0 = B.mean(0) - a.mean(0)          # 两套点集是同一批城，质心直接对应
        ox0, oy0 = int(round(c0[0])), int(round(c0[1]))
        for ox in range(ox0 - 8, ox0 + 9):
            for oy in range(oy0 - 8, oy0 + 9):
                P = np.stack([A[:, 0] + ox, Ay + oy], 1)
                d = np.sqrt(((P[:, None, :] - B[None, :, :]) ** 2).sum(-1))
                err = float(d.min(1).mean())
                if best is None or err < best[0]:
                    best = (err, flip, ox, oy, float(d.min(1).max()))
    err, flip, ox, oy, worst = best
    print(f"\n最佳配准: flip={flip} offset=({ox},{oy}) 平均误差={err:.2f} 格 最大={worst:.2f} 格")
    Ay = (gy - 1 - A[:, 1]) if flip else A[:, 1]
    P = np.stack([A[:, 0] + ox, Ay + oy], 1)
    d = np.sqrt(((P[:, None, :] - B[None, :, :]) ** 2).sum(-1))
    print("  前 8 对: ", [(tuple(P[i].astype(int)), tuple(B[int(d[i].argmin())].astype(int)),
                          round(float(d[i].min()), 1)) for i in range(8)])


if __name__ == "__main__":
    main()
