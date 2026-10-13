# -*- coding: utf-8 -*-
"""一次性：用郡名对应的真实郡治经纬度，拟合 yyxk_2x 地图的「格坐标 -> 经纬度」投影。

有了这个标定，才能：
  · 用真实高程/地表数据做地格属性的对照（item 1「真实数据对照」）
  · 按三国行政区划（州/区域）给植被分带（item 1 A 方案）
  · 给山体加细节时知道真实山脉走向（item 2）

投影假设（汉地地图通常为正北朝上的等距/等经纬投影）：
    lat = a*y + b*y2?  -> 先用 lat = a*y + c*x + b 线性拟合，看 R2 与残差
"""
import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(r"D:\sango_infinity\Tools")))

import numpy as np

SC = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx\Scenario\Scenario1.json")

# 郡治真实经纬度（我按汉末三国行政区划的确切位置整理，用于标定）
REF = {
    "辽东郡": (41.27, 123.17), "右北平郡": (39.90, 118.20), "广阳郡": (39.90, 116.40),
    "渔阳郡": (40.38, 116.85), "渤海郡": (38.30, 116.85), "平原郡": (37.44, 116.35),
    "清河郡": (37.05, 115.66), "太原郡": (37.87, 112.55), "上党郡": (36.20, 113.12),
    "魏郡": (36.33, 114.60), "河内郡": (35.22, 113.24), "河南郡": (34.62, 112.45),
    "弘农郡": (34.52, 110.87), "京兆郡": (34.27, 108.94), "陇西郡": (34.58, 105.72),
    "武威郡": (37.93, 102.64), "张掖郡": (38.93, 100.45), "酒泉郡": (39.74, 98.51),
    "敦煌郡": (40.14, 94.66), "金城郡": (36.06, 103.83), "安定郡": (35.50, 106.50),
    "天水郡": (34.58, 105.72), "颍川郡": (34.03, 113.82), "汝南郡": (33.00, 114.36),
    "南阳郡": (33.00, 112.53), "陈留郡": (34.80, 114.31), "东郡": (35.76, 115.03),
    "济阴郡": (35.30, 115.70), "山阳郡": (35.10, 116.40), "北海郡": (36.71, 119.11),
    "东莱郡": (37.46, 121.44), "琅琊郡": (35.10, 118.35), "东海郡": (34.60, 118.35),
    "下邳郡": (34.31, 117.94), "彭城郡": (34.26, 117.19), "九江郡": (32.58, 116.78),
    "庐江郡": (31.25, 117.29), "广陵郡": (32.39, 119.42), "丹阳郡": (30.95, 118.75),
    "吴郡": (31.30, 120.62), "会稽郡": (30.00, 120.58), "豫章郡": (28.68, 115.89),
    "江夏郡": (30.59, 114.31), "南郡": (30.35, 112.19), "长沙郡": (28.23, 112.94),
    "桂阳郡": (25.80, 113.03), "零陵郡": (26.42, 111.61), "武陵郡": (29.03, 111.70),
    "汉中郡": (33.08, 107.03), "巴郡": (29.56, 106.55), "蜀郡": (30.66, 104.07),
    "犍为郡": (28.77, 104.63), "益州郡": (25.04, 102.71), "永昌郡": (25.11, 99.17),
    "越嶲郡": (27.89, 102.26), "牂牁郡": (26.58, 106.71), "苍梧郡": (23.48, 111.28),
    "南海郡": (23.13, 113.26), "交趾郡": (21.03, 105.85), "九真郡": (19.80, 105.80),
}


def norm(s):
    s = str(s).strip()
    for suf in ("郡", "国", "关", "港", "城", "县"):
        if s.endswith(suf):
            return s[: -len(suf)]
    return s


sc = json.loads(SC.read_text(encoding="utf-8"))
cities = sc["citySet"]
print(f"据点 {len(cities)} 个")

by_name = {}
for v in cities.values():
    if "x" in v:
        by_name.setdefault(norm(v.get("Name", "")), []).append(v)

pts = []
miss = []
for k, (la, lo) in REF.items():
    n = norm(k)
    if n in by_name:
        for v in by_name[n]:
            pts.append((v.get("Name"), int(v["x"]), int(v["y"]), la, lo))
    else:
        miss.append(k)

