# -*- coding: utf-8 -*-
"""清点：1) 各处 BaseMap/预览图副本（谁在读哪一份）2) 找「河流/海洋布局」相关的图是否还在。"""
import os
import re
from pathlib import Path

ROOTS = [r"D:\sango_infinity", r"D:\世界地图",
         r"C:\Users\Administrator\Downloads", r"C:\Users\Administrator\Desktop"]
KEY = re.compile(r"(river|ocean|sea|water|河|海|洋|美术|art|aigen|gen|ai[-_]|koei|羊皮|basemap|map_bounds|preview)",
                 re.I)
EXTS = {".png", ".jpg", ".jpeg", ".webp", ".psd", ".tga", ".bmp", ".exr"}

hits = []
basemaps = []
for root in ROOTS:
    if not os.path.isdir(root):
        continue
    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames
                       if d not in (".git", "Library", "obj", "node_modules", "Temp", "Logs")
                       or dirpath.count(os.sep) < 3]
        for fn in filenames:
            ext = os.path.splitext(fn)[1].lower()
            if ext not in EXTS:
                continue
            p = os.path.join(dirpath, fn)
            try:
                st = os.stat(p)
            except OSError:
                continue
            rec = (st.st_mtime, st.st_size, p)
            if fn.lower().startswith("basemap"):
                basemaps.append(rec)
            if KEY.search(fn):
                hits.append(rec)

print("=== 所有 BaseMap* 副本（按修改时间倒序）===")
for t, s, p in sorted(basemaps, reverse=True)[:40]:
    import datetime
    print(f"  {datetime.datetime.fromtimestamp(t):%m-%d %H:%M}  {s/1024:9.0f} KB  {p}")

print(f"\n=== 文件名含 河流/海洋/美术/AI/预览 等关键词的图（{len(hits)} 个，按时间倒序前 45）===")
for t, s, p in sorted(hits, reverse=True)[:45]:
    import datetime
    print(f"  {datetime.datetime.fromtimestamp(t):%m-%d %H:%M}  {s/1024:9.0f} KB  {p}")
