---
name: sango-gc-perf
description: Sango Infinity（Unity 2021.3 / C#）的 GC 分配治理与遍历安全规程。当任务涉及 Profiler 里 GC.Alloc 偏高、单帧分配堆到几十上百 KB、回合切换卡顿、闭包/lambda 分配、LINQ、List 扩容、函数内 new 容器，或要把 SangoObjectList / SangoObjectSet 的 ForEach 改写成 for 循环时使用。覆盖「SangoObjectList 必须倒序遍历」这条硬约束（正序删元素会跳项，已有夏口港陷落实测事故）、6 类高频分配源的识别与改法、本项目已知热点账本，以及一个能扫出可疑点的脚本。
---

# Sango GC 分配治理与遍历安全

## 概述

Sango Infinity 的卡顿绝大多数不是「算得太多」，而是「**每帧在堆上扔垃圾**」：一个捕获变量的 lambda、一次 `List` 翻倍扩容、一个函数内 `new` 的常量表，单看都是几十到几百字节，乘以「每城 × 每势力 × 每月 × 每个候选落点」之后就是单帧上百 KB 的 `GC.Alloc`，进而触发 GC 尖峰。

本 skill 的作用：**先拿到 Profiler 证据，再按固定分类改动，改完必须过等价性与倒序两道关。**

## 何时使用

- 用户说「优化一下 GC / 卡顿 / 帧率」，或贴了 Profiler 截图/导出文件
- 要把 `ForEach(lambda)`、`Find(lambda)`、LINQ 改写成手写循环
- 要给容器预设容量，或把函数内 `new` 的表提成静态字段
- 排查「改完优化后行为变了」（多半是倒序或重载默认参数被破坏）
- 新写一段高频路径代码（部队 AI 扫描、每月/每势力回合刷新、寻路）

不适用：纯粹的功能 BUG（走 `sango-legacy-recon`）；与 Open311 对齐逻辑本身（走 `open311-alignment`，但优化后的等价性仍按本 skill 校验）。

## 三条硬约束

违反任意一条，要么制造静默的漏处理，要么把优化做成回归。

### 1. `SangoObjectList` 遍历必须倒序

`SangoObjectList<T>` 底层是 `List<T>`，`Remove` 会让后续元素整体前移。**正序 `for (i = 0; i < Count; i++)` 且循环体内移除当前元素，就会跳过紧邻的下一个元素。**

它的 `ForEach` 实现本来就是倒序（`Count-1 → 0`），所以历史代码用 `ForEach(lambda)` 是安全的；**一旦改写成 `for`，必须显式写成倒序**，否则就是行为回归。

真实事故（`City.cs` 城陷迁出武将）：夏口港陷落时只有诸葛亮被处理，关羽被跳过，一直留在已易主的城里，直到回合结束兜底自愈才捞回来。原因是循环体里的 `person.ChangeBelongCity(...)` 会走到 `City.RemovePerson` 把当前人从 `allPersons` 摘掉，正序时 `i++` 直接跳过了前移上来的关羽。

例外：`SangoObjectSet<T>` 底层是 `T[]`（按 Id 索引，有空位），删除只是置空不压缩，**必须正序 `1..Length`**（下标 0 保留）。

细节、错误/正确写法对照、迁移检查清单见 `references/reverse-traversal.md`。

### 2. 改写前后必须逐条等价

优化只允许改「怎么遍历」，不允许改「遍历到了谁、按什么顺序、命中后做什么」：

- **重载默认参数不同**：`Hex.Ring(radius, Action)` 默认 `startDir = 3`，`Hex.Ring(radius, List)` 默认 `4` —— 换重载必须显式补 `3`。
- **边界行为不同**：`radius == 0` 时 Action 重载会把自身作为唯一元素回调，List 重载什么都不加 —— 要单独补。
- **`return` → `continue`**：lambda 里的 `return` 只是结束本次回调，改写成循环后必须写成 `continue`，写成 `break` 就少处理后面所有人。
- **闭包捕获的是变量不是值**：循环里捕获循环变量，原语义可能是「全部拿到最后一个值」，改写时要确认调用方期望。
- **首次/末次语义**：`List.Find` 返回第一个匹配，倒序手写循环会返回最后一个 —— 需要「第一个」时保持正序。

