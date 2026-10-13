# -*- coding: utf-8 -*-
"""
sango_scale_bin.py —— 把 Sango 地图 .bin 缩放到指定的格数（支持非整数倍，最近邻重采样）

背景（见 sango-map-generation 技能 references/python-pipeline.md）：
    version 10 的 .bin 结构：
        int32   versionCode              (=10)
        string  WorkContent              (7bit 长度前缀 + UTF8)
        int32   mapWidth, mapHeight      (顶点尺寸-1；格数 = mapWidth/4，gridSize=20 时)
        mapGrid : int32 gridSize(=20) + 每格 7 字节(terrainType/terrainState(int32)/areaId(uint16))
                  + string gridTextureName
        mapData : int32 quadSize(=5) + 每顶点 3 字节(height/textureIndex/water)
                  顶点数 = (mapWidth+1)*(mapHeight+1)
        尾部段  : mapLayer / mapTerrain / mapLight / mapFog / mapBaseColor /
                  mapSkyBox / mapCamera / mapModels / mapLabelSet

    其中只有 mapModels / mapLabelSet 含坐标(世界坐标)，需要按世界尺寸比例缩放；
    其余尾部段与尺寸无关，原样照抄。

用法：
    # 只转地图
    python sango_scale_bin.py --src Map/yyxk_2x.bin --out Map/yyxk_2x_750.bin --cells 750
    # 同时把剧本里城市的格坐标一起缩放（地图改了格数，剧本城市必须同步缩放，否则错位/越界崩溃）
    python sango_scale_bin.py --src Map/yyxk_2x.bin --out Map/yyxk_2x_750.bin --cells 750 \
        --scenario Scenario/Scenario1.json --out-scenario Scenario/Scenario1_750.json
"""

import argparse
import json
import os
import struct
import sys

import numpy as np

QUAD_SIZE = 5          # 每格 5 个世界单位（MapData.quadSize）
SEASON_COUNT = 4       # 四季贴图数量（MapRender.SeasonNames.Length）
MODEL_RECORD = 52      # 单个模型记录字节数（4*int32 + 9*float）

# 六边形地格布局参数：Layout(size=(gridSize,gridSize), origin=(0,0))
#   HexToPixel: x = col*gridSize + gridSize/2 ; y = row*gridSize + (col&1)*gridSize/2 + gridSize/2
#   顶点世界坐标: position = (vy*quadSize, height*0.5, vx*quadSize)，即 worldX 对应 vy、worldZ 对应 vx
GRID_TOL = 1e-6


# --------------------------------------------------------------------------- #
# 二进制读写（对齐 Unity BinaryWriter / BinaryReader 语义）
# --------------------------------------------------------------------------- #
class Reader(object):
    def __init__(self, buf):
        self.b = buf
        self.o = 0

    def i32(self):
        v = struct.unpack_from('<i', self.b, self.o)[0]
        self.o += 4
        return v

    def i16u(self):
        v = struct.unpack_from('<H', self.b, self.o)[0]
        self.o += 2
        return v

    def f32(self):
        v = struct.unpack_from('<f', self.b, self.o)[0]
        self.o += 4
        return v

    def u8(self):
        v = self.b[self.o]
        self.o += 1
        return v

    def raw(self, n):
        v = self.b[self.o:self.o + n]
        self.o += n
        return v

    def rd7(self):
        """Unity 7bit 变长整数（字符串长度前缀）"""
        result = 0
        shift = 0
        while True:
            x = self.b[self.o]
            self.o += 1
            result |= (x & 0x7F) << shift
            if not (x & 0x80):
                break
            shift += 7
        return result

    def string(self):
        n = self.rd7()
        s = self.b[self.o:self.o + n].decode('utf-8')
        self.o += n
        return s


class Writer(object):
    def __init__(self):
        self.parts = []

    def i32(self, v):
        self.parts.append(struct.pack('<i', int(v)))

    def f32(self, v):
        self.parts.append(struct.pack('<f', float(v)))

    def u8(self, v):
        self.parts.append(struct.pack('<B', int(v) & 0xFF))

    def raw(self, b):
        self.parts.append(bytes(b))

    def string(self, s):
        data = s.encode('utf-8')
        n = len(data)
        # 7bit 变长长度前缀
        while True:
            x = n & 0x7F
            n >>= 7
            if n:
                self.parts.append(struct.pack('<B', x | 0x80))
            else:
                self.parts.append(struct.pack('<B', x))
                break
        self.parts.append(data)

    def bytes(self):
        return b''.join(self.parts)


