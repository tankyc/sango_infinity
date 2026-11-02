---
name: sango-config-table
description: Sango Infinity 数据配置表（Build/Content/Data/**/*.json）的安全改动规程。当任务涉及给配置表字段或条目做中文化、加中文注释（_comment）、重命名、调整顺序，或排查「改了 JSON 不生效」「加载后数值全是 0」「新增的表被莫名污染」「某字段到底能不能改」时使用。覆盖三条加载链路（ModManager.LoadFile 整文件覆盖 / ScenarioCommonData 全目录 PopulateObject / SangoObjectMap 强类型反序列化）、稀疏覆盖与「C# 内置表 + JSON 覆盖层」双落点陷阱，以及改完必须跑的字段对账脚本。
---

# Sango 配置表安全改动规程

## 概述

Sango Infinity 的绝大多数策划数值都在 `Build/Content/Data/**/*.json` 里，由 3 条互不相同的链路加载。这三条链路对「未知字段」的容忍度完全不一样，导致同一类改动（比如把 `name` 改成中文）在一张表里绝对安全、在另一张表里会静默把整行数值清零。

本 skill 的作用：**在动手改任何一个 JSON 字段之前，先判定它属于哪条链路、是否进入运行时逻辑；改完之后用脚本证明数值没被动过。**

## 何时使用

- 用户问「某表的某字段能不能中文化 / 加注释 / 改名 / 调顺序」
- 用户要求把配置表的标注字段翻译成中文
- 排查「我改了 JSON，游戏里没变化」
- 排查「加载日志说载入了，但效果不对 / 数值变成 0」
- 在 `Data/Common/` 下新增一张配置表
- 给配置表补 `_comment` 说明

不适用：改 C# 逻辑（走 `open311-alignment`）、改地图数据（走 `sango-map-generation`）。

## 三条硬约束

违反任意一条都会造成静默数据损坏，且**不会有任何报错**。

1. **只改「不进运行时逻辑」的字段**。判定标准见下方三步法。典型安全字段：`name`、`desc`、以 `_` 开头的注释键。典型禁区：索引键（`id` / `event` / `name` 有时兼作索引）、枚举名键、所有数值字段。
2. **分清「链路 A 的正常表」与「误撞保留名」**。`Data/Common/` 下每个 json 都会被 `PopulateObject` 扫一遍。顶层键与 `ScenarioCommonData` 的 `[JsonProperty]` 成员同名时（`Features.json` 的顶层就是 `Features`），说明这张表本来就是给链路 A 填充用的，**安全**；但如果同一个文件里还有别的业务键（新表写成 `{"Skills": [...], "entries": [...]}`），`Skills` 会被合并进全局数据库，污染且无任何报错。保留名清单见 `references/load-paths.md`。
3. **数值一律不碰**。做中文化 / 加注释时，`git diff` 里只允许出现文本字段和 `_comment` 的变更。

## 判定三步法

对「这个字段能不能改」这类问题，按顺序做三步，不要跳步。

### 第一步：找到加载入口

```bash
# 表名即线索，搜常量或路径字面量
rg "CombatExpTable.json" Project/Assets/Sango/Scripts
rg "ConfigPath\s*=" Project/Assets/Sango/Scripts --type cs
```

各表的加载入口已整理在 `references/load-paths.md`，先查这份清单，没有再搜。

### 第二步：判定反序列化方式（决定「未知键」的下场）

| 链路 | 特征代码 | 未知键下场 | 缺失字段下场 |
|---|---|---|---|
| A. 全目录扫描 | `Sango.Directory.EnumFiles(Path.ContentRootPath + "/Data/Common", ...)` → `JsonConvert.PopulateObject` | 静默忽略；**若与保留名同名则被合并** | 保持对象原值 |
| B. 整文件覆盖 + 稀疏回填 | `ModManager.Instance.LoadFile(path, f => ...)` + `item.ToObject<T>()` / 手工 `JObject` 取值 | 静默忽略 | **取类型默认值（int 即 0）**，覆盖掉 C# 内置表该行 |
| C. 强类型整表反序列化 | `JsonConvert.DeserializeObject<SangoObjectMap<T>>(...)` + 自定义 Converter | 静默忽略（`MemberSerialization.OptIn` 时更严格） | 取字段初始值 |

链路 B 是本项目最危险的一条 —— 见下方「双落点陷阱」。

### 第三步：确认字段是否被读取

```bash
# 字段名在整个脚本目录下的引用。只出现在加载代码里 = 纯标注字段，可安全改写
rg "\bname\b" Project/Assets/Sango/Scripts/Game/Scenario/CombatExpTable.cs
```

只出现在 `ToObject<T>()` 目标结构体定义里、没有任何业务代码读取 → 纯标注，可改。

## 双落点陷阱（链路 B 专属）

`CombatExpTable` / `GainValueConfig` 这类表的数值在**两个地方**都有：