### 3. 先有数据，再动手

没有 Profiler 的 `GC.Alloc` 数据、也没有明确的调用频次估算（每帧/每回合/每城）时，不要为了「看起来会分配」而重构。优化必须有可对比的前后数字，否则宁可不改。

## 工作流

### 第一步：定位（拿到符号，不只是数字）

1. Unity Profiler → CPU Usage → Hierarchy，排序 `GC Alloc` 列，锁定单帧最高的几条。
2. 展开调用栈，把符号抄下来。**闭包分配的符号长得很有辨识度**：
   - `<>c__DisplayClass158_0` = 捕获了局部变量的闭包类
   - `<UpdateCityRate>b__0` = 该闭包里生成的方法
   - `System.Action<...>:.ctor` / `Predicate<T>:.ctor` = 委托本身
3. 记下频次：这个方法每帧几次、每回合几次、每城几次。**频次 × 单次字节 = 真实收益**，`GetRing` 单次 168 B，但 AI 目标扫描一帧调几百次，就成了 132 KB。

### 第二步：分类（6 类，见 `references/allocation-patterns.md`）

| 类别 | 典型符号/写法 | 首选改法 |
|---|---|---|
| 闭包捕获 | `<>c__DisplayClass*`、`xxx.ForEach(v => ...)` | 手写 `for`，把捕获变量提成参数或字段 |
| 委托/Predicate 分配 | `List.Find(x => ...)`、`RemoveAll(x => ...)` | 手写 `for` + 直接比较 |
| 容器扩容 | `List.Resize`、`Dictionary.Resize`、`HashSet.Add` | 构造时给容量；复用字段 + `Clear()` |
| 函数内建表/建数组 | `new int[,]`、`new[] {...}` 在方法体里 | 提成 `static readonly` 字段 |
| 临时数组 | `.ToArray()`、`.ToList()`、`Values.ToArray()` | 直接遍历底层集合/字典值 |
| 重复计算 | 双层循环里反复读同一批属性 | 外层预取到局部数组/缓存列表 |

### 第三步：改写

只改热路径上的那一段。改完立刻在代码里写一条中文注释，说明：原写法、分配量（带 Profiler 出处）、改法与**等价性注意点**。本项目的既定风格是这样（示例见 `City.cs` 的 `UpdateRate`、`Map.cs` 的 `GetRing`）。

若改的是 `SangoObjectList` 的遍历，**先确认循环体是否会移除元素**；会移除的一律倒序。

### 第四步：等价性校验

逐条过 `references/reverse-traversal.md` 末尾的「迁移检查清单」，尤其是：遍历方向、`return/continue/break`、边界值、重载默认参数、空列表与 null 元素。

### 第五步：验证（如实汇报）

1. 用 Unity MCP 编译项目，读 console，确认 0 error（有 error 必须改到通过）。
2. 复测同一场景，比较前后 `GC.Alloc`；数字没降就说明没打到真正的热点。
3. 跑一遍受影响的业务路径（城陷、换季、AI 出兵、月度刷新），确认没有少处理对象。
4. 报告里必须写清：改了哪几处、每处前后数字、哪些是「按频次估算」而非实测。

## 本项目已知热点账本

已治理与已知未治理的热点、对应文件行号、Profiler 原始数字，见 `references/known-hotspots.md`。**动手前先查这张表**，避免重复分析或改错地方。

## 工具

```bash
# 扫描整个脚本目录里的疑似分配热点与正序删除风险
python .codebuddy/skills/sango-gc-perf/scripts/scan_gc_hotspots.py --path Project/Assets/Sango/Scripts

# 只看高危（正序删除 / 闭包遍历）
python .codebuddy/skills/sango-gc-perf/scripts/scan_gc_hotspots.py --path Project/Assets/Sango/Scripts --level high

# 汇总各规则命中数，判断从哪类下手
python .codebuddy/skills/sango-gc-perf/scripts/scan_gc_hotspots.py --path Project/Assets/Sango/Scripts --summary
```

脚本是**静态启发式**，只用来缩小范围，命中不等于必须改：最终以 Profiler 为准。

## 交付格式

按项目惯例输出「对齐汇报」五节，其中「验证结果」必须给出编译状态与实测数字，「未对齐/近似项」必须列出「按频次估算、未实测」的条目。