# --------------------------------------------------------------------------- #
# 解析
# --------------------------------------------------------------------------- #
def parse_bin(path):
    """解析 .bin，返回 dict（含各段的原始字节区间）"""
    with open(path, 'rb') as f:
        buf = f.read()

    r = Reader(buf)
    ver = r.i32()
    if ver != 10:
        print('[警告] versionCode = %d，本工具按 version 10 解析' % ver)
    work_content = r.string()
    map_w = r.i32()
    map_h = r.i32()

    # ---- mapGrid ----
    grid_size = r.i32()
    cells_x = map_w * QUAD_SIZE // grid_size
    cells_y = map_h * QUAD_SIZE // grid_size
    n_cells = cells_x * cells_y
    grid_start = r.o
    grid_raw = np.frombuffer(buf[grid_start:grid_start + n_cells * 7], dtype=np.uint8)
    grid_raw = grid_raw.reshape(cells_x, cells_y, 7)
    r.o += n_cells * 7
    grid_tex_name = r.string()

    # ---- mapData ----
    quad_size = r.i32()
    vx_n = map_w + 1
    vy_n = map_h + 1
    vert_start = r.o
    verts = np.frombuffer(buf[vert_start:vert_start + vx_n * vy_n * 3], dtype=np.uint8)
    verts = verts.reshape(vx_n, vy_n, 3)
    r.o += vx_n * vy_n * 3

    # ---- 尾部段 ----
    tail = {}

    # mapLayer
    s = r.o
    layer_count = r.i32()
    for _ in range(layer_count):
        r.u8()              # isLit(bool)
        r.f32()             # textureScale.x
        r.f32()             # textureScale.y
        for _i in range(SEASON_COUNT):
            r.string()      # diffuse
            r.string()      # normal
            r.string()      # mask
    tail['layer'] = (s, r.o)

    # mapTerrain
    s = r.o
    r.i32()                 # cellSize
    tail['terrain'] = (s, r.o)

    # mapLight（4 季 × 11 float）
    s = r.o
    r.raw(SEASON_COUNT * 11 * 4)
    tail['light'] = (s, r.o)

    # mapFog（4 季 × 6 float）
    s = r.o
    r.raw(SEASON_COUNT * 6 * 4)
    tail['fog'] = (s, r.o)

    # mapBaseColor（OnSave 不写任何字节，底图以 PNG 落在 Assets 目录）
    tail['baseColor'] = (r.o, r.o)

    # mapSkyBox
    s = r.o
    r.f32()                 # sky_blend_start
    r.f32()                 # sky_blend_end
    area_count = r.i32()
    for _ in range(area_count):
        r.raw(4 * 4)        # Rect(x,y,width,height)
        for _j in range(SEASON_COUNT):
            r.string()
    tail['skyBox'] = (s, r.o)

    # mapCamera
    s = r.o
    r.f32()                 # safeBoder
    tail['camera'] = (s, r.o)

    # mapModels
    s = r.o
    model_count = r.i32()
    models = []
    for _ in range(model_count):
        obj_id = r.i32()
        obj_type = r.i32()
        bind_id = r.i32()
        model_id = r.i32()
        px, py, pz = r.f32(), r.f32(), r.f32()
        rx, ry, rz = r.f32(), r.f32(), r.f32()
        sx, sy, sz = r.f32(), r.f32(), r.f32()
        models.append((obj_id, obj_type, bind_id, model_id,
                       (px, py, pz), (rx, ry, rz), (sx, sy, sz)))
    tail['models'] = (s, r.o, models)

    # mapLabelSet
    s = r.o
    label_count = r.i32()
    labels = []
    for _ in range(label_count):
        text = r.string()
        px, py, pz = r.f32(), r.f32(), r.f32()
        cr, cg, cb = r.u8(), r.u8(), r.u8()
        font_size = r.i32()
        labels.append((text, (px, py, pz), (cr, cg, cb), font_size))
    tail['labels'] = (s, r.o, labels)

    if r.o != len(buf):
        raise ValueError('解析结束位置 %d != 文件长度 %d，格式不符' % (r.o, len(buf)))

    return {
        'buf': buf,
        'version': ver,
        'workContent': work_content,
        'mapWidth': map_w,
        'mapHeight': map_h,
        'gridSize': grid_size,
        'cellsX': cells_x,
        'cellsY': cells_y,
        'gridRaw': grid_raw,
        'gridTexName': grid_tex_name,
        'quadSize': quad_size,
        'verts': verts,
        'tail': tail,
    }


