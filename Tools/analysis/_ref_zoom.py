"""把参考图的几个关键区域放大拼成一张，便于看清风格细节。"""
from pathlib import Path

import numpy as np
from PIL import Image

SRC = Path(r"D:\sango_infinity\Project\Assets\Mods\Content\Assets\UI\AtlasTexture\4855-1.png")
OUT = Path(r"C:\Users\Administrator\Downloads\san11-world-main\san11-world-main\_ref_zoom.png")

im = Image.open(SRC).convert("RGB")
W, H = im.size
a = np.asarray(im).astype(np.float64)
print("size", im.size)

# 逐行/逐列看边框在哪结束（与内部纸面的差异）
row = a[:, W // 2]
col = a[H // 2, :]
print("顶部 0..40 行 mean:", [tuple(int(x) for x in row[i]) for i in range(0, 40, 4)])
print("左部 0..40 列 mean:", [tuple(int(x) for x in col[i]) for i in range(0, 40, 4)])
print("亮度沿行(中列)前 48:", [int(row[i].mean()) for i in range(0, 48, 3)])
print("亮度沿列(中行)前 48:", [int(col[i].mean()) for i in range(0, 48, 3)])

# 采样几个区域做统计
regions = {
    "纸面(中上)": (100, 180, 150, 230, 110, 190),
    "海(右上)": (60, 120, 330, 390, 60, 120),
    "海(右下)": (330, 390, 330, 390, 60, 120),
    "山地(上中)": (20, 70, 180, 260, 60, 120),
    "山地(左下)": (380, 440, 60, 140, 60, 120),
}
for name, (y0, y1, x0, x1, _, _) in regions.items():
    r = a[y0:y1, x0:x1].reshape(-1, 3)
    print(f"{name}: mean={r.mean(0).round(1)} std={r.std(0).round(1)} p5={np.percentile(r,5,axis=0).round(0)} p95={np.percentile(r,95,axis=0).round(0)}")

# 拼图：左上角边框 / 海纹样 / 纸面 / 山地，各放大到 300x300
def crop(y0, y1, x0, x1, size=300):
    c = im.crop((x0, y0, x1, y1))
    return c.resize((size, size), Image.NEAREST)

tiles = [
    ("border TL", crop(0, 90, 0, 90)),
    ("sea", crop(300, 390, 330, 420)),
    ("paper", crop(150, 240, 180, 270)),
    ("mountain", crop(20, 110, 180, 270)),
    ("coast", crop(180, 270, 300, 390)),
    ("border BR", crop(H - 90, H, W - 90, W)),
]
sheet = Image.new("RGB", (300 * 3 + 40, 300 * 2 + 30), (30, 30, 30))
for i, (_, t) in enumerate(tiles):
    sheet.paste(t, (10 + (i % 3) * (300 + 10), 10 + (i // 3) * (300 + 10)))
sheet.save(OUT)
print("拼图写", OUT, sheet.size)
print("顺序: 上排 边框左上 / 海 / 纸面   下排 山地 / 海岸 / 边框右下")
