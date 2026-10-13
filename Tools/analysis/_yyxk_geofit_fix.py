# -*- coding: utf-8 -*-
"""一次性：从 yyxk_geofit.json 的 points 重新最小二乘拟合 格(x,y) -> 经纬度。

旧文件里的 lon 系数是错的（辽东郡(450,57) 应 123.17E，旧式给 103.5E）。
这里只做正经的二元线性回归，并打印残差以验证。
"""
import json
from pathlib import Path

import numpy as np

p = Path(r"D:\sango_infinity\Temp\yyxk_geofit.json")
g = json.loads(p.read_text(encoding="utf-8"))
pts = g["points"]
print(f"参与拟合的城 {len(pts)} 个")
X = np.array([[q["x"], q["y"], 1.0] for q in pts], np.float64)
lat = np.array([q["lat"] for q in pts], np.float64)
lon = np.array([q["lon"] for q in pts], np.float64)


def fit(v, name):
    coef, *_ = np.linalg.lstsq(X, v, rcond=None)
    pred = X @ coef
    res = pred - v
    ss = 1 - (res ** 2).sum() / ((v - v.mean()) ** 2).sum()
    print(f"\n{name}: x={coef[0]:+.6f}  y={coef[1]:+.6f}  b={coef[2]:+.4f}")
    print(f"  R2={ss:.4f}  残差 中位|.|={np.median(np.abs(res)):.3f}  p90={np.percentile(np.abs(res), 90):.3f}")
    return coef


clat = fit(lat, "lat")
clon = fit(lon, "lon")

print("\n抽查（真实 -> 拟合）:")
for q in pts[:6]:
    la = clat[0] * q["x"] + clat[1] * q["y"] + clat[2]
    lo = clon[0] * q["x"] + clon[1] * q["y"] + clon[2]
    print(f"  {q['name']:<8} 格({q['x']:>3},{q['y']:>3}) 真实 {q['lat']:6.2f},{q['lon']:7.2f}"
          f"  ->  拟合 {la:6.2f},{lo:7.2f}")

print(f"\n地图四角经纬:")
for x, y in ((0, 0), (511, 0), (0, 511), (511, 511)):
    la = clat[0] * x + clat[1] * y + clat[2]
    lo = clon[0] * x + clon[1] * y + clon[2]
    print(f"  格({x:>3},{y:>3}) -> {la:6.2f}N {lo:7.2f}E")

out = {"lat": {"x": float(clat[0]), "y": float(clat[1]), "b": float(clat[2])},
       "lon": {"x": float(clon[0]), "y": float(clon[1]), "b": float(clon[2])},
       "n": len(pts)}
Path(r"D:\sango_infinity\Temp\yyxk_geofit_fixed.json").write_text(
    json.dumps(out, ensure_ascii=False, indent=1), encoding="utf-8")
print(f"\n已写出 Temp/yyxk_geofit_fixed.json")
print(f"  --lat \"{clat[0]:.6f},{clat[1]:.6f},{clat[2]:.4f}\"  "
      f"--lon \"{clon[0]:.6f},{clon[1]:.6f},{clon[2]:.4f}\"")