# --------------------------------------------------------------------------- #
# 重采样映射
# --------------------------------------------------------------------------- #
def cell_center_world(col, row, grid_size):
    """格中心世界坐标（与 Layout.HexToPixel 一致）。返回 (worldZ, worldX)"""
    wz = col * grid_size + grid_size * 0.5
    wx = row * grid_size + (col & 1) * grid_size * 0.5 + grid_size * 0.5
    return wz, wx


def world_to_cell(wz, wx, grid_size):
    """世界坐标 -> 格坐标（与 Layout.PixelToOffset 一致）"""
    col = int(np.floor((wz - grid_size * 0.5) / grid_size))
    row = int(np.floor((wx - grid_size * 0.5 - (col & 1) * grid_size * 0.5) / grid_size))
    return col, row


def cell_resample_index(dst_nx, dst_ny, k, src_nx, src_ny, grid_size):
    """目标格 -> 源格：把目标格中心换算到源世界坐标后，用源布局的 PixelToOffset 取所属源格。

    必须走真实的六边形布局（odd-q：奇数列的半格偏移），不能简单按格序号等比取整，
    否则奇数列的地形会与顶点高度错开约半格。
    """
    cx = np.arange(dst_nx, dtype=np.int64).reshape(dst_nx, 1)
    cy = np.arange(dst_ny, dtype=np.int64).reshape(1, dst_ny)
    cz = np.broadcast_to(cx * grid_size + grid_size * 0.5, (dst_nx, dst_ny)).astype(np.float64)
    cwx = np.broadcast_to(cy * grid_size + (cx & 1) * grid_size * 0.5 + grid_size * 0.5,
                          (dst_nx, dst_ny)).astype(np.float64)

    sz = cz * k
    swx = cwx * k

    col = np.floor((sz - grid_size * 0.5) / grid_size).astype(np.int64)
    row = np.floor((swx - grid_size * 0.5 - (col & 1) * grid_size * 0.5) / grid_size).astype(np.int64)
    col = np.clip(col, 0, src_nx - 1)
    row = np.clip(row, 0, src_ny - 1)
    return col, row


def vertex_map(src_n, dst_n):
    """目标顶点 -> 源顶点（最近邻）。顶点是规则矩形网格，按世界坐标线性等比即可。"""
    f = float(src_n - 1) / float(dst_n - 1)
    return [min(src_n - 1, int(round(i * f))) for i in range(dst_n)]


def invert_cell_map(col, row, k, src_nx, src_ny, grid_size):
    """源格 -> 目标格：取「目标格中心最接近该源格中心」的那一格，保证城市落点与地形一致。"""
    dst_nx, dst_ny = col.shape[1], col.shape[0]

    cx = np.arange(dst_nx, dtype=np.int64).reshape(dst_nx, 1)
    cy = np.arange(dst_ny, dtype=np.int64).reshape(1, dst_ny)
    tz = np.broadcast_to(cx * grid_size + grid_size * 0.5, (dst_nx, dst_ny)).astype(np.float64)
    twx = np.broadcast_to(cy * grid_size + (cx & 1) * grid_size * 0.5 + grid_size * 0.5,
                          (dst_nx, dst_ny)).astype(np.float64)

    # 所属源格中心（源世界）换算到目标世界
    scz = (col.astype(np.float64) * grid_size + grid_size * 0.5) / k
    scwx = (row.astype(np.float64) * grid_size + (col & 1) * grid_size * 0.5 + grid_size * 0.5) / k

    dist = (tz - scz) ** 2 + (twx - scwx) ** 2
    flat_x = col.reshape(-1).astype(np.int64)
    flat_y = row.reshape(-1).astype(np.int64)
    key = flat_x * (src_ny + 1) + flat_y

    # 同一个源格可能有多个目标格，取距离源格中心最近的那个作为代表格
    order = np.lexsort((dist.reshape(-1), key))
    ks = key[order]
    first = np.ones(ks.shape[0], dtype=bool)
    first[1:] = ks[1:] != ks[:-1]

    inv = {}
    for idx in order[first]:
        t = np.unravel_index(int(idx), col.shape)      # (目标列, 目标行)
        inv[(int(flat_x[idx]), int(flat_y[idx]))] = (int(t[0]), int(t[1]))

    # 非整数倍放大时，个别源格可能没有被任何目标格取到（被挤掉）。
    # 这类源格按「源格中心 -> 目标世界坐标 -> 目标布局 PixelToOffset」解析式补一个代表格。
    for sx in range(src_nx):
        for sy in range(src_ny):
            if (sx, sy) in inv:
                continue
            wz = sx * grid_size + grid_size * 0.5
            wx = sy * grid_size + (sx & 1) * grid_size * 0.5 + grid_size * 0.5
            tz = wz / k
            twx = wx / k
            tcx = int(np.floor((tz - grid_size * 0.5) / grid_size))
            tcy = int(np.floor((twx - grid_size * 0.5 - (tcx & 1) * grid_size * 0.5) / grid_size))
            inv[(sx, sy)] = (min(max(tcx, 0), dst_nx - 1), min(max(tcy, 0), dst_ny - 1))
    return inv