- C# 里的 `BuildTable()` 内置原版表（`CombatExpTable.cs:374-546`）
- JSON 里的同名条目（`Data/Common/CombatExpTable.json`）

加载逻辑是「先铺内置表 → 再用 JSON 逐行覆盖」（`CombatExpTable.cs:304-361`）。由此得出四条推论：

1. **只改 C# 不改 JSON = 白改**，JSON 会覆盖回旧值。
2. **只改 JSON 不改 C# = 运行时生效**，但 JSON 文件缺失（老存档 / 裁剪包 / Mod 覆盖）时回落到旧值。
3. **改 JSON 时若某行的数值字段写漏，该行会被覆盖成全 0**，而不是「沿用内置值」。稀疏覆盖的粒度是**行**，不是字段。
   - 例：`{"event": 96, "name": "补给 (Supply)"}` 会让 `leadership` / `merit` 从 `2` / `100` 变成 `0` / `0`。
4. 因此**做纯文本改动（中文化 / 加注释）时必须逐行比对数值**，用 `scripts/check_config_json.py --csharp-builtin`。

## 注释惯例

`_comment` 是本项目既有的约定（`GainValueConfig.json`、`duelPersonBehaviour.json` 已在用），在三条链路里都安全：

- 链路 A：`ScenarioCommonData` 用 `[JsonObject(MemberSerialization.OptIn)]`，没有 `[JsonProperty]` 的键不参与填充。
- 链路 B：`GainValueConfig.cs:636` 显式跳过 `pair.Key[0] == '_'`；`ToObject<T>()` 则直接忽略。
- 链路 C：`OptIn` 语义下同样忽略。

写法要求（照 `GainValueConfig.json:2` 的密度）：

```
"_comment": "这张表是干什么的 + 键的语义 + 修改禁区 + 原版出处（文件:行号）"
```

中文化标注字段的格式：`"中文名 (OriginalName)"`，保留原名便于和 Open311 源码对照。

## 校验流程（改完必须执行）

```bash
# 1. 结构体检：语法、重复键、索引重复、保留名误撞、无数字段行、字段使用频次
python .codebuddy/skills/sango-config-table/scripts/check_config_json.py <json路径>

# 2. 双落点对账：JSON 每行数值 vs C# 内置表（链路 B 必做）
python .codebuddy/skills/sango-config-table/scripts/check_config_json.py <json路径> --csharp-builtin <对应.cs>
```

脚本退出码 0 才算通过。非 0 时不要把问题当成「已知差异」跳过 —— 逐条修正或明确报告。

脚本支持的两种序列化形式：

- 数组表 `[{...}, {...}]` —— 如 `CombatExpTable.json` 的 `entries`
- 字典表 `{"1": {...}, "2": {...}}` —— 如 `Features.json` / `Skills.json`（`SangoObjectSet` / `SangoObjectMap` 的结果）

`--csharp-builtin` 目前支持 `t[0xXX] = new T { 字段 = 值, ... };` 这种按索引赋值的内置表写法（`CombatExpTable.BuildTable()` 的形态）。其它形态的内置表需人工比对。

纯 KV 表（`GainValueConfig.json` 那种「枚举名 → 数值」）没有记录行，脚本只能做语法 / 重复键 / 顶层键检查，数值正确性仍需人工读 `BuiltinDefaultOf`。

## 排障：改了不生效

按可能性从高到低排查：

1. **Mod 目录里有同名文件**。`ModManager.LoadFile` 的顺序是「内容根目录 → 各 Mod 目录，命中即返回」（`ModManager.cs:691-715`），内容根目录那份优先。反过来，如果只改了 Mod 目录的副本而内容根目录也有同名文件，改的就被忽略了。
2. **`Path.ContentRootPath` 为空**。`CombatExpTable.EnsureLoaded()` / `GainValueConfig.EnsureJsonDefaultsLoaded()` 在 Path 未 Init 时**不置 `loaded` 标志**，等就绪后重读；但如果在编辑器里直接调用，永远读不到 JSON，只有内置值。
3. **`loaded` / `jsonDefaultsLoaded` 已置位**。同一次运行内改文件不会重新加载，必须重启游戏。
4. **JSON 缺 `entries` / 表节点名写错**。链路 B 会打 `Sango.Log.Warning("...缺少 entries 数组，沿用内置表")`；改完用 `read_console` 看这条日志，日志级别是 `Info` / `Warning` / `Error`，全部中文。

## 交付要求

按项目规则输出对齐汇报五节（改动清单 / 原版对齐表 / 未对齐项 / 校验结果 / 实测建议）。其中「校验结果」必须贴脚本的实际输出，不能写「应该没问题」。

## 资源

- `references/load-paths.md` —— 三条链路的完整加载入口清单、`ScenarioCommonData` 保留顶层键表、各表归属。
- `scripts/check_config_json.py` —— 结构体检 + 双落点对账。
