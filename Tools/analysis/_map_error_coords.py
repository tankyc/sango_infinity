"""临时脚本：把截图里报错的坐标逐条翻译成「地图 areaId -> 剧本城池」。"""
import json
import struct
from pathlib import Path

import numpy as np

BIN = Path(r"D:\sango_infinity\Build\Mods\m6dwbcwk\Map\xworld_full.bin")
SCEN = Path(r"D:\sango_infinity\Build\Mods\m6dwbcwk\Scenario\Scenario_world_full.json")

COORDS = [(593, 510), (599, 515), (490, 484), (854, 515), (851, 512), (367, 392),
          (860, 515), (342, 347), (366, 313), (413, 324), (836, 465), (835, 469),
          (414, 215), (707, 522), (505, 310), (188, 370), (187, 369), (503, 369),
          (455, 487), (522, 365), (361, 301)]


def read_str(f):
    n, sh = 0, 0
    while True:
        v = f.read(1)[0]
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    return f.read(n).decode("utf-8", "replace")


def main():
    with open(BIN, "rb") as f:
        f.read(4)
        read_str(f)
        w, h = struct.unpack("<ii", f.read(8))
        f.read(4)
        cx, cy = w // 4, h // 4
        raw = f.read(cx * cy * 7)
    cell = np.frombuffer(raw, dtype=np.uint8).reshape(cx, cy, 7)
    area = cell[..., 5:7].copy().view(np.uint16).reshape(cx, cy).astype(np.int32)
    cs = json.loads(SCEN.read_text(encoding="utf-8"))["citySet"]
    for x, y in COORDS:
        i = int(area[x, y])
        c = cs.get(str(i), {})
        print(f"({x},{y}) areaId={i} 城={c.get('Name')} 势力={c.get('BelongForce')} "
              f"城坐标=({c.get('x')},{c.get('y')})")


if __name__ == "__main__":
    main()
