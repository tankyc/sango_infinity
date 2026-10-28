---
name: sango-legacy-recon
description: 在 Sango Infinity（Open311 三国志11 复刻项目）里侦察原版行为、定位落点、验证改动的完整工作流。当任务涉及「把某系统对齐原版」「这个原版机制在项目里怎么落」「改完了但不确定有没有真的生效」「原版某字段/行为是什么作用」时使用。覆盖术语错位搜索、关联链追查、数据实证、派生字段陷阱、Unity MCP 编译验证兜底。
---

# Sango 原版对齐：侦察 → 落地 → 验证

## 适用场景

- 用户说"严格按照原版对齐""原版是怎么做的""这个参数要不要对齐"
- 要在项目里复刻原版的某个机制（异族根城、AI 攻城、特技压制等）
- 改动完成后需要确认"真的编译了、真的没报错"

## 铁律

1. **先读代码再动手**。任何"我觉得应该是"的实现都必须先被源码证实。
2. **原版有 → 逐行对齐**（语义、顺序、边界、异常分支）；原版无 → 写明"原版无对应实现"及理由。
3. **引用必须带出处**（`文件:行号`）。没有出处的"对齐"视为未完成。
4. **声称"已验证"前必须真的跑编译并读日志**；读不到日志就如实说没读到，不要用推理代替。

---

## 阶段一：侦察

### 1.1 术语错位搜索（最容易踩）

原版用一套名词，本项目可能换成另一套。**用原版术语搜不到，不等于项目没有这个功能**。

正确做法：
- 先用**原版英文名**搜（`nejiro` / `alerted` / `kyoten`）
- 搜不到 → 换**语义**搜（根城→`Camp`/`Base`/`Thief`；警报→`Alert`/`Warn`）
- 再搜**数据结构**（地图字段、枚举、GridState、Json 键）
- 最后才下结论"项目没有"

典型案例见 `references/alignment-traps.md` 的「nejiro 其实叫 Thief」。

### 1.2 关联链追查（找入口，别找命令）

某个行为"看起来没人触发"时，通常不是缺命令，而是由通用系统自然产生。

追查顺序：
1. 这个行为最终由哪个**执行函数**完成（如 `unit_attack_building_anim`）
2. 谁调用它（`UnitAction_Attack`）
3. 谁生成这个 Action（`get_target_action` 的目标枚举）
4. 枚举里有哪些**过滤条件**（`is_base_in_friend_area`、`is_rockfall`）
5. 打分函数的**每一档分值**（敌对 +5000、主目标 +10000、根城 +200）

结论要能回答"为什么 AI 会/不会做这件事"，而不是只说"有/没有"。

### 1.3 字段是派生还是存储

改归属、改类型前，先确认字段是**存储**还是**派生**：

- `Person.BelongForce => BelongCorps?.BelongForce`（派生，赋值无效，必须挂军团）
- `Troop.BelongCity => Leader.BelongCity`（派生，改主将才有用）
- `Corps.BelongForce` 是普通字段（可直接赋值）

派生字段写错的表现是"代码跑了但没效果"，极难排查。

### 1.4 数据实证，不靠印象

判断"地图/剧本里有没有这个数据"，直接读文件：

```bash
cd D:\sango_infinity\Tools\Map
python ..\..\.codebuddy\skills\sango-legacy-recon\scripts\inspect_map_bin.py ..\..\Build\Content\Map\DefaultMap.bin
```

或直接读 JSON：

```bash
python -c "import json,io; d=json.load(io.open(r'...\Scenario.json',encoding='utf-8')); print(sorted(d['forceSet']))"
```

**注意**：PowerShell 里 `$` 会被 shell 吃掉，Python 单行脚本里不要用 `$`；复杂逻辑写成临时 `.py` 再跑，用完删除。

---

## 阶段二：落地

### 2.1 先查"依赖字段到底谁在用"

给建筑/部队/武将加归属前，列出所有读取方：

| 字段 | 坑 |
|---|---|
| `Building.BelongCity` | 赋值即进 `City.allBuildings` → 城陷落时被 `ChangeCorps` 或 `OnFall`；`City.UpdateCorps` 批量改军团；城池收入遍历累加 |
| `Troop.BelongCity` | `Troop.Init` 里 `BelongCity.allTroops.Add` 会 NRE；`City.RebuildTroopLists` 按城归组 |
| `Person.BelongCorps` | 为 null 时 `BelongForce` 为 null，兵力/任务/巡检全部失效 |

需要"领地城"这类派生关系时，用格子上的 `Cell.BelongCity` 现查，不要占用归属字段。

### 2.2 数据缺失的写法

原版依赖美术数据（地图标记、剧本字段），项目没有时：

1. 先确认是不是**换了名字**（见 1.1）
2. 确实没有 → 实现等效结构，并在类注释写清：
   - 原版依赖什么数据
   - 项目为什么没有
   - 本项目用什么替代、等价到什么程度

### 2.3 硬规则优先于原版

项目有原版没有的硬约束时，**硬规则优先**，并在注释说明偏离原因。已知硬规则：

- 部队不能停在城市格/建筑格（`Cell.CanStay` 要求 `building == null && troop == null`）
- 一处注释：`Cell.CanStay` 的判定 = 落地格的唯一标准

---

## 阶段三：验证

### 3.1 编译

```
refresh_unity(mode="force", scope="all", compile="request", wait_for_ready=true)
```

**新增 .cs 文件必须用 `scope="all"`** —— `scope="scripts"` 只重编已有脚本，新文件不会生成 `.meta`、不会进程序集，表现为"类型名不存在"的诡异报错。

### 3.2 读日志（含兜底）

```
read_console(action="get", types=["error"], count=25, format="plain")
```

`read_console` 超时（Unity ping 不通）时的兜底：

```powershell
powershell -NoProfile -Command "Get-Content 'C:\Users\Administrator\AppData\Local\Unity\Editor\Editor.log' -Tail 500 | Select-String 'error CS'"
powershell -NoProfile -Command "Get-Item 'd:\sango_infinity\Project\Library\ScriptAssemblies\Assembly-CSharp.dll' | Select-Object LastWriteTime; Get-Date -Format 'HH:mm:ss'"
```

- `Editor.log` 尾部无 `error CS` + 程序集时间戳是刚刚 → 编译通过
- 程序集没更新 → 编译失败或没触发编译

### 3.3 交付汇报（固定五节）

1. 改动清单（文件 + 关键行 + 一句话）
2. 原版对齐表（出处 `文件:行号` → 本项目落点 → ✅/⚠️/❌）
3. 未对齐项及原因（数据缺失 / 架构差异 / 风险过高）
4. 验证结果（编译是否通过、日志实际内容、lint）；没验证就说没验证
5. 实测建议（具体操作 + 预期日志）

---

## 参考资源

- `references/alignment-traps.md` — 本项目已踩过的具体坑与术语映射表，落地前必读
- `references/unity-verify.md` — Unity MCP 编译验证、纯代码动态 UI 扩展、三源数据管理器模式
- `scripts/inspect_map_bin.py` — 解析 DefaultMap.bin，统计地形状态分布（验证地图标记是否真的存在）
