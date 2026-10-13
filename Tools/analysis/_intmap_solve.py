"""求 DefaultMap(中国区) 的格子坐标 -> 世界图格子坐标 的映射。

链路（已用襄平验证）：
  world_cell = floor((local + china_lo + 0.5) / DS)
所以只要求出 DefaultMap 格 -> 中国块局部坐标 local 的仿射（纯平移 ± y 翻转）即可。
标定物：DefaultMap 里 42 个「都市(17) 七格块」中心  <->  cities_georef.json 的 42 座三国城 (lo,hi)。
"""
import json
import struct
from collections import deque
from pathlib import Path

import numpy as np

DEF = Path(r"d:/sango_infinity/Build/Content/Map/DefaultMap.bin")
WORLD = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data\world")
DATA = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\data")
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


def load_def():
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


def centers_of(tt, kind):
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
                out.append((float(np.mean(xs)), float(np.mean(ys)), len(cells)))
    return out


def main():
    tt, st, gx, gy = load_def()
    city = [c for c in centers_of(tt, 17) if c[2] >= 5]
    geo = json.loads((DATA / "cities_georef.json").read_text(encoding="utf-8"))
    info = json.loads((WORLD / "world_info.json").read_text(encoding="utf-8"))
    clo, chi, ds = info["china_lo"], info["china_hi"], 1
    print(f"DefaultMap {gx}x{gy} 都市块 {len(city)}；georef {len(geo)} 座；china=({clo},{chi}) DS={ds}")

    A = np.array([[c[0], c[1]] for c in city])
    B = np.array([[float(g["lo"]), float(g["hi"])] for g in geo])
    print(f"  DefaultMap 中心 x {A[:,0].min():.0f}~{A[:,0].max():.0f} y {A[:,1].min():.0f}~{A[:,1].max():.0f}")
    print(f"  georef    lo {B[:,0].min():.0f}~{B[:,0].max():.0f} hi {B[:,1].min():.0f}~{B[:,1].max():.0f}")

    best = None
    for flip in (False, True):
        Ay = (gy - 1 - A[:, 1]) if flip else A[:, 1]
        a = np.stack([A[:, 0], Ay], 1)
        c0 = B.mean(0) - a.mean(0)
        ox0, oy0 = int(round(c0[0])), int(round(c0[1]))
        for ox in range(ox0 - 6, ox0 + 7):
            for oy in range(oy0 - 6, oy0 + 7):
                P = np.stack([A[:, 0] + ox, Ay + oy], 1)
                d = np.sqrt(((P[:, None, :] - B[None, :, :]) ** 2).sum(-1))
                err = float(d.min(1).mean())
                if best is None or err < best[0]:
                    best = (err, flip, ox, oy, float(d.min(1).max()))
    err, flip, ox, oy, worst = best
    print(f"\n最佳: flip={flip} offset=({ox},{oy}) 平均误差={err:.2f} 格, 最大={worst:.2f} 格")
    Ay = (gy - 1 - A[:, 1]) if flip else A[:, 1]
    P = np.stack([A[:, 0] + ox, Ay + oy], 1)
    d = np.sqrt(((P[:, None, :] - B[None, :, :]) ** 2).sum(-1))
    j = d.argmin(1)
    print("  配对抽样(name, DefaultMap中心 -> 映射后local, georef local, 误差):")
    for i in list(range(4)) + list(range(len(city) - 3, len(city))):
        print(f"    {geo[int(j[i])]['name']:<6} ({A[i,0]:.0f},{A[i,1]:.0f}) -> "
              f"({P[i,0]:.0f},{P[i,1]:.0f}) vs ({B[int(j[i]),0]:.0f},{B[int(j[i]),1]:.0f})  {d[i].min():.1f}")

    if err < 3.0:
        # 用最佳映射把内政格转到世界图
        it = np.argwhere((st & 4) != 0)
        wy = (gy - 1 - it[:, 1]) if flip else it[:, 1]
        lx = it[:, 0] + ox
        ly = wy + oy
        wx = np.floor((lx + clo + 0.5) / ds).astype(np.int32)
        wy2 = np.floor((ly + chi + 0.5) / ds).astype(np.int32)
        print(f"\n内政格 {len(it)} 个 -> 世界图坐标: x {wx.min()}~{wx.max()} y {wy2.min()}~{wy2.max()}")
        np.save("_intmap_world.npy", np.stack([wx, wy2], 1))
        print("  已存 _intmap_world.npy")


if __name__ == "__main__":
    main()