# --------------------------------------------------------------------------- #
# 缩放
# --------------------------------------------------------------------------- #
def scale_map(src_path, out_path, dst_cells):
    info = parse_bin(src_path)
    src_cells = info['cellsX']
    if info['cellsX'] != info['cellsY']:
        print('[提示] 源地图非方形: %dx%d 格' % (info['cellsX'], info['cellsY']))

    dst_cells_x = dst_cells
    dst_cells_y = dst_cells

    grid_size = info['gridSize']
    dst_map_w = dst_cells_x * grid_size // QUAD_SIZE
    dst_map_h = dst_cells_y * grid_size // QUAD_SIZE
    if dst_map_w * QUAD_SIZE % grid_size or dst_map_h * QUAD_SIZE % grid_size:
        raise ValueError('目标格数 %d 不能被 gridSize(%d)/quadSize(%d) 整除' % (dst_cells, grid_size, QUAD_SIZE))

    src_vx = info['verts'].shape[0]
    src_vy = info['verts'].shape[1]
    dst_vx = dst_map_w + 1
    dst_vy = dst_map_h + 1

    k = float(dst_map_w) / float(info['mapWidth'])              # 目标世界 / 源世界
    k_src_over_dst = float(info['mapWidth']) / float(dst_map_w)  # 源世界 / 目标世界

    print('源: %dpx -> %dx%d 格, 顶点 %dx%d, gridSize=%d' %
          (info['mapWidth'], info['cellsX'], info['cellsY'], src_vx, src_vy, grid_size))
    print('目标: %dpx -> %dx%d 格, 顶点 %dx%d, 世界坐标缩放比 k=%.6f' %
          (dst_map_w, dst_cells_x, dst_cells_y, dst_vx, dst_vy, k))

    # ---- 地格重采样（最近邻，terrainType/terrainState/areaId 整体搬运） ----
    grid = info['gridRaw']
    col, row = cell_resample_index(dst_cells_x, dst_cells_y, k_src_over_dst,
                                   info['cellsX'], info['cellsY'], grid_size)
    new_grid = grid[col, row]        # shape (dst_cells_x, dst_cells_y, 7)
    hit = np.unique(col.astype(np.int64) * (info['cellsY'] + 1) + row).size
    if hit != info['cellsX'] * info['cellsY']:
        print('[提示] 源地图 %d 个格中有 %d 个格没有被任何目标格覆盖' %
              (info['cellsX'] * info['cellsY'], info['cellsX'] * info['cellsY'] - hit))

    # ---- 顶点重采样（最近邻；height/textureIndex/water 一起搬，保持水陆边界锐利） ----
    vx = vertex_map(src_vx, dst_vx)
    vy = vertex_map(src_vy, dst_vy)
    new_verts = info['verts'][np.ix_(vx, vy)]

    # ---- 写文件 ----
    w = Writer()
    w.i32(info['version'])
    w.string(info['workContent'])
    w.i32(dst_map_w)
    w.i32(dst_map_h)

    # mapGrid
    w.i32(grid_size)
    w.raw(new_grid.tobytes())
    w.string(info['gridTexName'])

    # mapData
    w.i32(info['quadSize'])
    w.raw(new_verts.tobytes())

    # 尾部段：除 models/labels 外原样照抄
    buf = info['buf']
    for key in ('layer', 'terrain', 'light', 'fog', 'baseColor', 'skyBox', 'camera'):
        s, e = info['tail'][key]
        w.raw(buf[s:e])

    # mapModels（位置 x/z 按 k 缩放；高度与朝向不变——水平等比缩放不改朝向）
    models = info['tail']['models'][2]
    w.i32(len(models))
    for (obj_id, obj_type, bind_id, model_id, pos, rot, scl) in models:
        w.i32(obj_id)
        w.i32(obj_type)
        w.i32(bind_id)
        w.i32(model_id)
        w.f32(pos[0] * k)
        w.f32(pos[1])
        w.f32(pos[2] * k)
        w.f32(rot[0])
        w.f32(rot[1])
        w.f32(rot[2])
        w.f32(scl[0])
        w.f32(scl[1])
        w.f32(scl[2])

    # mapLabelSet（位置 x/z 按 k 缩放）
    labels = info['tail']['labels'][2]
    w.i32(len(labels))
    for (text, pos, color, font_size) in labels:
        w.string(text)
        w.f32(pos[0] * k)
        w.f32(pos[1])
        w.f32(pos[2] * k)
        w.u8(color[0])
        w.u8(color[1])
        w.u8(color[2])
        w.i32(font_size)

    out_dir = os.path.dirname(os.path.abspath(out_path))
    if out_dir and not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    with open(out_path, 'wb') as f:
        f.write(w.bytes())

    # ---- 统计 ----
    t_src = grid[:, :, 0]
    t_dst = new_grid[:, :, 0]
    aid_dst = new_grid[:, :, 5].astype(np.uint16) | (new_grid[:, :, 6].astype(np.uint16) << 8)
    st_dst = new_grid[:, :, 1].astype(np.uint32) | (new_grid[:, :, 2].astype(np.uint32) << 8) | \
             (new_grid[:, :, 3].astype(np.uint32) << 16) | (new_grid[:, :, 4].astype(np.uint32) << 24)
    print('地格: 地形种类 源 %d / 目标 %d；有归属格 areaId>0 源 %d / 目标 %d；内政位(4) 目标 %d' %
          (len(np.unique(t_src)), len(np.unique(t_dst)),
           int((grid[:, :, 5].astype(np.uint16) | (grid[:, :, 6].astype(np.uint16) << 8) > 0).sum()),
           int((aid_dst > 0).sum()), int((st_dst == 4).sum())))
    print('顶点: 海(0) 源 %d / 目标 %d；水面字节(11) 源 %d / 目标 %d' %
          (int((info['verts'][:, :, 0] == 0).sum()), int((new_verts[:, :, 0] == 0).sum()),
           int((info['verts'][:, :, 2] == 11).sum()), int((new_verts[:, :, 2] == 11).sum())))
    print('模型 %d 个、标注 %d 个已按 k 缩放' % (len(models), len(labels)))
    print('已写出: %s (%d 字节)' % (out_path, os.path.getsize(out_path)))

    return {'cellInv': invert_cell_map(col, row, k, info['cellsX'], info['cellsY'], grid_size)}


