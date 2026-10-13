# -*- coding: utf-8 -*-
"""校验缩放后的 .bin 与剧本：结构可解析、城市格坐标与地形/归属一一对应"""

import json
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from sango_scale_bin import parse_bin  # noqa

SRC_BIN = r'C:\Users\Administrator\Downloads\moonlightstarsky (1)\moonlightstarsky\Map\yyxk_2x.bin'
DST_BIN = r'C:\Users\Administrator\Downloads\moonlightstarsky (1)\moonlightstarsky\Map\yyxk_2x_750.bin'
SRC_SC = r'C:\Users\Administrator\Downloads\moonlightstarsky (1)\moonlightstarsky\Scenario\Scenario1.json'
DST_SC = r'C:\Users\Administrator\Downloads\moonlightstarsky (1)\moonlightstarsky\Scenario\Scenario1_750.json'

import numpy as np

a = parse_bin(SRC_BIN)
b = parse_bin(DST_BIN)
print('源 : %dpx %dx%d 格, 顶点 %s' % (a['mapWidth'], a['cellsX'], a['cellsY'], a['verts'].shape))
print('目标: %dpx %dx%d 格, 顶点 %s' % (b['mapWidth'], b['cellsX'], b['cellsY'], b['verts'].shape))

assert b['gridSize'] == 20 and b['quadSize'] == 5
assert b['workContent'] == a['workContent']
assert b['cellsX'] * 4 == b['mapWidth'] and b['cellsY'] * 4 == b['mapHeight']

# 尾部段逐字节比对（layer/terrain/light/fog/skyBox/camera 必须原样）
for key in ('layer', 'terrain', 'light', 'fog', 'skyBox', 'camera'):
    s, e = a['tail'][key]
    s2, e2 = b['tail'][key]
    same = a['buf'][s:e] == b['buf'][s2:e2]
    print('尾部段 %-10s 源 %6d 字节 / 目标 %6d 字节  一致=%s' % (key, e - s, e2 - s2, same))
    assert same
print('模型: 源 %d / 目标 %d' % (len(a['tail']['models'][2]), len(b['tail']['models'][2])))
print('标注: 源 %d / 目标 %d' % (len(a['tail']['labels'][2]), len(b['tail']['labels'][2])))

# 城市坐标一致性
sc_a = json.load(open(SRC_SC, encoding='utf-8'))
sc_b = json.load(open(DST_SC, encoding='utf-8'))
g_a = a['gridRaw']
g_b = b['gridRaw']
aid_a = g_a[:, :, 5].astype(np.uint16) | (g_a[:, :, 6].astype(np.uint16) << 8)
aid_b = g_b[:, :, 5].astype(np.uint16) | (g_b[:, :, 6].astype(np.uint16) << 8)
t_a = g_a[:, :, 0]
t_b = g_b[:, :, 0]

bad_terrain = bad_area = oob = 0
for k in sc_a['citySet']:
    ca = sc_a['citySet'][k]
    cb = sc_b['citySet'][k]
    if 'x' not in ca:
        continue
    x0, y0 = int(ca['x']), int(ca['y'])
    x1, y1 = int(cb['x']), int(cb['y'])
    if not (0 <= x1 < b['cellsX'] and 0 <= y1 < b['cellsY']):
        oob += 1
        continue
    if t_a[x0, y0] != t_b[x1, y1]:
        bad_terrain += 1
    if aid_a[x0, y0] != aid_b[x1, y1]:
        bad_area += 1

print('城市数 %d；越界 %d；地形不一致 %d；归属(areaId)不一致 %d' %
      (len(sc_a['citySet']), oob, bad_terrain, bad_area))

# 地形为都市(17)的城市
urban = [k for k in sc_a['citySet'] if 'x' in sc_a['citySet'][k]
         and t_b[int(sc_b['citySet'][k]['x']), int(sc_b['citySet'][k]['y'])] == 17]
print('缩落后中心格地形=都市(17) 的城市数: %d' % len(urban))

print('OK: 结构与坐标校验完成')
