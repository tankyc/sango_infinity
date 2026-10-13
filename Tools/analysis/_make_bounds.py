# -*- coding: utf-8 -*-
"""一次性：为 yyxk 生成 map_bounds.jpg。

依据：基础内容里并没有 map_bounds.jpg（是可选的、由地图 mod 提供），
世界图的先例是 2048x1136（与其 1800x1000 格的地图同比例）。
yyxk 的 BaseTex/BaseMap0.png 正好是 2048x2048 = 512 格 x 4 像素（整数倍对齐），
它本身就是引擎实际渲染出的底图，所以直接用它导出 JPEG 最忠实、不会与游戏画面不一致。
"""
from pathlib import Path

from PIL import Image

MOD = Path(r"D:\sango_infinity\Build\Mods\nftfwxpx")
SRC = MOD / "Assets/Map/yyxk/BaseTex/BaseMap0.png"
DST = MOD / "Assets/Map/yyxk/map_bounds.jpg"

im = Image.open(SRC)
print(f"源: {SRC.name}  {im.size[0]}x{im.size[1]}  {im.mode}")
out = im.convert("RGB")
out.save(DST, "JPEG", quality=92, optimize=True, subsampling=1)
print(f"已写出 {DST}  {DST.stat().st_size / 1024:.1f} KB")
print(f"尺寸 {Image.open(DST).size[0]}x{Image.open(DST).size[1]}"
      f"  = {Image.open(DST).size[0] / 512:.1f} 像素/格")
