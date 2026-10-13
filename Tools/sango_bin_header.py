# -*- coding: utf-8 -*-
"""sango_bin_header.py —— 读写地图 .bin 的头字段（versionCode / WorkContent / 尺寸）。

WorkContent 就是引擎里的「地图标识」(MapEditor 里叫 MapKey)：地图载入时用它拼资源目录
    {ContentRootPath}/Assets/Map/{WorkContent}/map_bounds.jpg      —— 剧本选择界面的预览图
    {ContentRootPath}/Assets/Map/{WorkContent}/Terrain/{季节}/layer_N.png 等 —— 地表图层
所以改 WorkContent 之后，必须有一份同名目录的资源，否则画面会缺图层。

头部结构（version>=6）：
    int32   versionCode
    string  WorkContent      7bit 变长长度前缀 + UTF8
    int32   mapWidth, mapHeight
    ...

用法：
    python sango_bin_header.py --bin Map/xworld_full.bin --report
    python sango_bin_header.py --bin Map/xworld_full.bin --work-content xworld --apply
"""
import argparse
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from sango_scale_bin import parse_bin, Writer          # noqa: E402

MIN_MAP_BYTES = 4096


def read_header(buf):
    """返回 (version, workContent, w, h, str_end, wh_end)。

    str_end = WorkContent 字符串结束处（重写头部时必须从这里往后接，
              否则会把 mapWidth/mapHeight 这 8 字节一起弄丢）
    wh_end  = 尺寸字段之后、mapGrid 段起始处
    """
    ver = struct.unpack_from("<i", buf, 0)[0]
    off = 4
    n, sh = 0, 0
    while True:
        v = buf[off]
        off += 1
        n |= (v & 0x7F) << sh
        if not (v & 0x80):
            break
        sh += 7
    wc = buf[off:off + n].decode("utf-8", "replace")
    str_end = off + n
    w, h = struct.unpack_from("<ii", buf, str_end)
    return ver, wc, w, h, str_end, str_end + 8


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--bin", dest="bin_path", required=True)
    ap.add_argument("--work-content", default=None, help="要写入的新 WorkContent")
    ap.add_argument("--out", default=None, help="输出文件（默认就地改写，留 .bak_header）")
    ap.add_argument("--report", action="store_true", help="只打印现状")
    ap.add_argument("--apply", action="store_true", help="真正写盘")
    args = ap.parse_args()

    p = Path(args.bin_path)
    if not p.exists():
        print(f"[错误] 找不到 {p}")
        return
    buf = p.read_bytes()
    ver, wc, w, h, str_end, head_end = read_header(buf)
    print(f"文件: {p}")
    print(f"  {len(buf)} 字节 ({len(buf) / 1048576:.2f} MB)")
    print(f"  versionCode = {ver}")
    print(f"  WorkContent = {wc!r}  （字符串结束于 {str_end}，含尺寸的头部共 {head_end} 字节）")
    print(f"  mapWidth x mapHeight = {w} x {h}")

    # 资源目录检查：mod 根 = .bin 所在目录的上一级（Map/xxx.bin -> <mod>/Assets/Map/<wc>/）
    mod_root = p.parent.parent
    for cand, label in ((mod_root, "同级 mod 目录"), (Path("."), "当前目录")):
        d = cand / "Assets" / "Map" / wc
        if d.is_dir():
            files = sorted(x.name for x in d.rglob("*") if x.is_file())
            print(f"  资源目录 {label}: {d}  —— 存在，{len(files)} 个文件: {files[:6]}")

    if not args.work_content:
        if not args.report:
            print("\n（没有 --work-content，未做任何改动；加 --work-content <名字> --apply 才会写盘）")
        return

    if args.work_content == wc:
        print(f"\nWorkContent 已经是 {wc!r}，无需改动。")
        return

    nd = mod_root / "Assets" / "Map" / args.work_content
    if nd.is_dir():
        files = sorted(x.name for x in nd.rglob("*") if x.is_file())
        print(f"\n新资源目录已存在: {nd}（{len(files)} 个文件: {files[:6]}）")
    else:
        print(f"\n[警告] 新资源目录不存在: {nd}")
        print("       地图能载入，但剧本选择预览图与地表图层会缺资源。")
        print(f"       可先复制: xcopy /E /I \"{mod_root / 'Assets' / 'Map' / wc}\" \"{nd}\"")

    if not args.apply:
        print(f"\n--report/未加 --apply：未写盘。（将把 {wc!r} 改为 {args.work_content!r}）")
        return

    wtr = Writer()
    wtr.i32(ver)
    wtr.string(args.work_content)
    new_head = wtr.bytes()
    # 关键：从 str_end 接回（含 mapWidth/mapHeight），不能从 head_end 接，
    # 否则会把尺寸字段整段丢掉，文件直接损坏。
    data = new_head + buf[str_end:]
    delta = len(data) - len(buf)
    expect = len(new_head) - (str_end - 0)
    if delta != expect:
        raise SystemExit(f"[自检失败] 总长变化 {delta:+d} != 头部变化 {expect:+d}，已中止，未写盘")
    print(f"\n头部 {str_end} -> {len(new_head)} 字节（总长 {delta:+d}）")
    print(f"  保留 mapWidth/mapHeight = {w} x {h}（紧随新字符串之后）")

    out = Path(args.out) if args.out else p
    if out == p:
        bak = p.with_suffix(p.suffix + ".bak_header")
        if not bak.exists():
            bak.write_bytes(buf)
            print(f"已备份 -> {bak.name}")
    out.write_bytes(data)
    print(f"已写出 {out}  {len(data)} 字节")

    # 复核：重新解析，确认除 WorkContent 外一切不变
    ver2, wc2, w2, h2, se2, he2 = read_header(data)
    print(f"复核头: version={ver2} WorkContent={wc2!r} 尺寸={w2}x{h2}")
    same = data[se2:] == buf[str_end:]
    print(f"  新字符串后 {len(data) - se2} 字节与原文件对应部分逐字节相同: {same}")
    d1 = parse_bin(str(out))
    ms = d1["tail"]["models"][2]
    print(f"复核解析: 格 {d1['cellsX']}x{d1['cellsY']}  work={d1['workContent']!r}  "
          f"mapModels {len(ms)} 条  mapLabelSet {len(d1['tail']['labels'][2])} 条  "
          f"文件 {len(data)} 字节 ({'正常' if len(data) > MIN_MAP_BYTES else '**异常**'})")


if __name__ == "__main__":
    main()