print(f"标定点匹配 {len(pts)} 个；未匹配 {len(miss)}: {miss}")
if len(pts) < 12:
    print("[中止] 标定点太少")
    raise SystemExit

x = np.array([p[1] for p in pts], float)
y = np.array([p[2] for p in pts], float)
lat = np.array([p[3] for p in pts], float)
lon = np.array([p[4] for p in pts], float)

print("\n=== 标定点（前 12）===")
for p in pts[:12]:
    print(f"  {p[0]:<8} 格({p[1]:>3},{p[2]:>3})  真实 {p[3]:+7.2f},{p[4]:+8.2f}")


def fit(t, label, terms):
    cols = []
    names = []
    if "y" in terms:
        cols.append(y); names.append("y")
    if "x" in terms:
        cols.append(x); names.append("x")
    cols.append(np.ones_like(y)); names.append("1")
    A = np.stack(cols, axis=1)
    coef, *_ = np.linalg.lstsq(A, t, rcond=None)
    pred = A @ coef
    res = t - pred
    r2 = 1 - (res ** 2).sum() / ((t - t.mean()) ** 2).sum()
    expr = " ".join(f"{c:+.6f}*{n}" if n != "1" else f"{c:+.4f}" for c, n in zip(coef, names))
    print(f"  {label} = {expr}    R2={r2:.4f}  |err| p50={np.percentile(np.abs(res),50):.2f}"
          f" p90={np.percentile(np.abs(res),90):.2f} max={np.abs(res).max():.2f}")
    return coef, res, r2


print("\n=== 拟合 ===")
clat2, elat2, r2lat2 = fit(lat, "lat", "yx")
clon2, elon2, r2lon2 = fit(lon, "lon", "xy")
clat_y, elat_y, r2lat_y = fit(lat, "lat", "y")
clon_x, elon_x, r2lon_x = fit(lon, "lon", "x")

print("\n=== 只用单轴（配置里最简单）===")
print(f"  lat = {clat_y[0]:+.6f}*y {clat_y[1]:+.4f}   R2={r2lat_y:.4f}")
print(f"  lon = {clon_x[0]:+.6f}*x {clon_x[1]:+.4f}   R2={r2lon_x:.4f}")

print("\n=== 反推全图地理范围 ===")
print(f"  y=0   -> lat {clat_y[1]:+.2f}      y=511 -> lat {clat_y[0]*511+clat_y[1]:+.2f}")
print(f"  x=0   -> lon {clon_x[1]:+.2f}      x=511 -> lon {clon_x[0]*511+clon_x[1]:+.2f}")

print("\n=== 残差最大的点（检查是否有标定错误）===")
r = np.abs(elat2)
order = np.argsort(-r)
for i in order[:8]:
    print(f"  {pts[i][0]:<8} 格({pts[i][1]:>3},{pts[i][2]:>3}) 真实 lat {lat[i]:+6.2f}"
          f" -> 拟合 {(clat2[0]*y[i]+clat2[1]*x[i]+clat2[2]):+6.2f}  差 {elat2[i]:+5.2f}")

out = {
    "lat": {"y": float(clat2[0]), "x": float(clat2[1]), "b": float(clat2[2]), "r2": float(r2lat2)},
    "lon": {"x": float(clon2[0]), "y": float(clon2[1]), "b": float(clon2[2]), "r2": float(r2lon2)},
    "lat_simple": {"y": float(clat_y[0]), "b": float(clat_y[1]), "r2": float(r2lat_y)},
    "lon_simple": {"x": float(clon_x[0]), "b": float(clon_x[1]), "r2": float(r2lon_x)},
    "points": [{"name": p[0], "x": p[1], "y": p[2], "lat": p[3], "lon": p[4]} for p in pts],
    "unmatched_ref": miss,
}
Path(r"D:\sango_infinity\Temp\yyxk_geofit.json").write_text(
    json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
print("\n已写出 Temp\\yyxk_geofit.json")

# 顺带全量清单，供建州/区域对照表
names = sorted(by_name)
Path(r"D:\sango_infinity\Temp\yyxk_names.txt").write_text(
    "\n".join(f"{n}\t{min(v['x'] for v in by_name[n])},{min(v['y'] for v in by_name[n])}"
              for n in names), encoding="utf-8")
print(f"已写出 Temp\\yyxk_names.txt（{len(names)} 个不同名称）")
