# -*- coding: utf-8 -*-
"""一次性：把世界图(cities.json)的 500 座城匹配到候选城表的真实经纬度，拟合 格坐标 -> 经纬度。

候选表格式（docs/地图与城市.md）：
    ["倫丁", "倫丁", "不列顛", 51.51, -0.1, 2]
     显示名  全名    地区     纬度   经度  重要程度
"""
import json
import re
from pathlib import Path

import numpy as np

ROOT = Path(".")
WORLD = ROOT / "data/sango_full/cities.json"


def load_candidates():
    """显示名 -> (lat, lon, region, full)"""
    out = {}
    for fn in ("data/cities_west.json", "data/cities_more.json", "data/new_cities.json"):
        p = ROOT / fn
        if not p.exists():
            print(f"  [跳过] {fn} 不存在")
            continue
        raw = p.read_text(encoding="utf-8")
        rows = json.loads(raw)
        print(f"  {fn}: {len(rows)} 条")
        for r in rows:
            try:
                name, full, region, lat, lon = r[0], r[1], r[2], float(r[3]), float(r[4])
            except Exception:
                continue
            # 同名多城时保留 |lat| 较大的？不猜，全部存下来由匹配阶段裁决
            out.setdefault(name, []).append((lat, lon, region, full))
    return out


def main():
    print("=== 候选城表 ===")
    cand = load_candidates()
    print(f"  合计不同显示名 {len(cand)}")

    georef = ROOT / "data/cities_georef.json"
    if georef.exists():
        print(f"  cities_georef.json: {georef.stat().st_size}B")
        print("   ", georef.read_text(encoding="utf-8")[:300].replace("\n", " "))

    cities = json.loads(WORLD.read_text(encoding="utf-8"))
    print(f"\n=== 世界图城市 {len(cities)} 座 ===")

    matched = []
    miss = []
    for c in cities:
        nm = c["name"]
        if nm in cand:
            # 同名候选取第一个（候选表内同名极少）
            lat, lon, region, full = cand[nm][0]
            matched.append((nm, int(c["x"]), int(c["y"]), lat, lon, region))
        else:
            miss.append(nm)
    print(f"  匹配到经纬度: {len(matched)} / {len(cities)}    未匹配 {len(miss)}")
    if miss:
        print(f"  未匹配样例: {miss[:25]}")

    if len(matched) < 30:
        print("\n[中止] 匹配太少，无法拟合")
        return

    x = np.array([m[1] for m in matched], float)
    y = np.array([m[2] for m in matched], float)
    lat = np.array([m[3] for m in matched], float)
    lon = np.array([m[4] for m in matched], float)

    def fit(target, label):
        A = np.stack([y, x, np.ones_like(y)], axis=1)
        coef, *_ = np.linalg.lstsq(A, target, rcond=None)
        pred = A @ coef
        res = target - pred
        ss = 1 - (res ** 2).sum() / ((target - target.mean()) ** 2).sum()
        print(f"  {label} = {coef[0]:+.6f}*y {coef[1]:+.6f}*x {coef[2]:+.4f}"
              f"    R2={ss:.4f}  |err| p50={np.percentile(np.abs(res),50):.2f}"
              f" p90={np.percentile(np.abs(res),90):.2f} max={np.abs(res).max():.2f}")
        return coef, np.abs(res)

    print("\n=== 拟合（线性，含 x 交叉项）===")
    clat, elat = fit(lat, "lat")
    clon, elon = fit(lon, "lon")

    print("\n=== 只用 y 拟合纬度（配置里用这个更简单）===")
    Ay = np.stack([y, np.ones_like(y)], axis=1)
    cy, *_ = np.linalg.lstsq(Ay, lat, rcond=None)
    pred = Ay @ cy
    res = lat - pred
    ss = 1 - (res ** 2).sum() / ((lat - lat.mean()) ** 2).sum()
    print(f"  lat = {cy[0]:+.6f}*y {cy[1]:+.4f}   R2={ss:.4f}"
          f"  |err| p50={np.percentile(np.abs(res),50):.2f}"
          f" p90={np.percentile(np.abs(res),90):.2f} max={np.abs(res).max():.2f}")

    print("\n=== 抽样核对（真实纬度 vs 拟合纬度）===")
    order = np.argsort(lat)
    for i in list(order[:6]) + list(order[len(order)//2-3:len(order)//2+3]) + list(order[-6:]):
        nm, xx, yy, la, lo, rg = matched[i]
        print(f"  {nm:<4} 格({xx:>4},{yy:>3}) 真实 lat={la:+6.2f} lon={lo:+7.2f} "
              f"| y拟合 lat={cy[0]*yy+cy[1]:+6.2f} 差={cy[0]*yy+cy[1]-la:+5.2f}  [{rg}]")

    # 全图纬度范围
    CX, CY = 1800, 1000
    print(f"\n=== 全图纬度范围（y=0..{CY-1}）===")
    for yy in (0, 100, 200, 300, 400, 500, 600, 700, 800, 900, 999):
        print(f"  y={yy:>3}  lat={cy[0]*yy+cy[1]:+7.2f}")

    # 落盘：城市 -> 纬度 供植被配置使用
    out = [{"name": m[0], "x": m[1], "y": m[2], "lat": m[3], "lon": m[4], "region": m[5]}
           for m in matched]
    Path("_city_geo.json").write_text(json.dumps(out, ensure_ascii=False, indent=1),
                                      encoding="utf-8")
    print(f"\n已写出 _city_geo.json（{len(out)} 座）")


if __name__ == "__main__":
    main()