def scale_scenario(src_json, out_json, cell_inv, new_map_type):
    """把剧本里城市的格坐标缩放到新格数（必须与地图重采样一致，否则城市错位/越界）"""
    with open(src_json, 'r', encoding='utf-8') as f:
        sc = json.load(f)

    city_set = sc.get('citySet', {})
    changed = 0
    miss = 0
    for key, city in city_set.items():
        if not isinstance(city, dict):
            continue
        if 'x' in city and 'y' in city:
            src = (int(city['x']), int(city['y']))
            dst = cell_inv.get(src)
            if dst is None:
                miss += 1
                continue
            city['x'], city['y'] = dst
            changed += 1

    if new_map_type:
        sc.setdefault('Info', {})['mapType'] = new_map_type

    out_dir = os.path.dirname(os.path.abspath(out_json))
    if out_dir and not os.path.isdir(out_dir):
        os.makedirs(out_dir)
    with open(out_json, 'w', encoding='utf-8') as f:
        json.dump(sc, f, ensure_ascii=False, indent=1)

    print('剧本: 缩放 %d 座城市的 x/y（%d 座找不到对应目标格）；mapType -> %s；已写出: %s' %
          (changed, miss, sc['Info'].get('mapType'), out_json))


# --------------------------------------------------------------------------- #
def main():
    ap = argparse.ArgumentParser(description='把 Sango 地图 .bin 缩放到指定格数')
    ap.add_argument('--src', required=True, help='源 .bin 路径')
    ap.add_argument('--out', required=True, help='输出 .bin 路径')
    ap.add_argument('--cells', type=int, required=True, help='目标格数（宽=高），如 750')
    ap.add_argument('--scenario', help='可选：同步缩放剧本 JSON 里城市的格坐标')
    ap.add_argument('--out-scenario', help='可选：剧本输出路径')
    ap.add_argument('--map-type', help='可选：剧本 Info.mapType 改成新的地图名')
    args = ap.parse_args()

    res = scale_map(args.src, args.out, args.cells)

    if args.scenario:
        out_scenario = args.out_scenario or (os.path.splitext(args.scenario)[0] + '_scaled.json')
        scale_scenario(args.scenario, out_scenario, res['cellInv'], args.map_type)


if __name__ == '__main__':
    main()
