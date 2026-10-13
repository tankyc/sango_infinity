# -*- coding: utf-8 -*-
"""一次性：按地图自带标签的校验结果，修正 A2 表里的 7 条归属错误。

B 类（我表里确实错了）6 条：
  鲁国/任城郡/东平郡/济北郡/泰山郡  青徐(3) -> 兖予(4)   （豫州/兖州）
  代郡                              幽州(1) -> 冀州(2)   （并州并入冀州）
C 类里的乐安郡 1 条：
  乐安郡                            冀州(2) -> 青徐(3)   （青州）
C 类另 2 条（九江郡、平原郡）按 province 字段（锚点）保持不变。
"""
import json
from pathlib import Path

T = Path(r"D:\sango_infinity\Tools\yyxk_provinces_A2.json")
MOVES = {"鲁国": 4, "任城郡": 4, "东平郡": 4, "济北郡": 4, "泰山郡": 4,
         "代郡": 2, "乐安郡": 3}

t = json.loads(T.read_text(encoding="utf-8"))
before = {}
for pid, blk in t["provinces"].items():
    for key in ("anchors", "members"):
        for n in blk.get(key, []):
            if n in MOVES:
                before[n] = pid
for pid, blk in t["provinces"].items():
    for key in ("anchors", "members"):
        if key in blk:
            blk[key] = [n for n in blk[key] if n not in MOVES]
for nm, pid in MOVES.items():
    blk = t["provinces"][str(pid)]
    blk.setdefault("members", []).append(nm)

name = {int(k): v["name"] for k, v in t["provinces"].items()}
print("修正明细:")
for nm, pid in sorted(MOVES.items()):
    print(f"  {nm:<6} {name.get(int(before.get(nm, 0)), before.get(nm))} -> {name[pid]}")

t.setdefault("_标签校验修正", {})["说明"] = \
    "依据地图自带的 128 条「州-郡」标签交叉校验，修正以下 7 条；另 九江郡/平原郡 依 province 字段保持不变。"
t["_标签校验修正"]["moves"] = {nm: {"from": before.get(nm), "to": pid,
                                     "to_name": name[pid]} for nm, pid in MOVES.items()}
T.write_text(json.dumps(t, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"\n已写回 {T}")

# 复核：总数不变
tot = sum(len(b.get("anchors", [])) + len(b.get("members", []))
          for b in t["provinces"].values())
print(f"表内总条目 {tot}（应仍是 473）")
