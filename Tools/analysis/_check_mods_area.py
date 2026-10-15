"""临时排查脚本：遍历 Build/Mods 下每个 mod 的 Map/*.bin 与 Scenario/*.json，
交叉核对单元格 areaId 是否都能在对应剧本的 citySet 里查到城池。
"""
import json
import struct
from pathlib import Path

import numpy as np

MODS = Path(r"D:\sango_infinity\Build\Mods")


def read_str(f):
    n, sh = 0, 0
    while True:
        b = f.read(1)
        if not b:
            return None
        v = b[0]
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    return f.read(n).decode("utf-8", "replace")


def head(bin_path):
    with open(bin_path, "rb") as f:
        ver = struct.unpack("<i", f.read(4))[0]
        work = read_str(f)
        w, h = struct.unpack("<ii", f.read(8))
        gs = struct.unpack("<i", f.read(4))[0]
        cx, cy = w // 4, h // 4
        raw = f.read(cx * cy * 7)
    cell = np.frombuffer(raw, dtype=np.uint8).reshape(cx, cy, 7)
    return ver, work, cx, cy, gs, cell


def main():
    for mod in sorted(p for p in MODS.iterdir() if p.is_dir()):
        bins = list((mod / "Map").glob("*.bin")) if (mod / "Map").exists() else []
        jss = list((mod / "Scenario").glob("*.json")) if (mod / "Scenario").exists() else []
        if not bins:
            continue
        print(f"=== {mod.name} ===")
        for js in jss:
            sd = json.loads(js.read_text(encoding="utf-8"))
            cities = {int(k) for k in sd.get("citySet", {}) if int(k) > 0}
            info = sd.get("Info", {})
            print(f"  {js.name}: 城池 {len(cities)}  Info.mapType={info.get('mapType')} "
                  f"地图名={info.get('name')}")
        for b in bins:
            ver, work, cx, cy, gs, cell = head(b)
            area = cell[..., 5:7].copy().view(np.uint16).reshape(cx, cy).astype(np.int32)
            terrain = cell[..., 0].astype(np.int32)
            state = cell[..., 1:5].copy().view(np.int32).reshape(cx, cy)
            used = np.unique(area)
            print(f"  {b.name}: ver={ver} work={work} 格 {cx}x{cy} areaId 0~{used.max()} "
                  f"（{len(used)} 种）areaId=0 {int((area == 0).sum())} 格")
            for js in jss:
                sd = json.loads(js.read_text(encoding="utf-8"))
                cities = {int(k) for k in sd.get("citySet", {}) if int(k) > 0}
                bad = sorted(int(i) for i in used if i != 0 and i not in cities)
                if not bad:
                    print(f"    vs {js.name}: 全部 areaId 都能查到城市 OK")
                else:
                    mask = np.isin(area, bad)
                    miss_cells = int(mask.sum())
                    print(f"    vs {js.name}: {len(bad)} 个 areaId 查不到城市 "
                          f"（{miss_cells} 格，占 {miss_cells / area.size * 100:.1f}%），"
                          f"前 20 个 {bad[:20]}")
                    ys, xs = np.where(mask)
                    print(f"      这些格坐标范围 x {xs.min()}~{xs.max()} y {ys.min()}~{ys.max()}")
                    # 打印前 15 个坐标
                    pts = list(zip(xs.tolist(), ys.tolist()))[:15]
                    print(f"      样例坐标 {pts}")


if __name__ == "__main__":
    main()
